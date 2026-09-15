using System.Buffers.Binary;
using System.Net.Sockets;

namespace VideoForensics.Providers.Uniview;

/// <summary>
/// Receives a recorded segment over the NVR's proprietary download transport:
/// a plain TCP connection to the NVR (address/port handed back by cmd 82),
/// framed exactly like RTSP interleaved mode ('$' + channel + u16 length +
/// RTP packet), carrying ordinary unencrypted RTP/HEVC. See
/// docs/NVR_API.md section 6 for the full protocol write-up.
/// </summary>
public sealed class RtpDownloadSession
{
    private const byte MagicByte = (byte)'$';

    /// RTP clock rate for the video payload (standard for H.264/H.265).
    private const int VideoClockRate = 90000;

    /// RTP payload type used for audio in this transport - the standard
    /// static assignment for G.711 mu-law (PCMU), confirmed by capturing a
    /// real download from the vendor plugin: video and audio are both sent
    /// on channel 0, distinguished only by RTP payload type (video uses a
    /// dynamic PT, observed as 108; audio always uses the static PT 0). See
    /// docs/NVR_API.md section 6.
    private const int AudioPayloadType = 0;

    /// <summary>
    /// Connects to the given host/port, sends the session-id hello, and
    /// writes the reassembled HEVC elementary stream (Annex-B) to
    /// <paramref name="elementaryStreamPath"/> and raw G.711 mu-law audio
    /// samples to <paramref name="audioStreamPath"/> (created even if no
    /// audio arrives - callers should check its length before muxing it).
    ///
    /// IMPORTANT: the server does not enforce cmd 82's u32End itself — it
    /// just keeps streaming from u32Begin onward until the client stops it.
    /// This method tracks elapsed video time via RTP timestamps and stops
    /// once <paramref name="targetDuration"/> has been received, falling
    /// back to the server closing the connection or
    /// <paramref name="idleTimeout"/> elapsing with no data.
    ///
    /// <paramref name="onStopping"/> is invoked once the receive loop
    /// decides to stop, while the TCP connection is still open, so the
    /// caller can send cmd 84 *before* the socket closes — closing our end
    /// first and only then telling the server we're done appears to leave
    /// the server-side download task slot stuck (suspected cause of the
    /// device's cmd 82 code 60031 capacity errors persisting far longer
    /// than a clean per-task release should allow). See docs/NVR_API.md
    /// section 6.
    /// </summary>
    public static async Task ReceiveAsync(
        string host, int port, string sessionId, string elementaryStreamPath, string audioStreamPath,
        TimeSpan targetDuration, TimeSpan idleTimeout, Func<Task> onStopping, CancellationToken ct = default)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, ct);
        using NetworkStream stream = client.GetStream();

        await stream.WriteAsync(BuildHelloPacket(sessionId), ct);

        await using FileStream output = File.Create(elementaryStreamPath);
        var reassembler = new HevcNalReassembler(output);
        await using FileStream audioOutput = File.Create(audioStreamPath);
        var audioExtractor = new RtpAudioExtractor(audioOutput);

        byte[] header = new byte[4];
        using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        uint? lastTimestamp = null;
        long accumulatedTicks = 0;
        // Recordings can be made of multiple concatenated physical files, each
        // with its own RTP timestamp base - a jump this large (in either
        // direction) is a file-boundary discontinuity, not real elapsed time.
        const uint maxReasonableDeltaTicks = VideoClockRate * 10;

        while (true)
        {
            idleCts.CancelAfter(idleTimeout);
            int read;
            try
            {
                read = await ReadExactAsync(stream, header, idleCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                break; // idle timeout - server likely finished without closing cleanly
            }

            if (read == 0)
            {
                break; // connection closed - segment finished
            }

            if (header[0] != MagicByte)
            {
                throw new InvalidOperationException($"Expected '$' framing byte, got 0x{header[0]:X2}");
            }

            byte channel = header[1];
            ushort length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2));

            byte[] payload = new byte[length];
            if (await ReadExactAsync(stream, payload, idleCts.Token) < length)
            {
                break; // truncated - connection closed mid-packet
            }

            if (channel == 0 && payload.Length >= 12) // video+audio RTP channel, per docs/NVR_API.md
            {
                int payloadType = payload[1] & 0x7F;
                if (payloadType == AudioPayloadType)
                {
                    audioExtractor.ProcessRtpPacket(payload);
                    continue;
                }

                reassembler.ProcessRtpPacket(payload);

                uint timestamp = BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(4, 4));
                if (lastTimestamp.HasValue)
                {
                    // Segment durations here are far shorter than the ~13 hour
                    // wraparound period of a 90kHz 32-bit clock, so any large
                    // delta (forward or backward) is a file-boundary
                    // discontinuity, not real wraparound - skip it.
                    uint delta = unchecked(timestamp - lastTimestamp.Value);
                    if (delta <= maxReasonableDeltaTicks)
                    {
                        accumulatedTicks += delta;
                    }
                }

                lastTimestamp = timestamp;

                double elapsedSeconds = accumulatedTicks / (double)VideoClockRate;
                if (elapsedSeconds >= targetDuration.TotalSeconds)
                {
                    break;
                }
            }
            // Other channels (RTCP) are not handled in this version.
        }

        // Tell the server we're done via cmd 84 (HTTP, over a separate
        // connection) while this TCP socket is still open - see the
        // ordering note on ReceiveAsync above.
        await onStopping();
    }

    private static byte[] BuildHelloPacket(string sessionId)
    {
        // Observed on the wire: '$' + channel 0 + u16 length(0x18) followed by
        // a 24-byte payload: FF FD, 00 24, 00 01, then the session id ASCII
        // zero-padded to 16 bytes. See docs/NVR_API.md section 6.
        byte[] payload = new byte[24];
        payload[0] = 0xFF;
        payload[1] = 0xFD;
        payload[2] = 0x00;
        payload[3] = 0x24;
        payload[4] = 0x00;
        payload[5] = 0x01;
        byte[] idBytes = System.Text.Encoding.ASCII.GetBytes(sessionId);
        Array.Copy(idBytes, 0, payload, 6, Math.Min(idBytes.Length, 16));

        byte[] packet = new byte[4 + payload.Length];
        packet[0] = MagicByte;
        packet[1] = 0x00;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2), (ushort)payload.Length);
        Array.Copy(payload, 0, packet, 4, payload.Length);
        return packet;
    }

    private static async Task<int> ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), ct);
            if (read == 0)
            {
                return offset;
            }

            offset += read;
        }

        return offset;
    }
}

/// <summary>
/// Reassembles HEVC (H.265) NAL units from RTP payloads (RFC 7798 - single
/// NAL unit packets and fragmentation units) into an Annex-B elementary
/// stream suitable for feeding directly to ffmpeg (`-f hevc`).
/// </summary>
public sealed class HevcNalReassembler
{
    private static readonly byte[] StartCode = [0x00, 0x00, 0x00, 0x01];

    private readonly Stream _output;
    private MemoryStream? _fragment;

    public HevcNalReassembler(Stream output)
    {
        _output = output;
    }

    public void ProcessRtpPacket(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < 12)
        {
            return; // too short to be a valid RTP packet
        }

        byte b0 = packet[0];
        int version = b0 >> 6;
        if (version != 2)
        {
            return; // not RTP
        }

        int csrcCount = b0 & 0x0F;
        bool hasExtension = (b0 & 0x10) != 0;
        int offset = 12 + (csrcCount * 4);
        if (offset > packet.Length)
        {
            return;
        }

        if (hasExtension)
        {
            if (offset + 4 > packet.Length)
            {
                return;
            }

            ushort extLenWords = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(offset + 2, 2));
            offset += 4 + (extLenWords * 4);
        }

        if (offset >= packet.Length)
        {
            return;
        }

        ReadOnlySpan<byte> nalPayload = packet[offset..];
        if (nalPayload.Length < 2)
        {
            return;
        }

        int nalType = (nalPayload[0] >> 1) & 0x3F;

        if (nalType is >= 0 and <= 47)
        {
            WriteNal(nalPayload);
        }
        else if (nalType == 49) // Fragmentation Unit (FU)
        {
            ProcessFragment(nalPayload);
        }
        // Aggregation packets (type 48) are not produced by this device in
        // observed captures and are not handled here.
    }

    private void ProcessFragment(ReadOnlySpan<byte> nalPayload)
    {
        if (nalPayload.Length < 3)
        {
            return;
        }

        byte fuIndicator = nalPayload[0];
        byte fuIndicator2 = nalPayload[1];
        byte fuHeader = nalPayload[2];
        bool start = (fuHeader & 0x80) != 0;
        bool end = (fuHeader & 0x40) != 0;
        int originalType = fuHeader & 0x3F;

        if (start)
        {
            _fragment = new MemoryStream();
            // Reconstruct the original 2-byte NAL header: original type in
            // bits 1-6 of byte0, layer id / tid from the FU indicator bytes.
            byte reconstructedByte0 = (byte)((fuIndicator & 0x81) | (originalType << 1));
            _fragment.WriteByte(reconstructedByte0);
            _fragment.WriteByte(fuIndicator2);
        }

        if (_fragment is null)
        {
            return; // fragment start never seen - drop
        }

        _fragment.Write(nalPayload[3..]);

        if (end)
        {
            WriteNal(_fragment.ToArray());
            _fragment = null;
        }
    }

    private void WriteNal(ReadOnlySpan<byte> nal)
    {
        _output.Write(StartCode);
        _output.Write(nal);
    }
}

/// <summary>
/// Extracts raw G.711 mu-law audio samples from RTP payloads (payload type
/// 0 packets on the same channel as the video, per docs/NVR_API.md section
/// 6) into a headerless 8kHz mono mu-law file, suitable for feeding
/// directly to ffmpeg (`-f mulaw -ar 8000 -ac 1`).
/// </summary>
public sealed class RtpAudioExtractor(Stream output)
{
    public void ProcessRtpPacket(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < 12)
        {
            return; // too short to be a valid RTP packet
        }

        byte b0 = packet[0];
        if (b0 >> 6 != 2)
        {
            return; // not RTP
        }

        int csrcCount = b0 & 0x0F;
        bool hasExtension = (b0 & 0x10) != 0;
        int offset = 12 + (csrcCount * 4);
        if (offset > packet.Length)
        {
            return;
        }

        if (hasExtension)
        {
            if (offset + 4 > packet.Length)
            {
                return;
            }

            ushort extLenWords = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(offset + 2, 2));
            offset += 4 + (extLenWords * 4);
        }

        if (offset > packet.Length)
        {
            return;
        }

        output.Write(packet[offset..]);
    }
}
