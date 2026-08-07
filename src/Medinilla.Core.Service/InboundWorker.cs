using Medinilla.Core.Service.Interfaces;
using Microsoft.Extensions.Hosting;

namespace Medinilla.Core.Service;

internal class InboundWorker(IInterfaceCommunication communication) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await communication.Run(stoppingToken);
    }
}
