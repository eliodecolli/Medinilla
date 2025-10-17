namespace Medinilla.Core.Actions;

public interface IOcppActionsFactory
{
    IOcppAction? GetAction(string actionName);
}