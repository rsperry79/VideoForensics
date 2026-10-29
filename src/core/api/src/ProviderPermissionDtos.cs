namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Wire and stored values for who may edit provider account settings. Deleting a provider account is always
    /// SuperAdmin only and is not governed by this setting.
    /// </summary>
    public static class ProviderEditAccessLevels
    {
        /// <summary>Admin and up may edit provider account settings. The default when nothing is stored.</summary>
        public const string Admin = "Admin";

        /// <summary>Only SuperAdmin may edit provider account settings.</summary>
        public const string SuperAdmin = "SuperAdmin";
    }

    /// <summary>Current provider-account permissions.</summary>
    /// <param name="ProviderAccountEditAccess">Lowest operator level that may edit provider accounts: "Admin" or "SuperAdmin".</param>
    public record ProviderPermissionsDto(string ProviderAccountEditAccess);

    /// <summary>The settings pages a Super Admin can open to admins. Everything else stays Super Admin only.</summary>
    [Flags]
    public enum RestrictedSettings
    {
        None = 0,
        Reports = 1,
        Email = 2,
        Notifications = 4,
        Llm = 8,
    }

    /// <summary>Who may change one restricted settings page.</summary>
    /// <param name="Access">Lowest operator level: "SuperAdmin" (the default) or "Admin".</param>
    public record SettingsAccessLevelDto(string Access);

    /// <summary>Request to change who may change one restricted settings page.</summary>
    /// <param name="Access">New lowest operator level: "SuperAdmin" or "Admin".</param>
    public record UpdateSettingsAccessRequest(string Access);

    /// <summary>Which settings pages a Super Admin has opened to admins. Lets the UI hide pages an admin cannot use.</summary>
    /// <param name="ReportsOpenToAdmin">True when admins may change report settings.</param>
    /// <param name="EmailOpenToAdmin">True when admins may change email settings.</param>
    /// <param name="NotificationsOpenToAdmin">True when admins may change notification settings.</param>
    /// <param name="LlmOpenToAdmin">True when admins may change LLM settings.</param>
    public record SettingsAccessDto(bool ReportsOpenToAdmin, bool EmailOpenToAdmin, bool NotificationsOpenToAdmin, bool LlmOpenToAdmin);

    /// <summary>Request to change provider-account permissions.</summary>
    /// <param name="ProviderAccountEditAccess">New lowest operator level for editing provider accounts: "Admin" or "SuperAdmin".</param>
    public record UpdateProviderPermissionsRequest(string ProviderAccountEditAccess);
}
