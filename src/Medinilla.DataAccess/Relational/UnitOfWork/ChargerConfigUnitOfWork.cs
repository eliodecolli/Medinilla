using System.Collections.Immutable;
using Medinilla.DataAccess.Interfaces;
using Medinilla.DataAccess.Relational.Models.ChargerConfig;

namespace Medinilla.DataAccess.Relational.UnitOfWork;

public class ChargerConfigUnitOfWork(MedinillaOcppDbContext context) : BaseUnitOfWork(context)
{
    private readonly IRepository<ChargerComponent> _components = new GenericRepository<ChargerComponent>(context);
    private readonly IRepository<ReportBaseStatus> _statuses = new GenericRepository<ReportBaseStatus>(context);
    private readonly IRepository<ComponentVariable> _variables = new GenericRepository<ComponentVariable>(context);

    public void DisableLazyLoading()
    {
        context.ChangeTracker.LazyLoadingEnabled = false;
    }

    public async Task<ComponentVariable> GetOrCreateVariable(string clientIdentifier, string componentName,
        string variableName, string? componentInstance, string? variableInstance)
    {
        var query = await _variables.Filter(v =>
             v.Component.ClientIdentifier == clientIdentifier &&
             v.Component.ComponentName == componentName &&
             v.Component.ComponentInstance == componentInstance &&
             v.Name == variableName &&
             v.Instance == variableInstance);
        
        var entity =  query.FirstOrDefault();
        if (entity is null)
        {
            entity = new ComponentVariable()
            {
                Name = variableName,
                Instance = variableInstance,
            };
        }

        return entity;
    }

    public async Task<List<int>> EnsurePieces(int requestId, int currentSeq)
    {
        var query = await _statuses.Filter(s => s.RequestId == requestId);
        var set = query.Select(r => r.SeqNo).Append(currentSeq).ToImmutableSortedSet();
        return Enumerable.Range(0, set.Max)
            .Where(i => !set.Contains(i))
            .ToList();
    }
    
    public Task ClearRequest(int requestId)
    {
        return _statuses.DeleteMany(s => s.RequestId == requestId);
    }

    public async Task<bool> EnsureRequest(int requestId, int seqNumber)
    {
        var query = await _statuses.Filter(s => s.RequestId == requestId);
        var set = query.Select(r => r.SeqNo)
            .ToImmutableSortedSet();
        
        if (!set.IsEmpty && (seqNumber <= set.Max || set.Contains(seqNumber)))
        {
            // yeah, we either have this or it's not valid
            return false;
        }

        await _statuses.Create(new ReportBaseStatus()
        {
            RequestId = requestId,
            SeqNo = seqNumber,
        });
        return true;
    }

    public async Task<ChargerComponent?> GetComponent(string clientIdentifier, string name, string? instance)
    {
        var query = await _components.Filter(c =>  c.ClientIdentifier == clientIdentifier && c.ComponentName == name && c.ComponentInstance == instance);
        return query.FirstOrDefault();
    }

    public async Task<ChargerComponent> UpdateComponent(ChargerComponent component, bool shouldCreate)
    {
        if (shouldCreate)
        {
            await _components.Create(component);
        }
        else
        {
            await _components.Update(component);
        }
        
        return component;
    }
}