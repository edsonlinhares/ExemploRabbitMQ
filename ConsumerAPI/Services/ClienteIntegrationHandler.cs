using System.Collections.Generic;
using System.Threading.Tasks;
using FluentValidation.Results;
using MessageBusCore;
using MessageBusCore.Messages;
using MessageBusCore.Messages.IntegrationEvents;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ConsumerAPI.Services
{
    public class ClienteIntegrationHandler : IIntegrationEventHandler<UsuarioRegistradoIntegrationEvent>
    {
        private readonly ILogger<ClienteIntegrationHandler> _logger;

        public ClienteIntegrationHandler(ILogger<ClienteIntegrationHandler> logger)
        {
            _logger = logger;
        }

        public async Task<ResponseMessage> Handle(UsuarioRegistradoIntegrationEvent @event)
        {
            _logger.LogInformation($"Cliente registrado: {JsonConvert.SerializeObject(@event)}");

            await Task.Delay(2);
            var failures = new List<ValidationFailure>();
            failures.Add(new ValidationFailure("Cpf", "Dígito inválido."));
            return new ResponseMessage(new FluentValidation.Results.ValidationResult(failures));
        }
    }
}
