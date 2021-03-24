using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MessageBusCore;
using MessageBusNovoTeste.Extensions;

namespace ConsumerAPI.Configuration
{
    public static class MessageBusConfig
    {
        public static void AddMessageBusConfiguration(this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<MessageBusOptions>(configuration.GetSection("MessageBusOptions"));

            services.AddMessageBusRabbitMQ();
        }
    }
}
