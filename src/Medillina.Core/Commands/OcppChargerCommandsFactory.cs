using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Commands;

public class OcppChargerCommandsFactory(
    ILogger<OcppChargerCommandsFactory> _logger,
    IEnumerable<IOcppChargerCommand> registeredCommands) : IOcppChargerCommandFactory
{
    private readonly Dictionary<string, IOcppChargerCommand> _registry =
        registeredCommands.ToDictionary(c => c.Action);

    public IOcppChargerCommand? GetCommand(string action)
    {
        if (_registry.TryGetValue(action, out var command))
        {
            return command;
        }

        _logger.LogWarning("No charger command registered for action '{Action}'", action);
        return null;
    }
}
