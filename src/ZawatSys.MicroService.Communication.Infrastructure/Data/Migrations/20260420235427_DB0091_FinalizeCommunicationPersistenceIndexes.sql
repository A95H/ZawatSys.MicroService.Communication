START TRANSACTION;
DROP INDEX "IX_MessageDeliveryAttempts_Tenant_DeliveryStatus_NextRetryAt";

DROP INDEX "UX_ExternalIdentityBindings_Tenant_Endpoint_NormalizedExternalUserId";

DROP INDEX "UX_ConversationSessions_Tenant_ExternalIdentityBinding_Active";

ALTER TABLE "ConversationControlTransitions" DROP CONSTRAINT "CK_ConversationControlTransitions_NewMode";

ALTER TABLE "ConversationControlTransitions" DROP CONSTRAINT "CK_ConversationControlTransitions_PreviousMode";

DROP INDEX "UX_ConversationControls_ConversationId";

DROP INDEX "IX_ConversationChannelEndpoints_Tenant_Channel_IsDefault";

DROP INDEX "UX_ConversationAssignments_ConversationId_AssignmentRole_ActiveOwner";

CREATE INDEX "IX_MessageDeliveryAttempts_Tenant_NextRetryAt_PendingRetry" ON "MessageDeliveryAttempts" ("TenantId", "NextRetryAt") WHERE NOT "IsFinal" AND "NextRetryAt" IS NOT NULL;

CREATE UNIQUE INDEX "UX_ExternalIdentityBindings_Tenant_Endpoint_NormalizedExternalUserId" ON "ExternalIdentityBindings" ("TenantId", "ConversationChannelEndpointId", "NormalizedExternalUserId") WHERE NOT "IsBlocked" AND NOT "IsDeleted";

CREATE UNIQUE INDEX "UX_ConversationSessions_Tenant_ExternalIdentityBinding_Active" ON "ConversationSessions" ("TenantId", "ExternalIdentityBindingId") WHERE "ClosedAt" IS NULL AND NOT "IsDeleted";

CREATE INDEX "IX_ConversationMessages_ConversationId_Direction_Sequence" ON "ConversationMessages" ("ConversationId", "Direction", "Sequence");

CREATE UNIQUE INDEX "UX_ConversationControlTransitions_ConversationId_ControlVersion" ON "ConversationControlTransitions" ("ConversationId", "ControlVersion");

ALTER TABLE "ConversationControlTransitions" ADD CONSTRAINT "CK_ConversationControlTransitions_NewMode" CHECK ("NewMode" IN ('AI_ACTIVE', 'HUMAN_ACTIVE', 'AI_PAUSED'));

ALTER TABLE "ConversationControlTransitions" ADD CONSTRAINT "CK_ConversationControlTransitions_PreviousMode" CHECK ("PreviousMode" IS NULL OR "PreviousMode" IN ('AI_ACTIVE', 'HUMAN_ACTIVE', 'AI_PAUSED'));

CREATE UNIQUE INDEX "UX_ConversationControls_ConversationId" ON "ConversationControls" ("ConversationId") WHERE NOT "IsDeleted";

CREATE UNIQUE INDEX "UX_ConversationChannelEndpoints_Tenant_Channel_Default" ON "ConversationChannelEndpoints" ("TenantId", "Channel") WHERE "IsDefault" = TRUE AND NOT "IsDeleted";

CREATE UNIQUE INDEX "UX_ConversationAssignments_ConversationId_AssignmentRole_ActiveOwner" ON "ConversationAssignments" ("ConversationId", "AssignmentRole") WHERE "IsActive" = TRUE AND "AssignmentRole" = 'Owner' AND NOT "IsDeleted";

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260420235427_DB0091_FinalizeCommunicationPersistenceIndexes', '10.0.1');

COMMIT;

