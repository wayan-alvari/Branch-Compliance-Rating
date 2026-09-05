using BranchCompliance.Application.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BranchCompliance.Infrastructure.Persistence;

public sealed class ComplianceDesignTimeFactory : IDesignTimeDbContextFactory<ComplianceDbContext>
{
    public const string PlaceholderConnection = "Server=127.0.0.1;Database=portfolio_branch_compliance;User=<app_user>;Password=<password>;CharSet=utf8mb4";
    public ComplianceDbContext CreateDbContext(string[] args)
    {
        // Design-time model/script generation never needs a live database or a secret.
        // Migration execution receives the owner's connection from an environment variable.
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? PlaceholderConnection;
        var options = new DbContextOptionsBuilder<ComplianceDbContext>()
            .UseMySql(connection, new MySqlServerVersion(new Version(8, 0, 46)), mysql => mysql.MigrationsHistoryTable("__ef_migrations_history"))
            .ReplaceService<IHistoryRepository, LowercaseMySqlHistoryRepository>().Options;
        return new ComplianceDbContext(options, new WorkspaceContext());
    }
}
