using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Medinilla.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        [HttpGet]
        public async Task<string> Get()
        {
            return await Task.FromResult("hello there! " + HttpContext.Request.Headers["X-FastAccess-Token"]);
        }
    }
}
