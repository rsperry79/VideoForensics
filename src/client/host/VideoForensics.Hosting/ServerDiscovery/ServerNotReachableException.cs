namespace VideoForensics.Hosting.ServerDiscovery
{
    /// <summary>
    /// Thrown when the server cannot be located via mDNS discovery and no cached Internet URL is available.
    /// This typically indicates that the device has not yet been paired and the server address is not known.
    /// </summary>
    public class ServerNotReachableException : Exception
    {
        public ServerNotReachableException()
            : base("Server not reachable: local mDNS discovery failed and no cached Internet URL is available. Device pairing may be required.")
        {
        }

        public ServerNotReachableException(string message)
            : base(message)
        {
        }

        public ServerNotReachableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
