using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using KoenZomers.Ring.Api.Entities;

namespace KoenZomers.Ring.Api
{
    /// <summary>
    /// Currently-active (in-progress) dings - a live doorbell press/motion event still ringing,
    /// as opposed to the historical record GetDoorbotsHistory() returns after the fact. Endpoint
    /// confirmed against python-ring-doorbell's const.py (DINGS_ENDPOINT).
    /// </summary>
    public partial class Session
    {
        /// <summary>
        /// Returns any dings (doorbell presses/motion events) currently active/in-progress across
        /// the account.
        /// </summary>
        public async Task<List<DoorbotHistoryEvent>> GetActiveDings(CancellationToken cancellationToken = default)
        {
            await EnsureSessionValid(cancellationToken);

            var uri = new Uri(BaseUrl, "dings/active");
            var response = await _httpUtility.GetContents(uri, AuthenticationToken, _hardwareId, cancellationToken);

            return JsonSerializer.Deserialize<List<DoorbotHistoryEvent>>(response) ?? new List<DoorbotHistoryEvent>();
        }
    }
}
