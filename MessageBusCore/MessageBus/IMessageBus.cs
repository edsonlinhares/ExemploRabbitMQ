using System;
using MessageBusCore.Messages;

namespace MessageBusCore
{
    public interface IMessageBus : IDisposable
    {
        void Publish<T>(T message) where T : IntegrationEvent;

        ResponseMessage Request<T>(T message) where T : IntegrationEvent;

        void Subscribe<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>;
    }
}
