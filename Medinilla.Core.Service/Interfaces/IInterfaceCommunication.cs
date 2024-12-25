using Medinilla.Core.Service.Types;

namespace Medinilla.Core.Service.Interfaces;

internal interface IInterfaceCommunication
{
    Task Connect(CommunicationSettings settings);

    Task Run();
}
