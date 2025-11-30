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
    }
}
