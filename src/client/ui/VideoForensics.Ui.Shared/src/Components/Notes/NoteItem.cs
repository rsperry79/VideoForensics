namespace VideoForensics.Client.Ui.Shared.Components.Notes;

/// <summary>One note as the notes panel shows it: an append-only, multi-line entry with its author and write time.</summary>
/// <param name="Id">Note ID, used to key the list.</param>
/// <param name="Author">Author as shown to the operator, for example the operator ID text.</param>
/// <param name="AddedAtUtc">UTC instant the note was written. Shown in the user's local time.</param>
/// <param name="Body">Note text, shown with its line breaks kept.</param>
public sealed record NoteItem(Guid Id, string Author, DateTime AddedAtUtc, string Body);
