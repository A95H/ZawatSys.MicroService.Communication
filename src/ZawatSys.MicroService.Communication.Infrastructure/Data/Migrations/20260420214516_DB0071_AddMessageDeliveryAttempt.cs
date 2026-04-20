using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZawatSys.MicroService.Communication.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DB0071_AddMessageDeliveryAttempt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MessageDeliveryAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationChannelEndpointId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    ProviderMessageId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DeliveryStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    HttpStatusCode = table.Column<int>(type: "integer", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ErrorMessageRedacted = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextRetryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinalizedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsFinal = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
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
                    table.PrimaryKey("PK_MessageDeliveryAttempts", x => x.Id);
                    table.CheckConstraint("CK_MessageDeliveryAttempts_AttemptNumber_Positive", "\"AttemptNumber\" >= 1");
                    table.CheckConstraint("CK_MessageDeliveryAttempts_DeliveryStatus", "\"DeliveryStatus\" IN ('QUEUED', 'ACCEPTED', 'SENT', 'DELIVERED', 'READ', 'FAILED')");
                    table.CheckConstraint("CK_MessageDeliveryAttempts_IsFinal_FinalizedAt_Consistency", "(\"IsFinal\" AND \"FinalizedAt\" IS NOT NULL) OR (NOT \"IsFinal\" AND \"FinalizedAt\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_MessageDeliveryAttempts_ConversationChannelEndpoints_ConversationChannelEndpointId",
                        column: x => x.ConversationChannelEndpointId,
                        principalTable: "ConversationChannelEndpoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessageDeliveryAttempts_ConversationMessages_ConversationMessageId",
                        column: x => x.ConversationMessageId,
                        principalTable: "ConversationMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessageDeliveryAttempts_ConversationChannelEndpointId",
                table: "MessageDeliveryAttempts",
                column: "ConversationChannelEndpointId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageDeliveryAttempts_ConversationMessageId_AttemptedAt",
                table: "MessageDeliveryAttempts",
                columns: new[] { "ConversationMessageId", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageDeliveryAttempts_Tenant_DeliveryStatus_NextRetryAt",
                table: "MessageDeliveryAttempts",
                columns: new[] { "TenantId", "DeliveryStatus", "NextRetryAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageDeliveryAttempts_Tenant_IsDeleted",
                table: "MessageDeliveryAttempts",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UX_MessageDeliveryAttempts_ConversationMessageId_AttemptNumber",
                table: "MessageDeliveryAttempts",
                columns: new[] { "ConversationMessageId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_MessageDeliveryAttempts_Tenant_Endpoint_ProviderMessageId_NotNull",
                table: "MessageDeliveryAttempts",
                columns: new[] { "TenantId", "ConversationChannelEndpointId", "ProviderMessageId" },
                unique: true,
                filter: "\"ProviderMessageId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessageDeliveryAttempts");
        }
    }
}
