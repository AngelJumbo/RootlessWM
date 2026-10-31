using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using RootlessWM.App;
using SkiaSharp;
using Xunit;

namespace RootlessWM.Tests;

public sealed class RunnerLoadingTests
{
    private static readonly Type FormType = typeof(RunnerController).GetNestedType("RunnerForm", BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("RunnerForm was not found.");

    [Fact]
    public void Typing_FiltersMemoryWithoutStartingAnotherDiscovery()
    {
        using var form = CreateForm();
        var catalog = Catalog("Alpha", "Beta", "Alphabet");
        Field("_catalog").SetValue(form, catalog);
        Field("_catalogTask").SetValue(form, null);

        Method("OnKeyPress").Invoke(form, [new KeyPressEventArgs('a')]);
        Method("OnKeyPress").Invoke(form, [new KeyPressEventArgs('l')]);

        Assert.Equal("al", Field("_query").GetValue(form));
        Assert.Equal(2, Assert.IsAssignableFrom<IList>(Field("_items").GetValue(form)).Count);
        Assert.Null(Field("_catalogTask").GetValue(form));
        Method("OnKeyDown").Invoke(form, [new KeyEventArgs(Keys.Back)]);
        Assert.Equal(3, Assert.IsAssignableFrom<IList>(Field("_items").GetValue(form)).Count);
        Assert.Null(Field("_catalogTask").GetValue(form));
    }

    [Fact]
    public void BackgroundCompletion_FiltersLatestQueryAndIgnoresReplacedLoad()
    {
        using var form = CreateForm();
        var resultType = Field("_catalogTask").FieldType.GetGenericArguments()[0];
        var sourceType = typeof(TaskCompletionSource<>).MakeGenericType(resultType);
        var oldSource = Activator.CreateInstance(sourceType)!;
        var source = Activator.CreateInstance(sourceType)!;
        var oldTask = sourceType.GetProperty("Task")!.GetValue(oldSource);
        var task = sourceType.GetProperty("Task")!.GetValue(source);
        Field("_catalogTask").SetValue(form, oldTask);
        Field("_catalogTask").SetValue(form, task);
        Field("_catalog").SetValue(form, Catalog());
        var watch = Stopwatch.StartNew();
        Method("OnKeyPress").Invoke(form, [new KeyPressEventArgs('b')]);
        watch.Stop();
        Assert.True(watch.Elapsed < TimeSpan.FromMilliseconds(250), $"Typing took {watch.ElapsedMilliseconds} ms while discovery was pending.");
        Assert.Empty(Assert.IsAssignableFrom<IList>(Field("_items").GetValue(form)));
        Assert.Same(task, Field("_catalogTask").GetValue(form));

        sourceType.GetMethod("SetResult")!.Invoke(oldSource,
            [Activator.CreateInstance(resultType, [Catalog("Stale"), null])]);
        Method("PollBackgroundLoads").Invoke(form, null);
        Assert.Empty(Assert.IsAssignableFrom<IList>(Field("_items").GetValue(form)));
        sourceType.GetMethod("SetResult")!.Invoke(source,
            [Activator.CreateInstance(resultType, [Catalog("Alpha", "Beta"), null])]);
        Method("PollBackgroundLoads").Invoke(form, null);

        Assert.Equal("b", Field("_query").GetValue(form));
        var item = Assert.Single(Assert.IsAssignableFrom<IList>(Field("_items").GetValue(form)).Cast<object>());
        Assert.Equal("Beta", item.GetType().GetProperty("Name")!.GetValue(item));
        Assert.Null(Field("_catalogTask").GetValue(form));
    }

    [Fact]
    public void Settings_ReusesCatalogUnlessSourcesChange()
    {
        using var form = CreateForm();
        var initialTask = Field("_catalogTask").GetValue(form);
        var settings = RunnerSettings.Default with { Sources = ["test-empty"], MaxResults = 3 };
        FormType.GetMethod("ApplySettings")!.Invoke(form, [settings]);
        // A completed task may be consumed by RefreshResults, but settings must not start a replacement.
        var after = Field("_catalogTask").GetValue(form);
        Assert.True(after is null || ReferenceEquals(initialTask, after));
        FormType.GetMethod("ApplySettings")!.Invoke(form, [settings with { Sources = ["different-empty"] }]);
        Assert.Equal("different-empty", Field("_sourcesSignature").GetValue(form));
        Assert.Empty(Assert.IsAssignableFrom<IList>(Field("_catalog").GetValue(form)));
    }

    [Fact]
    public async Task Icons_LoadOnceOffThreadAndCacheMissingResults()
    {
        using var form = CreateForm();
        using var bitmap = new SKBitmap(200, 100);
        using var canvas = new SKCanvas(bitmap);
        var target = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.exe");
        var arguments = new object[] { canvas, target, new SKRect(0, 0, 200, 40), 0F };

        Method("DrawIcon").Invoke(form, arguments);
        var icons = Assert.IsAssignableFrom<IDictionary>(Field("_icons").GetValue(form));
        var entry = icons[target]!;
        var task = Assert.IsAssignableFrom<Task>(entry.GetType().GetProperty("Task")!.GetValue(entry));
        Method("DrawIcon").Invoke(form, arguments);
        Assert.Single(icons.Keys.Cast<object>());
        Assert.Same(entry, icons[target]);
        await task.WaitAsync(TimeSpan.FromSeconds(10));
        Method("PollBackgroundLoads").Invoke(form, null);
        Assert.Equal(true, entry.GetType().GetProperty("Loaded")!.GetValue(entry));
        Method("DrawIcon").Invoke(form, arguments);
        Assert.Same(task, entry.GetType().GetProperty("Task")!.GetValue(entry));
    }

    private static Form CreateForm()
        => Assert.IsAssignableFrom<Form>(Activator.CreateInstance(FormType,
            [RunnerSettings.Default with { Sources = ["test-empty"] }, new ConsoleDiagnosticLog(writeToFile: false)]));

    private static IList Catalog(params string[] names)
    {
        var list = Assert.IsAssignableFrom<IList>(Activator.CreateInstance(Field("_catalog").FieldType));
        var itemType = Field("_catalog").FieldType.GetGenericArguments()[0];
        foreach (var name in names)
        {
            list.Add(Activator.CreateInstance(itemType, [name, name + ".exe", "", "", "test"]));
        }

        return list;
    }

    private static FieldInfo Field(string name)
        => FormType.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Missing field {name}.");

    private static MethodInfo Method(string name)
        => FormType.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Missing method {name}.");
}
