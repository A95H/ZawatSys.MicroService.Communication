using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZawatSys.MicroService.Communication.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DB0011_AddConversationChannelEndpoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationChannelEndpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EndpointKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    WebhookSecretRef = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    AccessTokenSecretRef = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    VerificationSecretRef = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    InboundEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    OutboundEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
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
                    table.PrimaryKey("PK_ConversationChannelEndpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Content = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    OccurredOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Sent = table.Column<bool>(type: "boolean", nullable: false),
                    SentOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUid = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedByUid = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationChannelEndpoints_Tenant_Channel_InboundEnabled",
                table: "ConversationChannelEndpoints",
                columns: new[] { "TenantId", "Channel", "InboundEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationChannelEndpoints_Tenant_Channel_IsDefault",
                table: "ConversationChannelEndpoints",
                columns: new[] { "TenantId", "Channel", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationChannelEndpoints_Tenant_IsDeleted",
                table: "ConversationChannelEndpoints",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UX_ConversationChannelEndpoints_Tenant_Channel_EndpointKey",
                table: "ConversationChannelEndpoints",
                columns: new[] { "TenantId", "Channel", "EndpointKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Sent_OccurredOn",
                table: "OutboxMessages",
                columns: new[] { "Sent", "OccurredOn" });

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "ConversationChannelEndpoints");
        }
    }
}
