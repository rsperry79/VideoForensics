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

    /// <summary>Current report-settings permissions.</summary>
    /// <param name="ReportSettingsAccess">Lowest operator level that may change report settings: "SuperAdmin" (the default) or "Admin".</param>
    public record ReportPermissionsDto(string ReportSettingsAccess);

    /// <summary>Request to change who may change report settings.</summary>
    /// <param name="ReportSettingsAccess">New lowest operator level for changing report settings: "SuperAdmin" or "Admin".</param>
    public record UpdateReportPermissionsRequest(string ReportSettingsAccess);

    /// <summary>Current email-integration permissions.</summary>
    /// <param name="EmailSettingsAccess">Lowest operator level that may change email settings: "SuperAdmin" (the default) or "Admin".</param>
    public record EmailPermissionsDto(string EmailSettingsAccess);

    /// <summary>Request to change who may change email settings.</summary>
    /// <param name="EmailSettingsAccess">New lowest operator level for changing email settings: "SuperAdmin" or "Admin".</param>
    public record UpdateEmailPermissionsRequest(string EmailSettingsAccess);

    /// <summary>Current notification-settings permissions.</summary>
    /// <param name="NotificationSettingsAccess">Lowest operator level that may change notification settings: "SuperAdmin" (the default) or "Admin".</param>
    public record NotificationPermissionsDto(string NotificationSettingsAccess);

    /// <summary>Request to change who may change notification settings.</summary>
    /// <param name="NotificationSettingsAccess">New lowest operator level for changing notification settings: "SuperAdmin" or "Admin".</param>
    public record UpdateNotificationPermissionsRequest(string NotificationSettingsAccess);

    /// <summary>Which settings pages a Super Admin has opened to admins. Lets the UI hide pages an admin cannot use.</summary>
    /// <param name="ReportsOpenToAdmin">True when admins may change report settings.</param>
    /// <param name="EmailOpenToAdmin">True when admins may change email settings.</param>
    /// <param name="NotificationsOpenToAdmin">True when admins may change notification settings.</param>
    /// <param name="LlmOpenToAdmin">True when admins may change LLM settings.</param>
    public record SettingsAccessDto(bool ReportsOpenToAdmin, bool EmailOpenToAdmin, bool NotificationsOpenToAdmin, bool LlmOpenToAdmin);

    /// <summary>Current LLM-settings permissions.</summary>
    /// <param name="LlmSettingsAccess">Lowest operator level that may change LLM settings: "SuperAdmin" (the default) or "Admin".</param>
    public record LlmPermissionsDto(string LlmSettingsAccess);

    /// <summary>Request to change who may change LLM settings.</summary>
    /// <param name="LlmSettingsAccess">New lowest operator level for changing LLM settings: "SuperAdmin" or "Admin".</param>
    public record UpdateLlmPermissionsRequest(string LlmSettingsAccess);

    /// <summary>Request to change provider-account permissions.</summary>
    /// <param name="ProviderAccountEditAccess">New lowest operator level for editing provider accounts: "Admin" or "SuperAdmin".</param>
    public record UpdateProviderPermissionsRequest(string ProviderAccountEditAccess);
}
