using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMultiQueue;

    /// <summary>
    /// Main RabbitMQ client that manages connections and channels
    /// </summary>
    public class RabbitMQClient : IAsyncDisposable
    {
        private readonly ILogger<RabbitMQClient> _logger;
        private readonly Dictionary<string, IConnection> _connections;
        private readonly Dictionary<string, IChannel> _channels;
        private readonly Dictionary<string, QueueConfiguration> _queueConfigs;
        private bool _isShuttingDown;

        public RabbitMQClient(ILogger<RabbitMQClient> logger)
        {
            _logger = logger;
            _connections = new Dictionary<string, IConnection>();
            _channels = new Dictionary<string, IChannel>();
            _queueConfigs = new Dictionary<string, QueueConfiguration>();
        }

        /// <summary>
        /// Adds a queue configuration to the client
        /// </summary>
        public void AddQueue(QueueConfiguration config)
        {
            _queueConfigs[config.QueueName] = config;
        }

        /// <summary>
        /// Initializes connections and channels for all configured queues
        /// </summary>
        public async Task Initialize(TimeSpan timeout)
        {
            foreach (var config in _queueConfigs.Values)
            {
                try
                {
                    var factory = new ConnectionFactory
                    {
                        Uri = new Uri(config.HostName),
                        UserName = config.Username,
                        Password = config.Password,
                        RequestedConnectionTimeout = timeout
                    };

                    var connection = await factory.CreateConnectionAsync();
                    var channel = await connection.CreateChannelAsync();

                    await channel.QueueDeclareAsync(
                        queue: config.QueueName,
                        durable: config.Durable,
                        exclusive: config.Exclusive,
                        autoDelete: config.AutoDelete,
                        arguments: config.Arguments
                    );

                    await channel.BasicQosAsync(0, config.PrefetchCount, false);

                    _connections[config.QueueName] = connection;
                    _channels[config.QueueName] = channel;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to initialize queue {config.QueueName}");
                    throw;
                }
            }
        }
        
        public async ValueTask AcknowledgeMessage(string queueName, ulong deliveryTag)
        {
            if (!_channels.TryGetValue(queueName, out var channel))
            {
                throw new InvalidOperationException($"Queue {queueName} not initialized");
            }
    
            await channel.BasicAckAsync(deliveryTag, false);
        }
        
        public async ValueTask NegativeAcknowledgeMessage(string queueName, ulong deliveryTag, bool requeue = true)
        {
            if (!_channels.TryGetValue(queueName, out var channel))
            {
                throw new InvalidOperationException($"Queue {queueName} not initialized");
            }
    
            await channel.BasicNackAsync(deliveryTag, false, requeue);
        }

        /// <summary>
        /// Starts consuming messages from a specific queue
        /// </summary>
        ///         /// <summary>
        /// Starts consuming messages from a specific queue
        /// </summary>
        public async ValueTask StartConsuming<TIn>(string queueName, IMessageHandler<TIn> handler)
        {
            if (!_channels.TryGetValue(queueName, out var channel))
            {
                throw new InvalidOperationException($"Queue {queueName} not initialized");
            }

            var queueConfig = _queueConfigs[queueName];
            var consumer = new AsyncEventingBasicConsumer(channel);
    
            consumer.ReceivedAsync += async (_, ea) =>
            {
                try
                {
                    var message = new RabbitMessage<TIn>
                    {
                        MessageId = ea.BasicProperties.MessageId,
                        DeliveryTag = ea.DeliveryTag,
                        Headers = ea.BasicProperties.Headers,
                        Payload = System.Text.Json.JsonSerializer.Deserialize<TIn>(Encoding.UTF8.GetString(ea.Body.ToArray()))
                    };

                    var result = await handler.HandleMessage(message);
            
                    if (!queueConfig.AutoAck)
                    {
                        if (result.ShouldAcknowledge && result.OriginalDeliveryTag != 0)
                        {
                            await channel.BasicAckAsync(result.OriginalDeliveryTag.Value, false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message");
                    if (!queueConfig.AutoAck)
                    {
                        await channel.BasicNackAsync(ea.DeliveryTag, false, true);
                    }
                }
            };

            await channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: queueConfig.AutoAck,
                consumer: consumer
            );
        }
        public async ValueTask StartConsuming<TIn, TOut>(string queueName, IMessageHandler<TIn, TOut?> handler)
        {
            if (!_channels.TryGetValue(queueName, out var channel))
            {
                throw new InvalidOperationException($"Queue {queueName} not initialized");
            }

            var queueConfig = _queueConfigs[queueName];
            var consumer = new AsyncEventingBasicConsumer(channel);
    
            consumer.ReceivedAsync += async (_, ea) =>
            {
                try
                {
                    var message = new RabbitMessage<TIn>
                    {
                        MessageId = ea.BasicProperties.MessageId,
                        DeliveryTag = ea.DeliveryTag,
                        Headers = ea.BasicProperties.Headers,
                        Payload = System.Text.Json.JsonSerializer.Deserialize<TIn>(Encoding.UTF8.GetString(ea.Body.ToArray()))
                    };

                    var result = await handler.HandleMessage(message);
            
                    if (!queueConfig.AutoAck)
                    {
                        if (result.ShouldAcknowledge && result.OriginalDeliveryTag != 0)
                        {
                            await channel.BasicAckAsync(result.OriginalDeliveryTag.Value, false);
                        }
                    }

                    if (result.OutputMessage != null && !string.IsNullOrEmpty(result.OutputMessage.QueueName))
                    {
                        await PublishMessage(result.OutputMessage.QueueName, result.OutputMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message");
                    if (!queueConfig.AutoAck)
                    {
                        await channel.BasicNackAsync(ea.DeliveryTag, false, true);
                    }
                }
            };

            await channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: queueConfig.AutoAck,
                consumer: consumer
            );
        }

        /// <summary>
        /// Publishes a message to a specific queue
        /// </summary>
        public async Task PublishMessage<T>(string queueName, RabbitMessage<T> message)
        {
            if (!_channels.TryGetValue(queueName, out var channel))
            {
                throw new InvalidOperationException($"Queue {queueName} not initialized");
            }

            var properties = new BasicProperties
            {
                MessageId = message.MessageId,
                Headers = message.Headers
            };

            var messageBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(message.Payload));

            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: queueName,
                mandatory: false,
                basicProperties: properties,
                body: messageBytes
            );

            _logger.LogInformation($"Published message {message.MessageId} to queue {queueName}");
        }

        /// <summary>
        /// Gracefully shuts down connections and channels
        /// </summary>
        public async Task Shutdown()
        {
            _isShuttingDown = true;

            foreach (var channel in _channels.Values)
            {
                try
                {
                    await channel.CloseAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error closing channel");
                }
            }

            foreach (var connection in _connections.Values)
            {
                try
                {
                    await connection.CloseAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error closing connection");
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_isShuttingDown)
            {
                await Shutdown();
            }
        }
    }