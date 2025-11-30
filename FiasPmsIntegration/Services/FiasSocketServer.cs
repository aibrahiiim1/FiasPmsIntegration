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

        public bool IsConnected => _client?.Connected ?? false;
        public event Action<string>? OnConnectionStatusChanged;

        public FiasSocketServer(FiasProtocolService protocolService, LogService logService)
        {
            _protocolService = protocolService;
            _logService = logService;
            _port = 5008; // Default port, make configurable
        }

        public async Task StartAsync()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start();
                _isRunning = true;

                _logService.Log("INFO", $"FIAS Socket Server started on port {_port}");
                OnConnectionStatusChanged?.Invoke("listening");

                // Accept client connections
                while (_isRunning)
                {
                    _client = await _listener.AcceptTcpClientAsync();
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
                }
            }
            catch (Exception ex)
            {
                _logService.Log("ERROR", $"Server error: {ex.Message}");
                OnConnectionStatusChanged?.Invoke("error");
            }
        }

        private async Task HandleClientAsync()
        {
            try
            {
                var buffer = new byte[4096];
                var messageBuilder = new StringBuilder();

                while (_isRunning && _client != null && _stream != null)
                {
                    var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
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
            catch (Exception ex)
            {
                _logService.Log("ERROR", $"Client handler error: {ex.Message}");
            }
            finally
            {
                Disconnect();
            }
        }

        private async Task ProcessMessageAsync(FiasMessage message)
        {
            // Send ACK
            await SendAckAsync();

            // Handle Link Start - need to send LD and LR records
            if (message.RecordId == "LS")
            {
                await Task.Delay(100); // Small delay

                // Send Link Description
                await SendRawMessageAsync(_protocolService.BuildLinkDescription());
                await Task.Delay(100);

                // Send Link Records
                foreach (var lr in _protocolService.BuildLinkRecords())
                {
                    await SendRawMessageAsync(lr);
                    await Task.Delay(50);
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
            if (_stream == null || !_client!.Connected) return;

            try
            {
                var bytes = Encoding.ASCII.GetBytes(message);
                await _stream.WriteAsync(bytes, 0, bytes.Length);
                await _stream.FlushAsync();

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

            var ack = new byte[] { 0x06 }; // ACK
            await _stream.WriteAsync(ack, 0, 1);
            await _stream.FlushAsync();
        }

        public void Disconnect()
        {
            _stream?.Close();
            _client?.Close();
            _logService.Log("INFO", "Client disconnected");
            OnConnectionStatusChanged?.Invoke("disconnected");
        }

        public void Stop()
        {
            _isRunning = false;
            Disconnect();
            _listener?.Stop();
            _logService.Log("INFO", "Server stopped");
        }
    }
}
