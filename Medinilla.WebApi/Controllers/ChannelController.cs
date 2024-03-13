using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.Services.Interfaces;
using Medinilla.WebApi.ApiModels;
using Microsoft.AspNetCore.Mvc;

using System.Diagnostics;

namespace Medinilla.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChannelController : ControllerBase
    {
        private readonly IWebSocketFactory _webSocketFactory;

        public ChannelController(IWebSocketFactory webSocketFactory)
        {
            _webSocketFactory = webSocketFactory;
        }
    }
}
