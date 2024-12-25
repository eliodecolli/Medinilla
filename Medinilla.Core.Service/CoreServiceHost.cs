using Medinilla.Core.Service.Interfaces;
using Medinilla.Core.Service.Types;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Service;

internal class CoreServiceHost : IHostedService
{
    private readonly IInterfaceCommunication _service;
    private readonly ILogger<CoreServiceHost> _logger;
    private readonly CommunicationSettings _settings;

    public CoreServiceHost(
        IInterfaceCommunication service,
        ILogger<CoreServiceHost> logger)
    {
        _service = service;
        _logger = logger;
        _settings = CommunicationSettings.FromSettignsFile("settings.json");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting Core Service...");
        await _service.Connect(_settings);
        await _service.Run();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Stopping Core Service...");
        return Task.CompletedTask;
    }
}