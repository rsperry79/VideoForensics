import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { addIceCandidate, startLiveView, stopLiveView } from "../../src/client/ui/VideoForensics.Ui.Shared/src/wwwroot/scripts/liveview.js";

// WebRTC negotiation for the live view. jsdom has no RTCPeerConnection, so a fake records calls.

type FakePc = {
    signalingState: string;
    ontrack: unknown;
    onicecandidate: unknown;
    setRemoteDescription: ReturnType<typeof vi.fn>;
    createAnswer: ReturnType<typeof vi.fn>;
    setLocalDescription: ReturnType<typeof vi.fn>;
    addIceCandidate: ReturnType<typeof vi.fn>;
    close: ReturnType<typeof vi.fn>;
};

let lastPc: FakePc;

function fakeHub(offerSdp: string | Error = "offer-sdp") {
    const handlers: Record<string, (...args: unknown[]) => void> = {};
    const hub = {
        invoke: vi.fn(async (method: string) => {
            if (method === "RequestOffer") {
                if (offerSdp instanceof Error) throw offerSdp;
                return offerSdp;
            }
            return undefined;
        }),
        on: vi.fn((method: string, handler: (...args: unknown[]) => void) => {
            handlers[method] = handler;
        }),
    };
    return { hub, handlers };
}

describe("liveview", () => {
    beforeEach(() => {
        document.body.innerHTML = `<video id="cam"></video>`;
        lastPc = undefined as unknown as FakePc;

        class FakeRTCPeerConnection {
            signalingState = "stable";
            ontrack: unknown = null;
            onicecandidate: unknown = null;
            setRemoteDescription = vi.fn(async () => undefined);
            createAnswer = vi.fn(async () => ({ type: "answer", sdp: "answer-sdp" }));
            setLocalDescription = vi.fn(async () => undefined);
            addIceCandidate = vi.fn(async () => undefined);
            close = vi.fn(() => { this.signalingState = "closed"; });
            constructor() {
                lastPc = this as unknown as FakePc;
            }
        }
        class FakeRTCIceCandidate {
            constructor(public init: unknown) {}
        }
        vi.stubGlobal("RTCPeerConnection", FakeRTCPeerConnection);
        vi.stubGlobal("RTCIceCandidate", FakeRTCIceCandidate);
        vi.spyOn(console, "log").mockImplementation(() => undefined);
    });

    afterEach(() => {
        vi.unstubAllGlobals();
        vi.restoreAllMocks();
        delete (window as any)._liveViewPeerConnection;
    });

    it("requests the offer, applies it, and sends the answer back to the server", async () => {
        const { hub } = fakeHub("offer-sdp");

        const pc = await startLiveView(hub as any, "session-1", "cam");

        expect(pc).toBe(lastPc as unknown);
        expect(hub.invoke).toHaveBeenCalledWith("RequestOffer", "session-1");
        expect(lastPc.setRemoteDescription).toHaveBeenCalledWith({ type: "offer", sdp: "offer-sdp" });
        expect(hub.invoke).toHaveBeenCalledWith("SubmitAnswer", "session-1", "answer-sdp");
    });

    it("feeds server ICE candidates into the peer connection", async () => {
        const { hub, handlers } = fakeHub();
        await startLiveView(hub as any, "session-1", "cam");

        handlers["ReceiveIceCandidate"]("candidate:1", 0);

        await vi.waitFor(() => expect(lastPc.addIceCandidate).toHaveBeenCalledOnce());
        const added = lastPc.addIceCandidate.mock.calls[0][0] as { init: { candidate: string; sdpMLineIndex: number } };
        expect(added.init).toEqual({ candidate: "candidate:1", sdpMLineIndex: 0 });
    });

    it("rejects when the video element is missing, without creating a connection", async () => {
        const { hub } = fakeHub();
        document.body.innerHTML = "";

        await expect(startLiveView(hub as any, "session-1", "cam")).rejects.toThrow("Video element with ID 'cam' not found");
        expect(hub.invoke).not.toHaveBeenCalled();
    });

    it("closes the peer connection and rethrows when negotiation fails", async () => {
        vi.spyOn(console, "error").mockImplementation(() => undefined);
        const { hub } = fakeHub(new Error("hub unavailable"));

        await expect(startLiveView(hub as any, "session-1", "cam")).rejects.toThrow("hub unavailable");
        expect(lastPc.close).toHaveBeenCalledOnce();
    });

    it("addIceCandidate forwards to the connection created by startLiveView", async () => {
        const { hub } = fakeHub();
        await startLiveView(hub as any, "session-1", "cam");

        await addIceCandidate("candidate:2", 1);

        expect(lastPc.addIceCandidate).toHaveBeenCalledOnce();
    });

    it("addIceCandidate is a no-op before any connection exists", async () => {
        await expect(addIceCandidate("candidate:3", 0)).resolves.toBeUndefined();
    });

    it("stopLiveView closes an open connection and leaves a closed one alone", async () => {
        const { hub } = fakeHub();
        const pc = await startLiveView(hub as any, "session-1", "cam");

        stopLiveView(pc);
        expect(lastPc.close).toHaveBeenCalledOnce();

        stopLiveView(pc);
        expect(lastPc.close).toHaveBeenCalledOnce();
    });
});
