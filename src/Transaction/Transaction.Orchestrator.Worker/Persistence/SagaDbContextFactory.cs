using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Transaction.Orchestrator.Worker.Persistence;

public sealed class SagaDbContextFactory : IDesignTimeDbContextFactory<OrchestratorSagaDbContext>
{
    public OrchestratorSagaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OrchestratorSagaDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=ato_db;Username=ato;Password=ato_pass")
            .Options;

        return new OrchestratorSagaDbContext(options);
    }
}
