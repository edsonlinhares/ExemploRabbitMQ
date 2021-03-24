using System.Text;
using System.Threading.Tasks;
using MessageBusCore.Messages;
using MessageBusNovoTeste.Extensions;
using MessageBusNovoTeste.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using MessageBusCore.Connection;
using System.Collections.Concurrent;

namespace MessageBusCore
{
    public class MessageBusRabbitMQ : IMessageBus
    {
        private readonly IBusConnection _connection;

        private IModel _consumerChannel = null;
        private readonly MessageBusOptions _options;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MessageBusRabbitMQ> _logger;

        private readonly IEventBusSubscriptionsManager _subsManager;

        //private string replyQueueName;
        private IModel _replyChannel = null;
        private AsyncEventingBasicConsumer consumerReply;
        private BlockingCollection<string> respQueue = new BlockingCollection<string>();

        public MessageBusRabbitMQ(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;

            _connection = serviceProvider.GetRequiredService<IBusConnection>();
            _logger = _serviceProvider.GetRequiredService<ILogger<MessageBusRabbitMQ>>();
            _subsManager = _serviceProvider.GetRequiredService<IEventBusSubscriptionsManager>();

            var appSettings = _serviceProvider.GetRequiredService<IOptions<MessageBusOptions>>();

            _options = appSettings?.Value ?? throw new ArgumentNullException(nameof(appSettings));

            _consumerChannel = CreateConsumerChannel();
        }

        public void Publish<T>(T message) where T : IntegrationEvent
        {
            using (var channel = _connection.CreateChannel())
            {
                channel.ExchangeDeclare(exchange: _options.ExchangeName, type: _options.ExchangeType ?? "direct");

                var properties = channel.CreateBasicProperties();
                properties.DeliveryMode = 2; // persistent
                properties.CorrelationId = message.AggregateId.ToString();// Guid.NewGuid().ToString();

                var _message = JsonConvert.SerializeObject(message);
                var body = Encoding.UTF8.GetBytes(_message);

                _logger.LogInformation("Sent '{0}':'{1}' -> '{2}':'{3}'", properties.CorrelationId, _options.QueueDefault, message.GetType().Name, _message);

                channel.BasicPublish(exchange: _options.ExchangeName,
                                 routingKey: message.GetType().Name,
                                 mandatory: true,
                                 basicProperties: properties,
                                 body: body);
            }
        }

        private  Task CreateReplyChannel(string correlationId, string replyQueueName)
        {
            _replyChannel = _connection.CreateChannel();

            //_replyChannel.ExchangeDeclare(exchange: _options.ExchangeName, type: _options.ExchangeType ?? "direct");

            //replyQueueName = _replyChannel.QueueDeclare().QueueName;
            _replyChannel.QueueDeclare(queue: replyQueueName,
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            consumerReply = new AsyncEventingBasicConsumer(_replyChannel);

            consumerReply.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var response = Encoding.UTF8.GetString(body);
                if (ea.BasicProperties.CorrelationId == correlationId)
                {
                    respQueue.Add(response);
                }
            };

            return Task.CompletedTask;
        }

        public ResponseMessage Request<T>(T message) where T : IntegrationEvent
        {
            var correlationId = message.AggregateId.ToString();
            var replyQueueName = $"{message.GetType().Name}_Reply";

            CreateReplyChannel(correlationId, replyQueueName);

            var properties = _replyChannel.CreateBasicProperties();
            properties.CorrelationId = correlationId;
            properties.ReplyTo = replyQueueName;

            var _message = JsonConvert.SerializeObject(message);
            var body = Encoding.UTF8.GetBytes(_message);

            _logger.LogInformation("Sent '{0}':'{1}' -> '{2}':'{3}'", properties.CorrelationId, _options.QueueDefault, message.GetType().Name, _message);

            _replyChannel.BasicPublish(exchange: _options.ExchangeName,
                             routingKey: message.GetType().Name,
                             mandatory: true,
                             basicProperties: properties,
                             body: body);

            _replyChannel.BasicConsume(
                consumer: consumerReply,
                queue: replyQueueName,
                autoAck: true);

            var response = respQueue.Take();

            _replyChannel.QueueDelete(replyQueueName);

            return response != null ? JsonConvert.DeserializeObject<ResponseMessage>(response) : null;
        }

        public void Subscribe<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = _subsManager.GetEventKey<T>();

            var containsKey = _subsManager.HasSubscriptionsForEvent(eventName);

            if (!containsKey)
            {
                _consumerChannel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

                _consumerChannel.QueueBind(queue: _options.QueueDefault, exchange: _options.ExchangeName, routingKey: eventName);
            }

            _logger.LogInformation("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(TH).GetGenericTypeName());

            _subsManager.AddSubscription<T, TH>();

            StartBasicConsume();
        }

        #region Consumo

        private IModel CreateConsumerChannel()
        {
            _logger.LogTrace("Creating RabbitMQ consumer channel.");

            var channel = _connection.CreateChannel();

            channel.ExchangeDeclare(exchange: _options.ExchangeName, type: _options.ExchangeType ?? "direct");

            channel.QueueDeclare(queue: _options.QueueDefault,
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            channel.CallbackException += (sender, ea) =>
            {
                _logger.LogWarning(ea.Exception, "Recreating RabbitMQ consumer channel.");

                _consumerChannel.Dispose();
                _consumerChannel = CreateConsumerChannel();
                StartBasicConsume();
            };

            return channel;
        }

        private void StartBasicConsume()
        {
            _logger.LogInformation($"Starting RabbitMQ basic consume.");

            if (_consumerChannel != null)
            {
                var consumer = new AsyncEventingBasicConsumer(_consumerChannel);

                consumer.Received += Consumer_Received;

                _consumerChannel.BasicConsume(
                    queue: _options.QueueDefault,
                    autoAck: false,
                    consumer: consumer);
            }
            else
            {
                _logger.LogError("StartBasicConsume can't call on _consumerChannel == null");
            }
        }

        private async Task Consumer_Received(object sender, BasicDeliverEventArgs eventArgs)
        {
            var eventName = eventArgs.RoutingKey;
            var message = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
            var correlationId = eventArgs.BasicProperties.CorrelationId ?? string.Empty;
            var replyQueue = eventArgs.BasicProperties.ReplyTo ?? string.Empty;

            _logger.LogInformation($"EventName: {eventName} - CorrelationId: {correlationId} - ReplyQueue: {replyQueue}");

            try
            {
                if (message.ToLowerInvariant().Contains("throw-fake-exception"))
                {
                    throw new InvalidOperationException($"Fake exception requested: \"{message}\"");
                }

                var retorno = await ProcessEvent(eventName, message);

                if (!string.IsNullOrEmpty(replyQueue) && retorno != null)
                {
                    // publicar resposta
                    var responseBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(retorno));

                    using (var channel = _connection.CreateChannel())
                    {
                        //channel.ExchangeDeclare(exchange: _options.ExchangeName, type: _options.ExchangeType ?? "direct");

                        var replyProps = channel.CreateBasicProperties();
                        replyProps.CorrelationId = correlationId; ;

                        channel.BasicPublish(exchange: "",//_options.ExchangeName,
                                         routingKey: replyQueue,
                                         mandatory: true,
                                         basicProperties: replyProps,
                                         body: responseBytes);
                    }
                }

                _consumerChannel.BasicAck(eventArgs.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "----- ERROR Processing message \"{Message}\"", message);
            }

        }

        private async Task<ResponseMessage> ProcessEvent(string eventName, string message)
        {
            _logger.LogInformation("Processing RabbitMQ event: {EventName} -> {Message}", eventName, message);

            if (_subsManager.HasSubscriptionsForEvent(eventName))
            {
                var subscriptions = _subsManager.GetHandlersForEvent(eventName);

                foreach (var subscription in subscriptions)
                {
                    var handler = _serviceProvider.GetService(subscription.HandlerType);

                    if (handler == null) continue;

                    var eventType = _subsManager.GetEventTypeByName(eventName);
                    var integrationEvent = JsonConvert.DeserializeObject(message, eventType);
                    var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);

                    await Task.Yield();
                    var retorno = await (Task<ResponseMessage>)concreteType.GetMethod("Handle").Invoke(handler, new object[] { integrationEvent });

                    return retorno;
                }
            }
            else
            {
                _logger.LogWarning("No subscription for RabbitMQ event: {EventName}", eventName);
            }

            return null;
        }

        #endregion

        public void Dispose()
        {
            _connection?.Dispose();
        }

    }
}
