namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// The kind of record a <see cref="Note"/> is attached to. Stored by name.
    /// The target ID is interpreted by kind: <see cref="Case"/> is <c>ForensicCase.Id</c>,
    /// <see cref="JammingIncident"/> is <c>JammingIncidentRecord.Id</c>, and <see cref="MediaFlag"/> is <c>MediaFlag.Id</c>.
    /// </summary>
    public enum NoteTargetKind
    {
        /// <summary>A forensic case. The repository checks that the case exists and is open.</summary>
        Case,

        /// <summary>A jamming incident record. The repository does not check that the record exists.</summary>
        JammingIncident,

        /// <summary>A media flag. The repository does not check that the flag exists.</summary>
        MediaFlag,
    }
}
