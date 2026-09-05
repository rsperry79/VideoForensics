using System.Collections.Generic;

using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Providers.Ring.Models
{
    public class DeviceInfo
    {
        public string Name { get; set; }
        public long Id { get; set; }
        public string DeviceId { get; set; }
    }

    public class DeviceList
    {
        public List<DeviceInfo> Devices { get; } = [];
        public DeviceList ExtractDevices(Devices ringDevices)
        {
            foreach (Doorbot x in ringDevices.Doorbots)
            {
                Devices.Add(new DeviceInfo() { Id = x.Id, Name = x.Description, DeviceId = x.DeviceId });
            }

            foreach (Chime x in ringDevices.Chimes)
            {
                Devices.Add(new DeviceInfo() { Id = x.Id, Name = x.Description, DeviceId = x.DeviceId });
            }

            foreach (Doorbot x in ringDevices.AuthorizedDoorbots)
            {
                Devices.Add(new DeviceInfo() { Id = x.Id, Name = x.Description, DeviceId = x.DeviceId });
            }

            foreach (StickupCam x in ringDevices.StickupCams)
            {
                Devices.Add(new DeviceInfo() { Id = x.Id.Value, Name = x.Description, DeviceId = x.DeviceId });
            }

            return this;
        }
    }
}
