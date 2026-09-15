using System.Globalization;

namespace VideoForensics.Ui.Shared.Services
{
    /// <summary>Applies a culture change to the current logical flow. See ThemePreferenceService for callers.</summary>
    public interface ICultureSwitcher
    {
        void Apply(string cultureName);
    }

    /// <summary>
    /// Sets CurrentCulture/CurrentUICulture on the calling logical flow. .NET flows CultureInfo
    /// through ExecutionContext across await points, so this only affects the current
    /// component/circuit's subsequent renders - not the whole process - making one implementation
    /// safe for both the single-process MAUI host and the multi-circuit Blazor Server WebApp host.
    /// </summary>
    public class CultureSwitcher : ICultureSwitcher
    {
        public void Apply(string cultureName)
        {
            try
            {
                var culture = CultureInfo.GetCultureInfo(cultureName);
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
            }
            catch (CultureNotFoundException)
            {
                // Unknown culture name (e.g. corrupted stored preference) - leave the ambient culture as-is.
            }
        }
    }
}
