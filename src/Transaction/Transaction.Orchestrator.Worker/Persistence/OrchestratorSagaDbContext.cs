using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;

namespace Transaction.Orchestrator.Worker.Persistence;

public sealed class OrchestratorSagaDbContext(DbContextOptions<OrchestratorSagaDbContext> options)
    : MassTransit.EntityFrameworkCoreIntegration.SagaDbContext(options)
{
    protected override IEnumerable<ISagaClassMap> Configurations
    {
        get { yield return new TransactionOrchestrationStateMap(); }
    }
}
