# RabbitMultiQueue

RabbitMultiQueue is a .NET library that simplifies working with multiple RabbitMQ queues in a strictly typed manner. It provides an easy way to configure and manage multiple queues for both consuming and producing messages.

## Key Features

- Strictly typed message handling
- Easy configuration and management of multiple queues
- Support for both message consumption and production
- Ability to chain message handlers, where the output of one handler becomes the input of another
- Customizable message acknowledgement and retry behavior

## Getting Started

1. Install the RabbitMultiQueue library into your .NET project.

2. Configure the queues by creating instances of `QueueConfiguration` and adding them to the `RabbitMQClient`:

```csharp
var client = new RabbitMQClient(logger);
client.AddQueue(new QueueConfiguration 
{
    QueueName = "orders",
    HostName = "amqp://localhost:5672",
    Username = "guest",
    Password = "guest",
    Type = QueueType.Consumer
});

client.AddQueue(new QueueConfiguration
{
    QueueName = "payments",
    HostName = "amqp://localhost:5672",
    Username = "guest", 
    Password = "guest",
    Type = QueueType.Producer
});
```

3. Implement message handlers by creating classes that implement either `IMessageHandler<TIn>` or `IMessageHandler<TIn, TOut>` interfaces:

```csharp

// IMessageHandler<IncomingMessage, OutgoingMessage> 
public class OrderMessageHandler : IMessageHandler<OrderMessage, PaymentMessage>
{
    // Implement HandleMethod where <MessageHandlingResult<object> is your outgoing and RabbitMessage<object> is your incoming. 
    public async ValueTask<MessageHandlingResult<PaymentMessage>> HandleMessage(RabbitMessage<OrderMessage> message)
    {
        // Process the order and create a payment message
        var paymentMessage = new RabbitMessage<PaymentMessage>
        {
            MessageId = Guid.NewGuid().ToString(),
            QueueName = "payments", // define the queue you want to send to. 
            Payload = new PaymentMessage // create the payload. 
            {
                OrderId = message.Payload.OrderId,
                Amount = message.Payload.Amount,
                PaymentStatus = "PENDING"
            }
        };

        return new MessageHandlingResult<PaymentMessage>
        {
            Success = true, // return true once everything is processed in your handler, if not within the catch clause you may return success was false. 
            OriginalDeliveryTag = message.DeliveryTag, // for ack-ing the deliveryTag. 
            OutputMessage = paymentMessage, // reference your payment message here for sending
            ShouldAcknowledge = true // can set to false within catch clause for reprocessing. 
        };
    }
}

// IMessage<IncomingMessage> 
public class PaymentMessageHandler : IMessageHandler<PaymentMessage>
{
    // We do not need to return anything in MessageHandlingResult as this will no longer send anything back.
    public ValueTask<MessageHandlingResult> HandleMessage(RabbitMessage<PaymentMessage> message)
    {
        // Process the payment
        // ...

        return new ValueTask<MessageHandlingResult>(new MessageHandlingResult
        {
            Success = true,
            OriginalDeliveryTag = message.DeliveryTag,
            ShouldAcknowledge = true
        });
    }
}
```

4. Start consuming messages by calling the `StartConsuming` method on the `RabbitMQClient` instance:

```csharp
var orderProcessor = new OrderMessageHandler(client, logger);
var paymentProcessor = new PaymentMessageHandler(client, logger); 
await client.StartConsuming("orders", orderProcessor);
await client.StartConsuming("payments", paymentProcessor);
```

5. Publish messages using the `PublishMessage` method on the `RabbitMQClient` instance:

```csharp
var testOrder = new RabbitMessage<OrderMessage>
{
    MessageId = Guid.NewGuid().ToString(),
    Payload = new OrderMessage
    {
        OrderId = "TEST-001",
        Amount = 99.99m,
        CustomerId = "CUST-001"
    }
};

await client.PublishMessage("orders", testOrder);
```

Error Handling Example: 
```cs
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
```

## License

RabbitMultiQueue is released under the [MIT License](https://opensource.org/licenses/MIT).

---
