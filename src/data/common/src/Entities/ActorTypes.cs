namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Works out who acted for an audit entry. A Windows service or computer account is not a person, so work the service
    /// does on its own (scheduled downloads, sync, retention) is recorded as <see cref="ActorType.System"/>.
    /// </summary>
    public static class ActorTypes
    {
        /// <summary>
        /// True for a machine or built-in service account: a computer account (its name ends with '$', such as
        /// "DRAGONFIRE$") or one of the Windows service identities (SYSTEM, LOCAL SERVICE, NETWORK SERVICE).
        /// </summary>
        public static bool IsServiceAccount(string? userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                return false;
            }

            string name = userName.Trim();
            return name.EndsWith('$')
                || string.Equals(name, "SYSTEM", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "LOCAL SERVICE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "NETWORK SERVICE", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The actor type for work done by <paramref name="userName"/> when no person is signed in for it.</summary>
        public static ActorType ForAccount(string? userName)
        {
            return IsServiceAccount(userName) ? ActorType.System : ActorType.Human;
        }
    }
}
