using RootlessWM.Protocol;
using Xunit;

namespace RootlessWM.Tests;

public sealed class ProtocolErrorCodeTests
{
    [Theory]
    [InlineData(ProtocolErrorCode.Unknown, 0)]
    [InlineData(ProtocolErrorCode.ProtocolVersionMismatch, 1)]
    [InlineData(ProtocolErrorCode.UnauthorizedUser, 2)]
    [InlineData(ProtocolErrorCode.UnauthorizedSession, 3)]
    [InlineData(ProtocolErrorCode.MalformedMessage, 4)]
    [InlineData(ProtocolErrorCode.MessageTooLarge, 5)]
    [InlineData(ProtocolErrorCode.RequestTimedOut, 6)]
    [InlineData(ProtocolErrorCode.UnknownCommand, 7)]
    [InlineData(ProtocolErrorCode.InvalidCommandPayload, 8)]
    [InlineData(ProtocolErrorCode.CommandFailed, 9)]
    [InlineData(ProtocolErrorCode.NotConnected, 10)]
    [InlineData(ProtocolErrorCode.HelperShuttingDown, 11)]
    public void NumericValue_IsStable(ProtocolErrorCode code, int expected)
    {
        Assert.Equal(expected, (int)code);
    }

    [Fact]
    public void AllMembers_AreCoveredByStabilityTest()
    {
        var expectedCount = Enum.GetValues<ProtocolErrorCode>().Length;

        Assert.Equal(12, expectedCount);
    }
}
