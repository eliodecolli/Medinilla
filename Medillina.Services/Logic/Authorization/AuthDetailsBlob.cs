namespace Medinilla.Core.Logic.Authorization;

public sealed class ExpirationCheckBlob
{
    public bool Flag { get; set; }
}

public sealed class EvseDetailsBlob
{
    public int EvseId { get; set; }

    public bool Allowed { get; set; }
}

public sealed class EvseCheckBlob
{
    public IEnumerable<EvseDetailsBlob> Evses { get; set; }
}

public sealed class LocationCheckBlob
{
    public IEnumerable<string> BlockedLocations { get; set; }
}

public sealed class DateRangeCheckBlob
{
    public DateTime Start { get; set; }

    public DateTime End { get; set; }
}

public sealed class CreditCheckBlob
{
    public bool Flag { get; set; }
}

public sealed class AuthDetailsBlob
{
    public ExpirationCheckBlob? ExpiryCheck { get; set; }

    public EvseCheckBlob? EvseCheck { get; set; }

    public LocationCheckBlob? LocationCheck { get; set; }

    public CreditCheckBlob? CreditCheck { get; set; }
}
