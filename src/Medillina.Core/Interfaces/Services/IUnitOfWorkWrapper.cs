namespace Medinilla.Core.Interfaces.Services;

/// <summary>
/// Wrapper around the current scope's DbContext.
/// This is safe, each object injected by the DI container during a given scope receive the same DbContext.
/// </summary>
public interface IUnitOfWorkWrapper
{
    Task SaveChanges();

    void DiscardChanges();
}