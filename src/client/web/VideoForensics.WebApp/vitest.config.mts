import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

// Tests run against the compiled wwwroot/scripts output, which is what the browser actually loads.
// The test:js script builds first so the output is never stale.
// The root is the repo root: the suites span the root tests/ folder and both packages' tests/js folders.
export default defineConfig({
    root: fileURLToPath(new URL("../../../..", import.meta.url)),
    test: {
        environment: "jsdom",
        include: ["tests/js/**/*.test.ts", "src/client/web/VideoForensics.WebApp/tests/js/**/*.test.ts"],
    },
});
