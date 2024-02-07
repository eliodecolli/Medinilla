using Medinilla.Services.Interfaces;
using System.Net;
using System.Text;

namespace Medinilla.WebApi.Middleware;

public class HttpBasicAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;
    private readonly IMedinillaAuthentication _authentication;

    public HttpBasicAuthMiddleware(RequestDelegate next, ILogger<HttpBasicAuthMiddleware> logger,
        IMedinillaAuthentication authentication)
    {
        _next = next;
        _logger = logger;
        _authentication = authentication;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (string.IsNullOrEmpty(context.Request.Headers.Authorization))
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            return;
        }

        var details = context.Request.Headers.Authorization.ToString().Split(' ');
        _logger.LogInformation("Parsing Authorization '{0}' with values '{1}'", details[0], details[1]);

        if (details[0].ToLower() != "basic")
        {
            _logger.LogError("Invalid authentication scheme '{0}'.", details[0]);
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        var creds = Encoding.ASCII.GetString(Convert.FromBase64String(details[1])).Split(':');
        var token = await _authentication.ValidateCredentials(creds[0], creds[1]);

        if(string.IsNullOrEmpty(token))
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            return;
        }

        context.Request.Headers.Append("X-FastAccess-Token", token);

        await _next(context);
    }
}
