using Medinilla.Core;
using Medinilla.Core.Actions;
using Medinilla.Core.SharedContracts.Events;
using Medinilla.DataAccess.Relational;
using Medinilla.DataAccess.Relational.UnitOfWork;
using Medinilla.DataTypes.Core;
using Medinilla.Infrastructure.Events;
using Medinilla.Infrastructure.WAMP;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Medinilla.Core.Tests;

public class ServiceRegistrationShould
{
    [Fact]
    public void ResolveAllInboundActionsWithTheirRegisteredDependencies()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<MedinillaOcppDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddTransient<ChargerConfigUnitOfWork>();
        services.AddScoped<IChargerEventPublisher, NoOpEventPublisher>();
        services.AddMedinillaServices();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        var factory = scope.ServiceProvider.GetRequiredService<IOcppActionsFactory>();

        Assert.NotNull(factory.GetAction(OcppActionNames.BootNotification));
        Assert.NotNull(factory.GetAction(OcppActionNames.StatusNotification));
        Assert.NotNull(factory.GetAction(OcppActionNames.Authorize));
        Assert.NotNull(factory.GetAction(OcppActionNames.TransactionEvent));
        Assert.NotNull(factory.GetAction(OcppActionNames.NotifyReport));
        Assert.NotNull(factory.GetAction(OcppActionNames.Heartbeat));
        Assert.NotNull(factory.GetAction(OcppActionNames.SecurityEventNotification));
    }

    private sealed class NoOpEventPublisher : IChargerEventPublisher
    {
        public Task PublishAsync(
            MedinillaHttpEvent httpEvent,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
