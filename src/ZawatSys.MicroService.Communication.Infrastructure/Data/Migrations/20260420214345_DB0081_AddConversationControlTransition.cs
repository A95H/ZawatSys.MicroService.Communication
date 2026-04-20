using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZawatSys.MicroService.Communication.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DB0081_AddConversationControlTransition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationControlTransitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    NewMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TransitionReason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TriggeredByType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TriggeredByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedAIRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    NoteRedacted = table.Column<string>(type: "text", nullable: true),
                    ControlVersion = table.Column<long>(type: "bigint", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_ConversationControlTransitions", x => x.Id);
                    table.CheckConstraint("CK_ConversationControlTransitions_ControlVersion_Positive", "\"ControlVersion\" > 0");
                    table.CheckConstraint("CK_ConversationControlTransitions_NewMode", "\"NewMode\" IN ('AI_ACTIVE', 'HUMAN_ACTIVE', 'AI_PAUSED', 'RESOLVED')");
                    table.CheckConstraint("CK_ConversationControlTransitions_PreviousMode", "\"PreviousMode\" IS NULL OR \"PreviousMode\" IN ('AI_ACTIVE', 'HUMAN_ACTIVE', 'AI_PAUSED', 'RESOLVED')");
                    table.CheckConstraint("CK_ConversationControlTransitions_TriggeredByType", "\"TriggeredByType\" IN ('USER', 'HUMAN', 'AI', 'SYSTEM', 'POLICY')");
                    table.ForeignKey(
                        name: "FK_ConversationControlTransitions_ConversationSessions_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "ConversationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationControlTransitions_ConversationId_OccurredAt",
                table: "ConversationControlTransitions",
                columns: new[] { "ConversationId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationControlTransitions_Tenant_IsDeleted",
                table: "ConversationControlTransitions",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationControlTransitions_Tenant_NewMode_OccurredAt",
                table: "ConversationControlTransitions",
                columns: new[] { "TenantId", "NewMode", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationControlTransitions_Tenant_TriggeredByUserId_OccurredAt",
                table: "ConversationControlTransitions",
                columns: new[] { "TenantId", "TriggeredByUserId", "OccurredAt" });

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationControlTransitions");
        }
    }
}
