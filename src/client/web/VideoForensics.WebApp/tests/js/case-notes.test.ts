import { beforeAll, describe, expect, it } from "vitest";
import { loadClassicScript, uiScriptsRoot } from "./load-script";

// The case note entry check mirrors the server rule: the body is trimmed, then must be non-blank and at most 4000 characters.
// The server answers the same rule, so this check only decides whether the add button is enabled while the operator types.

const maxLength = 4000;

describe("vfCaseNoteCheck", () => {
    beforeAll(() => {
        loadClassicScript(`${uiScriptsRoot}/case-notes.js`);
    });

    it("rejects an empty body with the empty code", () => {
        expect(window.vfCaseNoteCheck("")).toEqual({ valid: false, trimmedLength: 0, code: "empty" });
    });

    it("rejects a whitespace-only body with the empty code", () => {
        expect(window.vfCaseNoteCheck(" \t\r\n  ")).toEqual({ valid: false, trimmedLength: 0, code: "empty" });
    });

    it("accepts a body of exactly 4000 characters", () => {
        const body = "a".repeat(maxLength);
        expect(window.vfCaseNoteCheck(body)).toEqual({ valid: true, trimmedLength: maxLength, code: null });
    });

    it("rejects a body of 4001 characters with the tooLong code", () => {
        const body = "a".repeat(maxLength + 1);
        expect(window.vfCaseNoteCheck(body)).toEqual({ valid: false, trimmedLength: maxLength + 1, code: "tooLong" });
    });

    it("measures the trimmed length, so 4000 characters with surrounding spaces is valid", () => {
        const body = `   ${"a".repeat(maxLength)}   `;
        expect(window.vfCaseNoteCheck(body)).toEqual({ valid: true, trimmedLength: maxLength, code: null });
    });

    it("keeps line breaks inside the body when measuring", () => {
        expect(window.vfCaseNoteCheck("first\nsecond")).toEqual({ valid: true, trimmedLength: 12, code: null });
    });
});
