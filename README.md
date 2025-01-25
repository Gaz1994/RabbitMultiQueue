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
public class OrderMessageHandler : IMessageHandler<OrderMessage, PaymentMessage>
{
    // Implement HandleMessage method
    public async ValueTask<MessageHandlingResult<PaymentMessage>> HandleMessage(RabbitMessage<OrderMessage> message)
    {
        // Process the order and create a payment message
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

        return new MessageHandlingResult<PaymentMessage>
        {
            Success = true,
            OriginalDeliveryTag = message.DeliveryTag,
            OutputMessage = paymentMessage,
            ShouldAcknowledge = true
        };
    }
}

public class PaymentMessageHandler : IMessageHandler<PaymentMessage>
{
    // Implement HandleMessage method
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

## Chaining Message Handlers

RabbitMultiQueue supports chaining message handlers to create pipelines where the output of one handler becomes the input of another. To chain message handlers:

1. Implement a message handler that takes an input type `TIn` and produces an output type `TOut`.
2. Configure a producer queue for the output type `TOut`.
3. In the message handler, create an instance of `RabbitMessage<TOut>` with the output payload, set the `QueueName` property to specify the target queue, and include it in the `MessageHandlingResult<TOut>`.
4. The RabbitMultiQueue library will automatically publish the output message to the configured producer queue.

## License

RabbitMultiQueue is released under the [MIT License](https://opensource.org/licenses/MIT).

---
