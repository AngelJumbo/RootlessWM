using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace RootlessWM.Protocol.Transport;

public static class ManagementPipeSecurityFactory
{
    public static PipeSecurity Create(SecurityIdentifier ownerSid, bool allowLocalSystem)
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(ownerSid, PipeAccessRights.ReadWrite, AccessControlType.Allow));

        if (allowLocalSystem)
        {
            var localSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, domainSid: null);
            security.AddAccessRule(new PipeAccessRule(localSystem, PipeAccessRights.FullControl, AccessControlType.Allow));
        }

        return security;
    }
}
