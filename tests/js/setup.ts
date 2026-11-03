// Node 25+ exposes an experimental localStorage global that is undefined without --localstorage-file,
// and it hides the jsdom storage. Where the test global has no storage, install an in-memory one on the
// Storage prototype, so the layout-prefs tests exercise real getItem/setItem calls.
if (typeof window !== "undefined" && typeof window.localStorage === "undefined") {
    class MemoryStorage {
        #items = new Map();
        getItem(key) { return this.#items.has(String(key)) ? this.#items.get(String(key)) : null; }
        setItem(key, value) { this.#items.set(String(key), String(value)); }
        removeItem(key) { this.#items.delete(String(key)); }
        clear() { this.#items.clear(); }
        key(index) { return [...this.#items.keys()][index] ?? null; }
        get length() { return this.#items.size; }
    }
    Object.defineProperty(globalThis, "Storage", { configurable: true, value: MemoryStorage });
    Object.defineProperty(globalThis, "localStorage", { configurable: true, value: new MemoryStorage() });
}
