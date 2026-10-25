"use strict";
// Builds a short, human-readable label for this browser and OS, e.g. "Chrome on Windows", used to
// prefill the passkey name field. Browsers do not expose the machine name, so this never tries to read it.
// Prefers navigator.userAgentData.platform (Chromium) and falls back to the UA string (Firefox, Safari).
(function () {
    function browserFromUserAgent(ua) {
        if (/Edg\//.test(ua))
            return "Edge";
        if (/Firefox\/|FxiOS\//.test(ua))
            return "Firefox";
        if (/Chrome\/|CriOS\//.test(ua))
            return "Chrome";
        if (/Safari\//.test(ua))
            return "Safari";
        return "Browser";
    }
    // iOS and Android are checked before macOS and Linux because their UA strings also contain those tokens.
    function platformFromUserAgent(ua) {
        if (/iPhone|iPad|iPod/.test(ua))
            return "iOS";
        if (/Android/.test(ua))
            return "Android";
        if (/Windows/.test(ua))
            return "Windows";
        if (/Mac OS X|Macintosh/.test(ua))
            return "macOS";
        if (/Linux/.test(ua))
            return "Linux";
        return "Device";
    }
    function platformFromUserAgentData(raw) {
        switch (raw) {
            case "Windows": return "Windows";
            case "macOS": return "macOS";
            case "Linux": return "Linux";
            case "Android": return "Android";
            case "iOS": return "iOS";
            default: return raw || "Device";
        }
    }
    // userAgentData is Chromium-only and missing from some TypeScript DOM lib versions, so read it
    // through a narrow local type rather than relying on the global Navigator declaration.
    const nav = navigator;
    window.vfDeviceLabel = {
        get: function () {
            const ua = navigator.userAgent || "";
            const uaDataPlatform = nav.userAgentData && nav.userAgentData.platform;
            const platform = uaDataPlatform
                ? platformFromUserAgentData(uaDataPlatform)
                : platformFromUserAgent(ua);
            return browserFromUserAgent(ua) + " on " + platform;
        }
    };
})();
