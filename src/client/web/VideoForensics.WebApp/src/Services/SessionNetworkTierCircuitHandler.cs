using Microsoft.AspNetCore.Components.Server.Circuits;

using VideoForensics.Client.Hosting;

namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>
    /// Captures the circuit's network tier from the /_blazor connection each time it comes up. A circuit
    /// that resumes after a long absence skips prerender, so NetworkTierCapture never sees an HttpContext
    /// and nothing was persisted; without this the tier would fall back to Internet.
    /// </summary>
    public sealed class SessionNetworkTierCircuitHandler(IHttpContextAccessor accessor, INetworkTierResolver resolver, SessionNetworkContext networkContext) : CircuitHandler
    {
        public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            HttpContext? httpContext = accessor.HttpContext;
            if (httpContext != null)
            {
                // No context means no connection to judge: leave any tier already recorded untouched.
                networkContext.CaptureFromInitialConnection(httpContext, resolver);
            }

            return Task.CompletedTask;
        }
    }
}
