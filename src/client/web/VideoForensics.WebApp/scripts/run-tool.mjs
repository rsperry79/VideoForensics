// Runs a named JavaScript tool from the repo root: `node scripts/run-tool.mjs <lint|test>`.
// Used by the npm scripts and by check-js.mjs, so the lint and test commands are defined once.
import { spawnSync } from "node:child_process";
import { eslintBin, eslintConfig, lintTargets, vitestBin, vitestConfig } from "./repo.mjs";

const commands = {
    lint: [eslintBin, ["-c", eslintConfig, ...lintTargets]],
    test: [vitestBin, ["run", "--config", vitestConfig]],
};

const [name] = process.argv.slice(2);
if (!commands[name]) {
    console.error(`Unknown command "${name}". Use ${Object.keys(commands).join(" or ")}.`);
    process.exit(1);
}

const [bin, args] = commands[name];
const result = spawnSync(process.execPath, [bin, ...args], { stdio: "inherit" });
process.exit(result.status ?? 1);
