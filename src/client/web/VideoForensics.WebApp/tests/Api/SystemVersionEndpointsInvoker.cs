using Microsoft.AspNetCore.Http;

using System.Reflection;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.WebApp.Api;

namespace VideoForensics.WebApp.Tests.Api
{
    /// <summary>
    /// Helper class to invoke private methods in SystemVersionEndpoints for testing.
    /// Uses reflection to access SystemVersionEndpoints.GetSystemVersion and GetVersionManifest without going through full WebApplication routing.
    /// </summary>
    internal static class SystemVersionEndpointsInvoker
    {
        public static async Task<IResult> GetSystemVersion(
            ISystemVersionProvider versionProvider,
            CancellationToken ct)
        {
            MethodInfo? method = typeof(SystemVersionEndpoints)
                .GetMethod("GetSystemVersion",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    [typeof(ISystemVersionProvider), typeof(CancellationToken)],
                    null);

            if (method == null)
            {
                throw new InvalidOperationException("Could not find GetSystemVersion method");
            }

            object? result = method.Invoke(null, [versionProvider, ct]);
            return await result;
        }

        public static async Task<IResult> GetVersionManifest(
            ISystemVersionProvider versionProvider,
            CancellationToken ct)
        {
            MethodInfo? method = typeof(SystemVersionEndpoints)
                .GetMethod("GetVersionManifest",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    [typeof(ISystemVersionProvider), typeof(CancellationToken)],
                    null);

            if (method == null)
            {
                throw new InvalidOperationException("Could not find GetVersionManifest method");
            }

            object? result = method.Invoke(null, [versionProvider, ct]);
            return await result;
        }
    }
}
