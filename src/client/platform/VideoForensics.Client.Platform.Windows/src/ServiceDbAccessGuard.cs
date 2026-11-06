using System.ComponentModel;
using System.Runtime.Versioning;
using System.ServiceProcess;

namespace VideoForensics.Client.Platform.Windows
{
    /// <summary>
    /// Stops the VideoForensics service while a utility writes to its database, then starts it again. The service caches
    /// settings and holds the SQLite file open, so a direct write behind its back would otherwise go unseen or race its writes.
    /// Does nothing when the service is not installed or already stopped, which is the case during installer provisioning.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class ServiceDbAccessGuard : IDisposable
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

        private readonly string _serviceName;
        private bool _stoppedByUs;

        private ServiceDbAccessGuard(string serviceName)
        {
            _serviceName = serviceName;
        }

        /// <summary>
        /// Stops the service if it is running. Returns null with <paramref name="error"/> set when it is running but could not be
        /// stopped; the caller must not touch the database then.
        /// </summary>
        public static ServiceDbAccessGuard? Acquire(string serviceName, out string? error)
        {
            error = null;
            var guard = new ServiceDbAccessGuard(serviceName);
            try
            {
                using var controller = new ServiceController(serviceName);
                if (controller.Status == ServiceControllerStatus.Stopped)
                {
                    return guard;
                }

                Console.WriteLine($"Stopping the {serviceName} service...");
                controller.Stop();
                controller.WaitForStatus(ServiceControllerStatus.Stopped, Timeout);
                guard._stoppedByUs = true;
                return guard;
            }
            catch (InvalidOperationException ex) when (ex.InnerException is Win32Exception { NativeErrorCode: 1060 })
            {
                // 1060 = service not installed: nothing to stop.
                return guard;
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or System.ServiceProcess.TimeoutException)
            {
                error = $"Could not stop the {serviceName} service ({ex.GetType().Name}: {ex.Message}). Stop it first, or run as administrator.";
                return null;
            }
        }

        public void Dispose()
        {
            if (!_stoppedByUs)
            {
                return;
            }

            _stoppedByUs = false;
            try
            {
                Console.WriteLine($"Starting the {_serviceName} service...");
                using var controller = new ServiceController(_serviceName);
                controller.Start();
                controller.WaitForStatus(ServiceControllerStatus.Running, Timeout);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or System.ServiceProcess.TimeoutException)
            {
                Console.Error.WriteLine($"Could not start the {_serviceName} service ({ex.GetType().Name}: {ex.Message}). Start it manually.");
            }
        }
    }
}
