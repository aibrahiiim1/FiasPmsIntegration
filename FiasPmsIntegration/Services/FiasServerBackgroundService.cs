using Microsoft.Extensions.Options;

namespace FiasPmsIntegration.Services
{
    public class FiasServerBackgroundService : BackgroundService
    {
        private readonly FiasSocketServer _socketServer;
        private readonly LogService _logService;
        private readonly ILogger<FiasServerBackgroundService> _logger;

        public FiasServerBackgroundService(
            FiasSocketServer socketServer,
            LogService logService,
            ILogger<FiasServerBackgroundService> logger)
        {
            _socketServer = socketServer;
            _logService = logService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("FIAS Background Service is starting...");
            _logService.Log("INFO", "FIAS Background Service is starting");

            // Wait a moment for the application to fully start
            await Task.Delay(1000, stoppingToken);

            try
            {
                // Start the socket server - this will run continuously
                await _socketServer.StartAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("FIAS Background Service is stopping due to cancellation");
                _logService.Log("INFO", "FIAS Background Service stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in FIAS Background Service");
                _logService.Log("ERROR", $"Fatal error in background service: {ex.Message}");

                // If the server crashes, try to restart it after a delay
                await Task.Delay(5000, stoppingToken);
                if (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Attempting to restart FIAS server...");
                    await ExecuteAsync(stoppingToken);
                }
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("FIAS Background Service is stopping...");
            _logService.Log("INFO", "FIAS Background Service stopping");

            _socketServer.Stop();

            await base.StopAsync(cancellationToken);
        }
    }
}