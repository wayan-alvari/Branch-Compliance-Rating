using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BranchCompliance.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialCompliance : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterDatabase()
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "asp_net_roles",
            columns: table => new
            {
                id = table.Column<string>(type: "varchar(255)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                normalized_name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                concurrency_stamp = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_asp_net_roles", x => x.id);
            })
            .Annotation("MySql:CharSet", "utf8mb4")
            .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

        migrationBuilder.CreateTable(
            name: "asp_net_users",
            columns: table => new
            {
                id = table.Column<string>(type: "varchar(255)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                user_name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                normalized_user_name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                normalized_email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                email_confirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                password_hash = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                security_stamp = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                concurrency_stamp = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                phone_number = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                phone_number_confirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                two_factor_enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                lockout_end = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                lockout_enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                access_failed_count = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_asp_net_users", x => x.id);
            })
            .Annotation("MySql:CharSet", "utf8mb4")
            .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

        migrationBuilder.CreateTable(
            name: "workspace_redirects",
            columns: table => new
            {
                workspace_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                replacement_workspace_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                retired_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_workspace_redirects", x => x.workspace_id);
            })
            .Annotation("MySql:CharSet", "utf8mb4")
            .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

        migrationBuilder.CreateTable(
            name: "workspaces",
            columns: table => new
            {
                workspace_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                last_activity_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                expires_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                seed_version = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_workspaces", x => x.workspace_id);
            })
            .Annotation("MySql:CharSet", "utf8mb4")
            .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

        migrationBuilder.CreateTable(
            name: "asp_net_role_claims",
            columns: table => new
            {
                id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                role_id = table.Column<string>(type: "varchar(255)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                claim_type = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                claim_value = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_asp_net_role_claims", x => x.id);
                table.ForeignKey(
                    name: "FK_asp_net_role_claims_asp_net_roles_role_id",
                    column: x => x.role_id,
                    principalTable: "asp_net_roles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4")
            .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

        migrationBuilder.CreateTable(
            name: "asp_net_user_claims",
            columns: table => new
            {
                id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                user_id = table.Column<string>(type: "varchar(255)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                claim_type = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                claim_value = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_asp_net_user_claims", x => x.id);
                table.ForeignKey(
                    name: "FK_asp_net_user_claims_asp_net_users_user_id",
                    column: x => x.user_id,
                    principalTable: "asp_net_users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4")
            .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

        migrationBuilder.CreateTable(
            name: "asp_net_user_logins",
            columns: table => new
            {
                login_provider = table.Column<string>(type: "varchar(255)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                provider_key = table.Column<string>(type: "varchar(255)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                provider_display_name = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                user_id = table.Column<string>(type: "varchar(255)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_asp_net_user_logins", x => new { x.login_provider, x.provider_key });
                table.ForeignKey(
                    name: "FK_asp_net_user_logins_asp_net_users_user_id",
                    column: x => x.user_id,
                    principalTable: "asp_net_users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4")
            .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

        migrationBuilder.CreateTable(
            name: "asp_net_user_roles",
            columns: table => new
            {
                user_id = table.Column<string>(type: "varchar(255)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                role_id = table.Column<string>(type: "varchar(255)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_asp_net_user_roles", x => new { x.user_id, x.role_id });
                table.ForeignKey(
                    name: "FK_asp_net_user_roles_asp_net_roles_role_id",
                    column: x => x.role_id,
                    principalTable: "asp_net_roles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_asp_net_user_roles_asp_net_users_user_id",
                    column: x => x.user_id,
                    principalTable: "asp_net_users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4")
            .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

        migrationBuilder.CreateTable(
            name: "asp_net_user_tokens",
            columns: table => new
            {
                user_id = table.Column<string>(type: "varchar(255)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                login_provider = table.Column<string>(type: "varchar(255)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                name = table.Column<string>(type: "varchar(255)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                value = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_asp_net_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                table.ForeignKey(
                    name: "FK_asp_net_user_tokens_asp_net_users_user_id",
                    column: x => x.user_id,
                    principalTable: "asp_net_users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4")
            .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

        migrationBuilder.CreateTable(
            name: "audit_events",
            columns: table => new
            {
                id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                actor_id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                action = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                entity_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                details = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                workspace_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                change_version = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_audit_events", x => x.id);
                table.UniqueConstraint("AK_audit_events_workspace_id_id", x => new { x.workspace_id, x.id });
                table.ForeignKey(
                    name: "FK_audit_events_workspaces_workspace_id",
                    column: x => x.workspace_id,
                    principalTable: "workspaces",
                    principalColumn: "workspace_id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4")
            .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

        migrationBuilder.CreateTable(
            name: "branches",
            columns: table => new
            {
                id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                code = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                region = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                branch_user_id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                workspace_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                change_version = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_branches", x => x.id);
                table.UniqueConstraint("AK_branches_workspace_id_id", x => new { x.workspace_id, x.id });
                table.ForeignKey(
                    name: "FK_branches_asp_net_users_branch_user_id",
                    column: x => x.branch_user_id,
                    principalTable: "asp_net_users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_branches_workspaces_workspace_id",
                    column: x => x.workspace_id,
                    principalTable: "workspaces",
                    principalColumn: "workspace_id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4")
            .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

        migrationBuilder.CreateTable(
            name: "templates",
            columns: table => new
            {
                id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                family_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                version = table.Column<int>(type: "int", nullable: false),
                state = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                workspace_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                change_version = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_templates", x => x.id);
                table.UniqueConstraint("AK_templates_workspace_id_id", x => new { x.workspace_id, x.id });
                table.ForeignKey(
                    name: "FK_templates_workspaces_workspace_id",
                    column: x => x.workspace_id,
                    principalTable: "workspaces",
                    principalColumn: "workspace_id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4")
            .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

        migrationBuilder.CreateTable(
            name: "periods",
            columns: table => new
            {
                id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                template_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                template_name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                template_version = table.Column<int>(type: "int", nullable: false),
                opens_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                submission_deadline_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                assessment_deadline_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                appeal_deadline_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                finalization_deadline_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                phase = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                workspace_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                change_version = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_periods", x => x.id);
                table.UniqueConstraint("AK_periods_workspace_id_id", x => new { x.workspace_id, x.id });
                table.ForeignKey(
                    name: "FK_periods_templates_workspace_id_template_id",
                    columns: x => new { x.workspace_id, x.template_id },
                    principalTable: "templates",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_periods_workspaces_workspace_id",
                    column: x => x.workspace_id,
                    principalTable: "workspaces",
                    principalColumn: "workspace_id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4")
            .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

        migrationBuilder.CreateTable(
            name: "rating_bands",
            columns: table => new
            {
                id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                template_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                label = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                minimum_inclusive = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                color = table.Column<string>(type: "varchar(12)", maxLength: 12, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                order = table.Column<int>(type: "int", nullable: false),
                workspace_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                change_version = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_rating_bands", x => x.id);
                table.UniqueConstraint("AK_rating_bands_workspace_id_id", x => new { x.workspace_id, x.id });
                table.ForeignKey(
                    name: "FK_rating_bands_templates_workspace_id_template_id",
                    columns: x => new { x.workspace_id, x.template_id },
                    principalTable: "templates",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_rating_bands_workspaces_workspace_id",
                    column: x => x.workspace_id,
                    principalTable: "workspaces",
                    principalColumn: "workspace_id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4")
            .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

        migrationBuilder.CreateTable(
            name: "template_categories",
            columns: table => new
            {
                id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                template_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                order = table.Column<int>(type: "int", nullable: false),
                workspace_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                change_version = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_template_categories", x => x.id);
                table.UniqueConstraint("AK_template_categories_workspace_id_id", x => new { x.workspace_id, x.id });
                table.ForeignKey(
                    name: "FK_template_categories_templates_workspace_id_template_id",
                    columns: x => new { x.workspace_id, x.template_id },
                    principalTable: "templates",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_template_categories_workspaces_workspace_id",
                    column: x => x.workspace_id,
                    principalTable: "workspaces",
                    principalColumn: "workspace_id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4")
            .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

        migrationBuilder.CreateTable(
            name: "assessments",
            columns: table => new
            {
                id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                period_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                branch_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                branch_name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                branch_code = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                region = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                branch_user_id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                assessor_id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                state = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                submitted_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                scored_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                finalized_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                provisional_score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                provisional_rating = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                final_score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                final_rating = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                workspace_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                change_version = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_assessments", x => x.id);
                table.UniqueConstraint("AK_assessments_workspace_id_id", x => new { x.workspace_id, x.id });
                table.ForeignKey(
                    name: "FK_assessments_asp_net_users_assessor_id",
                    column: x => x.assessor_id,
                    principalTable: "asp_net_users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_assessments_asp_net_users_branch_user_id",
                    column: x => x.branch_user_id,
                    principalTable: "asp_net_users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_assessments_branches_workspace_id_branch_id",
                    columns: x => new { x.workspace_id, x.branch_id },
                    principalTable: "branches",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_assessments_periods_workspace_id_period_id",
                    columns: x => new { x.workspace_id, x.period_id },
                    principalTable: "periods",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_assessments_workspaces_workspace_id",
                    column: x => x.workspace_id,
                    principalTable: "workspaces",
                    principalColumn: "workspace_id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4")
            .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

        migrationBuilder.CreateTable(
            name: "period_criteria",
            columns: table => new
            {
                id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                period_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                source_criterion_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                category = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                category_order = table.Column<int>(type: "int", nullable: false),
                code = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                title = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                guidance = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                weight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                evidence_required = table.Column<bool>(type: "tinyint(1)", nullable: false),
                order = table.Column<int>(type: "int", nullable: false),
                workspace_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                change_version = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_period_criteria", x => x.id);
                table.UniqueConstraint("AK_period_criteria_workspace_id_id", x => new { x.workspace_id, x.id });
                table.ForeignKey(
                    name: "FK_period_criteria_periods_workspace_id_period_id",
                    columns: x => new { x.workspace_id, x.period_id },
                    principalTable: "periods",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_period_criteria_workspaces_workspace_id",
                    column: x => x.workspace_id,
                    principalTable: "workspaces",
                    principalColumn: "workspace_id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4")
            .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

        migrationBuilder.CreateTable(
            name: "period_rating_bands",
            columns: table => new
            {
                id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                period_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                label = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                minimum_inclusive = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                color = table.Column<string>(type: "varchar(12)", maxLength: 12, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                order = table.Column<int>(type: "int", nullable: false),
                workspace_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                change_version = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_period_rating_bands", x => x.id);
                table.UniqueConstraint("AK_period_rating_bands_workspace_id_id", x => new { x.workspace_id, x.id });
                table.ForeignKey(
                    name: "FK_period_rating_bands_periods_workspace_id_period_id",
                    columns: x => new { x.workspace_id, x.period_id },
                    principalTable: "periods",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_period_rating_bands_workspaces_workspace_id",
                    column: x => x.workspace_id,
                    principalTable: "workspaces",
                    principalColumn: "workspace_id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4")
            .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

        migrationBuilder.CreateTable(
            name: "template_criteria",
            columns: table => new
            {
                id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                template_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                category_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                code = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                title = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                guidance = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                weight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                evidence_required = table.Column<bool>(type: "tinyint(1)", nullable: false),
                order = table.Column<int>(type: "int", nullable: false),
                workspace_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                change_version = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_template_criteria", x => x.id);
                table.UniqueConstraint("AK_template_criteria_workspace_id_id", x => new { x.workspace_id, x.id });
                table.ForeignKey(
                    name: "FK_template_criteria_template_categories_workspace_id_category_~",
                    columns: x => new { x.workspace_id, x.category_id },
                    principalTable: "template_categories",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_template_criteria_templates_workspace_id_template_id",
                    columns: x => new { x.workspace_id, x.template_id },
                    principalTable: "templates",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_template_criteria_workspaces_workspace_id",
                    column: x => x.workspace_id,
                    principalTable: "workspaces",
                    principalColumn: "workspace_id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4")
            .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

        migrationBuilder.CreateTable(
            name: "appeals",
            columns: table => new
            {
                id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                assessment_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                period_criterion_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                reason = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                clarification = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                original_score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                submitted_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                submitted_by = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                decision = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                decision_note = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                revised_score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                decided_by = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                decided_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                workspace_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                change_version = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_appeals", x => x.id);
                table.UniqueConstraint("AK_appeals_workspace_id_id", x => new { x.workspace_id, x.id });
                table.ForeignKey(
                    name: "FK_appeals_assessments_workspace_id_assessment_id",
                    columns: x => new { x.workspace_id, x.assessment_id },
                    principalTable: "assessments",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_appeals_period_criteria_workspace_id_period_criterion_id",
                    columns: x => new { x.workspace_id, x.period_criterion_id },
                    principalTable: "period_criteria",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_appeals_workspaces_workspace_id",
                    column: x => x.workspace_id,
                    principalTable: "workspaces",
                    principalColumn: "workspace_id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4")
            .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

        migrationBuilder.CreateTable(
            name: "responses",
            columns: table => new
            {
                id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                assessment_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                period_criterion_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                answer = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                comment = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                updated_by = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                completed_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                workspace_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                change_version = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_responses", x => x.id);
                table.UniqueConstraint("AK_responses_workspace_id_id", x => new { x.workspace_id, x.id });
                table.ForeignKey(
                    name: "FK_responses_assessments_workspace_id_assessment_id",
                    columns: x => new { x.workspace_id, x.assessment_id },
                    principalTable: "assessments",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_responses_period_criteria_workspace_id_period_criterion_id",
                    columns: x => new { x.workspace_id, x.period_criterion_id },
                    principalTable: "period_criteria",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_responses_workspaces_workspace_id",
                    column: x => x.workspace_id,
                    principalTable: "workspaces",
                    principalColumn: "workspace_id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4")
            .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

        migrationBuilder.CreateTable(
            name: "scores",
            columns: table => new
            {
                id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                assessment_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                period_criterion_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                value = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                note = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                actor_id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                revision = table.Column<int>(type: "int", nullable: false),
                appeal_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                workspace_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                change_version = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_scores", x => x.id);
                table.UniqueConstraint("AK_scores_workspace_id_id", x => new { x.workspace_id, x.id });
                table.ForeignKey(
                    name: "FK_scores_appeals_workspace_id_appeal_id",
                    columns: x => new { x.workspace_id, x.appeal_id },
                    principalTable: "appeals",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_scores_assessments_workspace_id_assessment_id",
                    columns: x => new { x.workspace_id, x.assessment_id },
                    principalTable: "assessments",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_scores_period_criteria_workspace_id_period_criterion_id",
                    columns: x => new { x.workspace_id, x.period_criterion_id },
                    principalTable: "period_criteria",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_scores_workspaces_workspace_id",
                    column: x => x.workspace_id,
                    principalTable: "workspaces",
                    principalColumn: "workspace_id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4")
            .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

        migrationBuilder.CreateTable(
            name: "evidence_files",
            columns: table => new
            {
                id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                assessment_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                response_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                appeal_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                original_name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                storage_name = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                media_type = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                length = table.Column<long>(type: "bigint", nullable: false),
                sha256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                uploaded_by = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false, collation: "utf8mb4_0900_ai_ci")
                    .Annotation("MySql:CharSet", "utf8mb4"),
                uploaded_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                workspace_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                change_version = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_evidence_files", x => x.id);
                table.UniqueConstraint("AK_evidence_files_workspace_id_id", x => new { x.workspace_id, x.id });
                table.ForeignKey(
                    name: "FK_evidence_files_appeals_workspace_id_appeal_id",
                    columns: x => new { x.workspace_id, x.appeal_id },
                    principalTable: "appeals",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_evidence_files_assessments_workspace_id_assessment_id",
                    columns: x => new { x.workspace_id, x.assessment_id },
                    principalTable: "assessments",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_evidence_files_responses_workspace_id_response_id",
                    columns: x => new { x.workspace_id, x.response_id },
                    principalTable: "responses",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_evidence_files_workspaces_workspace_id",
                    column: x => x.workspace_id,
                    principalTable: "workspaces",
                    principalColumn: "workspace_id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4")
            .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

        migrationBuilder.CreateIndex(
            name: "IX_appeals_workspace_id_assessment_id_period_criterion_id",
            table: "appeals",
            columns: new[] { "workspace_id", "assessment_id", "period_criterion_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_appeals_workspace_id_decision",
            table: "appeals",
            columns: new[] { "workspace_id", "decision" });

        migrationBuilder.CreateIndex(
            name: "IX_appeals_workspace_id_period_criterion_id",
            table: "appeals",
            columns: new[] { "workspace_id", "period_criterion_id" });

        migrationBuilder.CreateIndex(
            name: "IX_asp_net_role_claims_role_id",
            table: "asp_net_role_claims",
            column: "role_id");

        migrationBuilder.CreateIndex(
            name: "RoleNameIndex",
            table: "asp_net_roles",
            column: "normalized_name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_asp_net_user_claims_user_id",
            table: "asp_net_user_claims",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "IX_asp_net_user_logins_user_id",
            table: "asp_net_user_logins",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "IX_asp_net_user_roles_role_id",
            table: "asp_net_user_roles",
            column: "role_id");

        migrationBuilder.CreateIndex(
            name: "EmailIndex",
            table: "asp_net_users",
            column: "normalized_email");

        migrationBuilder.CreateIndex(
            name: "UserNameIndex",
            table: "asp_net_users",
            column: "normalized_user_name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_assessments_assessor_id",
            table: "assessments",
            column: "assessor_id");

        migrationBuilder.CreateIndex(
            name: "IX_assessments_branch_user_id",
            table: "assessments",
            column: "branch_user_id");

        migrationBuilder.CreateIndex(
            name: "IX_assessments_workspace_id_branch_id",
            table: "assessments",
            columns: new[] { "workspace_id", "branch_id" });

        migrationBuilder.CreateIndex(
            name: "IX_assessments_workspace_id_branch_user_id",
            table: "assessments",
            columns: new[] { "workspace_id", "branch_user_id" });

        migrationBuilder.CreateIndex(
            name: "IX_assessments_workspace_id_period_id_branch_id",
            table: "assessments",
            columns: new[] { "workspace_id", "period_id", "branch_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_assessments_workspace_id_period_id_final_score",
            table: "assessments",
            columns: new[] { "workspace_id", "period_id", "final_score" });

        migrationBuilder.CreateIndex(
            name: "IX_assessments_workspace_id_state_assessor_id",
            table: "assessments",
            columns: new[] { "workspace_id", "state", "assessor_id" });

        migrationBuilder.CreateIndex(
            name: "IX_audit_events_workspace_id_at_utc",
            table: "audit_events",
            columns: new[] { "workspace_id", "at_utc" });

        migrationBuilder.CreateIndex(
            name: "IX_branches_branch_user_id",
            table: "branches",
            column: "branch_user_id");

        migrationBuilder.CreateIndex(
            name: "IX_branches_workspace_id_code",
            table: "branches",
            columns: new[] { "workspace_id", "code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_branches_workspace_id_is_active",
            table: "branches",
            columns: new[] { "workspace_id", "is_active" });

        migrationBuilder.CreateIndex(
            name: "IX_evidence_files_workspace_id_appeal_id",
            table: "evidence_files",
            columns: new[] { "workspace_id", "appeal_id" });

        migrationBuilder.CreateIndex(
            name: "IX_evidence_files_workspace_id_assessment_id",
            table: "evidence_files",
            columns: new[] { "workspace_id", "assessment_id" });

        migrationBuilder.CreateIndex(
            name: "IX_evidence_files_workspace_id_response_id",
            table: "evidence_files",
            columns: new[] { "workspace_id", "response_id" });

        migrationBuilder.CreateIndex(
            name: "IX_evidence_files_workspace_id_storage_name",
            table: "evidence_files",
            columns: new[] { "workspace_id", "storage_name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_period_criteria_workspace_id_period_id_code",
            table: "period_criteria",
            columns: new[] { "workspace_id", "period_id", "code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_period_rating_bands_workspace_id_period_id_minimum_inclusive",
            table: "period_rating_bands",
            columns: new[] { "workspace_id", "period_id", "minimum_inclusive" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_periods_workspace_id_phase",
            table: "periods",
            columns: new[] { "workspace_id", "phase" });

        migrationBuilder.CreateIndex(
            name: "IX_periods_workspace_id_template_id",
            table: "periods",
            columns: new[] { "workspace_id", "template_id" });

        migrationBuilder.CreateIndex(
            name: "IX_rating_bands_workspace_id_template_id_minimum_inclusive",
            table: "rating_bands",
            columns: new[] { "workspace_id", "template_id", "minimum_inclusive" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_responses_workspace_id_assessment_id_period_criterion_id",
            table: "responses",
            columns: new[] { "workspace_id", "assessment_id", "period_criterion_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_responses_workspace_id_period_criterion_id",
            table: "responses",
            columns: new[] { "workspace_id", "period_criterion_id" });

        migrationBuilder.CreateIndex(
            name: "IX_scores_workspace_id_appeal_id",
            table: "scores",
            columns: new[] { "workspace_id", "appeal_id" });

        migrationBuilder.CreateIndex(
            name: "IX_scores_workspace_id_assessment_id_period_criterion_id_revisi~",
            table: "scores",
            columns: new[] { "workspace_id", "assessment_id", "period_criterion_id", "revision" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_scores_workspace_id_period_criterion_id",
            table: "scores",
            columns: new[] { "workspace_id", "period_criterion_id" });

        migrationBuilder.CreateIndex(
            name: "IX_template_categories_workspace_id_template_id_name",
            table: "template_categories",
            columns: new[] { "workspace_id", "template_id", "name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_template_criteria_workspace_id_category_id",
            table: "template_criteria",
            columns: new[] { "workspace_id", "category_id" });

        migrationBuilder.CreateIndex(
            name: "IX_template_criteria_workspace_id_template_id_code",
            table: "template_criteria",
            columns: new[] { "workspace_id", "template_id", "code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_templates_workspace_id_family_id_version",
            table: "templates",
            columns: new[] { "workspace_id", "family_id", "version" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_workspace_redirects_retired_at_utc",
            table: "workspace_redirects",
            column: "retired_at_utc");

        migrationBuilder.CreateIndex(
            name: "IX_workspaces_expires_at_utc",
            table: "workspaces",
            column: "expires_at_utc");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "asp_net_role_claims");

        migrationBuilder.DropTable(
            name: "asp_net_user_claims");

        migrationBuilder.DropTable(
            name: "asp_net_user_logins");

        migrationBuilder.DropTable(
            name: "asp_net_user_roles");

        migrationBuilder.DropTable(
            name: "asp_net_user_tokens");

        migrationBuilder.DropTable(
            name: "audit_events");

        migrationBuilder.DropTable(
            name: "evidence_files");

        migrationBuilder.DropTable(
            name: "period_rating_bands");

        migrationBuilder.DropTable(
            name: "rating_bands");

        migrationBuilder.DropTable(
            name: "scores");

        migrationBuilder.DropTable(
            name: "template_criteria");

        migrationBuilder.DropTable(
            name: "workspace_redirects");

        migrationBuilder.DropTable(
            name: "asp_net_roles");

        migrationBuilder.DropTable(
            name: "responses");

        migrationBuilder.DropTable(
            name: "appeals");

        migrationBuilder.DropTable(
            name: "template_categories");

        migrationBuilder.DropTable(
            name: "assessments");

        migrationBuilder.DropTable(
            name: "period_criteria");

        migrationBuilder.DropTable(
            name: "branches");

        migrationBuilder.DropTable(
            name: "periods");

        migrationBuilder.DropTable(
            name: "asp_net_users");

        migrationBuilder.DropTable(
            name: "templates");

        migrationBuilder.DropTable(
            name: "workspaces");
    }
}
