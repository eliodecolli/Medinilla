namespace Medinilla.DataTypes.Core;

public class BootReasonCode
{
    public static BootReasonCode BootOk = new BootReasonCode()
    {
        Code = "200",
        Detail = "Ok",
    };

    public static BootReasonCode BootError = new BootReasonCode()
    {
        Code = "500",
        Detail = "Internal System Error",
    };
    
    public string Code { get; init; }
    
    public string Detail { get; init; }
}