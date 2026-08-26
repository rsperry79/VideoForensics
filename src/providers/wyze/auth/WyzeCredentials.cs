using System;

#nullable enable

namespace VideoForensics.Providers.Wyze;

/// <summary>Wyze account credentials</summary>
public record WyzeCredentials(
    string Email,
    string Password,
    string? AuthToken = null,
    DateTime? TokenExpiration = null);

