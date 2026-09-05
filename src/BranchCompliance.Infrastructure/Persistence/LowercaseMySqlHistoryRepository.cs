using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Pomelo.EntityFrameworkCore.MySql.Migrations.Internal;

namespace BranchCompliance.Infrastructure.Persistence;

// EF's documented IHistoryRepository extension point needs the provider's base
// implementation. This narrow adapter is tested against pinned Pomelo 8.0.3.
// https://learn.microsoft.com/ef/core/managing-schemas/migrations/history-table
#pragma warning disable EF1001
public sealed class LowercaseMySqlHistoryRepository(HistoryRepositoryDependencies dependencies) : MySqlHistoryRepository(dependencies)
{
    protected override void ConfigureTable(EntityTypeBuilder<HistoryRow> history)
    {
        base.ConfigureTable(history);
        history.Property(row => row.MigrationId).HasColumnName("migration_id");
        history.Property(row => row.ProductVersion).HasColumnName("product_version");
    }
}
#pragma warning restore EF1001
