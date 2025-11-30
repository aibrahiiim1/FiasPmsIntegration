using FiasPmsIntegration.Models;

namespace FiasPmsIntegration.Services
{
    public class LogService
    {
        private readonly List<LogEntry> _logs = new();
        private readonly object _lock = new();
        private const int MaxLogs = 1000;

        public void Log(string level, string message, string? rawData = null)
        {
            lock (_lock)
            {
                _logs.Add(new LogEntry
                {
                    Level = level,
                    Message = message,
                    RawData = rawData,
                    Timestamp = DateTime.Now
                });

                // Keep only last 1000 logs
                if (_logs.Count > MaxLogs)
                {
                    _logs.RemoveAt(0);
                }
            }

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {level}: {message}");
        }

        public List<LogEntry> GetLogs(int count = 100)
        {
            lock (_lock)
            {
                return _logs.TakeLast(count).ToList();
            }
        }

        public void ClearLogs()
        {
            lock (_lock)
            {
                _logs.Clear();
            }
        }
    }
}
