import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

// Not new URL(): jsdom replaces the global URL class, and it rejects the file: base.
const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "../..");

// Classic (non-module) scripts assign to window.* at load time, so they must run in the global
// scope. Indirect eval is the global-scope form of eval, and in the jsdom environment the global
// object is the window, so the assignments land where the Blazor side would find them.
export function loadClassicScript(relativePath: string): void {
    const source = readFileSync(resolve(repoRoot, relativePath), "utf8");
    (0, eval)(source);
}

export const scriptsRoot = "src/client/web/VideoForensics.WebApp/src/wwwroot/scripts";
export const uiScriptsRoot = "src/client/ui/VideoForensics.Ui.Shared/src/wwwroot/scripts";
