using Medinilla.Core.Service.Interfaces;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Medinilla.Core.Service;

internal class InboundWorker(IInterfaceCommunication communication) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await communication.Run(stoppingToken);
    }
}
