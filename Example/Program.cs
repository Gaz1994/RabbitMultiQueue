// Setup

using System.Runtime.InteropServices;
using ConsoleApp1;
using Microsoft.Extensions.Logging;
using RabbitMultiQueue;



var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<RabbitMQClient>();


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

await client.Initialize(TimeSpan.FromSeconds(30));


// Start consuming messages
var orderProcessor = new OrderMessageHandler(client, logger);
var paymentProcessor = new PaymentMessageHandler(client, logger); 
await client.StartConsuming("orders", orderProcessor);
await client.StartConsuming("payments", paymentProcessor);
// Then send test message

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
Console.WriteLine("Test order message published!");

Console.ReadKey();