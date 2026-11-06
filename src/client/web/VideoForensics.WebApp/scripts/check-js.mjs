// The JavaScript/TypeScript gate: compile, lint, test, then confirm the committed compiled output
// matches the sources. Run by `npm run check`, by the Check JavaScript CI job, and by the full gate.
// Each step runs in its own process and stops at the first failure, so the first error is the one shown.
import { spawnSync } from "node:child_process";
import { createRequire } from "node:module";
import { repoRoot } from "./repo.mjs";

const require = createRequire(import.meta.url);
const runTool = require.resolve("./run-tool.mjs");

// Each command is one string run through the shell, so the Node binary path is quoted for Windows and POSIX alike.
function run(label, commandLine) {
    console.log(`\n== ${label} ==`);
    const result = spawnSync(commandLine, { stdio: "inherit", shell: true, cwd: repoRoot });
    if (result.status !== 0) {
        console.error(`\n${label} failed (exit ${result.status ?? "signal"}).`);
        process.exit(result.status ?? 1);
    }
}

run("Compile TypeScript", `"${process.execPath}" "${require.resolve("./build-ts.mjs")}"`);
run("Lint", `"${process.execPath}" "${runTool}" lint`);
run("Test", `"${process.execPath}" "${runTool}" test`);

// Compiled output is committed, so a source edit without a rebuild would ship stale JavaScript.
// git diff compares the working tree with the index, so it is clean only when the rebuild changed nothing.
const compiledPaths = [
    "src/client/ui/VideoForensics.Ui.Shared/src/wwwroot/scripts",
    "src/client/web/VideoForensics.WebApp/src/wwwroot/scripts",
    "src/client/web/VideoForensics.WebApp/src/wwwroot/push-sw.js",
];
const drift = spawnSync("git", ["diff", "--quiet", "--", ...compiledPaths], { stdio: "ignore", cwd: repoRoot });
if (drift.status !== 0) {
    console.error("\nCompiled output is out of date. Run `npm run build:ts` and commit the changed files under wwwroot.");
    process.exit(1);
}

console.log("\nJavaScript checks passed.");
