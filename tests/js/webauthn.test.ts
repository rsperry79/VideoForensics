import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import { loadClassicScript, scriptsRoot } from "./load-script";

// Ceremony glue between Fido2NetLib's base64url JSON and the browser's ArrayBuffer API. The
// conversions are the part that fails silently ("looks right and isn't"), so they are pinned here.

const b64url = (bytes: number[]) => Buffer.from(bytes).toString("base64url");
const bytesOf = (buffer: ArrayBuffer | Uint8Array) => Array.from(new Uint8Array(buffer as ArrayBuffer));

describe("vfWebAuthn", () => {
    beforeAll(() => {
        loadClassicScript(`${scriptsRoot}/webauthn.js`);
    });

    afterEach(() => {
        vi.restoreAllMocks();
        Reflect.deleteProperty(globalThis.navigator, "credentials");
        Reflect.deleteProperty(window, "PublicKeyCredential");
    });

    describe("isAvailable", () => {
        it("is false when the browser has no PublicKeyCredential", () => {
            expect(window.vfWebAuthn.isAvailable()).toBe(false);
        });

        it("is true when PublicKeyCredential and navigator.credentials both exist", () => {
            Object.defineProperty(window, "PublicKeyCredential", { value: function () {}, configurable: true });
            Object.defineProperty(navigator, "credentials", { value: {}, configurable: true });
            expect(window.vfWebAuthn.isAvailable()).toBe(true);
        });
    });

    describe("register", () => {
        it("decodes the challenge and user id from base64url and encodes the attestation back", async () => {
            const create = vi.fn(async () => ({
                id: "cred-1",
                rawId: new Uint8Array([9, 9]).buffer,
                type: "public-key",
                response: {
                    attestationObject: new Uint8Array([4, 5]).buffer,
                    clientDataJSON: new Uint8Array([6, 7]).buffer,
                },
            }));
            Object.defineProperty(navigator, "credentials", { value: { create }, configurable: true });

            const options = {
                rp: { name: "VF", id: "localhost" },
                user: { id: b64url([1, 2, 3]), name: "op", displayName: "Op" },
                challenge: b64url([251, 255]),
                pubKeyCredParams: [{ type: "public-key", alg: -7 }],
            };

            const json = await window.vfWebAuthn.register(JSON.stringify(options));
            const publicKey = create.mock.calls[0][0].publicKey;

            expect(bytesOf(publicKey.challenge)).toEqual([251, 255]);
            expect(bytesOf(publicKey.user.id)).toEqual([1, 2, 3]);
            expect(publicKey.attestation).toBe("none");

            const result = JSON.parse(json);
            expect(result.id).toBe("cred-1");
            expect(result.rawId).toBe(b64url([9, 9]));
            expect(result.response.attestationObject).toBe(b64url([4, 5]));
            expect(result.response.clientDataJSON).toBe(b64url([6, 7]));
        });

        it("maps excludeCredentials ids from base64url", async () => {
            const create = vi.fn(async () => ({
                id: "x", rawId: new ArrayBuffer(0), type: "public-key",
                response: { attestationObject: new ArrayBuffer(0), clientDataJSON: new ArrayBuffer(0) },
            }));
            Object.defineProperty(navigator, "credentials", { value: { create }, configurable: true });

            await window.vfWebAuthn.register(JSON.stringify({
                rp: { name: "VF" },
                user: { id: b64url([1]), name: "a", displayName: "a" },
                challenge: b64url([2]),
                pubKeyCredParams: [],
                excludeCredentials: [{ id: b64url([7, 8]) }],
            }));

            const excluded = create.mock.calls[0][0].publicKey.excludeCredentials;
            expect(excluded).toHaveLength(1);
            expect(bytesOf(excluded[0].id)).toEqual([7, 8]);
            expect(excluded[0].type).toBe("public-key");
        });
    });

    describe("authenticate", () => {
        it("sends the challenge and allowCredentials decoded, and returns a null userHandle when absent", async () => {
            const get = vi.fn(async () => ({
                id: "cred-2",
                rawId: new Uint8Array([1]).buffer,
                type: "public-key",
                response: {
                    authenticatorData: new Uint8Array([2]).buffer,
                    clientDataJSON: new Uint8Array([3]).buffer,
                    signature: new Uint8Array([4]).buffer,
                    userHandle: null,
                },
            }));
            Object.defineProperty(navigator, "credentials", { value: { get }, configurable: true });

            const json = await window.vfWebAuthn.authenticate(JSON.stringify({
                challenge: b64url([200, 201]),
                rpId: "localhost",
                allowCredentials: [{ id: b64url([5]) }],
                userVerification: "preferred",
            }));

            const publicKey = get.mock.calls[0][0].publicKey;
            expect(bytesOf(publicKey.challenge)).toEqual([200, 201]);
            expect(bytesOf(publicKey.allowCredentials[0].id)).toEqual([5]);

            const result = JSON.parse(json);
            expect(result.response.signature).toBe(b64url([4]));
            expect(result.response.userHandle).toBeNull();
        });
    });

    describe("session cookie", () => {
        it("round-trips a session token and clears it", () => {
            const token = JSON.stringify({ sid: "abc", exp: 1 });

            window.vfWebAuthn.saveSession(token);
            expect(window.vfWebAuthn.loadSession()).toBe(token);

            window.vfWebAuthn.clearSession();
            expect(window.vfWebAuthn.loadSession()).toBeNull();
        });
    });
});

beforeEach(() => {
    document.cookie = "vf.pairedSession=; path=/; Max-Age=0";
});
