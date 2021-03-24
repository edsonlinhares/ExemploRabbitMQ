using System;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace MessageBusCore.Connection
{
    public class RabbitPersistentConnection : IBusConnection
    {
        private bool _disposed;
        private IConnection _connection;
        private readonly object semaphore = new object();
        private readonly IConnectionFactory _connectionFactory;

        public RabbitPersistentConnection(IServiceProvider serviceProvider)
        {
            var appSettings = serviceProvider.GetRequiredService<IOptions<MessageBusOptions>>();

            var _options = appSettings?.Value ?? throw new ArgumentNullException(nameof(appSettings));

            _connectionFactory = new ConnectionFactory()
            {
                HostName = _options.HostName,
                UserName = _options.UserName,
                Password = _options.Password,
                DispatchConsumersAsync = true
            };
        }

        public bool IsConnected => _connection != null && _connection.IsOpen && !_disposed;

        public IModel CreateChannel()
        {
            TryConnect();

            if (!IsConnected)
                throw new InvalidOperationException("No RabbitMQ connections are available to perform this action");

            return _connection.CreateModel();
        }

        private void TryConnect()
        {
            lock (semaphore)
            {
                if (IsConnected)
                    return;

                var policy = Policy.Handle<SocketException>()
                        .Or<BrokerUnreachableException>()
                        .WaitAndRetry(5, retryAttempt =>
                            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

                policy.Execute(() =>
                {
                    _connection = _connectionFactory.CreateConnection();
                });

                _connection.ConnectionShutdown += OnDisconnect;
                _connection.CallbackException += (s, e) => TryConnect();
                _connection.ConnectionBlocked += (s, e) => TryConnect();
            }
        }

        private void OnDisconnect(object s, EventArgs e)
        {
            var policy = Policy.Handle<SocketException>()
                .Or<BrokerUnreachableException>()
                .RetryForever();

            policy.Execute(TryConnect);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _connection.Dispose();
        }
    }
}
