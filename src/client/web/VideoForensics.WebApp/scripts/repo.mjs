// Shared setup for the JavaScript tooling. ESLint and Vitest resolve file patterns from the working
// directory, and the sources they check live across the repo, so every tool runs from the repo root.
// Importing this file moves the process there and exposes the tool binaries from this package's node_modules.
import { fileURLToPath } from "node:url";
import { dirname, join, resolve } from "node:path";

export const packageDir = resolve(dirname(fileURLToPath(import.meta.url)), "..");
export const repoRoot = resolve(packageDir, "../../../..");

process.chdir(repoRoot);

export const eslintBin = join(packageDir, "node_modules", "eslint", "bin", "eslint.js");
export const vitestBin = join(packageDir, "node_modules", "vitest", "vitest.mjs");
export const eslintConfig = "src/client/web/VideoForensics.WebApp/eslint.config.mjs";
export const vitestConfig = "src/client/web/VideoForensics.WebApp/vitest.config.mts";

export const lintTargets = [
    "src/client/ui/VideoForensics.Ui.Shared/src/ts",
    "src/client/web/VideoForensics.WebApp/src/ts",
    "src/client/web/VideoForensics.WebApp/tests",
    "src/client/web/VideoForensics.WebApp/scripts",
    "src/client/web/VideoForensics.WebApp/vitest.config.mts",
    "src/client/web/VideoForensics.WebApp/eslint.config.mjs",
];
