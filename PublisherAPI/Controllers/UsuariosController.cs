using System;
using System.Threading.Tasks;
using MessageBusCore;
using MessageBusCore.Messages;
using MessageBusCore.Messages.IntegrationEvents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PublisherAPI.Models;

namespace PublisherAPI.Controllers
{
    [ApiController]
    [Route("api/usuarios")]
    public class UsuariosController : MainController
    {
        private readonly ILogger<UsuariosController> _logger;
        private readonly IMessageBus _bus;

        public UsuariosController(ILogger<UsuariosController> logger, IMessageBus bus)
        {
            _logger = logger;
            _bus = bus;
        }

        [HttpGet]
        public IActionResult Get()
        {
            _logger.LogInformation("Get Usuarios");

            _bus.Publish<TextoIntegrationEvent>(new TextoIntegrationEvent("Testando"));

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> Registro([FromBody] RegistroRequest request)
        {
            var clienteResult = await RegistrarCliente(request);

            if (!clienteResult.ValidationResult.IsValid)
            {
                //await _userManager.DeleteAsync(user);
                return CustomResponse(clienteResult.ValidationResult);
            }

            return CustomResponse(request);
        }

        private async Task<ResponseMessage> RegistrarCliente(RegistroRequest request)
        {
            var usuarioRegistrado = new UsuarioRegistradoIntegrationEvent(Guid.NewGuid(),
                request.Nome, request.Email, request.Cpf);

            try
            {
                var result = _bus.Request(usuarioRegistrado);

                return result;
            }
            catch
            {
                //await _userManager.DeleteAsync(usuario);
                throw;
            }
        }
    }
}
