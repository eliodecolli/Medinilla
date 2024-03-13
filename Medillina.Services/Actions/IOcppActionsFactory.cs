namespace Medinilla.Services.Actions;

public interface IOcppActionsFactory
{
    IOcppAction? GetAction(string actionName);
}