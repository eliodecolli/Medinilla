using Medinilla.Infrastructure;
using Medinilla.Services.Interfaces;
using Medinilla.WebApi.ApiModels;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

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

        var token = await _authentication.ValidateCredentials(details[1]);

        if(string.IsNullOrEmpty(token))
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;

            var error = new ErrorApiModel()
            {
                TraceId = System.Diagnostics.Activity.Current!.TraceId.ToString(),
                Error = "Invalid username or password."
            };
            await JsonSerializer.SerializeAsync(context.Response.Body, error, 
                new JsonSerializerOptions() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            return;
        }

        context.Request.Headers.Append(Constants.FastAccessHeaderKey, token);

        await _next(context);
    }
}
