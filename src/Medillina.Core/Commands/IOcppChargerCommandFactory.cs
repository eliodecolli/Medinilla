namespace Medinilla.Core.Commands;

public interface IOcppChargerCommandFactory
{
    IOcppChargerCommand? GetCommand(string action);
}
