using System.Reflection;

namespace VideoForensics.Providers.Wyze;

/// <summary>Wyze API provider package information</summary>
public static class Package
{
    private static string? _version;

    /// <summary>Gets the version of the Wyze API package</summary>
    public static string Version
    {
        get
        {
            _version ??= typeof(Package).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? "1.0.0-placeholder";

            return _version;
        }
    }
}
