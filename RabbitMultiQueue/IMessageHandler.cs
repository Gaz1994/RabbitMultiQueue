namespace RabbitMultiQueue;

/// <summary>
/// Interface for message handling
/// </summary>
// For handlers that produce output (original implementation)
public interface IMessageHandler<TIn, TOut>
{
    ValueTask<MessageHandlingResult<TOut>> HandleMessage(RabbitMessage<TIn> message);
}

// For handlers with no output
public interface IMessageHandler<TIn>
{
    ValueTask<MessageHandlingResult> HandleMessage(RabbitMessage<TIn> message);
}