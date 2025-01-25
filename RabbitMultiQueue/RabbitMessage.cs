namespace RabbitMultiQueue;

/// <summary>
/// Represents a message to be processed
/// </summary>

public class RabbitMessage<T>
{
    public string MessageId { get; set; }
    public T Payload { get; set; }
    public ulong? DeliveryTag { get; set; }
    public IDictionary<string, object> Headers { get; set; }
    public string QueueName { get; set; }
}