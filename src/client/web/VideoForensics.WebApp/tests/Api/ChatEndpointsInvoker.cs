using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using System.Reflection;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.WebApp.Api;

namespace VideoForensics.WebApp.Tests.Api
{
    /// <summary>
    /// Helper class to invoke private methods in ChatEndpoints for testing.
    /// Uses reflection to access ChatEndpoints.SendChatMessageAsync without going through full WebApplication routing.
    /// </summary>
    internal static class ChatEndpointsInvoker
    {
        public static async Task<IResult> SendChatMessageAsync(
            ChatRequestDto request,
            IChatService chatService,
            HttpContext context,
            ILogger<Program> logger,
            CancellationToken ct)
        {
            MethodInfo? method = typeof(ChatEndpoints)
                .GetMethod("SendChatMessageAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    [typeof(ChatRequestDto), typeof(IChatService), typeof(HttpContext), typeof(ILogger<Program>), typeof(CancellationToken)],
                    null);

            if (method == null)
            {
                throw new InvalidOperationException("Could not find SendChatMessageAsync method");
            }

            object? result = method.Invoke(null, [request, chatService, context, logger, ct]);
            return await result;
        }
    }
}
