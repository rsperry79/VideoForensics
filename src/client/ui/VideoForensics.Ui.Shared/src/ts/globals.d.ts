// Ambient declarations for the window globals that Blazor reaches by name through JS interop.
// Each classic script assigns one of these members and the C# side calls it by string name, so
// the shapes here must match the call sites in the *.razor and *.cs files.

interface DotNetObjectReference {
    invokeMethodAsync<T = unknown>(methodName: string, ...args: unknown[]): Promise<T>;
}

interface Window {
    vfClickElement: (element: HTMLElement | null) => void;
    vfDeviceLabel: { get(): string };
    vfLayoutPrefs: { get(key: string): string | null; set(key: string, value: string): void };
    vfPanelResize: {
        attach(handleId: string, panelId: string, options: { min?: number; max?: number } | null, dotNetRef: DotNetObjectReference | null): void;
        detach(handleId: string): void;
    };
    vfSetVideoTime: (element: HTMLVideoElement | null, seconds: number) => void;
    vfGetVideoTime: (element: HTMLVideoElement | null) => number;
    vfTheme: {
        getPrefersDark(): boolean;
        getStoredMode(): string | null;
        setStoredMode(mode: string): void;
        watchPrefersDark(dotNetRef: DotNetObjectReference): void;
        setStylesheet(isDark: boolean): void;
    };
    vfViewport: {
        getIsMobile(): boolean;
        watchViewport(dotNetRef: DotNetObjectReference): void;
    };
    // Set by startLiveView so that addIceCandidate can reach the connection it created.
    _liveViewPeerConnection?: RTCPeerConnection;
}
