namespace RootlessWM.Protocol;

// Members must only be appended, never reordered or removed: the numeric
// value is part of the wire protocol.
public enum ProtocolErrorCode
{
    Unknown = 0,
    ProtocolVersionMismatch,
    UnauthorizedUser,
    UnauthorizedSession,
    MalformedMessage,
    MessageTooLarge,
    RequestTimedOut,
    UnknownCommand,
    InvalidCommandPayload,
    CommandFailed,
    NotConnected,
    HelperShuttingDown
}
