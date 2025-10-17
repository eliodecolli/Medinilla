namespace Medinilla.Infrastructure.Core.Authorization;

public enum AuthorizationAlgorithm
{
    Default,
    ExpirationCheck,
    EvseCheck,
    LocationCheck,
    DateRangeCheck,
    CreditCheck
}
