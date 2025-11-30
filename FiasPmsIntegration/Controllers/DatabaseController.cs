using FiasPmsIntegration.Services;
using Microsoft.AspNetCore.Mvc;

namespace FiasPmsIntegration.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DatabaseController : ControllerBase
    {
        private readonly FiasSocketServer _socketServer;
        private readonly GuestDataStore _guestStore;
        private readonly LogService _logService;

        public DatabaseController(
            FiasSocketServer socketServer,
            GuestDataStore guestStore,
            LogService logService)
        {
            _socketServer = socketServer;
            _guestStore = guestStore;
            _logService = logService;
        }

        /// <summary>
        /// Request full database resync from PMS - Gets all in-house guests
        /// </summary>
        [HttpPost("resync")]
        public async Task<IActionResult> RequestResync()
        {
            if (!_socketServer.IsConnected)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "PMS is not connected. Cannot request database resync.",
                    isConnected = false
                });
            }

            try
            {
                _logService.Log("INFO", "Database resync requested via API");

                // Send DR (Database Resync Request) to PMS
                await _socketServer.SendMessageAsync("DR", new Dictionary<string, string>
                {
                    { "DA", DateTime.Now.ToString("yyMMdd") },
                    { "TI", DateTime.Now.ToString("HHmmss") }
                });

                return Ok(new
                {
                    success = true,
                    message = "Database resync request sent to PMS. The PMS should respond with all in-house guests.",
                    timestamp = DateTime.Now,
                    note = "Check /api/guests after a few seconds to see the updated guest list"
                });
            }
            catch (Exception ex)
            {
                _logService.Log("ERROR", $"Failed to send resync request: {ex.Message}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error sending resync request",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Send Database Start (DS) - Initiates sending of all guest data
        /// </summary>
        [HttpPost("start-sync")]
        public async Task<IActionResult> StartDatabaseSync()
        {
            if (!_socketServer.IsConnected)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "PMS is not connected",
                    isConnected = false
                });
            }

            try
            {
                _logService.Log("INFO", "Database Start (DS) sent via API");

                // Send DS (Database Start)
                await _socketServer.SendMessageAsync("DS", new Dictionary<string, string>
                {
                    { "DA", DateTime.Now.ToString("yyMMdd") },
                    { "TI", DateTime.Now.ToString("HHmmss") }
                });

                return Ok(new
                {
                    success = true,
                    message = "Database Start sent. PMS should begin sending guest records.",
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logService.Log("ERROR", $"Failed to send DS: {ex.Message}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error sending Database Start",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get current guest count and statistics
        /// </summary>
        [HttpGet("stats")]
        public IActionResult GetStats()
        {
            var guests = _guestStore.GetAllGuests();
            var checkedIn = guests.Count(g => g.Status == "checked-in");
            var totalBalance = guests.Sum(g => g.Balance);

            return Ok(new
            {
                totalGuests = guests.Count,
                checkedIn,
                rooms = guests.Select(g => g.RoomNumber).Distinct().Count(),
                totalBalance,
                lastUpdate = guests.Any() ? guests.Max(g => g.LastUpdate) : (DateTime?)null,
                isConnectedToPms = _socketServer.IsConnected,
                guestsByLanguage = guests.GroupBy(g => g.Language)
                    .Select(g => new { language = g.Key, count = g.Count() })
            });
        }

        /// <summary>
        /// Clear all guest data from local storage
        /// </summary>
        [HttpDelete("clear")]
        public IActionResult ClearDatabase()
        {
            var guests = _guestStore.GetAllGuests();
            var count = guests.Count;

            foreach (var guest in guests)
            {
                _guestStore.RemoveGuest(guest.ReservationNumber);
            }

            _logService.Log("INFO", $"Cleared {count} guests from local database");

            return Ok(new
            {
                success = true,
                message = $"Cleared {count} guests from local database",
                clearedCount = count
            });
        }
    }
}