using VideoForensics.Client.Hosting.Maintenance;

namespace VideoForensics.Client.WebApp.Auth
{
    /// <summary>
    /// Refuses user-started work while server maintenance blocks it, with 503 and the current maintenance message. The
    /// lease is held for the request, so a maintenance drain waits for a request already in progress.
    /// </summary>
    public sealed class MaintenanceEndpointFilter(IServiceProvider services) : IEndpointFilter
    {
        /// <inheritdoc />
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            IMaintenanceGate gate = MaintenanceWork.GateFrom(services);
            using IDisposable? lease = gate.TryEnterWork();
            return lease is null
                ? Results.Problem(
                    title: "Server maintenance",
                    detail: gate.Current.Message,
                    statusCode: StatusCodes.Status503ServiceUnavailable)
                : await next(context);
        }
    }
}
