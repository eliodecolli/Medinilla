namespace Medinilla.WebApi.ApiModels;

public sealed class WAMPApiResult<T> : ErrorApiModel
{
    T? Result { get; set; }
}
