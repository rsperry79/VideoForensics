using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Client.Ui.Shared.Layout;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests;
/// <summary>
/// The UI's <see cref="AccessPolicy"/> names are copies of the server's <see cref="VideoForensicsPolicies"/>
/// constants (the UI cannot reference the WebApp). These tests fail if the two sets drift apart.
/// </summary>
public class PageAccessPolicyParity_Tests
{
    private static readonly HashSet<string> ServerPolicyNames = typeof(VideoForensicsPolicies)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void AccessPolicy_EveryValue_HasMatchingServerPolicy()
    {
        foreach (string name in Enum.GetNames<AccessPolicy>())
        {
            Assert.Contains($"VF.{name}", ServerPolicyNames);
        }
    }

    [Fact]
    public void AccessPolicy_ValueCount_MatchesServerPolicyCount()
    {
        Assert.Equal(ServerPolicyNames.Count, Enum.GetValues<AccessPolicy>().Length);
    }
}
