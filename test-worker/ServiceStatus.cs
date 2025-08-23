namespace test_worker;

public class ServiceStatus
{
    public string ServiceName { get; set; }
    public bool IsRunning { get; set; }
    public DateTime LastChecked { get; set; }
}

public class Command
{
    public string ServiceName { get; set; }
    public string Action { get; set; } // "start" ou "stop"
}