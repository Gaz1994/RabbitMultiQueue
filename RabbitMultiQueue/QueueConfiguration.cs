namespace RabbitMultiQueue;

/// <summary>
/// Configuration for a single queue
/// </summary>
public class QueueConfiguration
{
    public string QueueName { get; set; }
    public string HostName { get; set; }
    public bool Durable { get; set; } = true;
    public bool Exclusive { get; set; } = false;
    public bool AutoDelete { get; set; } = false;
    public ushort PrefetchCount { get; set; } = 1;
    public QueueType Type { get; set; }
    public IDictionary<string, object> Arguments { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public bool AutoAck { get; set; } = false;
}