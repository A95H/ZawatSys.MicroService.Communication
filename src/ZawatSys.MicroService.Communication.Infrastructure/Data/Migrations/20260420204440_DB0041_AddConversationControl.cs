using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZawatSys.MicroService.Communication.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DB0041_AddConversationControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationControls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedQueueCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    HandoffReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PauseReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LastUserActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastHumanActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAIActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastTakenOverAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastResumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IntegrationVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
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
                    table.PrimaryKey("PK_ConversationControls", x => x.Id);
                    table.CheckConstraint("CK_ConversationControls_IntegrationVersion_Positive", "\"IntegrationVersion\" > 0");
                    table.CheckConstraint("CK_ConversationControls_Mode", "\"Mode\" IN ('AI_ACTIVE', 'HUMAN_ACTIVE', 'AI_PAUSED')");
                    table.ForeignKey(
                        name: "FK_ConversationControls_ConversationSessions_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "ConversationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationControls_Tenant_AssignedQueueCode_Mode",
                table: "ConversationControls",
                columns: new[] { "TenantId", "AssignedQueueCode", "Mode" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationControls_Tenant_AssignedToUserId_Mode",
                table: "ConversationControls",
                columns: new[] { "TenantId", "AssignedToUserId", "Mode" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationControls_Tenant_IsDeleted",
                table: "ConversationControls",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationControls_Tenant_Mode_ModifiedAt",
                table: "ConversationControls",
                columns: new[] { "TenantId", "Mode", "ModifiedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_ConversationControls_ConversationId",
                table: "ConversationControls",
                column: "ConversationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationControls");
        }
    }
}
