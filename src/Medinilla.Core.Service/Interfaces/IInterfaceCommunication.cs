namespace Medinilla.Core.Service.Interfaces;

internal interface IInterfaceCommunication
{
    Task Run(CancellationToken ct);
}
