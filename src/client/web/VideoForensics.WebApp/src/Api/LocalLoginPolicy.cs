namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>What a password sign-in leads to.</summary>
    public enum LocalPasswordOutcome
    {
        /// <summary>The sign-in completes with the password alone.</summary>
        Complete,

        /// <summary>The operator is taken to add a passkey, through a limited setup session.</summary>
        AddPasskey,

        /// <summary>The existing two-factor flow applies.</summary>
        Normal,
    }

    /// <summary>
    /// Decides what a password sign-in does. On the local network (a direct request, not the tunnel), an operator who does not require
    /// a passkey signs in with the password alone; one who does is taken to add a passkey. Over the tunnel the normal flow applies.
    /// </summary>
    public static class LocalLoginPolicy
    {
        /// <param name="passkeyRequired">The operator's setting. When false, the password alone is enough on the local network.</param>
        /// <param name="isDirectLocal">True for a direct local request, as opposed to the tunnel.</param>
        public static LocalPasswordOutcome Decide(bool passkeyRequired, bool isDirectLocal)
        {
            if (!isDirectLocal)
            {
                return LocalPasswordOutcome.Normal;
            }

            return passkeyRequired ? LocalPasswordOutcome.AddPasskey : LocalPasswordOutcome.Complete;
        }
    }
}
