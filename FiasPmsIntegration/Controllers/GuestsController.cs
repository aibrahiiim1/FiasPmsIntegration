using FiasPmsIntegration.Models;
using FiasPmsIntegration.Services;
using Microsoft.AspNetCore.Mvc;

namespace FiasPmsIntegration.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GuestsController : ControllerBase
    {
        private readonly GuestDataStore _guestStore;

        public GuestsController(GuestDataStore guestStore)
        {
            _guestStore = guestStore;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            return Ok(_guestStore.GetAllGuests());
        }

        [HttpGet("{reservationNumber}")]
        public IActionResult Get(string reservationNumber)
        {
            var guest = _guestStore.GetGuest(reservationNumber);
            if (guest == null)
                return NotFound();

            return Ok(guest);
        }

        [HttpPost("test")]
        public IActionResult AddTestGuest()
        {
            var guest = new GuestData
            {
                ReservationNumber = "TEST" + DateTime.Now.Ticks,
                RoomNumber = "101",
                GuestName = "John Doe (Test)",
                Language = "EN",
                ArrivalDate = DateTime.Now,
                DepartureDate = DateTime.Now.AddDays(3),
                Balance = 0,
                Status = "checked-in"
            };

            _guestStore.AddOrUpdateGuest(guest);
            return Ok(new
            {
                message = "Test guest added successfully",
                guest
            });
        }

        [HttpDelete("test")]
        public IActionResult ClearAllGuests()
        {
            // Get all guests and remove them
            var guests = _guestStore.GetAllGuests();
            foreach (var guest in guests)
            {
                _guestStore.RemoveGuest(guest.ReservationNumber);
            }

            return Ok(new
            {
                message = $"Cleared {guests.Count} guests"
            });
        }
    }
}