using Microsoft.Extensions.Logging;
using RabbitMultiQueue;

namespace ConsoleApp1;

public class PaymentMessageHandler(RabbitMQClient rabbitMqClient, ILogger logger)
    : IMessageHandler<PaymentMessage>
{
    public ValueTask<MessageHandlingResult> HandleMessage(RabbitMessage<PaymentMessage> message)
    {
        logger.LogInformation("🎯 Received payment message: PaymentId={OrderId}, Amount={Amount}",
            message.Payload.OrderId,
            message.Payload.Amount);
    
        logger.LogInformation("✅ Successfully processed payment");
        var someData = new MessageHandlingResult
        {
            Success = true,
            OriginalDeliveryTag = message.DeliveryTag,
            ShouldAcknowledge = true
        };
        return new ValueTask<MessageHandlingResult>(someData); 
    }
}