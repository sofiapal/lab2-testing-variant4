using Energy_Project.Services.Interfaces;

namespace Energy_Project.Services
{
    public class EnergyMonitorService
    {
        private readonly IDeviceRepository _devices;
        private readonly IEnergyPlanRepository _plans;
        private readonly INotificationService _notify;

        public EnergyMonitorService(IDeviceRepository devices, IEnergyPlanRepository plans, INotificationService notify)
        {
            _devices = devices;
            _plans = plans;
            _notify = notify;
        }

        public double CalculateCurrentUsageKwh()
        {
            var activeDevices = _devices.GetAll().Where(d => d.IsOn);
            return activeDevices.Sum(d => d.PowerUsageWatts) / 1000.0;
        }

        public void CheckForOverload()
        {
            var usage = CalculateCurrentUsageKwh();
            var plan = _plans.GetCurrentPlan();

            if (usage > plan.DailyLimitKwh)
                _notify.SendAlert($"Overload detected: {usage} kWh used!");
        }

        public void UpdateEnergyLimit(double newLimit)
        {
            var plan = _plans.GetCurrentPlan();
            plan.DailyLimitKwh = newLimit;
            _plans.UpdatePlan(plan);
        }
    }

}
