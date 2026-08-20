using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Ring.Api.Alarm;
using Ring.Api.Sockets;

using IWebSocketTransport = Ring.Api.Sockets.IWebSocketTransport;

namespace Ring.Api.Streaming
{
    /// <summary>
    /// WebSocket signaling client for Ring's dedicated WebRTC live-view server
    /// (wss://api.prod.signalling.ring.devices.a2z.com). Protocol inferred from dgreif/ring's
    /// streaming/webrtc-connection.ts. Handles the live_view/sdp/ice/ping-pong message envelope;
    /// SDP/ICE/media negotiation itself lives in RingLiveViewSession.
    /// </summary>
    internal class RingSignalingClient : IDisposable
    {
        private readonly IWebSocketTransport _transport;
        private readonly string _dialogId;
        private readonly CancellationTokenSource _cts = new();
        private Task _receiveLoopTask;
        private Timer _pingTimer;
        private string _url;

        /// <summary>
        /// Raised when an SDP answer is received from Ring.
        /// </summary>
        public event Action<string> OnSdpAnswer;

        /// <summary>
        /// Raised when an ICE candidate is received from Ring. Args: candidate string, m-line index.
        /// </summary>
        public event Action<string, int> OnIceCandidate;

        internal RingSignalingClient(IWebSocketTransport transport, string dialogId)
        {
            _transport = transport;
            _dialogId = dialogId;
        }

        internal async Task ConnectAsync(Uri uri)
        {
            _url = uri.ToString();
            await _transport.ConnectAsync(uri, _cts.Token);
            _receiveLoopTask = Task.Run(ReceiveLoopAsync);
            _pingTimer = new Timer(_ => FireAndForgetPing(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        private void FireAndForgetPing()
        {
            _ = SendRawAsync("ping", null);
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
                // so signaling messages (including SDP answers/ICE candidates) land in the same raw
                // response log the app already writes.
                ApiRawLogger.Raise("WS-RECV", _url, 0, text);

                JsonElement root;
                try
                {
                    root = JsonDocument.Parse(text).RootElement;
                }
                catch (JsonException)
                {
                    continue;
                }

                if (!root.TryGetProperty("method", out var methodEl) || methodEl.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                switch (methodEl.GetString())
                {
                    case "sdp":
                        HandleSdpAnswer(root);
                        break;
                    case "ice":
                        HandleIceCandidate(root);
                        break;
                }
            }
        }

        private void HandleSdpAnswer(JsonElement root)
        {
            if (!root.TryGetProperty("body", out var bodyEl))
            {
                return;
            }

            string sdp = bodyEl.ValueKind switch
            {
                JsonValueKind.String => bodyEl.GetString(),
                JsonValueKind.Object when bodyEl.TryGetProperty("sdp", out var sdpEl) => sdpEl.GetString(),
                _ => null
            };

            if (sdp != null)
            {
                OnSdpAnswer?.Invoke(sdp);
            }
        }

        private void HandleIceCandidate(JsonElement root)
        {
            if (!root.TryGetProperty("body", out var bodyEl) || !bodyEl.TryGetProperty("ice", out var iceEl))
            {
                return;
            }

            var mlineIndex = bodyEl.TryGetProperty("mlineindex", out var mEl) && mEl.ValueKind == JsonValueKind.Number
                ? mEl.GetInt32()
                : 0;

            OnIceCandidate?.Invoke(iceEl.GetString(), mlineIndex);
        }

        internal async Task SendOfferAsync(long doorbotId, string sdp)
        {
            var message = new
            {
                method = "live_view",
                dialog_id = _dialogId,
                body = new
                {
                    doorbot_id = doorbotId,
                    stream_options = new { audio_enabled = true, video_enabled = true },
                    sdp
                }
            };
            await SendAndLogAsync(message);
        }

        internal async Task SendIceCandidateAsync(long doorbotId, string candidate, int mlineIndex)
        {
            var message = new
            {
                method = "ice",
                dialog_id = _dialogId,
                body = new { doorbot_id = doorbotId, ice = candidate, mlineindex = mlineIndex }
            };
            await SendAndLogAsync(message);
        }

        private async Task SendRawAsync(string method, object body)
        {
            try
            {
                var message = new { method, dialog_id = _dialogId, body };
                await SendAndLogAsync(message);
            }
            catch (Exception)
            {
                // Best-effort keepalive - a failed ping will surface via the connection state change
                // events on the peer connection instead of throwing out of a background timer.
            }
        }

        private async Task SendAndLogAsync(object message)
        {
            var serialized = JsonSerializer.Serialize(message);
            ApiRawLogger.Raise("WS-SEND", _url, 0, serialized);
            await _transport.SendAsync(serialized, _cts.Token);
        }

        public async Task CloseAsync()
        {
            _pingTimer?.Dispose();
            _cts.Cancel();
            await _transport.CloseAsync(CancellationToken.None);
        }

        public void Dispose()
        {
            _pingTimer?.Dispose();
            _cts.Cancel();
            _transport.Dispose();
        }
    }
}
