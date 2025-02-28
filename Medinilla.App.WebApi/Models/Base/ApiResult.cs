namespace Medinilla.App.WebApi.Models.Base;

public sealed class ApiResult<T>
{
    public T? Result { get; set; }

    public bool Error { get; set; }

    public string? ErrorMessage { get; set; }
}
