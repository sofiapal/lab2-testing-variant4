namespace Energy_Project.Models
{
    public class Device
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public double PowerUsageWatts { get; set; }
        public bool IsOn { get; set; }
    }
}
