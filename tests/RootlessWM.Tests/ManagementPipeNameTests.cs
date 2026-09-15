using System.Security.Principal;
using RootlessWM.Protocol.Transport;
using Xunit;

namespace RootlessWM.Tests;

public sealed class ManagementPipeNameTests
{
    [Fact]
    public void Create_ReturnsNamePrefixedWithSid()
    {
        var sid = new SecurityIdentifier("S-1-5-21-1111111111-2222222222-3333333333-1001");

        var name = ManagementPipeName.Create(sid);

        Assert.Equal("RootlessWM." + sid.Value, name);
    }

    [Fact]
    public void CreateForCurrentUser_ContainsCurrentUserSid()
    {
        var currentSid = WindowsIdentity.GetCurrent().User!;

        var name = ManagementPipeName.CreateForCurrentUser();

        Assert.Contains(currentSid.Value, name);
    }

    [Fact]
    public void Create_DifferentSids_ProduceDifferentNames()
    {
        var first = new SecurityIdentifier("S-1-5-21-1111111111-2222222222-3333333333-1001");
        var second = new SecurityIdentifier("S-1-5-21-1111111111-2222222222-3333333333-1002");

        Assert.NotEqual(ManagementPipeName.Create(first), ManagementPipeName.Create(second));
    }
}
