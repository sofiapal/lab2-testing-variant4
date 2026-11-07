using Energy_Project.Models;
using Energy_Project.Services.Interfaces;

namespace Energy_Project.Services
{
    public class DeviceService
    {
        private readonly IDeviceRepository _repo;

        public DeviceService(IDeviceRepository repo)
        {
            _repo = repo;
        }

        public bool ToggleDevice(int id, bool turnOn)
        {
            var device = _repo.GetById(id);
            if (device == null)
                throw new ArgumentException("Device not found");

            device.IsOn = turnOn;
            _repo.Update(device);
            return device.IsOn;
        }

        public IEnumerable<Device> GetActiveDevices()
        {
            return _repo.GetAll().Where(d => d.IsOn);
        }
    }

}
