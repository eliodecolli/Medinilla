namespace Medinilla.WebApi.ApiModels;

public abstract class BaseApiModel
{
    public required string TraceId { get; init; }
}
