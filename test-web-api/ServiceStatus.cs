namespace test_web_api;

public class ServiceStatus
{
    public string ServiceName { get; set; }
    public bool IsRunning { get; set; }
    public DateTime LastChecked { get; set; }
    public string ClientId { get; set; }
}

public class Command
{
    public string ServiceName { get; set; }
    public string Action { get; set; } // "start" ou "stop"
    public string ClientId { get; set; }
}