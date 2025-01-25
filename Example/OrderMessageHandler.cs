using Microsoft.Extensions.Logging;
using RabbitMultiQueue;

public class OrderMessage
{
    public string OrderId { get; set; }
    public decimal Amount { get; set; }
    public string CustomerId { get; set; }
}

public class PaymentMessage
{
    public string OrderId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentStatus { get; set; }
}


public class OrderMessageHandler(RabbitMQClient rabbitMqClient, ILogger logger)
    : IMessageHandler<OrderMessage, PaymentMessage>
{

    public ValueTask SomeMethod(OrderMessage orderMessage)
    {
        return ValueTask.CompletedTask;
    }

    public async ValueTask<MessageHandlingResult<PaymentMessage>> HandleMessage(RabbitMessage<OrderMessage> message)
    {
        logger.LogInformation("🎯 Received order message: OrderId={OrderId}, Amount={Amount}",
            message.Payload.OrderId,
            message.Payload.Amount);

        try
        {
            logger.LogInformation("Processing order...");
            await SomeMethod(message.Payload);

            var paymentMessage = new RabbitMessage<PaymentMessage>
            {
                MessageId = Guid.NewGuid().ToString(),
                QueueName = "payments",
                Payload = new PaymentMessage
                {
                    OrderId = message.Payload.OrderId,
                    Amount = message.Payload.Amount,
                    PaymentStatus = "PENDING"
                }
            };

            logger.LogInformation("✅ Successfully processed order");
            return new MessageHandlingResult<PaymentMessage>
            {
                Success = true,
                OriginalDeliveryTag = message.DeliveryTag,
                OutputMessage = paymentMessage,
                ShouldAcknowledge = true
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to process order");
            return new MessageHandlingResult<PaymentMessage>
            {
                Success = false,
                OriginalDeliveryTag = message.DeliveryTag,
                ShouldAcknowledge = false
            };
        }
    }
}