using System;
using RabbitMQ.Client;

namespace MessageBusCore.Connection
{
    public interface IBusConnection : IDisposable
    {
        bool IsConnected { get; }
        IModel CreateChannel();
    }
}
