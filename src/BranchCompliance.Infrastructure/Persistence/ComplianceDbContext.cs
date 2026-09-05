using System.Text.RegularExpressions;
using BranchCompliance.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BranchCompliance.Infrastructure.Persistence;

public partial class ComplianceDbContext(DbContextOptions<ComplianceDbContext> options)
    : IdentityDbContext<DemoUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        if (Database.IsMySql())
        {
            builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        }

        foreach (var entity in builder.Model.GetEntityTypes())
        {
            entity.SetTableName(SnakeCase(entity.GetTableName()!));
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(SnakeCase(property.Name));
            }
        }
    }

    private static string SnakeCase(string value) => WordBoundary().Replace(value, "$1_$2").ToLowerInvariant();

    [GeneratedRegex("([a-z0-9])([A-Z])", RegexOptions.CultureInvariant)]
    private static partial Regex WordBoundary();
}
