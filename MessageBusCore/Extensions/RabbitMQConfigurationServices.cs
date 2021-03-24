using MessageBusCore;
using MessageBusCore.Connection;
using MessageBusNovoTeste.Subscriptions;
using Microsoft.Extensions.DependencyInjection;

namespace MessageBusNovoTeste.Extensions
{
    public static class RabbitMQConfigurationServices
    {
        public static IServiceCollection AddMessageBusRabbitMQ(this IServiceCollection services)
        {
            services.AddSingleton<IBusConnection, RabbitPersistentConnection>();

            services.AddSingleton<IEventBusSubscriptionsManager, InMemoryEventBusSubscriptionsManager>();

            services.AddSingleton<IMessageBus, MessageBusRabbitMQ>();

            return services;
        }
    }
}
