using System;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Plaintext Ring account credentials, as loaded from or about to be saved to disk via <see cref="ICredentialStore"/>.
    /// Implements IDisposable to securely clear sensitive data from memory when disposed.
    /// </summary>
    public class RingCredentials : IDisposable
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public string RefreshToken { get; set; }

        public void Dispose()
        {
            ClearSensitiveData();
            GC.SuppressFinalize(this);
        }

        ~RingCredentials()
        {
            ClearSensitiveData();
        }

        private void ClearSensitiveData()
        {
            if (Password != null)
            {
                Array.Clear(Password.ToCharArray(), 0, Password.Length);
                Password = null;
            }

            if (RefreshToken != null)
            {
                Array.Clear(RefreshToken.ToCharArray(), 0, RefreshToken.Length);
                RefreshToken = null;
            }
        }
    }
}
