using Energy_Project.Models;

namespace Energy_Project.Services.Interfaces
{
    public interface IDeviceRepository
    {
        IEnumerable<Device> GetAll();
        Device? GetById(int id);
        void Update(Device device);
    }


}
