using Microsoft.AspNetCore.Components.Server.Circuits;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Contracts;
using VideoForensics.Client.WebApp.Contracts;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Client.WebApp.Services.CircuitGuards;

namespace VideoForensics.Client.WebApp.Services.CircuitGuards
{
    public static class CircuitGuardRegistration
    {
        /// <summary>
        /// Wraps the in-process services that privileged page actions call, so each action is checked on the server
        /// against the circuit's operator, not only hidden in the UI. Must run after the services it wraps are registered.
        /// </summary>
        public static IServiceCollection AddCircuitOperatorGuards(this IServiceCollection services)
        {
            _ = services.AddScoped<CircuitScope>();
            _ = services.AddScoped<CircuitHandler, CircuitScopeHandler>();
            _ = services.AddScoped<ICircuitOperatorGate, CircuitOperatorGate>();

            _ = services.DecorateScoped<IProviderAccountRepository>((inner, sp) => new GuardedProviderAccountRepository(inner, sp.GetRequiredService<ICircuitOperatorGate>(), sp.GetRequiredService<IProviderAccountEditPolicy>()));
            _ = services.DecorateScoped<IDeviceConfigRepository>((inner, sp) => new GuardedDeviceConfigRepository(inner, sp.GetRequiredService<ICircuitOperatorGate>()));
            _ = services.DecorateScoped<IEvidenceExportService>((inner, sp) => new GuardedEvidenceExportService(inner, sp.GetRequiredService<ICircuitOperatorGate>()));
            _ = services.DecorateScoped<IRingSelfTestService>((inner, sp) => new GuardedRingSelfTestService(inner, sp.GetRequiredService<ICircuitOperatorGate>()));
            _ = services.DecorateScoped<IReleaseChannelService>((inner, sp) => new GuardedReleaseChannelService(inner, sp.GetRequiredService<ICircuitOperatorGate>(), sp.GetRequiredService<ISecurityAuditLogger>(), sp.GetRequiredService<SessionNetworkContext>()));
            _ = services.DecorateScoped<ICaseRepository>((inner, sp) => new GuardedCaseRepository(inner, sp.GetRequiredService<ICircuitOperatorGate>()));
            _ = services.DecorateScoped<ILegalHoldRepository>((inner, sp) => new GuardedLegalHoldRepository(inner, sp.GetRequiredService<ICircuitOperatorGate>()));
            _ = services.DecorateScoped<IMediaStillCaptureService>((inner, sp) => new GuardedMediaStillCaptureService(inner, sp.GetRequiredService<ICircuitOperatorGate>()));
            _ = services.DecorateScoped<IConfigToolsOrchestrator>((inner, sp) => new GuardedConfigToolsOrchestrator(inner, sp.GetRequiredService<ICircuitOperatorGate>()));
            _ = services.DecorateScoped<IJammingRepository>((inner, sp) => new GuardedJammingRepository(inner, sp.GetRequiredService<ICircuitOperatorGate>()));

            return services;
        }

        /// <summary>
        /// Re-registers the service as a scoped decorator around its current registration. The original registration
        /// stays in place and is resolved inside the decorator, so its own lifetime and construction are unchanged.
        /// </summary>
        private static IServiceCollection DecorateScoped<TService>(this IServiceCollection services, Func<TService, IServiceProvider, TService> decorate)
            where TService : class
        {
            ServiceDescriptor original = services.LastOrDefault(d => d.ServiceType == typeof(TService))
                ?? throw new InvalidOperationException($"{typeof(TService).Name} is not registered, so it cannot be decorated.");

            if (original.Lifetime != ServiceLifetime.Scoped)
            {
                throw new InvalidOperationException($"{typeof(TService).Name} must be scoped to be decorated for a circuit.");
            }

            services.Add(ServiceDescriptor.Scoped<TService>(sp =>
            {
                TService inner = original.ImplementationFactory is not null
                    ? (TService)original.ImplementationFactory(sp)
                    : (TService)ActivatorUtilities.CreateInstance(sp, original.ImplementationType
                        ?? throw new InvalidOperationException($"Unsupported registration for {typeof(TService).Name}."));
                return decorate(inner, sp);
            }));

            return services;
        }
    }
}
