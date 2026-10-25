import { defineConfig } from "vitest/config";

// Tests run against the compiled wwwroot/scripts output, which is what the browser actually loads.
// The test:js script builds first so the output is never stale.
export default defineConfig({
    test: {
        environment: "jsdom",
        include: ["tests/js/**/*.test.ts"],
    },
});
