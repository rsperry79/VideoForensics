using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using KoenZomers.Ring.Api.Entities;
using KoenZomers.Ring.Api.Sockets;

namespace KoenZomers.Ring.Api.Alarm
{
    /// <summary>
    /// A persistent, authenticated device-command websocket for a single Ring location ("asset
    /// socket"), used for Ring Alarm security-panel control. Obtained via Session.ConnectAssetSocket.
    ///
    /// Protocol inferred from dgreif/ring's location.ts, not confirmed against real hardware - the
    /// account this was developed against has no Alarm hub (0 base_stations, 0 beams in every logged
    /// GetRingDevices response). In particular, the "dst" used to address the security-panel.switch-mode
    /// command is assumed to be the panel device's own id (Zid) rather than its owning asset id -
    /// dgreif/ring's source was ambiguous between the two. Treat this class as best-effort until
    /// verified against a real Alarm hub.
    /// </summary>
    public class RingAssetSocket : IDisposable
    {
        private readonly IWebSocketTransport _transport;
        private readonly List<string> _assetIds;
        private readonly CancellationTokenSource _cts = new();
        private readonly object _waitersLock = new();
        private readonly List<(Func<JsonElement, bool> Predicate, TaskCompletionSource<JsonElement> Tcs)> _waiters = new();
        private long _seq = 1;
        private Task _receiveLoopTask;
        private string _webSocketUrl;

        /// <summary>
        /// Raised when a device's state changes (a DataUpdate push from the socket).
        /// </summary>
        public event EventHandler<AlarmDevice> DeviceUpdated;

        internal RingAssetSocket(IWebSocketTransport transport, List<string> assetIds)
        {
            _transport = transport;
            _assetIds = assetIds ?? new List<string>();
        }

        internal async Task ConnectAsync(Uri webSocketUri)
        {
            _webSocketUrl = webSocketUri.ToString();
            await _transport.ConnectAsync(webSocketUri, _cts.Token);
            _receiveLoopTask = Task.Run(ReceiveLoopAsync);
        }

        private async Task ReceiveLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                string text;
                try
                {
                    text = await _transport.ReceiveAsync(_cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (text == null)
                {
                    break;
                }

                // Surfaced through the same ApiRawLogger channel as HTTP traffic (method "WS-RECV")
                // so websocket messages land in the same raw response log the app already writes.
                ApiRawLogger.Raise("WS-RECV", _webSocketUrl, 0, text);

                JsonElement root;
                try
                {
                    root = JsonDocument.Parse(text).RootElement;
                }
                catch (JsonException)
                {
                    continue;
                }

                if (root.TryGetProperty("channel", out var channelEl) &&
                    channelEl.ValueKind == JsonValueKind.String &&
                    channelEl.GetString() == "DataUpdate")
                {
                    RaiseDeviceUpdated(root);
                    continue;
                }

                DispatchToWaiters(root);
            }
        }

        private void RaiseDeviceUpdated(JsonElement root)
        {
            if (!root.TryGetProperty("body", out var bodyEl))
            {
                return;
            }

            try
            {
                var device = JsonSerializer.Deserialize<AlarmDevice>(bodyEl.GetRawText());
                if (device != null)
                {
                    DeviceUpdated?.Invoke(this, device);
                }
            }
            catch (JsonException)
            {
                // Malformed or unrecognized DataUpdate shape - ignore rather than crash the receive loop.
            }
        }

        private void DispatchToWaiters(JsonElement root)
        {
            lock (_waitersLock)
            {
                for (var i = _waiters.Count - 1; i >= 0; i--)
                {
                    if (_waiters[i].Predicate(root))
                    {
                        _waiters[i].Tcs.TrySetResult(root.Clone());
                        _waiters.RemoveAt(i);
                    }
                }
            }
        }

        private Task<JsonElement> WaitForMessageAsync(Func<JsonElement, bool> predicate, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_waitersLock)
            {
                _waiters.Add((predicate, tcs));
            }

            var timeoutCts = new CancellationTokenSource(timeout);
            timeoutCts.Token.Register(() => tcs.TrySetException(
                new TimeoutException($"Timed out after {timeout.TotalSeconds}s waiting for an asset socket response")));

            return tcs.Task;
        }

        /// <summary>
        /// Sends a command to a device on this location's asset socket and waits for the matching
        /// response (correlated by message type, since Ring does not appear to echo the outgoing
        /// seq number back on responses).
        /// </summary>
        internal async Task<JsonElement> SendCommandAsync(string msgType, string dst, object body, TimeSpan? timeout = null)
        {
            var seq = Interlocked.Increment(ref _seq);

            var envelope = new
            {
                channel = "message",
                msg = new { msg = msgType, dst, seq, body }
            };

            var waitTask = WaitForMessageAsync(
                el => el.TryGetProperty("msg", out var m) && m.ValueKind == JsonValueKind.String && m.GetString() == msgType,
                timeout ?? TimeSpan.FromSeconds(10));

            var serialized = JsonSerializer.Serialize(envelope);
            ApiRawLogger.Raise("WS-SEND", _webSocketUrl, 0, serialized);
            await _transport.SendAsync(serialized, _cts.Token);
            return await waitTask;
        }

        /// <summary>
        /// Returns every device known to this location, discovered via DeviceInfoDocGetList against
        /// each of the location's assets.
        /// </summary>
        public async Task<List<AlarmDevice>> GetDevices()
        {
            var devices = new List<AlarmDevice>();

            foreach (var assetId in _assetIds)
            {
                var response = await SendCommandAsync("DeviceInfoDocGetList", assetId, null);
                if (!response.TryGetProperty("body", out var bodyEl) || bodyEl.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var deviceEl in bodyEl.EnumerateArray())
                {
                    try
                    {
                        var device = JsonSerializer.Deserialize<AlarmDevice>(deviceEl.GetRawText());
                        if (device != null)
                        {
                            devices.Add(device);
                        }
                    }
                    catch (JsonException)
                    {
                        // Skip devices whose doc doesn't match the inferred AlarmDevice shape.
                    }
                }
            }

            return devices;
        }

        private async Task<AlarmDevice> GetSecurityPanelAsync()
        {
            var devices = await GetDevices();
            var panel = devices.FirstOrDefault(d => d.DeviceType == "security-panel");
            if (panel == null)
            {
                throw new InvalidOperationException("No security panel device found at this location.");
            }
            return panel;
        }

        /// <summary>
        /// Arms the alarm in "away" mode.
        /// </summary>
        public Task ArmAway(IEnumerable<string> bypassSensorZids = null) => SetAlarmModeAsync("all", bypassSensorZids);

        /// <summary>
        /// Arms the alarm in "home" mode.
        /// </summary>
        public Task ArmHome(IEnumerable<string> bypassSensorZids = null) => SetAlarmModeAsync("some", bypassSensorZids);

        /// <summary>
        /// Disarms the alarm.
        /// </summary>
        public Task Disarm() => SetAlarmModeAsync("none", null);

        private async Task SetAlarmModeAsync(string mode, IEnumerable<string> bypassSensorZids)
        {
            var panel = await GetSecurityPanelAsync();
            var body = new { mode, bypass = bypassSensorZids?.ToArray() ?? Array.Empty<string>() };
            await SendCommandAsync("security-panel.switch-mode", panel.Zid, body);
        }

        public async Task CloseAsync()
        {
            _cts.Cancel();
            await _transport.CloseAsync(CancellationToken.None);
        }

        public void Dispose()
        {
            _cts.Cancel();
            _transport.Dispose();
        }
    }
}
