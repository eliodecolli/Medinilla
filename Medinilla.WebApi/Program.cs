using Medinilla.DataAccess;
using Medinilla.Infrastructure;
using Medinilla.Services;
using Medinilla.WebApi.Middleware;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddMedinillaInfrastructure();
builder.Services.AddMedinillaDataSources();
builder.Services.AddMedinillaServices();

// builder.WebHost.ConfigureKestrel(serverOptions => serverOptions.ConfigureHttpsDefaults(o =>
// {
//     var xp = File.ReadAllBytes("cert.pfx");
//     o.ServerCertificate = new X509Certificate2(xp);
// }));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseAuthentication();

//app.UseMiddleware<HttpBasicAuthMiddleware>();

app.MapControllers();

app.UseWebSockets();

app.Run();
