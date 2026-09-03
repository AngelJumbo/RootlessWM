using Xunit;
using RootlessWM.Domain;

namespace RootlessWM.Tests;

public sealed class WindowEligibilityClassifierTests
{
    [Fact]
    public void Classify_NormalVisibleTopLevelWindow_ReturnsManaged()
    {
        var classifier = new WindowEligibilityClassifier();

        var eligibility = classifier.Classify(CreateCandidate());

        Assert.Equal(WindowEligibility.Managed, eligibility);
    }

    [Fact]
    public void Classify_CloakedWindow_ReturnsCloaked()
    {
        var classifier = new WindowEligibilityClassifier();
        var candidate = CreateCandidate(className: "ApplicationFrameWindow", processName: "explorer") with
        {
            IsCloaked = true
        };

        var eligibility = classifier.Classify(candidate);

        Assert.Equal(WindowEligibility.Cloaked, eligibility);
    }

    [Theory]
    [InlineData(false, false, false, false, WindowEligibility.NotVisible)]
    [InlineData(true, true, false, false, WindowEligibility.ChildWindow)]
    [InlineData(true, false, true, false, WindowEligibility.ToolWindow)]
    [InlineData(true, false, false, true, WindowEligibility.Minimized)]
    public void Classify_NonEligibleWindow_ReturnsAuditReason(
        bool isVisible,
        bool isChildWindow,
        bool isToolWindow,
        bool isMinimized,
        WindowEligibility expected)
    {
        var classifier = new WindowEligibilityClassifier();
        var candidate = CreateCandidate(isVisible, isChildWindow, isToolWindow, isMinimized);

        var eligibility = classifier.Classify(candidate);

        Assert.Equal(expected, eligibility);
    }

    [Fact]
    public void Classify_ConfiguredExecutable_ReturnsExcludedExecutable()
    {
        var options = new WindowEligibilityOptions();
        options.ExcludedExecutableNames.Add("sample-app");
        var classifier = new WindowEligibilityClassifier(options);

        var eligibility = classifier.Classify(CreateCandidate(processName: "Sample-App"));

        Assert.Equal(WindowEligibility.ExcludedExecutable, eligibility);
    }

    [Fact]
    public void Classify_DocumentedWindowsTooltipClass_ReturnsTooltip()
    {
        var classifier = new WindowEligibilityClassifier();

        var eligibility = classifier.Classify(CreateCandidate(className: "tooltips_class32"));

        Assert.Equal(WindowEligibility.Tooltip, eligibility);
    }

    [Fact]
    public void Classify_NonActivatingPopup_ReturnsTooltip()
    {
        var classifier = new WindowEligibilityClassifier();
        var candidate = CreateCandidate(className: "Chrome_WidgetWin_1", processName: "brave") with
        {
            Style = 0x80000000,
            ExtendedStyle = 0x08000000
        };

        var eligibility = classifier.Classify(candidate);

        Assert.Equal(WindowEligibility.Tooltip, eligibility);
    }

    [Fact]
    public void Classify_ActivatingPopup_RemainsManaged()
    {
        var classifier = new WindowEligibilityClassifier();
        var candidate = CreateCandidate() with { Style = 0x80000000 };

        var eligibility = classifier.Classify(candidate);

        Assert.Equal(WindowEligibility.Managed, eligibility);
    }

    [Theory]
    [InlineData("Shell_TrayWnd")]
    [InlineData("Shell_SecondaryTrayWnd")]
    public void Classify_ShellWindow_ReturnsShellWindow(string className)
    {
        var classifier = new WindowEligibilityClassifier();

        var eligibility = classifier.Classify(CreateCandidate(className: className));

        Assert.Equal(WindowEligibility.ShellWindow, eligibility);
    }

    [Fact]
    public void Classify_StandardPopupMenu_ReturnsShellWindow()
    {
        var classifier = new WindowEligibilityClassifier();

        var eligibility = classifier.Classify(CreateCandidate(className: "#32768", processName: "notepad"));

        Assert.Equal(WindowEligibility.ShellWindow, eligibility);
    }

    [Fact]
    public void Classify_WinUiPopupBridge_ReturnsShellWindow()
    {
        var classifier = new WindowEligibilityClassifier();

        var eligibility = classifier.Classify(CreateCandidate(
            className: "Microsoft.UI.Content.PopupWindowSiteBridge",
            processName: "Notepad"));

        Assert.Equal(WindowEligibility.ShellWindow, eligibility);
    }

    [Fact]
    public void Classify_TextInputHost_ReturnsSystemWindow()
    {
        var classifier = new WindowEligibilityClassifier();

        var eligibility = classifier.Classify(CreateCandidate(
            className: "Windows.UI.Core.CoreWindow",
            processName: "TextInputHost"));

        Assert.Equal(WindowEligibility.SystemWindow, eligibility);
    }

    [Theory]
    [InlineData("SearchHost")]
    [InlineData("StartMenuExperienceHost")]
    public void Classify_ShellHostProcess_ReturnsSystemWindow(string processName)
    {
        var classifier = new WindowEligibilityClassifier();

        var eligibility = classifier.Classify(CreateCandidate(
            className: "Windows.UI.Core.CoreWindow",
            processName: processName));

        Assert.Equal(WindowEligibility.SystemWindow, eligibility);
    }

    [Fact]
    public void Classify_WindowWithUnusableBounds_ReturnsInvalidBounds()
    {
        var classifier = new WindowEligibilityClassifier();
        var candidate = CreateCandidate() with { Bounds = new WindowBounds(0, 0, 0, 0) };

        var eligibility = classifier.Classify(candidate);

        Assert.Equal(WindowEligibility.InvalidBounds, eligibility);
    }

    private static WindowCandidate CreateCandidate(
        bool isVisible = true,
        bool isChildWindow = false,
        bool isToolWindow = false,
        bool isMinimized = false,
        string className = "SampleWindowClass",
        string? processName = "sample-app")
    {
        return new WindowCandidate(
            (nint)1,
            isVisible,
            isChildWindow,
            isToolWindow,
            isMinimized,
            className,
            processName,
            new WindowBounds(12, 34, 800, 600));
    }
}
