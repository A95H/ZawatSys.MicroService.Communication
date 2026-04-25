START TRANSACTION;
DROP INDEX "IX_MessageDeliveryAttempts_Tenant_NextRetryAt_PendingRetry";

DROP INDEX "UX_ExternalIdentityBindings_Tenant_Endpoint_NormalizedExternalUserId";

DROP INDEX "UX_ConversationSessions_Tenant_ExternalIdentityBinding_Active";

DROP INDEX "IX_ConversationMessages_ConversationId_Direction_Sequence";

DROP INDEX "UX_ConversationControlTransitions_ConversationId_ControlVersion";

ALTER TABLE "ConversationControlTransitions" DROP CONSTRAINT "CK_ConversationControlTransitions_NewMode";

ALTER TABLE "ConversationControlTransitions" DROP CONSTRAINT "CK_ConversationControlTransitions_PreviousMode";

DROP INDEX "UX_ConversationControls_ConversationId";

DROP INDEX "UX_ConversationChannelEndpoints_Tenant_Channel_Default";

DROP INDEX "UX_ConversationAssignments_ConversationId_AssignmentRole_ActiveOwner";

CREATE INDEX "IX_MessageDeliveryAttempts_Tenant_DeliveryStatus_NextRetryAt" ON "MessageDeliveryAttempts" ("TenantId", "DeliveryStatus", "NextRetryAt");

CREATE UNIQUE INDEX "UX_ExternalIdentityBindings_Tenant_Endpoint_NormalizedExternalUserId" ON "ExternalIdentityBindings" ("TenantId", "ConversationChannelEndpointId", "NormalizedExternalUserId");

CREATE UNIQUE INDEX "UX_ConversationSessions_Tenant_ExternalIdentityBinding_Active" ON "ConversationSessions" ("TenantId", "ExternalIdentityBindingId") WHERE "ClosedAt" IS NULL;

ALTER TABLE "ConversationControlTransitions" ADD CONSTRAINT "CK_ConversationControlTransitions_NewMode" CHECK ("NewMode" IN ('AI_ACTIVE', 'HUMAN_ACTIVE', 'AI_PAUSED', 'RESOLVED'));

ALTER TABLE "ConversationControlTransitions" ADD CONSTRAINT "CK_ConversationControlTransitions_PreviousMode" CHECK ("PreviousMode" IS NULL OR "PreviousMode" IN ('AI_ACTIVE', 'HUMAN_ACTIVE', 'AI_PAUSED', 'RESOLVED'));

CREATE UNIQUE INDEX "UX_ConversationControls_ConversationId" ON "ConversationControls" ("ConversationId");

CREATE INDEX "IX_ConversationChannelEndpoints_Tenant_Channel_IsDefault" ON "ConversationChannelEndpoints" ("TenantId", "Channel", "IsDefault");

CREATE UNIQUE INDEX "UX_ConversationAssignments_ConversationId_AssignmentRole_ActiveOwner" ON "ConversationAssignments" ("ConversationId", "AssignmentRole") WHERE "IsActive" = TRUE AND "AssignmentRole" = 'Owner';

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20260420235427_DB0091_FinalizeCommunicationPersistenceIndexes';

COMMIT;

