using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Ring Intercom door unlock. Triggers a REAL physical door unlock - only call with explicit
    /// user intent. Body shape mirrors ring-client-api's RingIntercom.unlock() (a device_rpc JSON-RPC
    /// call). Endpoint base path (commands/v1, not devices/v1) confirmed against
    /// python-ring-doorbell's const.py (INTERCOM_OPEN_ENDPOINT) after devices/v1/.../device_rpc
    /// 404'd in a live ApiTester run.
    /// </summary>
    public partial class Session
    {
        /// <summary>
        /// Base Uri for Ring's device command API, used for Intercom unlock.
        /// </summary>
        public Uri RingCommandsApiBaseUrl => new Uri("https://api.ring.com/commands/v1/");

        /// <summary>
        /// Unlocks a Ring Intercom device. This triggers a real, physical door unlock.
        /// </summary>
        /// <param name="deviceId">ID of the Intercom device to unlock</param>
        public async Task Unlock(long deviceId)
        {
            await EnsureSessionValid();

            var uri = new Uri(RingCommandsApiBaseUrl, $"devices/{deviceId}/device_rpc");
            var bodyContent = JsonSerializer.Serialize(new
            {
                command_name = "device_rpc",
                request = new
                {
                    jsonrpc = "2.0",
                    method = "unlock_control.unlock",
                    @params = new { door_id = 0 }
                }
            });
            await _httpUtility.SendRequestWithExpectedStatusOutcome(uri, System.Net.Http.HttpMethod.Put, null, bodyContent, AuthenticationToken);
        }
    }
}
