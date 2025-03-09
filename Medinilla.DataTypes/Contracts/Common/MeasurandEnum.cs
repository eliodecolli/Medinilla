
namespace Medinilla.DataTypes.Contracts.Common;

/// <summary>
/// Type of measurement
/// Default = "Energy.Active.Import.Register"
/// </summary>
public enum MeasurandEnum
{
    /// <summary>
    /// Export current
    /// </summary>
    CurrentExport,

    /// <summary>
    /// Import current
    /// </summary>
    CurrentImport,

    /// <summary>
    /// Offered current
    /// </summary>
    CurrentOffered,

    /// <summary>
    /// Exported active energy register value
    /// </summary>
    EnergyActiveExportRegister,

    /// <summary>
    /// Imported active energy register value
    /// </summary>
    EnergyActiveImportRegister,

    /// <summary>
    /// Exported reactive energy register value
    /// </summary>
    EnergyReactiveExportRegister,

    /// <summary>
    /// Imported reactive energy register value
    /// </summary>
    EnergyReactiveImportRegister,

    /// <summary>
    /// Exported active energy interval value
    /// </summary>
    EnergyActiveExportInterval,

    /// <summary>
    /// Imported active energy interval value
    /// </summary>
    EnergyActiveImportInterval,

    /// <summary>
    /// Net active energy
    /// </summary>
    EnergyActiveNet,

    /// <summary>
    /// Exported reactive energy interval value
    /// </summary>
    EnergyReactiveExportInterval,

    /// <summary>
    /// Imported reactive energy interval value
    /// </summary>
    EnergyReactiveImportInterval,

    /// <summary>
    /// Net reactive energy
    /// </summary>
    EnergyReactiveNet,

    /// <summary>
    /// Net apparent energy
    /// </summary>
    EnergyApparentNet,

    /// <summary>
    /// Imported apparent energy
    /// </summary>
    EnergyApparentImport,

    /// <summary>
    /// Exported apparent energy
    /// </summary>
    EnergyApparentExport,

    /// <summary>
    /// Power line frequency
    /// </summary>
    Frequency,

    /// <summary>
    /// Active power exported
    /// </summary>
    PowerActiveExport,

    /// <summary>
    /// Active power imported
    /// </summary>
    PowerActiveImport,

    /// <summary>
    /// Power factor
    /// </summary>
    PowerFactor,

    /// <summary>
    /// Power offered
    /// </summary>
    PowerOffered,

    /// <summary>
    /// Reactive power exported
    /// </summary>
    PowerReactiveExport,

    /// <summary>
    /// Reactive power imported
    /// </summary>
    PowerReactiveImport,

    /// <summary>
    /// State of Charge in percentage
    /// </summary>
    SoC,

    /// <summary>
    /// Voltage
    /// </summary>
    Voltage
}
