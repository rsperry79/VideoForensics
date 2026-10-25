// Compiles every TypeScript interop project into the wwwroot folder that Blazor serves.
// A Node script rather than an npm "&&" chain, so the build runs the same from PowerShell and cmd.
import { spawnSync } from "node:child_process";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const tscEntry = require.resolve("typescript/bin/tsc");

const projects = [
    "src/client/ui/VideoForensics.Ui.Shared/src/ts/tsconfig.json",
    "src/client/web/VideoForensics.WebApp/src/ts/tsconfig.json",
    "src/client/web/VideoForensics.WebApp/src/ts/tsconfig.sw.json",
];

for (const project of projects) {
    const result = spawnSync(process.execPath, [tscEntry, "-p", project], { stdio: "inherit" });
    if (result.status !== 0) {
        process.exit(result.status ?? 1);
    }
}
