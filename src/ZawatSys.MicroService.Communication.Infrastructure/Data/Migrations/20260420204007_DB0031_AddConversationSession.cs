using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZawatSys.MicroService.Communication.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DB0031_AddConversationSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationChannelEndpointId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalIdentityBindingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SessionStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    CurrentScenarioKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CurrentScenarioInstanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentScenarioStateCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ScenarioSnapshotJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    CurrentPendingActionId = table.Column<Guid>(type: "uuid", nullable: true),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastInboundMessageAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastOutboundMessageAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastUserMessageAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastHumanMessageAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAIMessageAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastMessageSequence = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolutionCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_ConversationSessions", x => x.Id);
                    table.CheckConstraint("CK_ConversationSessions_IntegrationVersion_Positive", "\"IntegrationVersion\" > 0");
                    table.CheckConstraint("CK_ConversationSessions_LastMessageSequence_NonNegative", "\"LastMessageSequence\" >= 0");
                    table.CheckConstraint("CK_ConversationSessions_SessionStatus", "\"SessionStatus\" IN ('OPEN', 'RESOLVED', 'CLOSED', 'ARCHIVED')");
                    table.ForeignKey(
                        name: "FK_ConversationSessions_ConversationChannelEndpoints_ConversationChannelEndpointId",
                        column: x => x.ConversationChannelEndpointId,
                        principalTable: "ConversationChannelEndpoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationSessions_ExternalIdentityBindings_ExternalIdentityBindingId",
                        column: x => x.ExternalIdentityBindingId,
                        principalTable: "ExternalIdentityBindings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSessions_ConversationChannelEndpointId",
                table: "ConversationSessions",
                column: "ConversationChannelEndpointId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSessions_ExternalIdentityBindingId_OpenedAt",
                table: "ConversationSessions",
                columns: new[] { "ExternalIdentityBindingId", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSessions_Tenant_CurrentPendingActionId",
                table: "ConversationSessions",
                columns: new[] { "TenantId", "CurrentPendingActionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSessions_Tenant_IsDeleted",
                table: "ConversationSessions",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSessions_Tenant_SessionStatus_LastInboundMessageAt",
                table: "ConversationSessions",
                columns: new[] { "TenantId", "SessionStatus", "LastInboundMessageAt" });

            migrationBuilder.CreateIndex(
                name: "UX_ConversationSessions_Tenant_ExternalIdentityBinding_Active",
                table: "ConversationSessions",
                columns: new[] { "TenantId", "ExternalIdentityBindingId" },
                unique: true,
                filter: "\"ClosedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationSessions");
        }
    }
}
