using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZawatSys.MicroService.Communication.Infrastructure.Data.Migrations;

[DbContext(typeof(CommunicationDbContext))]
[Migration("20260420212000_DB0061_AddConversationMessage")]
public sealed class DB0061_AddConversationMessage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ConversationMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                ConversationChannelEndpointId = table.Column<Guid>(type: "uuid", nullable: false),
                Sequence = table.Column<long>(type: "bigint", nullable: false),
                Channel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                SenderType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                SenderUserId = table.Column<Guid>(type: "uuid", nullable: true),
                SenderDisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                MessageKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                ProviderMessageId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                ProviderCorrelationKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                ReplyToMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                TextNormalized = table.Column<string>(type: "text", nullable: true),
                TextRedacted = table.Column<string>(type: "text", nullable: true),
                MetadataJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                IsInternalOnly = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                RelatedPendingActionId = table.Column<Guid>(type: "uuid", nullable: true),
                RelatedAIRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                table.PrimaryKey("PK_ConversationMessages", x => x.Id);
                table.CheckConstraint("CK_ConversationMessages_Direction", "\"Direction\" IN ('INBOUND', 'OUTBOUND', 'INTERNAL')");
                table.CheckConstraint("CK_ConversationMessages_MessageKind", "\"MessageKind\" IN ('TEXT', 'MEDIA', 'BUTTON_REPLY', 'COMMAND', 'SYSTEM_NOTICE', 'SUGGESTION')");
                table.CheckConstraint("CK_ConversationMessages_SenderType", "\"SenderType\" IN ('USER', 'AI', 'HUMAN', 'SYSTEM')");
                table.CheckConstraint("CK_ConversationMessages_Sequence_NonNegative", "\"Sequence\" >= 0");
                table.ForeignKey(
                    name: "FK_ConversationMessages_ConversationChannelEndpoints_ConversationChannelEndpointId",
                    column: x => x.ConversationChannelEndpointId,
                    principalTable: "ConversationChannelEndpoints",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ConversationMessages_ConversationSessions_ConversationId",
                    column: x => x.ConversationId,
                    principalTable: "ConversationSessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ConversationMessages_ConversationChannelEndpointId",
            table: "ConversationMessages",
            column: "ConversationChannelEndpointId");

        migrationBuilder.CreateIndex(
            name: "IX_ConversationMessages_ConversationId_OccurredAt",
            table: "ConversationMessages",
            columns: new[] { "ConversationId", "OccurredAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ConversationMessages_Tenant_IsDeleted",
            table: "ConversationMessages",
            columns: new[] { "TenantId", "IsDeleted" });

        migrationBuilder.CreateIndex(
            name: "IX_ConversationMessages_Tenant_RelatedAIRequestId",
            table: "ConversationMessages",
            columns: new[] { "TenantId", "RelatedAIRequestId" });

        migrationBuilder.CreateIndex(
            name: "IX_ConversationMessages_Tenant_RelatedPendingActionId",
            table: "ConversationMessages",
            columns: new[] { "TenantId", "RelatedPendingActionId" });

        migrationBuilder.CreateIndex(
            name: "IX_ConversationMessages_Tenant_SenderType_OccurredAt",
            table: "ConversationMessages",
            columns: new[] { "TenantId", "SenderType", "OccurredAt" });

        migrationBuilder.CreateIndex(
            name: "UX_ConversationMessages_ConversationId_Sequence",
            table: "ConversationMessages",
            columns: new[] { "ConversationId", "Sequence" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UX_ConversationMessages_Tenant_Endpoint_ProviderMessageId_NotNull",
            table: "ConversationMessages",
            columns: new[] { "TenantId", "ConversationChannelEndpointId", "ProviderMessageId" },
            unique: true,
            filter: "\"ProviderMessageId\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ConversationMessages");
    }
}
