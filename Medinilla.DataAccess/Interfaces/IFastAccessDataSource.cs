namespace Medinilla.DataAccess.Interfaces;

public interface IFastAccessDataSource
{
    public T? GetValue<T>(string key);

    public void SetValue<T>(string key, T value);
}
