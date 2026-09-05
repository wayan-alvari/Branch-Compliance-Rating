using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Branches;
using BranchCompliance.Domain.Periods;
using BranchCompliance.Domain.Templates;
using BranchCompliance.Domain.Workspaces;
using BranchCompliance.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BranchCompliance.Infrastructure.Persistence;

public partial class ComplianceDbContext
{
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<AssessmentTemplate> Templates => Set<AssessmentTemplate>();
    public DbSet<TemplateCategory> TemplateCategories => Set<TemplateCategory>();
    public DbSet<TemplateCriterion> TemplateCriteria => Set<TemplateCriterion>();
    public DbSet<RatingBand> RatingBands => Set<RatingBand>();
    public DbSet<AssessmentPeriod> Periods => Set<AssessmentPeriod>();
    public DbSet<PeriodCriterion> PeriodCriteria => Set<PeriodCriterion>();
    public DbSet<PeriodRatingBand> PeriodRatingBands => Set<PeriodRatingBand>();
    public DbSet<BranchAssessment> Assessments => Set<BranchAssessment>();
    public DbSet<BranchResponse> Responses => Set<BranchResponse>();
    public DbSet<CriterionScore> Scores => Set<CriterionScore>();
    public DbSet<Appeal> Appeals => Set<Appeal>();
    public DbSet<EvidenceFile> EvidenceFiles => Set<EvidenceFile>();

    private void ConfigureComplianceModel(ModelBuilder builder)
    {
        ConfigureWorkspaceEntity<Branch>(builder);
        ConfigureWorkspaceEntity<AssessmentTemplate>(builder);
        ConfigureWorkspaceEntity<TemplateCategory>(builder);
        ConfigureWorkspaceEntity<TemplateCriterion>(builder);
        ConfigureWorkspaceEntity<RatingBand>(builder);
        ConfigureWorkspaceEntity<AssessmentPeriod>(builder);
        ConfigureWorkspaceEntity<PeriodCriterion>(builder);
        ConfigureWorkspaceEntity<PeriodRatingBand>(builder);
        ConfigureWorkspaceEntity<BranchAssessment>(builder);
        ConfigureWorkspaceEntity<BranchResponse>(builder);
        ConfigureWorkspaceEntity<CriterionScore>(builder);
        ConfigureWorkspaceEntity<Appeal>(builder);
        ConfigureWorkspaceEntity<EvidenceFile>(builder);

        var branch = builder.Entity<Branch>();
        branch.HasIndex(row => new { row.WorkspaceId, row.Code }).IsUnique();
        branch.HasIndex(row => new { row.WorkspaceId, row.IsActive });
        branch.Property(row => row.Code).HasMaxLength(24);
        branch.Property(row => row.Name).HasMaxLength(120);
        branch.Property(row => row.Region).HasMaxLength(80);
        branch.Property(row => row.BranchUserId).HasMaxLength(128);
        branch.HasOne<DemoUser>().WithMany().HasForeignKey(row => row.BranchUserId).OnDelete(DeleteBehavior.Restrict);

        var template = builder.Entity<AssessmentTemplate>();
        template.HasIndex(row => new { row.WorkspaceId, row.FamilyId, row.Version }).IsUnique();
        template.Property(row => row.Name).HasMaxLength(120);
        template.Property(row => row.Description).HasMaxLength(1000);
        template.Property(row => row.State).HasConversion<string>().HasMaxLength(24);
        template.HasMany(row => row.Categories).WithOne().HasForeignKey(row => new { row.WorkspaceId, row.TemplateId })
            .HasPrincipalKey(row => new { row.WorkspaceId, row.Id }).OnDelete(DeleteBehavior.Cascade);
        template.HasMany(row => row.Criteria).WithOne().HasForeignKey(row => new { row.WorkspaceId, row.TemplateId })
            .HasPrincipalKey(row => new { row.WorkspaceId, row.Id }).OnDelete(DeleteBehavior.Cascade);
        template.HasMany(row => row.Bands).WithOne().HasForeignKey(row => new { row.WorkspaceId, row.TemplateId })
            .HasPrincipalKey(row => new { row.WorkspaceId, row.Id }).OnDelete(DeleteBehavior.Cascade);
        template.Navigation(row => row.Categories).UsePropertyAccessMode(PropertyAccessMode.Field);
        template.Navigation(row => row.Criteria).UsePropertyAccessMode(PropertyAccessMode.Field);
        template.Navigation(row => row.Bands).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Entity<TemplateCategory>().Property(row => row.Name).HasMaxLength(100);
        builder.Entity<TemplateCategory>().HasIndex(row => new { row.WorkspaceId, row.TemplateId, row.Name }).IsUnique();
        var criterion = builder.Entity<TemplateCriterion>();
        criterion.Property(row => row.Code).HasMaxLength(24);
        criterion.Property(row => row.Title).HasMaxLength(160);
        criterion.Property(row => row.Guidance).HasMaxLength(2000);
        criterion.HasIndex(row => new { row.WorkspaceId, row.TemplateId, row.Code }).IsUnique();
        criterion.HasOne<TemplateCategory>().WithMany().HasForeignKey(row => new { row.WorkspaceId, row.CategoryId })
            .HasPrincipalKey(row => new { row.WorkspaceId, row.Id }).OnDelete(DeleteBehavior.Restrict);
        var band = builder.Entity<RatingBand>();
        band.Property(row => row.Label).HasMaxLength(60);
        band.Property(row => row.Color).HasMaxLength(12);
        band.HasIndex(row => new { row.WorkspaceId, row.TemplateId, row.MinimumInclusive }).IsUnique();

        var period = builder.Entity<AssessmentPeriod>();
        period.Property(row => row.Name).HasMaxLength(120);
        period.Property(row => row.TemplateName).HasMaxLength(120);
        period.Property(row => row.Phase).HasConversion<string>().HasMaxLength(24);
        period.HasIndex(row => new { row.WorkspaceId, row.Phase });
        period.HasOne<AssessmentTemplate>().WithMany().HasForeignKey(row => new { row.WorkspaceId, row.TemplateId })
            .HasPrincipalKey(row => new { row.WorkspaceId, row.Id }).OnDelete(DeleteBehavior.Restrict);
        period.HasMany(row => row.Criteria).WithOne().HasForeignKey(row => new { row.WorkspaceId, row.PeriodId })
            .HasPrincipalKey(row => new { row.WorkspaceId, row.Id }).OnDelete(DeleteBehavior.Cascade);
        period.HasMany(row => row.Bands).WithOne().HasForeignKey(row => new { row.WorkspaceId, row.PeriodId })
            .HasPrincipalKey(row => new { row.WorkspaceId, row.Id }).OnDelete(DeleteBehavior.Cascade);
        period.Navigation(row => row.Criteria).UsePropertyAccessMode(PropertyAccessMode.Field);
        period.Navigation(row => row.Bands).UsePropertyAccessMode(PropertyAccessMode.Field);
        var snapshot = builder.Entity<PeriodCriterion>();
        snapshot.Property(row => row.Category).HasMaxLength(100);
        snapshot.Property(row => row.Code).HasMaxLength(24);
        snapshot.Property(row => row.Title).HasMaxLength(160);
        snapshot.Property(row => row.Guidance).HasMaxLength(2000);
        snapshot.HasIndex(row => new { row.WorkspaceId, row.PeriodId, row.Code }).IsUnique();
        builder.Entity<PeriodRatingBand>().Property(row => row.Label).HasMaxLength(60);
        builder.Entity<PeriodRatingBand>().Property(row => row.Color).HasMaxLength(12);
        builder.Entity<PeriodRatingBand>().HasIndex(row => new { row.WorkspaceId, row.PeriodId, row.MinimumInclusive }).IsUnique();

        var assessment = builder.Entity<BranchAssessment>();
        assessment.Property(row => row.BranchName).HasMaxLength(120);
        assessment.Property(row => row.BranchCode).HasMaxLength(24);
        assessment.Property(row => row.Region).HasMaxLength(80);
        assessment.Property(row => row.AssessorId).HasMaxLength(128);
        assessment.Property(row => row.BranchUserId).HasMaxLength(128);
        assessment.Property(row => row.ProvisionalRating).HasMaxLength(60);
        assessment.Property(row => row.FinalRating).HasMaxLength(60);
        assessment.Property(row => row.State).HasConversion<string>().HasMaxLength(32);
        assessment.HasIndex(row => new { row.WorkspaceId, row.PeriodId, row.BranchId }).IsUnique();
        assessment.HasIndex(row => new { row.WorkspaceId, row.PeriodId, row.FinalScore });
        assessment.HasIndex(row => new { row.WorkspaceId, row.State, row.AssessorId });
        assessment.HasIndex(row => new { row.WorkspaceId, row.BranchUserId });
        assessment.HasOne<AssessmentPeriod>().WithMany().HasForeignKey(row => new { row.WorkspaceId, row.PeriodId })
            .HasPrincipalKey(row => new { row.WorkspaceId, row.Id }).OnDelete(DeleteBehavior.Restrict);
        assessment.HasOne<Branch>().WithMany().HasForeignKey(row => new { row.WorkspaceId, row.BranchId })
            .HasPrincipalKey(row => new { row.WorkspaceId, row.Id }).OnDelete(DeleteBehavior.Restrict);
        assessment.HasOne<DemoUser>().WithMany().HasForeignKey(row => row.AssessorId).OnDelete(DeleteBehavior.Restrict);
        assessment.HasOne<DemoUser>().WithMany().HasForeignKey(row => row.BranchUserId).OnDelete(DeleteBehavior.Restrict);
        assessment.HasMany(row => row.Responses).WithOne().HasForeignKey(row => new { row.WorkspaceId, row.AssessmentId })
            .HasPrincipalKey(row => new { row.WorkspaceId, row.Id }).OnDelete(DeleteBehavior.Cascade);
        assessment.HasMany(row => row.Scores).WithOne().HasForeignKey(row => new { row.WorkspaceId, row.AssessmentId })
            .HasPrincipalKey(row => new { row.WorkspaceId, row.Id }).OnDelete(DeleteBehavior.Cascade);
        assessment.HasMany(row => row.Appeals).WithOne().HasForeignKey(row => new { row.WorkspaceId, row.AssessmentId })
            .HasPrincipalKey(row => new { row.WorkspaceId, row.Id }).OnDelete(DeleteBehavior.Cascade);
        assessment.HasMany(row => row.Evidence).WithOne().HasForeignKey(row => new { row.WorkspaceId, row.AssessmentId })
            .HasPrincipalKey(row => new { row.WorkspaceId, row.Id }).OnDelete(DeleteBehavior.Cascade);
        assessment.Navigation(row => row.Responses).UsePropertyAccessMode(PropertyAccessMode.Field);
        assessment.Navigation(row => row.Scores).UsePropertyAccessMode(PropertyAccessMode.Field);
        assessment.Navigation(row => row.Appeals).UsePropertyAccessMode(PropertyAccessMode.Field);
        assessment.Navigation(row => row.Evidence).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Entity<BranchResponse>().Property(row => row.UpdatedBy).HasMaxLength(128);
        builder.Entity<BranchResponse>().HasIndex(row => new { row.WorkspaceId, row.AssessmentId, row.PeriodCriterionId }).IsUnique();
        builder.Entity<BranchResponse>().HasOne<PeriodCriterion>().WithMany().HasForeignKey(row => new { row.WorkspaceId, row.PeriodCriterionId })
            .HasPrincipalKey(row => new { row.WorkspaceId, row.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<CriterionScore>().Property(row => row.ActorId).HasMaxLength(128);
        builder.Entity<CriterionScore>().HasIndex(row => new { row.WorkspaceId, row.AssessmentId, row.PeriodCriterionId, row.Revision }).IsUnique();
        builder.Entity<CriterionScore>().HasOne<PeriodCriterion>().WithMany().HasForeignKey(row => new { row.WorkspaceId, row.PeriodCriterionId })
            .HasPrincipalKey(row => new { row.WorkspaceId, row.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<CriterionScore>().HasOne<Appeal>().WithMany().HasForeignKey(row => new { row.WorkspaceId, row.AppealId })
            .HasPrincipalKey(row => new { row.WorkspaceId, row.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Appeal>().Property(row => row.SubmittedBy).HasMaxLength(128);
        builder.Entity<Appeal>().Property(row => row.DecidedBy).HasMaxLength(128);
        builder.Entity<Appeal>().Property(row => row.Decision).HasConversion<string>().HasMaxLength(16);
        builder.Entity<Appeal>().HasIndex(row => new { row.WorkspaceId, row.AssessmentId, row.PeriodCriterionId }).IsUnique();
        builder.Entity<Appeal>().HasIndex(row => new { row.WorkspaceId, row.Decision });
        builder.Entity<Appeal>().HasOne<PeriodCriterion>().WithMany().HasForeignKey(row => new { row.WorkspaceId, row.PeriodCriterionId })
            .HasPrincipalKey(row => new { row.WorkspaceId, row.Id }).OnDelete(DeleteBehavior.Restrict);
        var evidence = builder.Entity<EvidenceFile>();
        evidence.Property(row => row.OriginalName).HasMaxLength(150);
        evidence.Property(row => row.StorageName).HasMaxLength(40);
        evidence.Property(row => row.MediaType).HasMaxLength(32);
        evidence.Property(row => row.Sha256).HasMaxLength(64);
        evidence.Property(row => row.UploadedBy).HasMaxLength(128);
        evidence.HasIndex(row => new { row.WorkspaceId, row.StorageName }).IsUnique();
        evidence.HasOne<BranchResponse>().WithMany().HasForeignKey(row => new { row.WorkspaceId, row.ResponseId })
            .HasPrincipalKey(row => new { row.WorkspaceId, row.Id }).OnDelete(DeleteBehavior.Restrict);
        evidence.HasOne<Appeal>().WithMany().HasForeignKey(row => new { row.WorkspaceId, row.AppealId })
            .HasPrincipalKey(row => new { row.WorkspaceId, row.Id }).OnDelete(DeleteBehavior.Restrict);

        var utc = new ValueConverter<DateTime, DateTime>(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        foreach (var type in builder.Model.GetEntityTypes().Where(type => typeof(WorkspaceEntity).IsAssignableFrom(type.ClrType) || type.ClrType == typeof(DemoWorkspace)))
        {
            foreach (var property in type.GetProperties())
            {
                if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                {
                    property.SetPrecision(5);
                    property.SetScale(2);
                }
                if (property.ClrType == typeof(string) && property.GetMaxLength() is null) property.SetMaxLength(2000);
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?)) property.SetValueConverter(utc);
            }
        }
    }

    internal async Task DeleteExpiredWorkspaceRowsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        // Explicit leaf-to-root order respects MySQL restrictive foreign keys.
        // This method is reserved for the coordinator-protected expiry path.
        await EvidenceFiles.Where(row => row.WorkspaceId == workspaceId).IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        await Scores.Where(row => row.WorkspaceId == workspaceId).IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        await Appeals.Where(row => row.WorkspaceId == workspaceId).IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        await Responses.Where(row => row.WorkspaceId == workspaceId).IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        await Assessments.Where(row => row.WorkspaceId == workspaceId).IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        await PeriodCriteria.Where(row => row.WorkspaceId == workspaceId).IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        await PeriodRatingBands.Where(row => row.WorkspaceId == workspaceId).IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        await Periods.Where(row => row.WorkspaceId == workspaceId).IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        await TemplateCriteria.Where(row => row.WorkspaceId == workspaceId).IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        await TemplateCategories.Where(row => row.WorkspaceId == workspaceId).IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        await RatingBands.Where(row => row.WorkspaceId == workspaceId).IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        await Templates.Where(row => row.WorkspaceId == workspaceId).IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        await Branches.Where(row => row.WorkspaceId == workspaceId).IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        await AuditEvents.Where(row => row.WorkspaceId == workspaceId).IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        await Workspaces.Where(row => row.WorkspaceId == workspaceId).ExecuteDeleteAsync(cancellationToken);
    }
}
