// Case note entry check, run on each keystroke by CaseNotes.razor so the add button is enabled only for a valid body.
// The server applies the same rule (trimmed, non-blank, at most 4000 characters) and stays the authority on save.
const caseNoteMaxLength = 4000;

window.vfCaseNoteCheck = function (body: string): { valid: boolean; trimmedLength: number; code: "empty" | "tooLong" | null } {
    // Trim only the ends, matching the server. Line breaks inside the body count toward the limit.
    const trimmedLength = body.trim().length;
    if (trimmedLength === 0) {
        return { valid: false, trimmedLength, code: "empty" };
    }
    if (trimmedLength > caseNoteMaxLength) {
        return { valid: false, trimmedLength, code: "tooLong" };
    }
    return { valid: true, trimmedLength, code: null };
};
