using FiasPmsIntegration.Models;

namespace FiasPmsIntegration.Services
{
    public class GuestDataStore
    {
        private readonly Dictionary<string, GuestData> _guests = new();
        private readonly object _lock = new();

        public void AddOrUpdateGuest(GuestData guest)
        {
            lock (_lock)
            {
                _guests[guest.ReservationNumber] = guest;
            }
        }

        public void RemoveGuest(string reservationNumber)
        {
            lock (_lock)
            {
                _guests.Remove(reservationNumber);
            }
        }

        public GuestData? GetGuest(string reservationNumber)
        {
            lock (_lock)
            {
                return _guests.TryGetValue(reservationNumber, out var guest) ? guest : null;
            }
        }

        public List<GuestData> GetAllGuests()
        {
            lock (_lock)
            {
                return _guests.Values.ToList();
            }
        }

        public List<GuestData> GetGuestsByRoomOrName(string search)
        {
            lock (_lock)
            {
                return _guests.Values
                    .Where(g => g.RoomNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                               g.GuestName.Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }
    }
}
