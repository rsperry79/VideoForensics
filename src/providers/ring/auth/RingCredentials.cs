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

        private string _password;
        public string Password
        {
            get => _password;
            set => _password = value;
        }

        private string _refreshToken;
        public string RefreshToken
        {
            get => _refreshToken;
            set => _refreshToken = value;
        }

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
            if (_password != null)
            {
                Array.Clear(_password.ToCharArray(), 0, _password.Length);
                _password = null;
            }
            if (_refreshToken != null)
            {
                Array.Clear(_refreshToken.ToCharArray(), 0, _refreshToken.Length);
                _refreshToken = null;
            }
        }
    }
}
