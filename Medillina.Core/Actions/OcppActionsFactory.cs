using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Actions;

public class OcppActionsFactory : IOcppActionsFactory
{
    private Dictionary<string, IOcppAction> _registry;
    private readonly ILogger<OcppActionsFactory> _logger;


    public OcppActionsFactory(ILogger<OcppActionsFactory> logger, IEnumerable<IOcppAction> registeredActions)
    {
        _logger = logger;
        _registry = new Dictionary<string, IOcppAction>();
        foreach (var action in registeredActions)
        {
            _registry.Add(action.ActionName, action);
        }
    }

    public IOcppAction? GetAction(string actionName)
    {
        if (_registry.TryGetValue(actionName, out var action))
        {
            return action;
        }

        return null;
    }
}
