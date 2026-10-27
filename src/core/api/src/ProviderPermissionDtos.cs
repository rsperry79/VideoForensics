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

    /// <summary>Request to change provider-account permissions.</summary>
    /// <param name="ProviderAccountEditAccess">New lowest operator level for editing provider accounts: "Admin" or "SuperAdmin".</param>
    public record UpdateProviderPermissionsRequest(string ProviderAccountEditAccess);
}
