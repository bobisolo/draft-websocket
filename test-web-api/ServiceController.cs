using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace test_web_api;

// Contrôleur dédié pour les endpoints REST
[ApiController]
[Route("api/[controller]")]
public class ServicesController : ControllerBase
{
    private readonly ConcurrentDictionary<string, WebSocket> _clients;
    private readonly ConcurrentDictionary<string, ServiceStatus> _serviceStatuses;

    public ServicesController(
        ConcurrentDictionary<string, WebSocket> clients,
        ConcurrentDictionary<string, ServiceStatus> serviceStatuses)
    {
        _clients = clients;
        _serviceStatuses = serviceStatuses;
    }

    [HttpGet("status")]
    public IActionResult GetStatuses()
    {
        var statuses = _serviceStatuses.Values;
        return Ok(statuses);
    }

    [HttpPost("command")]
    public async Task<IActionResult> SendCommand([FromBody] Command command)
    {
        if (command == null || string.IsNullOrEmpty(command.ClientId) || string.IsNullOrEmpty(command.ServiceName))
        {
            return BadRequest("Invalid command.");
        }

        var message = JsonSerializer.Serialize(command);
        var buffer = Encoding.UTF8.GetBytes(message);

        if (_clients.TryGetValue(command.ClientId, out var client) && client.State == WebSocketState.Open)
        {
            await client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            return Ok();
        }

        return NotFound("Client not found or disconnected.");
    }
}