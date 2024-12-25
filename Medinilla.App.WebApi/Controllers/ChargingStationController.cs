using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Medinilla.App.WebApi.Controllers
{
    [Route("api/{accountId}/charging-stations")]
    [ApiController]
    public class ChargingStationController : ControllerBase
    {
        public async Task<IActionResult> Get()
        {
            return Ok();
        }
    }
}
