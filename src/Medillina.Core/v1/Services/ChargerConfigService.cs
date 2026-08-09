using Medinilla.Core.Interfaces.Services;
using Medinilla.DataAccess.Relational.Enums;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.Models.ChargerConfig;
using Medinilla.DataAccess.Relational.UnitOfWork;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core.Enums;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.v1.Services;

public sealed class ChargerConfigService(ILogger<ChargerConfigService> log, ChargerConfigUnitOfWork unitOfWork, ChargingStationUnitOfWork cUnitOfWork) : IChargerConfigService
{
    public async Task<ReportNotificationIngestionResult> IngestReport(string clientIdentifier, int requestId, int seqNumber, bool tbc, IEnumerable<ReportData> data)
    {
        log.LogInformation("[{ci}]: Ingesting charger report id={ri} notify seqNo={sn}", clientIdentifier, requestId, seqNumber);
        if (!await unitOfWork.EnsureRequest(requestId, seqNumber))
        {
            log.LogError("[{ci}]: Invalid seqNo={sn} in report id={ri} notify", clientIdentifier, seqNumber, requestId);
            return ReportNotificationIngestionResult.InvalidSeqNo;
        }
        
        // now make sure that if this is the last report we have all the required parts
        var missing = await unitOfWork.EnsurePieces(requestId, seqNumber);
        if (missing.Count > 0)
        {
            log.LogError("[{ci}]: Received last report id={ri} data but we're missing seq=[{m}]", clientIdentifier, requestId, string.Join(",", missing));
            return ReportNotificationIngestionResult.MissingSeq;
        }
        
        // alright we have our statuses registered - now update the fucking components
        var chargingStation = await cUnitOfWork.GetChargingStation(clientIdentifier);
        if (chargingStation is null)
        {
            log.LogError("[{ci}]: Request id={ri} was trying to update component info but no registered charging station was found",
                clientIdentifier, requestId);
            return ReportNotificationIngestionResult.InternalError;
        }
        
        // turn of lazy loading so we're not skyrocketing queries
        unitOfWork.DisableLazyLoading();
        
        foreach (var rd in data)
        {
            var shouldCreate = false;
            var component = await unitOfWork.GetComponent(clientIdentifier, rd.Component.Name, rd.Component.Instance);
            if (component is null)
            {
                component = new ChargerComponent()
                {
                    ChargingStation = chargingStation,
                    ClientIdentifier = clientIdentifier,
                    ComponentName = rd.Component.Name,
                    ComponentInstance = rd.Component.Instance,
                };
                shouldCreate = true;
            }

            component.ComponentVariables ??= [];

            // now fetch the EVSE if needed
            if (rd.Component.Evse is not null)
            {
                var targetEvse = rd.Component.Evse;
                var evseQuery = await cUnitOfWork.EvseConnectorSubUnit.EvseConnectorRepository.Filter(e =>
                    e.EvseId == targetEvse.Id
                    && (!targetEvse.ConnectorId.HasValue || e.ConnectorId == targetEvse.ConnectorId.Value));
                var evse = evseQuery.FirstOrDefault();

                // if we dont have this evse right now then just add it ? - let's go with yes for now
                if (evse is null)
                {
                    EvseConnector evseConnector = new EvseConnector()
                    {
                        ChargingStationId = chargingStation.Id,
                        EvseId = rd.Component.Evse.Id,
                        ConnectorId = rd.Component.Evse.ConnectorId ?? 0
                    };
                    evse = await cUnitOfWork.EvseConnectorSubUnit.EvseConnectorRepository.Create(evseConnector);
                }

                component.Connector = evse;
            }
            
            // now fill in the variables
            if (rd.VariableAttribute is not null)
            {
                foreach (var attr in rd.VariableAttribute)
                {
                    var variable = await unitOfWork.GetOrCreateVariable(clientIdentifier, rd.Component.Name,
                        rd.Variable.Name, rd.Component.Instance, rd.Variable.Instance);
                    variable.AttributeType = attr.Type.HasValue
                        ? Enum.Parse<VariableAttributeType>(attr.Type.Value.ToString(), true)
                        : VariableAttributeType.Unkown;
                    variable.Mutability = attr.Mutability.HasValue
                        ? Enum.Parse<VariableMutability>(attr.Mutability.Value.ToString(), true)
                        : VariableMutability.Unknown;
                    variable.Constant = attr.Constant;
                    variable.Value = attr.Value;

                    if (rd.VariableCharacteristics is not null)
                    {
                        variable.Unit = rd.VariableCharacteristics.Unit;
                        variable.DataType = rd.VariableCharacteristics.DataType.ToString();
                        variable.MinLimit = rd.VariableCharacteristics.MinLimit;
                        variable.MaxLimit = rd.VariableCharacteristics.MaxLimit;
                        variable.ValuesList = rd.VariableCharacteristics.ValuesList;
                    }

                    component.ComponentVariables.Add(variable);
                }
            }

            await unitOfWork.UpdateComponent(component, shouldCreate);
        }

        try
        {
            await unitOfWork.Save(); // this should trigger a save in the charging station unit of work as well tbh

            if (!tbc)
            {
                // report's done - drop the sequence tracking so the id is free again.
                // has to happen after the save, otherwise this seqNo is still only in the
                // change tracker and would get written back out behind us.
                await unitOfWork.ClearRequest(requestId);
                await unitOfWork.Save();
                log.LogInformation("[{ci}]: Completed report id={ri} at seqNo={sn}", clientIdentifier, requestId, seqNumber);
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "[{ci}]: Error while trying to save update for reqId={ri} seqNo={sn}",
                clientIdentifier, requestId, seqNumber);
            return ReportNotificationIngestionResult.InternalError;
        }

        return ReportNotificationIngestionResult.Ok;
    }
}