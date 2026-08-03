namespace Medinilla.WebApi.Interfaces;

public interface IMessageQueueFactory
{
    IMessageQueue Create();
}
