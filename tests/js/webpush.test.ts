import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import { loadClassicScript, scriptsRoot } from "./load-script";

// VAPID key decoding is the easy-to-break part: base64url needs '-'/'_' mapping and padding restored
// before atob, and pushManager.subscribe rejects a key of the wrong shape.

type FakeSubscription = {
    endpoint: string;
    toJSON: () => { keys: { p256dh: string; auth: string } };
    unsubscribe: () => Promise<boolean>;
};

function installServiceWorker(subscription: FakeSubscription | null) {
    const subscribe = vi.fn(async () => subscription);
    const getSubscription = vi.fn(async () => subscription);
    const registration = {
        pushManager: { subscribe, getSubscription },
    };
    const register = vi.fn(async () => registration);
    const sw = { register, ready: Promise.resolve(registration) };
    Object.defineProperty(navigator, "serviceWorker", { value: sw, configurable: true });
    return { register, subscribe, getSubscription };
}

function installNotification(permission: NotificationPermission, requestResult: NotificationPermission = "granted") {
    const requestPermission = vi.fn(async () => requestResult);
    Object.defineProperty(globalThis, "Notification", {
        value: { permission, requestPermission },
        configurable: true,
        writable: true,
    });
    return requestPermission;
}

const sampleSubscription = (): FakeSubscription => ({
    endpoint: "https://push.example/endpoint-1",
    toJSON: () => ({ keys: { p256dh: "p256-value", auth: "auth-value" } }),
    unsubscribe: vi.fn(async () => true),
});

describe("vfWebPush", () => {
    beforeAll(() => {
        loadClassicScript(`${scriptsRoot}/webpush.js`);
    });

    afterEach(() => {
        vi.restoreAllMocks();
        Reflect.deleteProperty(globalThis, "Notification");
    });

    it("decodes the VAPID key from base64url, restoring the '-' '_' characters and padding", async () => {
        // [251, 255] is "+/8=" in base64 and "-_8" in base64url. Decoding must reproduce the bytes.
        installNotification("granted");
        const { subscribe } = installServiceWorker(sampleSubscription());

        await window.vfWebPush.subscribe("-_8");

        const { applicationServerKey, userVisibleOnly } = subscribe.mock.calls[0][0] as any;
        expect(userVisibleOnly).toBe(true);
        expect(Array.from(applicationServerKey as Uint8Array)).toEqual([251, 255]);
    });

    it("registers the service worker at the site root and returns the subscription keys", async () => {
        installNotification("granted");
        const { register } = installServiceWorker(sampleSubscription());

        const result = await window.vfWebPush.subscribe("AQID");

        expect(register).toHaveBeenCalledWith("/push-sw.js");
        expect(result).toEqual({
            endpoint: "https://push.example/endpoint-1",
            p256dhKey: "p256-value",
            authKey: "auth-value",
        });
    });

    it("asks for permission when it is still 'default' and fails if the user declines", async () => {
        const requestPermission = installNotification("default", "denied");
        installServiceWorker(sampleSubscription());

        await expect(window.vfWebPush.subscribe("AQID")).rejects.toThrow("Notification permission denied");
        expect(requestPermission).toHaveBeenCalledOnce();
    });

    it("fails without prompting when permission was already denied", async () => {
        const requestPermission = installNotification("denied");
        installServiceWorker(sampleSubscription());

        await expect(window.vfWebPush.subscribe("AQID")).rejects.toThrow("denied by user");
        expect(requestPermission).not.toHaveBeenCalled();
    });

    it("unsubscribe returns the endpoint that was removed, or null when there is none", async () => {
        const subscription = sampleSubscription();
        installServiceWorker(subscription);

        expect(await window.vfWebPush.unsubscribe()).toBe("https://push.example/endpoint-1");
        expect(subscription.unsubscribe).toHaveBeenCalledOnce();

        installServiceWorker(null);
        expect(await window.vfWebPush.unsubscribe()).toBeNull();
    });

    it("getCurrentSubscriptionEndpoint returns null instead of throwing when the worker is unavailable", async () => {
        Object.defineProperty(navigator, "serviceWorker", {
            value: { ready: Promise.reject(new Error("no worker")) },
            configurable: true,
        });
        // Attach a no-op catch so the rejected promise is not reported as unhandled before the call awaits it.
        (navigator.serviceWorker.ready as Promise<unknown>).catch(() => undefined);

        expect(await window.vfWebPush.getCurrentSubscriptionEndpoint()).toBeNull();
    });
});
