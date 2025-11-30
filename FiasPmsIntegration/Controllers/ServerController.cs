using FiasPmsIntegration.Services;
using Microsoft.AspNetCore.Mvc;

namespace FiasPmsIntegration.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ServerController : ControllerBase
    {
        private readonly FiasSocketServer _socketServer;

        public ServerController(FiasSocketServer socketServer)
        {
            _socketServer = socketServer;
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new
            {
                isConnected = _socketServer.IsConnected,
                status = _socketServer.IsConnected ? "connected" : "disconnected"
            });
        }

        [HttpPost("disconnect")]
        public IActionResult Disconnect()
        {
            _socketServer.Disconnect();
            return Ok();
        }
    }
}
