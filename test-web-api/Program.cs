using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using test_web_api;

namespace ToolboxServer
{
    public class Program
    {
        private static readonly ConcurrentDictionary<string, WebSocket> _clients =
            new ConcurrentDictionary<string, WebSocket>();

        private static readonly ConcurrentDictionary<string, ServiceStatus> _serviceStatuses =
            new ConcurrentDictionary<string, ServiceStatus>();

        private static async Task HandleWebSocketAsync(string clientId, WebSocket webSocket)
        {
            var buffer = new byte[1024];
            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine($"Received from {clientId}: {message}");

                    var status = JsonSerializer.Deserialize<ServiceStatus>(message);
                    status.ClientId = clientId;
                    _serviceStatuses[clientId + ":" + status.ServiceName] = status;
                }
            }
            catch
            {
                _clients.TryRemove(clientId, out _);
                foreach (var key in _serviceStatuses.Keys.Where(k => k.StartsWith(clientId + ":")))
                {
                    _serviceStatuses.TryRemove(key, out _);
                }
            }
        }

        private static async Task CleanupOldStatuses(TimeSpan maxAge)
        {
            while (true)
            {
                var now = DateTime.UtcNow;
                foreach (var key in _serviceStatuses.Keys.ToList())
                {
                    if (_serviceStatuses.TryGetValue(key, out var status) && now - status.LastChecked > maxAge)
                    {
                        _serviceStatuses.TryRemove(key, out _);
                        Console.WriteLine($"Removed outdated status: {key}");
                    }
                }

                await Task.Delay(60000); // Vérifier toutes les minutes
            }
        }


        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddControllers();

            builder.Services.AddSingleton(_clients);
            builder.Services.AddSingleton(_serviceStatuses);

            // Add services to the container.
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            var app = builder.Build();
            
            // Lancer la tâche de nettoyage des statuts obsolètes
            Task.Run(() => CleanupOldStatuses(TimeSpan.FromMinutes(10)));

            app.UseWebSockets();
            
            // Activer le routage pour les contrôleurs
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers(); // Activer les contrôleurs
            });
            

            // Endpoint WebSocket pour les toolbox-client
            app.Map("/ws", async context =>
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    var clientId = Guid.NewGuid().ToString();
                    _clients.TryAdd(clientId, webSocket);

                    await HandleWebSocketAsync(clientId, webSocket);
                }
                else
                {
                    context.Response.StatusCode = 400;
                }
            });

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();


            app.Run("http://localhost:5000");
        }

    }
}