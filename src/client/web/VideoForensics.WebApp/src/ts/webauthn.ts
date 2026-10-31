// Browser-side WebAuthn ceremony glue (plan §5.1/§5.10). Converts between the base64url-encoded
// JSON shapes Fido2NetLib (server) expects/produces and the ArrayBuffer-based shapes the browser's
// native navigator.credentials API requires. Property names on both sides (challenge, rp, user,
// pubKeyCredParams, excludeCredentials/allowCredentials, attestationObject, clientDataJSON,
// authenticatorData, signature, userHandle) were confirmed against the installed Fido2.Models
// 4.0.1 assembly, not guessed - a mismatch here is exactly the "looks right and isn't" risk the
// project plan calls out for this ceremony.

// Wire shapes from Fido2NetLib. Typed loosely on purpose: the server owns these contracts, and the
// fields are passed to the browser API unchanged apart from the base64url-to-buffer conversions.
interface CredentialDescriptorJson {
    id: string;
    type?: string;
    transports?: AuthenticatorTransport[];
}

interface RegistrationOptionsJson {
    rp: PublicKeyCredentialRpEntity;
    user: { id: string; name: string; displayName: string };
    challenge: string;
    pubKeyCredParams: PublicKeyCredentialParameters[];
    timeout?: number;
    excludeCredentials?: CredentialDescriptorJson[];
    authenticatorSelection?: AuthenticatorSelectionCriteria;
    attestation?: AttestationConveyancePreference;
}

interface AssertionOptionsJson {
    challenge: string;
    timeout?: number;
    rpId?: string;
    allowCredentials?: CredentialDescriptorJson[];
    userVerification?: UserVerificationRequirement;
}

(function () {
    function base64UrlToBuffer(base64url: string): ArrayBuffer {
        const padding = "=".repeat((4 - (base64url.length % 4)) % 4);
        const base64 = (base64url + padding).replace(/-/g, "+").replace(/_/g, "/");
        const raw = atob(base64);
        const buffer = new Uint8Array(raw.length);
        for (let i = 0; i < raw.length; i++) {
            buffer[i] = raw.charCodeAt(i);
        }
        return buffer.buffer;
    }

    function bufferToBase64Url(buffer: ArrayBuffer): string {
        const bytes = new Uint8Array(buffer);
        let str = "";
        for (let i = 0; i < bytes.byteLength; i++) {
            str += String.fromCharCode(bytes[i]);
        }
        return btoa(str).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
    }

    function mapCredentialDescriptors(list: CredentialDescriptorJson[] | undefined): PublicKeyCredentialDescriptor[] {
        if (!list) return [];
        return list.map(c => ({
            id: base64UrlToBuffer(c.id),
            type: (c.type || "public-key") as PublicKeyCredentialType,
            transports: c.transports
        }));
    }

    window.vfWebAuthn = {
        isAvailable: function (): boolean {
            return !!(window.PublicKeyCredential && navigator.credentials);
        },

        register: async function (optionsJson: string): Promise<string> {
            const options = JSON.parse(optionsJson) as RegistrationOptionsJson;
            const publicKey: PublicKeyCredentialCreationOptions = {
                rp: options.rp,
                user: {
                    id: base64UrlToBuffer(options.user.id),
                    name: options.user.name,
                    displayName: options.user.displayName
                },
                challenge: base64UrlToBuffer(options.challenge),
                pubKeyCredParams: options.pubKeyCredParams,
                timeout: options.timeout,
                excludeCredentials: mapCredentialDescriptors(options.excludeCredentials),
                authenticatorSelection: options.authenticatorSelection,
                attestation: options.attestation || "none"
            };

            const credential = await navigator.credentials.create({ publicKey }) as PublicKeyCredential;
            const response = credential.response as AuthenticatorAttestationResponse;

            const result = {
                id: credential.id,
                rawId: bufferToBase64Url(credential.rawId),
                type: credential.type,
                response: {
                    attestationObject: bufferToBase64Url(response.attestationObject),
                    clientDataJSON: bufferToBase64Url(response.clientDataJSON)
                },
                extensions: {}
            };

            return JSON.stringify(result);
        },

        authenticate: async function (optionsJson: string): Promise<string> {
            const options = JSON.parse(optionsJson) as AssertionOptionsJson;
            const publicKey: PublicKeyCredentialRequestOptions = {
                challenge: base64UrlToBuffer(options.challenge),
                timeout: options.timeout,
                rpId: options.rpId,
                allowCredentials: mapCredentialDescriptors(options.allowCredentials),
                userVerification: options.userVerification
            };

            const credential = await navigator.credentials.get({ publicKey }) as PublicKeyCredential;
            const response = credential.response as AuthenticatorAssertionResponse;

            const result = {
                id: credential.id,
                rawId: bufferToBase64Url(credential.rawId),
                type: credential.type,
                response: {
                    authenticatorData: bufferToBase64Url(response.authenticatorData),
                    clientDataJSON: bufferToBase64Url(response.clientDataJSON),
                    signature: bufferToBase64Url(response.signature),
                    userHandle: response.userHandle ? bufferToBase64Url(response.userHandle) : null
                },
                extensions: {}
            };

            return JSON.stringify(result);
        },

        // Session-token persistence (session cookie - no Expires/Max-Age means the browser discards it
        // when the browser itself closes, while cookies are automatically shared across every tab/window
        // of the same browser so navigation and new tabs stay signed in. The token itself is a short-lived
        // opaque bearer credential via IDataProtector, not a secret worth extra protection beyond that.)
        saveSession: function (json: string): void {
            document.cookie = "vf.pairedSession=" + encodeURIComponent(json) + "; path=/; SameSite=Lax";
        },
        loadSession: function (): string | null {
            const match = document.cookie.match(/(?:^|;\s*)vf\.pairedSession=([^;]*)/);
            return match ? decodeURIComponent(match[1]) : null;
        },
        clearSession: function (): void {
            document.cookie = "vf.pairedSession=; path=/; SameSite=Lax; Max-Age=0";
        }
    };
})();
