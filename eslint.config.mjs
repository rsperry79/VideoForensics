// Flat config for the TypeScript sources and the test suite. The compiled wwwroot output is not
// linted: it is generated, and linting it would only report on tsc's emit.
import js from "@eslint/js";
import globals from "globals";
import tseslint from "typescript-eslint";

export default tseslint.config(
    {
        ignores: ["**/wwwroot/**", "node_modules/**"],
    },
    js.configs.recommended,
    ...tseslint.configs.recommended,
    {
        files: ["src/client/**/src/ts/**/*.ts"],
        languageOptions: {
            globals: globals.browser,
        },
    },
    {
        files: ["src/client/web/VideoForensics.WebApp/src/ts/push-sw.ts"],
        languageOptions: {
            globals: globals.serviceworker,
        },
    },
    {
        files: ["tests/**/*.ts", "vitest.config.ts", "scripts/**/*.mjs"],
        languageOptions: {
            globals: globals.node,
        },
    },
);
