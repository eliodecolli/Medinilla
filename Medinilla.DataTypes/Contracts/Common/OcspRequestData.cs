namespace Medinilla.DataTypes.Contracts.Common;

public sealed class OcspRequestData
{
    public string HashAlgorithm {  get; set; }

    public string IssuerNameHash { get; set; }

    public string IssuerKeyHash { get; set; }

    public string SerialNumber { get; set; }

    public string ResponderURL { get; set; }
}
