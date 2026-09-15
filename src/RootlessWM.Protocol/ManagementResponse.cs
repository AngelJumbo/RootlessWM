using System.Text.Json.Serialization;

namespace RootlessWM.Protocol;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "responseType")]
[JsonDerivedType(typeof(CommandAckResponse), "ack")]
[JsonDerivedType(typeof(SnapshotResponse), "snapshot")]
public abstract record ManagementResponse(RequestId RequestId, bool Success, ProtocolErrorCode? ErrorCode, string? ErrorMessage);

public sealed record CommandAckResponse(RequestId RequestId, bool Success, ProtocolErrorCode? ErrorCode, string? ErrorMessage)
    : ManagementResponse(RequestId, Success, ErrorCode, ErrorMessage);

public sealed record SnapshotResponse(RequestId RequestId, bool Success, ProtocolErrorCode? ErrorCode, string? ErrorMessage, ManagementStateSnapshot? Snapshot)
    : ManagementResponse(RequestId, Success, ErrorCode, ErrorMessage);
