namespace KoenZomers.Ring.Api
{
    /// <summary>
    /// Plaintext Ring account credentials, as loaded from or about to be saved to disk via <see cref="ICredentialStore"/>.
    /// </summary>
    public class RingCredentials
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public string RefreshToken { get; set; }
    }
}
