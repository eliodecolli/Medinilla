using Medinilla.DataAccess;
using Medinilla.Infrastructure;
using Medinilla.RealTime;
using Medinilla.WebApi;
using Medinilla.WebApi.Interfaces;
using Medinilla.WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers(options => options.InputFormatters.Add(new PlainTextFormatter()));
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole();

builder.Services.AddMedinillaInfrastructure();
builder.Services.AddRealTimeServices();
builder.Services.AddMedinillaDataAccess();

builder.Services.AddScoped<IWSDigestionServiceCollection, WSDigestionServiceCollection>();
builder.Services.AddScoped<IBasicWebSocketDigestionService, WebSocketDigestionService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    //app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseAuthentication();

app.MapControllers();

app.UseWebSockets();

app.Run();
