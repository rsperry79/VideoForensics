// Browser-side WebAuthn ceremony glue (plan §5.1/§5.10). Converts between the base64url-encoded
// JSON shapes Fido2NetLib (server) expects/produces and the ArrayBuffer-based shapes the browser's
// native navigator.credentials API requires. Property names on both sides (challenge, rp, user,
// pubKeyCredParams, excludeCredentials/allowCredentials, attestationObject, clientDataJSON,
// authenticatorData, signature, userHandle) were confirmed against the installed Fido2.Models
// 4.0.1 assembly, not guessed - a mismatch here is exactly the "looks right and isn't" risk the
// project plan calls out for this ceremony.
(function () {
    function base64UrlToBuffer(base64url) {
        const padding = "=".repeat((4 - (base64url.length % 4)) % 4);
        const base64 = (base64url + padding).replace(/-/g, "+").replace(/_/g, "/");
        const raw = atob(base64);
        const buffer = new Uint8Array(raw.length);
        for (let i = 0; i < raw.length; i++) {
            buffer[i] = raw.charCodeAt(i);
        }
        return buffer.buffer;
    }

    function bufferToBase64Url(buffer) {
        const bytes = new Uint8Array(buffer);
        let str = "";
        for (let i = 0; i < bytes.byteLength; i++) {
            str += String.fromCharCode(bytes[i]);
        }
        return btoa(str).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
    }

    function mapCredentialDescriptors(list) {
        if (!list) return [];
        return list.map(c => ({
            id: base64UrlToBuffer(c.id),
            type: c.type || "public-key",
            transports: c.transports
        }));
    }

    window.vfWebAuthn = {
        isAvailable: function () {
            return !!(window.PublicKeyCredential && navigator.credentials);
        },

        register: async function (optionsJson) {
            const options = JSON.parse(optionsJson);
            const publicKey = {
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

            const credential = await navigator.credentials.create({ publicKey });

            const result = {
                id: credential.id,
                rawId: bufferToBase64Url(credential.rawId),
                type: credential.type,
                response: {
                    attestationObject: bufferToBase64Url(credential.response.attestationObject),
                    clientDataJSON: bufferToBase64Url(credential.response.clientDataJSON)
                },
                extensions: {}
            };

            return JSON.stringify(result);
        },

        authenticate: async function (optionsJson) {
            const options = JSON.parse(optionsJson);
            const publicKey = {
                challenge: base64UrlToBuffer(options.challenge),
                timeout: options.timeout,
                rpId: options.rpId,
                allowCredentials: mapCredentialDescriptors(options.allowCredentials),
                userVerification: options.userVerification
            };

            const credential = await navigator.credentials.get({ publicKey });

            const result = {
                id: credential.id,
                rawId: bufferToBase64Url(credential.rawId),
                type: credential.type,
                response: {
                    authenticatorData: bufferToBase64Url(credential.response.authenticatorData),
                    clientDataJSON: bufferToBase64Url(credential.response.clientDataJSON),
                    signature: bufferToBase64Url(credential.response.signature),
                    userHandle: credential.response.userHandle ? bufferToBase64Url(credential.response.userHandle) : null
                },
                extensions: {}
            };

            return JSON.stringify(result);
        },

        // Session-token persistence (plain localStorage - the token itself is a short-lived opaque
        // bearer credential via IDataProtector, not a secret worth extra protection beyond what the
        // browser already gives same-origin storage).
        saveSession: function (json) {
            localStorage.setItem("vf.pairedSession", json);
        },
        loadSession: function () {
            return localStorage.getItem("vf.pairedSession");
        },
        clearSession: function () {
            localStorage.removeItem("vf.pairedSession");
        }
    };
})();
