namespace RootlessWM.Domain;

public sealed record TrackedWindow(
    WindowCandidate Candidate,
    WindowEligibility Eligibility,
    WindowBounds OriginalBounds)
{
    public nint Handle => Candidate.Handle;
}
