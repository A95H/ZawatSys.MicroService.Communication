using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZawatSys.MicroService.Communication.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DB0091_FinalizeCommunicationPersistenceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MessageDeliveryAttempts_Tenant_DeliveryStatus_NextRetryAt",
                table: "MessageDeliveryAttempts");

            migrationBuilder.DropIndex(
                name: "UX_ExternalIdentityBindings_Tenant_Endpoint_NormalizedExternalUserId",
                table: "ExternalIdentityBindings");

            migrationBuilder.DropIndex(
                name: "UX_ConversationSessions_Tenant_ExternalIdentityBinding_Active",
                table: "ConversationSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConversationControlTransitions_NewMode",
                table: "ConversationControlTransitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConversationControlTransitions_PreviousMode",
                table: "ConversationControlTransitions");

            migrationBuilder.DropIndex(
                name: "UX_ConversationControls_ConversationId",
                table: "ConversationControls");

            migrationBuilder.DropIndex(
                name: "IX_ConversationChannelEndpoints_Tenant_Channel_IsDefault",
                table: "ConversationChannelEndpoints");

            migrationBuilder.DropIndex(
                name: "UX_ConversationAssignments_ConversationId_AssignmentRole_ActiveOwner",
                table: "ConversationAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_MessageDeliveryAttempts_Tenant_NextRetryAt_PendingRetry",
                table: "MessageDeliveryAttempts",
                columns: new[] { "TenantId", "NextRetryAt" },
                filter: "NOT \"IsFinal\" AND \"NextRetryAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ExternalIdentityBindings_Tenant_Endpoint_NormalizedExternalUserId",
                table: "ExternalIdentityBindings",
                columns: new[] { "TenantId", "ConversationChannelEndpointId", "NormalizedExternalUserId" },
                unique: true,
                filter: "NOT \"IsBlocked\" AND NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "UX_ConversationSessions_Tenant_ExternalIdentityBinding_Active",
                table: "ConversationSessions",
                columns: new[] { "TenantId", "ExternalIdentityBindingId" },
                unique: true,
                filter: "\"ClosedAt\" IS NULL AND NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_ConversationId_Direction_Sequence",
                table: "ConversationMessages",
                columns: new[] { "ConversationId", "Direction", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "UX_ConversationControlTransitions_ConversationId_ControlVersion",
                table: "ConversationControlTransitions",
                columns: new[] { "ConversationId", "ControlVersion" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConversationControlTransitions_NewMode",
                table: "ConversationControlTransitions",
                sql: "\"NewMode\" IN ('AI_ACTIVE', 'HUMAN_ACTIVE', 'AI_PAUSED')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConversationControlTransitions_PreviousMode",
                table: "ConversationControlTransitions",
                sql: "\"PreviousMode\" IS NULL OR \"PreviousMode\" IN ('AI_ACTIVE', 'HUMAN_ACTIVE', 'AI_PAUSED')");

            migrationBuilder.CreateIndex(
                name: "UX_ConversationControls_ConversationId",
                table: "ConversationControls",
                column: "ConversationId",
                unique: true,
                filter: "NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "UX_ConversationChannelEndpoints_Tenant_Channel_Default",
                table: "ConversationChannelEndpoints",
                columns: new[] { "TenantId", "Channel" },
                unique: true,
                filter: "\"IsDefault\" = TRUE AND NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "UX_ConversationAssignments_ConversationId_AssignmentRole_ActiveOwner",
                table: "ConversationAssignments",
                columns: new[] { "ConversationId", "AssignmentRole" },
                unique: true,
                filter: "\"IsActive\" = TRUE AND \"AssignmentRole\" = 'Owner' AND NOT \"IsDeleted\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MessageDeliveryAttempts_Tenant_NextRetryAt_PendingRetry",
                table: "MessageDeliveryAttempts");

            migrationBuilder.DropIndex(
                name: "UX_ExternalIdentityBindings_Tenant_Endpoint_NormalizedExternalUserId",
                table: "ExternalIdentityBindings");

            migrationBuilder.DropIndex(
                name: "UX_ConversationSessions_Tenant_ExternalIdentityBinding_Active",
                table: "ConversationSessions");

            migrationBuilder.DropIndex(
                name: "IX_ConversationMessages_ConversationId_Direction_Sequence",
                table: "ConversationMessages");

            migrationBuilder.DropIndex(
                name: "UX_ConversationControlTransitions_ConversationId_ControlVersion",
                table: "ConversationControlTransitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConversationControlTransitions_NewMode",
                table: "ConversationControlTransitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConversationControlTransitions_PreviousMode",
                table: "ConversationControlTransitions");

            migrationBuilder.DropIndex(
                name: "UX_ConversationControls_ConversationId",
                table: "ConversationControls");

            migrationBuilder.DropIndex(
                name: "UX_ConversationChannelEndpoints_Tenant_Channel_Default",
                table: "ConversationChannelEndpoints");

            migrationBuilder.DropIndex(
                name: "UX_ConversationAssignments_ConversationId_AssignmentRole_ActiveOwner",
                table: "ConversationAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_MessageDeliveryAttempts_Tenant_DeliveryStatus_NextRetryAt",
                table: "MessageDeliveryAttempts",
                columns: new[] { "TenantId", "DeliveryStatus", "NextRetryAt" });

            migrationBuilder.CreateIndex(
                name: "UX_ExternalIdentityBindings_Tenant_Endpoint_NormalizedExternalUserId",
                table: "ExternalIdentityBindings",
                columns: new[] { "TenantId", "ConversationChannelEndpointId", "NormalizedExternalUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ConversationSessions_Tenant_ExternalIdentityBinding_Active",
                table: "ConversationSessions",
                columns: new[] { "TenantId", "ExternalIdentityBindingId" },
                unique: true,
                filter: "\"ClosedAt\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConversationControlTransitions_NewMode",
                table: "ConversationControlTransitions",
                sql: "\"NewMode\" IN ('AI_ACTIVE', 'HUMAN_ACTIVE', 'AI_PAUSED', 'RESOLVED')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConversationControlTransitions_PreviousMode",
                table: "ConversationControlTransitions",
                sql: "\"PreviousMode\" IS NULL OR \"PreviousMode\" IN ('AI_ACTIVE', 'HUMAN_ACTIVE', 'AI_PAUSED', 'RESOLVED')");

            migrationBuilder.CreateIndex(
                name: "UX_ConversationControls_ConversationId",
                table: "ConversationControls",
                column: "ConversationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationChannelEndpoints_Tenant_Channel_IsDefault",
                table: "ConversationChannelEndpoints",
                columns: new[] { "TenantId", "Channel", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "UX_ConversationAssignments_ConversationId_AssignmentRole_ActiveOwner",
                table: "ConversationAssignments",
                columns: new[] { "ConversationId", "AssignmentRole" },
                unique: true,
                filter: "\"IsActive\" = TRUE AND \"AssignmentRole\" = 'Owner'");
        }
    }
}
