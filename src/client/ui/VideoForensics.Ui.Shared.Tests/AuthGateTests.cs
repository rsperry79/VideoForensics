namespace VideoForensics.Ui.Shared.Tests;

using VideoForensics.Ui.Shared.Layout;

using Xunit;

/// <summary>
/// Tests for AuthGate.razor's three-way bootstrap redirect decision logic
/// (<see cref="AuthGate.ResolveBootstrapRedirectTarget"/>).
///
/// The full component render test is impractical in bUnit due to AuthGate's OnAfterRenderAsync-only
/// redirect logic and NavigationManager event subscription setup. These tests instead call the
/// component's own public static decision method directly, so they exercise the real logic rather
/// than a duplicate of it:
///
/// - If isEmpty &amp;&amp; isLocal: redirect to /setup (local admin creates first SuperAdmin)
/// - Else if isEmpty &amp;&amp; !isLocal: redirect to /awaiting-setup (remote caller waits for local admin)
/// - Else (!isEmpty): redirect to /welcome (normal login flow, operators exist)
/// </summary>
public class AuthGateTests
{
    [Fact]
    public void ResolveBootstrapRedirectTarget_NoOperatorsAndLocal_ReturnsSetup()
    {
        string target = AuthGate.ResolveBootstrapRedirectTarget(isEmpty: true, isLocal: true);
        Assert.Equal("/setup", target);
    }

    [Fact]
    public void ResolveBootstrapRedirectTarget_NoOperatorsAndRemote_ReturnsAwaitingSetup()
    {
        string target = AuthGate.ResolveBootstrapRedirectTarget(isEmpty: true, isLocal: false);
        Assert.Equal("/awaiting-setup", target);
    }

    [Fact]
    public void ResolveBootstrapRedirectTarget_OperatorsExist_ReturnsWelcome()
    {
        string target = AuthGate.ResolveBootstrapRedirectTarget(isEmpty: false, isLocal: true);
        Assert.Equal("/welcome", target);
    }

    [Fact]
    public void ResolveBootstrapRedirectTarget_OperatorsExistRemote_ReturnsWelcome()
    {
        string target = AuthGate.ResolveBootstrapRedirectTarget(isEmpty: false, isLocal: false);
        Assert.Equal("/welcome", target);
    }
}
