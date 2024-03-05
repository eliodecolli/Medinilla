using Medinilla.DataAccess.Interfaces;

namespace Medinilla.DataAccess;

public sealed class InMemoryFastAccessDataSource : IFastAccessDataSource
{
    private readonly Dictionary<string, object> _data;

    public InMemoryFastAccessDataSource()
    {
        _data = new Dictionary<string, object>();
    }

    private T? SafeCast<T>(object value)
    {
        if(value.GetType() == typeof(T))
        {
            return (T)value;
        }

        return default;
    }

    public T? GetValue<T>(string key)
    {
        if(_data.TryGetValue(key, out var value))
        {
            return SafeCast<T>(value);
        }

        return default;
    }

    public void SetValue<T>(string key, T value)
    {
        _data.Add(key, value!);
    }
}
