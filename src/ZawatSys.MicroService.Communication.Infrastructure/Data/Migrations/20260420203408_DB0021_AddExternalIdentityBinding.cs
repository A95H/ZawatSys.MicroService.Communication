using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZawatSys.MicroService.Communication.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DB0021_AddExternalIdentityBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExternalIdentityBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationChannelEndpointId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExternalUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedExternalUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ExternalDisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    BindingKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VerificationStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsBlocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    BlockReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LastResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUid = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedByUid = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalIdentityBindings", x => x.Id);
                    table.CheckConstraint("CK_ExternalIdentityBindings_BindingKind", "\"BindingKind\" IN ('Verified', 'Inferred', 'Guest', 'Lead')");
                    table.CheckConstraint("CK_ExternalIdentityBindings_BlockReason_When_Blocked", "NOT \"IsBlocked\" OR length(trim(coalesce(\"BlockReason\", ''))) > 0");
                    table.CheckConstraint("CK_ExternalIdentityBindings_VerificationStatus", "\"VerificationStatus\" IN ('Unverified', 'Verified', 'Suspended')");
                    table.ForeignKey(
                        name: "FK_ExternalIdentityBindings_ConversationChannelEndpoints_ConversationChannelEndpointId",
                        column: x => x.ConversationChannelEndpointId,
                        principalTable: "ConversationChannelEndpoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentityBindings_ConversationChannelEndpointId",
                table: "ExternalIdentityBindings",
                column: "ConversationChannelEndpointId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentityBindings_Tenant_AccountId",
                table: "ExternalIdentityBindings",
                columns: new[] { "TenantId", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentityBindings_Tenant_ContactId",
                table: "ExternalIdentityBindings",
                columns: new[] { "TenantId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentityBindings_Tenant_EntityType_EntityId",
                table: "ExternalIdentityBindings",
                columns: new[] { "TenantId", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentityBindings_Tenant_IsBlocked",
                table: "ExternalIdentityBindings",
                columns: new[] { "TenantId", "IsBlocked" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentityBindings_Tenant_IsDeleted",
                table: "ExternalIdentityBindings",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentityBindings_Tenant_SubjectId",
                table: "ExternalIdentityBindings",
                columns: new[] { "TenantId", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "UX_ExternalIdentityBindings_Tenant_Endpoint_NormalizedExternalUserId",
                table: "ExternalIdentityBindings",
                columns: new[] { "TenantId", "ConversationChannelEndpointId", "NormalizedExternalUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalIdentityBindings");
        }
    }
}
