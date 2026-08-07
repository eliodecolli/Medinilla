using Medinilla.Core.WebApi.Services;
using Medinilla.DataAccess;
using Medinilla.Infrastructure;
using Medinilla.RealTime;
using Medinilla.WebApi;
using Medinilla.WebApi.Interfaces;

using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5033, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
    });
});

builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddControllers(options => options.InputFormatters.Add(new PlainTextFormatter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddMedinillaInfrastructure();
builder.Services.AddRealTimeServices();
builder.Services.AddMedinillaDataAccess();

builder.Services.AddWebSocketRoutingTable();
builder.Services.AddSubscriptionReceiver();

builder.Services.AddSingleton<IInstanceIdentifier, InstanceIdentifier>();
builder.Services.AddSingleton<IMessageQueueFactory, MessageQueueFactory>();
builder.Services.AddHostedService<SubscriptionReceiverHostedService>();

builder.Services.AddScoped<IWSDigestionServiceCollection, WSDigestionServiceCollection>();
builder.Services.AddScoped<IBasicWebSocketDigestionService, WebSocketDigestionService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    //app.UseSwaggerUI();
}
else if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.UseAuthentication();

app.MapControllers();

app.UseWebSockets();

app.Run();
