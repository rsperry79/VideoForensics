using Microsoft.AspNetCore.Components.Server.Circuits;

namespace VideoForensics.WebApp.Services.CircuitGuards
{
    /// <summary>
    /// Scoped marker that is open only while a Blazor circuit (a browser UI session) owns this DI scope.
    /// Background work resolves services from its own scope, where this stays closed.
    /// </summary>
    public sealed class CircuitScope
    {
        /// <summary>True between a circuit opening and closing.</summary>
        public bool IsOpen { get; set; }
    }

    /// <summary>Opens and closes <see cref="CircuitScope"/> as the browser circuit starts and ends.</summary>
    public sealed class CircuitScopeHandler(CircuitScope scope) : CircuitHandler
    {
        public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            scope.IsOpen = true;
            return Task.CompletedTask;
        }

        public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            scope.IsOpen = false;
            return Task.CompletedTask;
        }
    }
}
