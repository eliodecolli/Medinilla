namespace Medinilla.Services.Actions;

public class OcppActionsFactory : IOcppActionsFactory
{
    private static Dictionary<string, IOcppAction> _registry;

    // pass actions as interfaces here in order to inject them via DI :)
    public OcppActionsFactory()
    {
        _registry = new Dictionary<string, IOcppAction>();
        // add actions here
    }

    public IOcppAction? GetAction(string actionName)
    {
        if(_registry.TryGetValue(actionName, out var action))
        {
            return action;
        }

        return null;
    }
}
