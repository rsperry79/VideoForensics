// Ambient declarations for the window globals that Blazor reaches by name through JS interop.
// Each classic script assigns one of these members and the C# side calls it by string name, so
// the shapes here must match the call sites in the *.razor and *.cs files.

interface Window {
    vfWebAuthn: {
        isAvailable(): boolean;
        register(optionsJson: string): Promise<string>;
        authenticate(optionsJson: string): Promise<string>;
        saveSession(json: string): void;
        loadSession(): string | null;
        clearSession(): void;
    };
    vfWebPush: {
        isSupported(): Promise<boolean>;
        getPermissionState(): Promise<NotificationPermission>;
        subscribe(vapidPublicKeyBase64: string): Promise<{ endpoint: string; p256dhKey: string; authKey: string }>;
        unsubscribe(): Promise<string | null>;
        getCurrentSubscriptionEndpoint(): Promise<string | null>;
    };
    vfExportDownload: {
        trigger(url: string): void;
    };
}
