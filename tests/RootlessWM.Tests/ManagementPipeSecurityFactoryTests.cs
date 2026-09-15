using System.IO.Pipes;
using System.Security.Principal;
using RootlessWM.Protocol.Transport;
using Xunit;

namespace RootlessWM.Tests;

public sealed class ManagementPipeSecurityFactoryTests
{
    [Fact]
    public void Create_WithoutLocalSystem_GrantsOnlyOwner()
    {
        var owner = new SecurityIdentifier("S-1-5-21-1111111111-2222222222-3333333333-1001");

        var security = ManagementPipeSecurityFactory.Create(owner, allowLocalSystem: false);
        var rules = security.GetAccessRules(true, false, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().ToList();

        var ownerRule = Assert.Single(rules, r => r.IdentityReference == owner);
        Assert.True((ownerRule.PipeAccessRights & PipeAccessRights.ReadWrite) == PipeAccessRights.ReadWrite);
        Assert.DoesNotContain(rules, r => IsWorldOrAuthenticatedUsers((SecurityIdentifier)r.IdentityReference));
    }

    [Fact]
    public void Create_WithLocalSystem_AlsoGrantsLocalSystemFullControl()
    {
        var owner = new SecurityIdentifier("S-1-5-21-1111111111-2222222222-3333333333-1001");
        var localSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

        var security = ManagementPipeSecurityFactory.Create(owner, allowLocalSystem: true);
        var rules = security.GetAccessRules(true, false, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().ToList();

        var systemRule = Assert.Single(rules, r => r.IdentityReference == localSystem);
        Assert.True((systemRule.PipeAccessRights & PipeAccessRights.FullControl) == PipeAccessRights.FullControl);
        Assert.DoesNotContain(rules, r => IsWorldOrAuthenticatedUsers((SecurityIdentifier)r.IdentityReference));
    }

    private static bool IsWorldOrAuthenticatedUsers(SecurityIdentifier sid) =>
        sid.IsWellKnown(WellKnownSidType.WorldSid) || sid.IsWellKnown(WellKnownSidType.AuthenticatedUserSid);
}
