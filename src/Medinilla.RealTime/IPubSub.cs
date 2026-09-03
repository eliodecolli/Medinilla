namespace Medinilla.RealTime;

public interface IPubSub
{
    Task ListenForTopic(string topic, Action<string, string> callback);
    Task Publish(string topic, string message);
}