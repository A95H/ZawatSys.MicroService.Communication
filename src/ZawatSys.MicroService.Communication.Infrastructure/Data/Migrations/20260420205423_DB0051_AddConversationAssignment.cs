using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZawatSys.MicroService.Communication.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DB0051_AddConversationAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedQueueCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AssignmentRole = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReleaseReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("PK_ConversationAssignments", x => x.Id);
                    table.CheckConstraint("CK_ConversationAssignments_AssignmentRole", "\"AssignmentRole\" IN ('Owner', 'Observer')");
                    table.CheckConstraint("CK_ConversationAssignments_IsActive_ReleasedAt_Consistency", "\"ReleasedAt\" IS NULL OR NOT \"IsActive\"");
                    table.CheckConstraint("CK_ConversationAssignments_ReleaseReason_When_Released", "\"ReleasedAt\" IS NOT NULL OR length(trim(coalesce(\"ReleaseReason\", ''))) = 0");
                    table.ForeignKey(
                        name: "FK_ConversationAssignments_ConversationSessions_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "ConversationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationAssignments_ConversationId_AssignedAt",
                table: "ConversationAssignments",
                columns: new[] { "ConversationId", "AssignedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationAssignments_Tenant_AssignedQueueCode_IsActive",
                table: "ConversationAssignments",
                columns: new[] { "TenantId", "AssignedQueueCode", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationAssignments_Tenant_AssignedToUserId_IsActive",
                table: "ConversationAssignments",
                columns: new[] { "TenantId", "AssignedToUserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationAssignments_Tenant_IsDeleted",
                table: "ConversationAssignments",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UX_ConversationAssignments_ConversationId_AssignmentRole_ActiveOwner",
                table: "ConversationAssignments",
                columns: new[] { "ConversationId", "AssignmentRole" },
                unique: true,
                filter: "\"IsActive\" = TRUE AND \"AssignmentRole\" = 'Owner'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationAssignments");
        }
    }
}
