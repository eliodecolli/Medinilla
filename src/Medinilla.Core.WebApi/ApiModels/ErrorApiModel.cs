namespace Medinilla.WebApi.ApiModels;

public sealed class ErrorApiModel : BaseApiModel
{
    public required string? Error { get; init; }
}
