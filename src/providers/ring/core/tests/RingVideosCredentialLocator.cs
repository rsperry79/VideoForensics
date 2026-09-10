namespace VideoForensics.Providers.Ring.Core.Tests
{
    /// <summary>
    /// [DEPRECATED] Credentials are now stored exclusively in the database via RingAuthService.
    /// Use RealSessionHelper instead, which loads credentials from the database.
    /// </summary>
    internal static class RingVideosCredentialLocator
    {
        [Obsolete("Credentials are now stored in the database, not auth.json. Use RealSessionHelper instead.")]
        public static bool TryLoad(out string? userName, out string? password, out string? refreshToken)
        {
            userName = null;
            password = null;
            refreshToken = null;
            return false;
        }
    }
}
