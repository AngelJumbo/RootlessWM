using System.Security.Principal;

namespace RootlessWM.Protocol.Transport;

public static class ManagementPipeName
{
    public static string Create(SecurityIdentifier userSid) => "RootlessWM." + userSid.Value;

    public static string CreateForCurrentUser()
    {
        var sid = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The current identity has no user SID.");
        return Create(sid);
    }
}
