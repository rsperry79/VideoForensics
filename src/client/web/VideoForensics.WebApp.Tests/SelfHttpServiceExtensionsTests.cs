using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Hosting;
using VideoForensics.Ui.Shared.Services;
using VideoForensics.WebApp.Services;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class SelfHttpServiceExtensionsTests
    {
        /// <summary>Minimal concrete NavigationManager for tests - the base class only exposes
        /// BaseUri/Uri via the protected Initialize method.</summary>
        private sealed class TestNavigationManager : NavigationManager
        {
            public TestNavigationManager(string baseUri)
            {
                Initialize(baseUri, baseUri);
            }
        }

        private static ServiceCollection MakeServices(string baseUri = "https://videoforensics.example.com/")
        {
            var services = new ServiceCollection();
            services.AddScoped(_ => new PairedSessionState(new Mock<IJSRuntime>().Object));
            services.AddScoped<NavigationManager>(_ => new TestNavigationManager(baseUri));
            services.AddScoped(_ => new SessionNetworkContext());
            services.AddSingleton(new Mock<ISessionTokenService>().Object);
            services.AddSingleton(new Mock<ISessionTierHeaderProtector>().Object);
            return services;
        }

        [Fact]
        public void AddSelfHttpService_ResolvesServiceWithBaseAddressFromNavigationManager()
        {
            var services = MakeServices("https://videoforensics.example.com/");

            HttpClient? capturedClient = null;
            services.AddSelfHttpService<string>(http =>
            {
                capturedClient = http;
                return "fake-service";
            });

            using ServiceProvider provider = services.BuildServiceProvider();
            using IServiceScope scope = provider.CreateScope();

            string resolved = scope.ServiceProvider.GetRequiredService<string>();

            Assert.Equal("fake-service", resolved);
            Assert.NotNull(capturedClient);
            Assert.Equal(new Uri("https://videoforensics.example.com/"), capturedClient!.BaseAddress);
        }

        [Fact]
        public void AddSelfHttpService_DifferentCircuits_UseTheirOwnNavigationManagerBaseUri()
        {
            // A LAN-connected circuit and an Internet-Tunnel-connected circuit see different
            // NavigationManager.BaseUri values for the SAME running WebApp instance - the factory
            // must read it per-scope, not cache a value from the first resolution.
            var services = new ServiceCollection();
            services.AddScoped(_ => new PairedSessionState(new Mock<IJSRuntime>().Object));
            services.AddScoped(_ => new SessionNetworkContext());
            services.AddSingleton(new Mock<ISessionTokenService>().Object);
            services.AddSingleton(new Mock<ISessionTierHeaderProtector>().Object);

            var uris = new Queue<string>(new[] { "https://lan.example.com/", "https://tunnel.example.com/" });
            services.AddScoped<NavigationManager>(_ => new TestNavigationManager(uris.Dequeue()));

            var capturedBaseAddresses = new List<Uri?>();
            services.AddSelfHttpService<string>(http =>
            {
                capturedBaseAddresses.Add(http.BaseAddress);
                return "fake-service";
            });

            using ServiceProvider provider = services.BuildServiceProvider();

            using (IServiceScope lanScope = provider.CreateScope())
            {
                _ = lanScope.ServiceProvider.GetRequiredService<string>();
            }

            using (IServiceScope tunnelScope = provider.CreateScope())
            {
                _ = tunnelScope.ServiceProvider.GetRequiredService<string>();
            }

            Assert.Equal(new Uri("https://lan.example.com/"), capturedBaseAddresses[0]);
            Assert.Equal(new Uri("https://tunnel.example.com/"), capturedBaseAddresses[1]);
        }

        [Fact]
        public void AddSelfHttpService_WithinOneScope_ResolvesSameInstance()
        {
            var services = MakeServices();

            int factoryCallCount = 0;
            services.AddSelfHttpService<object>(_ =>
            {
                factoryCallCount++;
                return new object();
            });

            using ServiceProvider provider = services.BuildServiceProvider();
            using IServiceScope scope = provider.CreateScope();

            object first = scope.ServiceProvider.GetRequiredService<object>();
            object second = scope.ServiceProvider.GetRequiredService<object>();

            Assert.Same(first, second);
            Assert.Equal(1, factoryCallCount);
        }
    }
}
