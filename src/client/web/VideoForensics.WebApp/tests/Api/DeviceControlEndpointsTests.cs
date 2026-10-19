using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.WebApp.Api;
using Xunit;

using DeviceEntity = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.WebApp.Tests.Api
{
    /// <summary>
    /// Tests for the device-control endpoint handlers: provider-device-id lookup, boundary validation and status mapping.
    /// </summary>
    public class DeviceControlEndpointsTests
    {
        private static DeviceEntity BuildDevice(Guid id) => new()
        {
            Id = id,
            ProviderDeviceId = "123",
            Name = "Front Door",
            Type = "doorbell",
        };

        [Fact]
        public async Task SetLightAsync_UnknownProviderDeviceId_ReturnsNotFound()
        {
            var devices = new Mock<IDeviceRepository>();
            _ = devices.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<DeviceEntity>());
            var control = new Mock<IDeviceControlService>();

            var result = await DeviceControlEndpoints.SetLightAsync(
                "999", new SetLightRequest(true), devices.Object, control.Object, NullLogger.Instance, CancellationToken.None);

            var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status404NotFound, status.StatusCode);
            control.Verify(c => c.SetLightAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetLightAsync_ProviderRefusesAmbiguousAccount_ReturnsConflict()
        {
            var deviceId = Guid.NewGuid();
            var devices = new Mock<IDeviceRepository>();
            _ = devices.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<DeviceEntity> { BuildDevice(deviceId) });
            var control = new Mock<IDeviceControlService>();
            _ = control.Setup(c => c.SetLightAsync("123", true, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Device is linked to more than one Ring account; control refused"));

            var result = await DeviceControlEndpoints.SetLightAsync(
                "123", new SetLightRequest(true), devices.Object, control.Object, NullLogger.Instance, CancellationToken.None);

            var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status409Conflict, status.StatusCode);
        }

        [Fact]
        public async Task SetLightAsync_ProviderReportsNoAccountForDevice_ReturnsNotFound()
        {
            var deviceId = Guid.NewGuid();
            var devices = new Mock<IDeviceRepository>();
            _ = devices.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<DeviceEntity> { BuildDevice(deviceId) });
            var control = new Mock<IDeviceControlService>();
            _ = control.Setup(c => c.SetLightAsync("123", true, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException("No Ring account is linked to this device"));

            var result = await DeviceControlEndpoints.SetLightAsync(
                "123", new SetLightRequest(true), devices.Object, control.Object, NullLogger.Instance, CancellationToken.None);

            var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status404NotFound, status.StatusCode);
        }

        [Fact]
        public async Task SetVolumeAsync_VolumeOutOfRange_ReturnsBadRequestWithoutControlling()
        {
            var devices = new Mock<IDeviceRepository>();
            var control = new Mock<IDeviceControlService>();

            var result = await DeviceControlEndpoints.SetVolumeAsync(
                "123", new SetVolumeRequest(12), devices.Object, control.Object, NullLogger.Instance, CancellationToken.None);

            var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, status.StatusCode);
            devices.Verify(r => r.ListAsync(It.IsAny<CancellationToken>()), Times.Never);
            control.Verify(c => c.SetVolumeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetLightAsync_ValidRequest_ReturnsNoContentAndControlsProviderDeviceOnce()
        {
            var deviceId = Guid.NewGuid();
            var devices = new Mock<IDeviceRepository>();
            _ = devices.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<DeviceEntity> { BuildDevice(deviceId) });
            var control = new Mock<IDeviceControlService>();

            var result = await DeviceControlEndpoints.SetLightAsync(
                "123", new SetLightRequest(true), devices.Object, control.Object, NullLogger.Instance, CancellationToken.None);

            var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status204NoContent, status.StatusCode);
            control.Verify(c => c.SetLightAsync("123", true, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
