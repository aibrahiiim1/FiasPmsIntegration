using FiasPmsIntegration.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FiasPmsIntegration.Services
{
    public class FiasSocketServer
    {
        private TcpListener? _listener;
        private TcpClient? _client;
        private NetworkStream? _stream;
        private bool _isRunning;
        private readonly FiasProtocolService _protocolService;
        private readonly LogService _logService;
        private readonly int _port;
        private CancellationToken _cancellationToken;

        public bool IsConnected => _client?.Connected ?? false;
        public bool IsRunning => _isRunning;
        public event Action<string>? OnConnectionStatusChanged;

        public FiasSocketServer(FiasProtocolService protocolService, LogService logService)
        {
            _protocolService = protocolService;
            _logService = logService;
            _port = 5008; // Default port, make configurable
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _cancellationToken = cancellationToken;

            try
            {
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start();
                _isRunning = true;

                _logService.Log("INFO", $"FIAS Socket Server started on port {_port} - Running in background");
                OnConnectionStatusChanged?.Invoke("listening");

                // Accept client connections in a loop - runs continuously
                while (_isRunning && !_cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        _logService.Log("INFO", "Waiting for PMS client connection...");

                        // Use cancellation token to allow graceful shutdown
                        _client = await _listener.AcceptTcpClientAsync(_cancellationToken);
                        _stream = _client.GetStream();

                        _logService.Log("INFO", "PMS Client connected");
                        OnConnectionStatusChanged?.Invoke("connected");

                        // Send initial Link Start
                        await SendMessageAsync("LS", new Dictionary<string, string>
                        {
                            { "DA", DateTime.Now.ToString("yyMMdd") },
                            { "TI", DateTime.Now.ToString("HHmmss") }
                        });

                        // Handle client communication
                        await HandleClientAsync();

                        // After client disconnects, loop back to accept new connections
                        _logService.Log("INFO", "Client disconnected. Waiting for new connection...");
                    }
                    catch (OperationCanceledException)
                    {
                        _logService.Log("INFO", "Server shutdown requested");
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logService.Log("ERROR", $"Connection error: {ex.Message}");
                        await Task.Delay(2000, _cancellationToken); // Wait before accepting new connection
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logService.Log("INFO", "Server startup cancelled");
            }
            catch (Exception ex)
            {
                _logService.Log("ERROR", $"Server error: {ex.Message}");
                OnConnectionStatusChanged?.Invoke("error");
            }
            finally
            {
                Stop();
            }
        }

        private async Task HandleClientAsync()
        {
            try
            {
                var buffer = new byte[4096];
                var messageBuilder = new StringBuilder();

                while (_isRunning && _client != null && _stream != null && !_cancellationToken.IsCancellationRequested)
                {
                    // Check if client is still connected
                    if (!_client.Connected)
                    {
                        _logService.Log("INFO", "Client connection lost");
                        break;
                    }

                    // Read with timeout
                    if (_stream.DataAvailable || await WaitForDataAsync(5000))
                    {
                        var bytesRead = await _stream.ReadAsync(buffer.AsMemory(0, buffer.Length), _cancellationToken);
                        if (bytesRead == 0) break;

                        var data = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                        messageBuilder.Append(data);

                        // Check for complete message (ETX)
                        var message = messageBuilder.ToString();
                        if (message.Contains((char)0x03)) // ETX
                        {
                            _logService.Log("INFO", "Message received", message);

                            // Parse and process
                            var parsedMessage = _protocolService.ParseMessage(message);
                            if (parsedMessage != null)
                            {
                                await ProcessMessageAsync(parsedMessage);
                            }

                            messageBuilder.Clear();
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logService.Log("INFO", "Client handler cancelled");
            }
            catch (Exception ex)
            {
                _logService.Log("ERROR", $"Client handler error: {ex.Message}");
            }
            finally
            {
                DisconnectClient();
            }
        }

        private async Task<bool> WaitForDataAsync(int timeoutMs)
        {
            var startTime = DateTime.Now;
            while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
            {
                if (_stream?.DataAvailable ?? false)
                    return true;

                await Task.Delay(100, _cancellationToken);
            }
            return false;
        }

        private async Task ProcessMessageAsync(FiasMessage message)
        {
            // Send ACK
            await SendAckAsync();

            // Handle Link Start - need to send LD and LR records
            if (message.RecordId == "LS")
            {
                await Task.Delay(100, _cancellationToken);

                // Send Link Description
                await SendRawMessageAsync(_protocolService.BuildLinkDescription());
                await Task.Delay(100, _cancellationToken);

                // Send Link Records
                foreach (var lr in _protocolService.BuildLinkRecords())
                {
                    await SendRawMessageAsync(lr);
                    await Task.Delay(50, _cancellationToken);
                }

                // Send Link Alive
                await SendMessageAsync("LA", new Dictionary<string, string>
                {
                    { "DA", DateTime.Now.ToString("yyMMdd") },
                    { "TI", DateTime.Now.ToString("HHmmss") }
                });

                return;
            }

            // Process other messages
            var response = _protocolService.ProcessMessage(message);
            if (!string.IsNullOrEmpty(response))
            {
                await SendRawMessageAsync(response);
            }
        }

        public async Task SendMessageAsync(string recordId, Dictionary<string, string> fields)
        {
            var message = _protocolService.BuildMessage(recordId, fields);
            await SendRawMessageAsync(message);
        }

        private async Task SendRawMessageAsync(string message)
        {
            if (_stream == null || _client?.Connected != true) return;

            try
            {
                var bytes = Encoding.ASCII.GetBytes(message);
                await _stream.WriteAsync(bytes.AsMemory(0, bytes.Length), _cancellationToken);
                await _stream.FlushAsync(_cancellationToken);

                _logService.Log("INFO", "Message sent", message);
            }
            catch (Exception ex)
            {
                _logService.Log("ERROR", $"Send error: {ex.Message}");
            }
        }

        private async Task SendAckAsync()
        {
            if (_stream == null) return;

            try
            {
                var ack = new byte[] { 0x06 }; // ACK
                await _stream.WriteAsync(ack.AsMemory(0, 1), _cancellationToken);
                await _stream.FlushAsync(_cancellationToken);
            }
            catch (Exception ex)
            {
                _logService.Log("ERROR", $"ACK send error: {ex.Message}");
            }
        }

        private void DisconnectClient()
        {
            try
            {
                _stream?.Close();
                _client?.Close();
                _logService.Log("INFO", "Client disconnected");
                OnConnectionStatusChanged?.Invoke("disconnected");
            }
            catch (Exception ex)
            {
                _logService.Log("ERROR", $"Error disconnecting client: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            DisconnectClient();
        }

        public void Stop()
        {
            _logService.Log("INFO", "Stopping FIAS server...");
            _isRunning = false;
            DisconnectClient();

            try
            {
                _listener?.Stop();
                _logService.Log("INFO", "Server stopped");
                OnConnectionStatusChanged?.Invoke("stopped");
            }
            catch (Exception ex)
            {
                _logService.Log("ERROR", $"Error stopping server: {ex.Message}");
            }
        }
    }
}