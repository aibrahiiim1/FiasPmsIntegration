using FiasPmsIntegration.Services;
using Microsoft.AspNetCore.Mvc;

namespace FiasPmsIntegration.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LogsController : ControllerBase
    {
        private readonly LogService _logService;

        public LogsController(LogService logService)
        {
            _logService = logService;
        }

        [HttpGet]
        public IActionResult GetLogs([FromQuery] int count = 100)
        {
            return Ok(_logService.GetLogs(count));
        }

        [HttpDelete]
        public IActionResult ClearLogs()
        {
            _logService.ClearLogs();
            return Ok();
        }
    }
}
