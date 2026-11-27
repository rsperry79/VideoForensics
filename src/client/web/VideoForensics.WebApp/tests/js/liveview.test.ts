import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { initializeHubConnection, startLiveView, stopHubConnection, stopLiveView } from "../../../../../../src/client/ui/VideoForensics.Ui.Shared/src/wwwroot/scripts/liveview.js";

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
        for (const id of ["session-1", "session-2", "session-missing"]) stopLiveView(id);
        vi.unstubAllGlobals();
        vi.restoreAllMocks();
    });

    it("requests the offer, applies it, and sends the answer back to the server", async () => {
        const { hub } = fakeHub("offer-sdp");

        const pc = await startLiveView(hub as never, "session-1", "cam");

        expect(pc).toBe(lastPc as unknown);
        expect(hub.invoke).toHaveBeenCalledWith("RequestOffer", "session-1");
        expect(lastPc.setRemoteDescription).toHaveBeenCalledWith({ type: "offer", sdp: "offer-sdp" });
        expect(hub.invoke).toHaveBeenCalledWith("SubmitAnswer", "session-1", "answer-sdp");
    });

    it("records no window global and registers no ReceiveIceCandidate handler", async () => {
        const { hub } = fakeHub();

        await startLiveView(hub as never, "session-1", "cam");

        expect((window as { _liveViewPeerConnection?: unknown })._liveViewPeerConnection).toBeUndefined();
        expect(hub.on).not.toHaveBeenCalled();
    });

    it("closes the connection and records nothing when the video element is missing", async () => {
        const { hub } = fakeHub();
        document.body.innerHTML = "";

        await expect(startLiveView(hub as never, "session-missing", "cam")).rejects.toThrow("Video element with ID 'cam' not found");
        expect(hub.invoke).not.toHaveBeenCalled();
        expect(lastPc.close).toHaveBeenCalledOnce();

        stopLiveView("session-missing");
        expect(lastPc.close).toHaveBeenCalledOnce();
    });

    it("closes the peer connection and rethrows when negotiation fails", async () => {
        vi.spyOn(console, "error").mockImplementation(() => undefined);
        const { hub } = fakeHub(new Error("hub unavailable"));

        await expect(startLiveView(hub as never, "session-1", "cam")).rejects.toThrow("hub unavailable");
        expect(lastPc.close).toHaveBeenCalledOnce();
    });

    it("keeps one connection per session: stopping A closes only A's connection", async () => {
        const { hub } = fakeHub();
        await startLiveView(hub as never, "session-1", "cam");
        const fakeA = lastPc;
        await startLiveView(hub as never, "session-2", "cam");
        const fakeB = lastPc;

        stopLiveView("session-1");

        expect(fakeA.close).toHaveBeenCalledOnce();
        expect(fakeB.close).not.toHaveBeenCalled();
        expect(fakeB.signalingState).toBe("stable");
    });

    it("stopLiveView with an unknown session id does nothing and does not throw", () => {
        expect(() => stopLiveView("no-such-session")).not.toThrow();
    });

    it("stopLiveView closes a session's connection once and ignores repeat calls", async () => {
        const { hub } = fakeHub();
        await startLiveView(hub as never, "session-1", "cam");

        stopLiveView("session-1");
        expect(lastPc.close).toHaveBeenCalledOnce();

        stopLiveView("session-1");
        expect(lastPc.close).toHaveBeenCalledOnce();
    });

    describe("hub connection", () => {
        function fakeSignalR() {
            const connection = { start: vi.fn(async () => undefined), stop: vi.fn(async () => undefined) };
            const builder = {
                withUrl: vi.fn<(url: string, options: unknown) => unknown>(() => builder),
                withAutomaticReconnect: vi.fn(() => builder),
                build: vi.fn(() => connection),
            };
            return { connection, builder, lib: { HubConnectionBuilder: vi.fn(function () { return builder; }) } };
        }

        afterEach(() => {
            Reflect.deleteProperty(window, "signalR");
            Reflect.deleteProperty(window, "DotNet");
            document.head.innerHTML = "";
        });

        it("builds and starts a connection to the hub url and returns the connection itself", async () => {
            const { connection, builder, lib } = fakeSignalR();
            const createJSObjectReference = vi.fn(() => ({ __jsObjectId: 7 }));
            vi.stubGlobal("signalR", lib);
            vi.stubGlobal("DotNet", { createJSObjectReference });

            const result = await initializeHubConnection("https://host/hubs/liveview-signaling", "session-token");

            expect(builder.withUrl).toHaveBeenCalledWith(
                "https://host/hubs/liveview-signaling",
                expect.objectContaining({ accessTokenFactory: expect.any(Function) }));
            const options = builder.withUrl.mock.calls[0][1] as { accessTokenFactory: () => string };
            expect(options.accessTokenFactory()).toBe("session-token");
            expect(connection.start).toHaveBeenCalledTimes(1);
            // startLiveView calls .on and .invoke on this value, so it must be the connection, not a DotNet wrapper.
            expect(createJSObjectReference).not.toHaveBeenCalled();
            expect(result).toBe(connection);
        });

        it("loads the vendored SignalR client script when the global is missing", async () => {
            const { lib } = fakeSignalR();
            vi.stubGlobal("DotNet", { createJSObjectReference: vi.fn(() => ({})) });

            const pending = initializeHubConnection("https://host/hubs/liveview-signaling", "t");

            const script = document.head.querySelector("script");
            expect(script?.getAttribute("src")).toBe("_content/VideoForensics.Client.Ui.Shared/lib/signalr/signalr.min.js");
            vi.stubGlobal("signalR", lib);
            script?.dispatchEvent(new Event("load"));
            await pending;

            expect(lib.HubConnectionBuilder).toHaveBeenCalled();
        });

        it("rejects when the SignalR client script fails to load", async () => {
            vi.stubGlobal("DotNet", { createJSObjectReference: vi.fn(() => ({})) });

            const pending = initializeHubConnection("https://host/hubs/liveview-signaling", "t");
            document.head.querySelector("script")?.dispatchEvent(new Event("error"));

            await expect(pending).rejects.toThrow();
        });

        it("stops the connection", async () => {
            const { connection } = fakeSignalR();

            await stopHubConnection(connection as never);

            expect(connection.stop).toHaveBeenCalledTimes(1);
        });
    });
});
