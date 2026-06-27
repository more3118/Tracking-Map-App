namespace Tracking_Map_App.Models
{
    public class LocationData
    {
        public int Id { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime Timestamp { get; set; }
        public double Accuracy { get; set; }
        public double Altitude { get; set; }
        public double Speed { get; set; }
        public bool IsSynced { get; set; } = false;
    }
}
