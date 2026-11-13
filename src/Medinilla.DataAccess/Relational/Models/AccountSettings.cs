namespace Medinilla.DataAccess.Relational.Models;

public class AccountSettings
{
    public Guid AccountId { get; set; }
    
    /// <summary>
    /// Comma separated list of the <see cref="Medinilla.DataAccess.Relational.Enums.Measurand"/> values
    /// that are allowed to be stored for this account.
    /// </summary>
    public string AllowedMeasurands { get; set; } = string.Empty;
    
    public virtual required Account Account { get; set; }
}