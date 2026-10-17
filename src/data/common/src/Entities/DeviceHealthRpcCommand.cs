namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One entry of a health reading's supported_rpc_commands, in the order Ring reports them.</summary>
    public class DeviceHealthRpcCommand
    {
        public Guid Id { get; set; }
        public Guid DeviceHealthId { get; set; }
        /// <summary>Zero-based index in Ring's array; preserves order.</summary>
        public int Position { get; set; }
        public string Command { get; set; } = string.Empty;
    }
}
