using System.Text.Json.Serialization;

namespace RootlessWM.Protocol;

// Explicit whitelist of the types that may cross the pipe: nothing else can
// be (de)serialized through this context.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ManagementCommand))]
[JsonSerializable(typeof(ManagementResponse))]
[JsonSerializable(typeof(ManagementStateSnapshot))]
public sealed partial class ManagementProtocolJsonContext : JsonSerializerContext
{
}
