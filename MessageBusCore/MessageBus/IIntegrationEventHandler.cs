using System.Threading.Tasks;
using MessageBusCore.Messages;

namespace MessageBusCore
{
    public interface IIntegrationEventHandler { }

    public interface IIntegrationEventHandler<in TIntegrationEvent> : IIntegrationEventHandler
        where TIntegrationEvent : IntegrationEvent
    {
        Task<ResponseMessage> Handle(TIntegrationEvent @event);
    }
}
