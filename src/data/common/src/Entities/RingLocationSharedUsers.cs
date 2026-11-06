namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One user shared onto a Ring location (GET clients_api/locations/{id}/users). Device grants are child rows.</summary>
    public class RingLocationSharedUser
    {
        public Guid Id { get; set; }
        public Guid LocationId { get; set; }
        public long ProviderUserId { get; set; }
        public bool? Verified { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? ObjectType { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public List<RingLocationSharedUserDevice> Devices { get; set; } = [];
    }

    /// <summary>One device grant of a shared user: the role and device type apply to that device only.</summary>
    public class RingLocationSharedUserDevice
    {
        public Guid Id { get; set; }
        public Guid RingLocationSharedUserId { get; set; }
        public int Position { get; set; }
        public long ProviderDeviceId { get; set; }
        public string? DeviceType { get; set; }
        public string? Role { get; set; }
        public List<RingLocationSharedUserDevicePermission> Permissions { get; set; } = [];
    }

    /// <summary>One permission of a device grant. Element type is a guess (permissions is null in every sample).</summary>
    public class RingLocationSharedUserDevicePermission
    {
        public Guid Id { get; set; }
        public Guid RingLocationSharedUserDeviceId { get; set; }
        public int Position { get; set; }
        public string? Permission { get; set; }
    }
}
