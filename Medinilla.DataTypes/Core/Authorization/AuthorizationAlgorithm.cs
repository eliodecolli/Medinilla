namespace Medinilla.DataTypes.Core.Authorization;

public enum AuthorizationAlgorithm
{
    Default,
    ExpirationCheck,
    EvseCheck,
    LocationCheck,
    DateRangeCheck,
    CreditCheck
}
