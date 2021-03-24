using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ConsumerAPI.Controllers
{
    [ApiController]
    [Route("api/clientes")]
    public class ClientesController : ControllerBase
    {
        private readonly ILogger<ClientesController> _logger;

        public ClientesController(ILogger<ClientesController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Get()
        {
            _logger.LogInformation("Get Clientes");

            return Ok();
        }
    }
}
