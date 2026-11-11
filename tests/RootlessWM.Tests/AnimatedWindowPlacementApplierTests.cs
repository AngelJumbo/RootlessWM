using RootlessWM.Domain;
using RootlessWM.Platform.Win32;
using Xunit;

namespace RootlessWM.Tests;

public sealed class AnimatedWindowPlacementApplierTests
{
    private static readonly nint Handle = new(1);
    private static readonly WindowBounds Start = new(0, 0, 100, 100);
    private static readonly WindowBounds Target = new(100, 200, 300, 400);

    [Fact]
    public void AnimatesWithEasingAndFinishesExactlyAtTarget()
    {
        var fixture = new Fixture();
        using var animator = fixture.Create();
        animator.Configure(true, 200);

        Assert.True(animator.Apply(new WindowPlacement(Handle, Target)).Applied);
        Assert.Equal(Start, fixture.Bounds[Handle]);
        fixture.Now = 100;
        animator.Tick();
        Assert.Equal(new WindowBounds(88, 175, 275, 362), fixture.Bounds[Handle]);
        fixture.Now = 200;
        animator.Tick();
        Assert.Equal(Target, fixture.Bounds[Handle]);
        var count = fixture.Placements.Count;
        animator.Tick();
        Assert.Equal(count, fixture.Placements.Count);
    }

    [Fact]
    public void RetargetsFromCurrentBoundsAndDoesNotRestartIdenticalTarget()
    {
        var fixture = new Fixture();
        using var animator = fixture.Create();
        animator.Configure(true, 200);
        animator.Apply(new WindowPlacement(Handle, Target));
        fixture.Now = 100;
        animator.Tick();
        animator.Apply(new WindowPlacement(Handle, Target));
        fixture.Now = 200;
        animator.Tick();
        Assert.Equal(Target, fixture.Bounds[Handle]);

        var next = new WindowBounds(-100, 0, 200, 100);
        animator.Apply(new WindowPlacement(Handle, next));
        fixture.Now = 300;
        animator.Tick();
        var interrupted = fixture.Bounds[Handle];
        var final = new WindowBounds(400, 200, 400, 200);
        animator.Apply(new WindowPlacement(Handle, final));
        Assert.Equal(interrupted, fixture.Bounds[Handle]);
        fixture.Now = 400;
        animator.Tick();
        Assert.NotEqual(next, fixture.Bounds[Handle]);
        fixture.Now = 500;
        animator.Tick();
        Assert.Equal(final, fixture.Bounds[Handle]);
    }

    [Fact]
    public void DragCancelsAnimationAndSkipsPlacementUntilReleased()
    {
        var fixture = new Fixture();
        using var animator = fixture.Create();
        animator.Apply(new WindowPlacement(Handle, Target));
        animator.BeginMoveSize(Handle);
        animator.Apply(new WindowPlacement(Handle, Target));
        fixture.Now = 200;
        animator.Tick();
        Assert.Equal(Start, fixture.Bounds[Handle]);
        animator.EndMoveSize(Handle);
        animator.Apply(new WindowPlacement(Handle, Target));
        fixture.Now = 360;
        animator.Tick();
        Assert.Equal(Target, fixture.Bounds[Handle]);
    }

    [Fact]
    public void DisabledAndMaximizedWindowsUseImmediatePlacement()
    {
        var fixture = new Fixture();
        using var animator = fixture.Create();
        animator.Configure(false, 160);
        animator.Apply(new WindowPlacement(Handle, Target));
        Assert.Equal(Target, fixture.Bounds[Handle]);

        fixture.Bounds[Handle] = Start;
        fixture.Maximized = true;
        animator.Configure(true, 160);
        animator.Apply(new WindowPlacement(Handle, Target));
        Assert.Equal(Target, fixture.Bounds[Handle]);
    }

    [Fact]
    public void RetainAndSuspendCancelTransitions()
    {
        var fixture = new Fixture();
        using var animator = fixture.Create();
        animator.Apply(new WindowPlacement(Handle, Target));
        animator.Retain(new HashSet<nint>());
        fixture.Now = 160;
        animator.Tick();
        Assert.Equal(Start, fixture.Bounds[Handle]);

        animator.Apply(new WindowPlacement(Handle, Target));
        animator.SetSuspended(true);
        animator.Apply(new WindowPlacement(Handle, Target));
        fixture.Now = 320;
        animator.Tick();
        Assert.Equal(Start, fixture.Bounds[Handle]);
        animator.SetSuspended(false);
        animator.Apply(new WindowPlacement(Handle, Target));
        fixture.Now = 480;
        animator.Tick();
        Assert.Equal(Target, fixture.Bounds[Handle]);
    }

    [Fact]
    public void ReportsAsynchronousPlacementFailureAndStopsTransition()
    {
        var fixture = new Fixture();
        using var animator = fixture.Create();
        animator.Apply(new WindowPlacement(Handle, Target));
        fixture.Fail = true;
        fixture.Now = 80;
        animator.Tick();
        Assert.Equal("failed", Assert.Single(fixture.Failures).Reason);
        var count = fixture.Placements.Count;
        animator.Tick();
        Assert.Equal(count, fixture.Placements.Count);
    }

    [Fact]
    public void TimerCompletesTransitionWithoutManualTicks()
    {
        using var completed = new ManualResetEventSlim();
        var inner = new CallbackApplier(placement =>
        {
            if (placement.Bounds == Target)
            {
                completed.Set();
            }
        });
        using var animator = new AnimatedWindowPlacementApplier(
            inner, _ => Start, _ => false, _ => { });
        animator.Configure(true, 30);
        animator.Apply(new WindowPlacement(Handle, Target));
        Assert.True(completed.Wait(TimeSpan.FromSeconds(5)));
    }

    private sealed class CallbackApplier(Action<WindowPlacement> callback) : IWindowPlacementApplier
    {
        public PlacementResult Apply(WindowPlacement placement)
        {
            callback(placement);
            return PlacementResult.Success(placement.Handle);
        }
    }

    private sealed class Fixture : IWindowPlacementApplier
    {
        public readonly Dictionary<nint, WindowBounds> Bounds = new() { [Handle] = Start };
        public readonly List<WindowPlacement> Placements = [];
        public readonly List<PlacementResult> Failures = [];
        public long Now;
        public bool Maximized;
        public bool Fail;

        public AnimatedWindowPlacementApplier Create() => new(
            this,
            handle => Bounds.TryGetValue(handle, out var bounds) ? bounds : null,
            _ => Maximized,
            Failures.Add,
            () => Now,
            runTimer: false);

        public PlacementResult Apply(WindowPlacement placement)
        {
            Placements.Add(placement);
            if (Fail)
            {
                return PlacementResult.Skipped(placement.Handle, "failed");
            }

            Bounds[placement.Handle] = placement.Bounds;
            return PlacementResult.Success(placement.Handle);
        }
    }
}
