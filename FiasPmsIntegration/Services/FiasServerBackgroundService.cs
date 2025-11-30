namespace FiasPmsIntegration.Services
{
    public class FiasServerBackgroundService : BackgroundService
    {
        private readonly FiasSocketServer _socketServer;

        public FiasServerBackgroundService(FiasSocketServer socketServer)
        {
            _socketServer = socketServer;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _socketServer.StartAsync();
        }
    }
}
