namespace RabbitMultiQueue;

// Base MessageHandlingResult without output
public class MessageHandlingResult
{
    public bool Success { get; set; }
    public ulong? OriginalDeliveryTag { get; set; }
    public bool ShouldAcknowledge { get; set; }
}

// Extended version with output message
public class MessageHandlingResult<TOut> : MessageHandlingResult
{
    public RabbitMessage<TOut> OutputMessage { get; set; }
}
