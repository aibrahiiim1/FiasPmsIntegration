namespace FiasPmsIntegration.Models
{
    public class GuestData
    {
        public string ReservationNumber { get; set; } = string.Empty;
        public string RoomNumber { get; set; } = string.Empty;
        public string GuestName { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public DateTime ArrivalDate { get; set; }
        public DateTime DepartureDate { get; set; }
        public decimal Balance { get; set; }
        public string Status { get; set; } = "checked-in";
        public DateTime LastUpdate { get; set; } = DateTime.Now;
    }

}
