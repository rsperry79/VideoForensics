using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace KoenZomers.Ring.Api
{
    /// <summary>
    /// Ring Intercom door unlock. Triggers a REAL physical door unlock - only call with explicit
    /// user intent. Body shape mirrors ring-client-api's RingIntercom.unlock() (a device_rpc JSON-RPC
    /// call) - not confirmed against a live capture.
    /// </summary>
    public partial class Session
    {
        /// <summary>
        /// Unlocks a Ring Intercom device. This triggers a real, physical door unlock.
        /// </summary>
        /// <param name="deviceId">ID of the Intercom device to unlock</param>
        public async Task Unlock(long deviceId)
        {
            await EnsureSessionValid();

            var uri = new Uri(RingDevicesApiBaseUrl, $"devices/{deviceId}/device_rpc");
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
