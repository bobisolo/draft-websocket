using System.Net.WebSockets;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;

namespace test_worker;

public class ToolboxClientService : BackgroundService
{
    private readonly ILogger<ToolboxClientService> _logger;
    private ClientWebSocket _webSocket = new ClientWebSocket();
    private readonly string _serverUri = "ws://localhost:5000/ws";
    private readonly string[] _servicesToMonitor = { "Service1", "Service2", "Service3", "Service4", "Service5" }; // À adapter


    public ToolboxClientService(ILogger<ToolboxClientService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ConnectToServerAsync(stoppingToken);
            await SendServiceStatusesAsync(stoppingToken);
            // await Task.WhenAll(
            //     SendServiceStatusesAsync(stoppingToken),
            //     //ListenForCommandsAsync(stoppingToken)
            // );
        }
    }

    
    
    
    //############################################
    

        private async Task ConnectToServerAsync(CancellationToken stoppingToken)
        {
            while (_webSocket.State != WebSocketState.Open && !stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Attempting to connect to Toolbox Server at {ServerUri}", _serverUri);
                    await _webSocket.ConnectAsync(new Uri(_serverUri), stoppingToken);
                    _logger.LogInformation("Connected to Toolbox Server.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Connection failed. Retrying in 5 seconds...");
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }

        private async Task SendServiceStatusesAsync(CancellationToken stoppingToken)
        {
            while (_webSocket.State == WebSocketState.Open && !stoppingToken.IsCancellationRequested)
            {
                try
                {
                    foreach (var serviceName in _servicesToMonitor)
                    {
                        var status = GetServiceStatus(serviceName);
                        var json = JsonSerializer.Serialize(status);
                        var buffer = Encoding.UTF8.GetBytes(json);
                        await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, stoppingToken);
                        _logger.LogInformation("Sent status: {Status}", json);
                    }
                    await Task.Delay(5000, stoppingToken); // Envoi toutes les 5 secondes
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending service statuses.");
                    await HandleWebSocketErrorAsync(stoppingToken);
                    break;
                }
            }
        }

        private ServiceStatus GetServiceStatus(string serviceName)
        {
            try
            {
                // using var service = new ServiceController(serviceName);
                return new ServiceStatus
                {
                    ServiceName = serviceName,
                    IsRunning = true,
                    // IsRunning = service.Status == ServiceControllerStatus.Running,
                    LastChecked = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking status for service {ServiceName}.", serviceName);
                return new ServiceStatus
                {
                    ServiceName = serviceName,
                    IsRunning = false,
                    LastChecked = DateTime.UtcNow
                };
            }
        }

        // private async Task ListenForCommandsAsync(CancellationToken stoppingToken)
        // {
        //     var buffer = new byte[1024];
        //     while (_webSocket.State == WebSocketState.Open && !stoppingToken.IsCancellationRequested)
        //     {
        //         try
        //         {
        //             var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), stoppingToken);
        //             var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
        //             _logger.LogInformation("Received command: {Command}", message);
        //
        //             var command = JsonSerializer.Deserialize<Command>(message);
        //             await HandleCommandAsync(command);
        //         }
        //         catch (Exception ex)
        //         {
        //             _logger.LogError(ex, "Error handling command.");
        //             await HandleWebSocketErrorAsync(stoppingToken);
        //             break;
        //         }
        //     }
        // }
        //
        // private async Task HandleCommandAsync(Command command)
        // {
        //     try
        //     {
        //         using var service = new ServiceController(command.ServiceName);
        //         if (command.Action.Equals("start", StringComparison.OrdinalIgnoreCase))
        //         {
        //             if (service.Status != ServiceControllerStatus.Running)
        //             {
        //                 service.Start();
        //                 service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
        //                 _logger.LogInformation("Started service: {ServiceName}", command.ServiceName);
        //             }
        //         }
        //         else if (command.Action.Equals("stop", StringComparison.OrdinalIgnoreCase))
        //         {
        //             if (service.Status == ServiceControllerStatus.Running)
        //             {
        //                 service.Stop();
        //                 service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
        //                 _logger.LogInformation("Stopped service: {ServiceName}", command.ServiceName);
        //             }
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         _logger.LogError(ex, "Error executing command {Action} on {ServiceName}", command.Action, command.ServiceName);
        //     }
        // }

        private async Task HandleWebSocketErrorAsync(CancellationToken stoppingToken)
        {
            if (_webSocket.State != WebSocketState.Open)
            {
                _logger.LogWarning("WebSocket disconnected. Attempting to reconnect...");
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", stoppingToken);
                _webSocket.Dispose();
                _webSocket = new ClientWebSocket();
                await ConnectToServerAsync(stoppingToken);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Toolbox Client Service is stopping.");
            if (_webSocket.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Service stopping", cancellationToken);
                _webSocket.Dispose();
            }
            await base.StopAsync(cancellationToken);
        }
    
}