CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE "ConversationChannelEndpoints" (
    "Id" uuid NOT NULL,
    "Channel" character varying(64) NOT NULL,
    "Provider" character varying(128) NOT NULL,
    "EndpointKey" character varying(256) NOT NULL,
    "DisplayName" character varying(256) NOT NULL,
    "WebhookSecretRef" character varying(512),
    "AccessTokenSecretRef" character varying(512),
    "VerificationSecretRef" character varying(512),
    "InboundEnabled" boolean NOT NULL DEFAULT TRUE,
    "OutboundEnabled" boolean NOT NULL DEFAULT TRUE,
    "IsDefault" boolean NOT NULL DEFAULT FALSE,
    "MetadataJson" jsonb NOT NULL DEFAULT '{}',
    "CreatedAt" timestamp with time zone NOT NULL,
    "ModifiedAt" timestamp with time zone NOT NULL,
    "CreatedByUid" uuid NOT NULL,
    "ModifiedByUid" uuid NOT NULL,
    "CorrelationId" uuid NOT NULL,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_ConversationChannelEndpoints" PRIMARY KEY ("Id")
);

CREATE TABLE "OutboxMessages" (
    "Id" uuid NOT NULL,
    "Type" character varying(500) NOT NULL,
    "Content" jsonb NOT NULL,
    "OccurredOn" timestamp with time zone NOT NULL,
    "Sent" boolean NOT NULL,
    "SentOn" timestamp with time zone,
    "RetryCount" integer NOT NULL,
    "Error" character varying(2000),
    "CreatedAt" timestamp with time zone NOT NULL,
    "ModifiedAt" timestamp with time zone NOT NULL,
    "CreatedByUid" uuid NOT NULL,
    "ModifiedByUid" uuid NOT NULL,
    "CorrelationId" uuid NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_OutboxMessages" PRIMARY KEY ("Id")
);

CREATE INDEX "IX_ConversationChannelEndpoints_Tenant_Channel_InboundEnabled" ON "ConversationChannelEndpoints" ("TenantId", "Channel", "InboundEnabled");

CREATE INDEX "IX_ConversationChannelEndpoints_Tenant_Channel_IsDefault" ON "ConversationChannelEndpoints" ("TenantId", "Channel", "IsDefault");

CREATE INDEX "IX_ConversationChannelEndpoints_Tenant_IsDeleted" ON "ConversationChannelEndpoints" ("TenantId", "IsDeleted");

CREATE UNIQUE INDEX "UX_ConversationChannelEndpoints_Tenant_Channel_EndpointKey" ON "ConversationChannelEndpoints" ("TenantId", "Channel", "EndpointKey");

CREATE INDEX "IX_OutboxMessages_Sent_OccurredOn" ON "OutboxMessages" ("Sent", "OccurredOn");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260420202500_DB0011_AddConversationChannelEndpoint', '10.0.1');

COMMIT;

START TRANSACTION;
CREATE TABLE "ExternalIdentityBindings" (
    "Id" uuid NOT NULL,
    "ConversationChannelEndpointId" uuid NOT NULL,
    "Channel" character varying(64) NOT NULL,
    "ExternalUserId" character varying(256) NOT NULL,
    "NormalizedExternalUserId" character varying(256) NOT NULL,
    "ExternalDisplayName" character varying(256),
    "ContactId" uuid,
    "SubjectId" uuid,
    "AccountId" uuid,
    "EntityType" character varying(128),
    "EntityId" uuid,
    "BindingKind" character varying(32) NOT NULL,
    "VerificationStatus" character varying(32) NOT NULL,
    "IsBlocked" boolean NOT NULL DEFAULT FALSE,
    "BlockReason" character varying(512),
    "LastResolvedAt" timestamp with time zone,
    "MetadataJson" jsonb NOT NULL DEFAULT '{}',
    "CreatedAt" timestamp with time zone NOT NULL,
    "ModifiedAt" timestamp with time zone NOT NULL,
    "CreatedByUid" uuid NOT NULL,
    "ModifiedByUid" uuid NOT NULL,
    "CorrelationId" uuid NOT NULL,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_ExternalIdentityBindings" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_ExternalIdentityBindings_BindingKind" CHECK ("BindingKind" IN ('Verified', 'Inferred', 'Guest', 'Lead')),
    CONSTRAINT "CK_ExternalIdentityBindings_BlockReason_When_Blocked" CHECK (NOT "IsBlocked" OR length(trim(coalesce("BlockReason", ''))) > 0),
    CONSTRAINT "CK_ExternalIdentityBindings_VerificationStatus" CHECK ("VerificationStatus" IN ('Unverified', 'Verified', 'Suspended')),
    CONSTRAINT "FK_ExternalIdentityBindings_ConversationChannelEndpoints_ConversationChannelEndpointId" FOREIGN KEY ("ConversationChannelEndpointId") REFERENCES "ConversationChannelEndpoints" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_ExternalIdentityBindings_ConversationChannelEndpointId" ON "ExternalIdentityBindings" ("ConversationChannelEndpointId");

CREATE INDEX "IX_ExternalIdentityBindings_Tenant_AccountId" ON "ExternalIdentityBindings" ("TenantId", "AccountId");

CREATE INDEX "IX_ExternalIdentityBindings_Tenant_ContactId" ON "ExternalIdentityBindings" ("TenantId", "ContactId");

CREATE INDEX "IX_ExternalIdentityBindings_Tenant_EntityType_EntityId" ON "ExternalIdentityBindings" ("TenantId", "EntityType", "EntityId");

CREATE INDEX "IX_ExternalIdentityBindings_Tenant_IsBlocked" ON "ExternalIdentityBindings" ("TenantId", "IsBlocked");

CREATE INDEX "IX_ExternalIdentityBindings_Tenant_IsDeleted" ON "ExternalIdentityBindings" ("TenantId", "IsDeleted");

CREATE INDEX "IX_ExternalIdentityBindings_Tenant_SubjectId" ON "ExternalIdentityBindings" ("TenantId", "SubjectId");

CREATE UNIQUE INDEX "UX_ExternalIdentityBindings_Tenant_Endpoint_NormalizedExternalUserId" ON "ExternalIdentityBindings" ("TenantId", "ConversationChannelEndpointId", "NormalizedExternalUserId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260420203408_DB0021_AddExternalIdentityBinding', '10.0.1');

COMMIT;

START TRANSACTION;
CREATE TABLE "ConversationSessions" (
    "Id" uuid NOT NULL,
    "ConversationChannelEndpointId" uuid NOT NULL,
    "ExternalIdentityBindingId" uuid NOT NULL,
    "Channel" character varying(64) NOT NULL,
    "SessionStatus" character varying(32) NOT NULL,
    "Locale" character varying(16),
    "CurrentScenarioKey" character varying(256),
    "CurrentScenarioInstanceId" uuid,
    "CurrentScenarioStateCode" character varying(64),
    "ScenarioSnapshotJson" jsonb NOT NULL DEFAULT '{}',
    "CurrentPendingActionId" uuid,
    "OpenedAt" timestamp with time zone NOT NULL,
    "LastInboundMessageAt" timestamp with time zone,
    "LastOutboundMessageAt" timestamp with time zone,
    "LastUserMessageAt" timestamp with time zone,
    "LastHumanMessageAt" timestamp with time zone,
    "LastAIMessageAt" timestamp with time zone,
    "LastMessageSequence" bigint NOT NULL DEFAULT 0,
    "ResolvedAt" timestamp with time zone,
    "ClosedAt" timestamp with time zone,
    "ResolutionCode" character varying(64),
    "IntegrationVersion" bigint NOT NULL DEFAULT 1,
    "CreatedAt" timestamp with time zone NOT NULL,
    "ModifiedAt" timestamp with time zone NOT NULL,
    "CreatedByUid" uuid NOT NULL,
    "ModifiedByUid" uuid NOT NULL,
    "CorrelationId" uuid NOT NULL,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_ConversationSessions" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_ConversationSessions_IntegrationVersion_Positive" CHECK ("IntegrationVersion" > 0),
    CONSTRAINT "CK_ConversationSessions_LastMessageSequence_NonNegative" CHECK ("LastMessageSequence" >= 0),
    CONSTRAINT "CK_ConversationSessions_SessionStatus" CHECK ("SessionStatus" IN ('OPEN', 'RESOLVED', 'CLOSED', 'ARCHIVED')),
    CONSTRAINT "FK_ConversationSessions_ConversationChannelEndpoints_ConversationChannelEndpointId" FOREIGN KEY ("ConversationChannelEndpointId") REFERENCES "ConversationChannelEndpoints" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ConversationSessions_ExternalIdentityBindings_ExternalIdentityBindingId" FOREIGN KEY ("ExternalIdentityBindingId") REFERENCES "ExternalIdentityBindings" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_ConversationSessions_ConversationChannelEndpointId" ON "ConversationSessions" ("ConversationChannelEndpointId");

CREATE INDEX "IX_ConversationSessions_ExternalIdentityBindingId_OpenedAt" ON "ConversationSessions" ("ExternalIdentityBindingId", "OpenedAt");

CREATE INDEX "IX_ConversationSessions_Tenant_CurrentPendingActionId" ON "ConversationSessions" ("TenantId", "CurrentPendingActionId");

CREATE INDEX "IX_ConversationSessions_Tenant_IsDeleted" ON "ConversationSessions" ("TenantId", "IsDeleted");

CREATE INDEX "IX_ConversationSessions_Tenant_SessionStatus_LastInboundMessageAt" ON "ConversationSessions" ("TenantId", "SessionStatus", "LastInboundMessageAt");

CREATE UNIQUE INDEX "UX_ConversationSessions_Tenant_ExternalIdentityBinding_Active" ON "ConversationSessions" ("TenantId", "ExternalIdentityBindingId") WHERE "ClosedAt" IS NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260420204007_DB0031_AddConversationSession', '10.0.1');

COMMIT;

START TRANSACTION;
CREATE TABLE "ConversationControls" (
    "Id" uuid NOT NULL,
    "ConversationId" uuid NOT NULL,
    "Mode" character varying(32) NOT NULL,
    "AssignedToUserId" uuid,
    "AssignedQueueCode" character varying(64),
    "HandoffReason" character varying(512),
    "PauseReason" character varying(512),
    "LastUserActivityAt" timestamp with time zone,
    "LastHumanActivityAt" timestamp with time zone,
    "LastAIActivityAt" timestamp with time zone,
    "LastTakenOverAt" timestamp with time zone,
    "LastResumedAt" timestamp with time zone,
    "IntegrationVersion" bigint NOT NULL DEFAULT 1,
    "CreatedAt" timestamp with time zone NOT NULL,
    "ModifiedAt" timestamp with time zone NOT NULL,
    "CreatedByUid" uuid NOT NULL,
    "ModifiedByUid" uuid NOT NULL,
    "CorrelationId" uuid NOT NULL,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_ConversationControls" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_ConversationControls_IntegrationVersion_Positive" CHECK ("IntegrationVersion" > 0),
    CONSTRAINT "CK_ConversationControls_Mode" CHECK ("Mode" IN ('AI_ACTIVE', 'HUMAN_ACTIVE', 'AI_PAUSED')),
    CONSTRAINT "FK_ConversationControls_ConversationSessions_ConversationId" FOREIGN KEY ("ConversationId") REFERENCES "ConversationSessions" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_ConversationControls_Tenant_AssignedQueueCode_Mode" ON "ConversationControls" ("TenantId", "AssignedQueueCode", "Mode");

CREATE INDEX "IX_ConversationControls_Tenant_AssignedToUserId_Mode" ON "ConversationControls" ("TenantId", "AssignedToUserId", "Mode");

CREATE INDEX "IX_ConversationControls_Tenant_IsDeleted" ON "ConversationControls" ("TenantId", "IsDeleted");

CREATE INDEX "IX_ConversationControls_Tenant_Mode_ModifiedAt" ON "ConversationControls" ("TenantId", "Mode", "ModifiedAt");

CREATE UNIQUE INDEX "UX_ConversationControls_ConversationId" ON "ConversationControls" ("ConversationId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260420204440_DB0041_AddConversationControl', '10.0.1');

COMMIT;

START TRANSACTION;
CREATE TABLE "ConversationAssignments" (
    "Id" uuid NOT NULL,
    "ConversationId" uuid NOT NULL,
    "AssignedToUserId" uuid,
    "AssignedQueueCode" character varying(64),
    "AssignmentRole" character varying(32) NOT NULL,
    "AssignedByUserId" uuid NOT NULL,
    "AssignedAt" timestamp with time zone NOT NULL,
    "ReleasedAt" timestamp with time zone,
    "ReleaseReason" character varying(512),
    "IsActive" boolean NOT NULL DEFAULT TRUE,
    "CreatedAt" timestamp with time zone NOT NULL,
    "ModifiedAt" timestamp with time zone NOT NULL,
    "CreatedByUid" uuid NOT NULL,
    "ModifiedByUid" uuid NOT NULL,
    "CorrelationId" uuid NOT NULL,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_ConversationAssignments" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_ConversationAssignments_AssignmentRole" CHECK ("AssignmentRole" IN ('Owner', 'Observer')),
    CONSTRAINT "CK_ConversationAssignments_IsActive_ReleasedAt_Consistency" CHECK ("ReleasedAt" IS NULL OR NOT "IsActive"),
    CONSTRAINT "CK_ConversationAssignments_ReleaseReason_When_Released" CHECK ("ReleasedAt" IS NOT NULL OR length(trim(coalesce("ReleaseReason", ''))) = 0),
    CONSTRAINT "FK_ConversationAssignments_ConversationSessions_ConversationId" FOREIGN KEY ("ConversationId") REFERENCES "ConversationSessions" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_ConversationAssignments_ConversationId_AssignedAt" ON "ConversationAssignments" ("ConversationId", "AssignedAt");

CREATE INDEX "IX_ConversationAssignments_Tenant_AssignedQueueCode_IsActive" ON "ConversationAssignments" ("TenantId", "AssignedQueueCode", "IsActive");

CREATE INDEX "IX_ConversationAssignments_Tenant_AssignedToUserId_IsActive" ON "ConversationAssignments" ("TenantId", "AssignedToUserId", "IsActive");

CREATE INDEX "IX_ConversationAssignments_Tenant_IsDeleted" ON "ConversationAssignments" ("TenantId", "IsDeleted");

CREATE UNIQUE INDEX "UX_ConversationAssignments_ConversationId_AssignmentRole_ActiveOwner" ON "ConversationAssignments" ("ConversationId", "AssignmentRole") WHERE "IsActive" = TRUE AND "AssignmentRole" = 'Owner';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260420205423_DB0051_AddConversationAssignment', '10.0.1');

COMMIT;

START TRANSACTION;
CREATE TABLE "ConversationMessages" (
    "Id" uuid NOT NULL,
    "ConversationId" uuid NOT NULL,
    "ConversationChannelEndpointId" uuid NOT NULL,
    "Sequence" bigint NOT NULL,
    "Channel" character varying(64) NOT NULL,
    "Direction" character varying(16) NOT NULL,
    "SenderType" character varying(16) NOT NULL,
    "SenderUserId" uuid,
    "SenderDisplayName" character varying(256),
    "MessageKind" character varying(32) NOT NULL,
    "ProviderMessageId" character varying(256),
    "ProviderCorrelationKey" character varying(256),
    "ReplyToMessageId" uuid,
    "TextNormalized" text,
    "TextRedacted" text,
    "MetadataJson" jsonb NOT NULL DEFAULT '{}',
    "IsInternalOnly" boolean NOT NULL DEFAULT FALSE,
    "RelatedPendingActionId" uuid,
    "RelatedAIRequestId" uuid,
    "OccurredAt" timestamp with time zone NOT NULL,
    "ProcessedAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "ModifiedAt" timestamp with time zone NOT NULL,
    "CreatedByUid" uuid NOT NULL,
    "ModifiedByUid" uuid NOT NULL,
    "CorrelationId" uuid NOT NULL,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_ConversationMessages" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_ConversationMessages_Direction" CHECK ("Direction" IN ('INBOUND', 'OUTBOUND', 'INTERNAL')),
    CONSTRAINT "CK_ConversationMessages_MessageKind" CHECK ("MessageKind" IN ('TEXT', 'MEDIA', 'BUTTON_REPLY', 'COMMAND', 'SYSTEM_NOTICE', 'SUGGESTION')),
    CONSTRAINT "CK_ConversationMessages_SenderType" CHECK ("SenderType" IN ('USER', 'AI', 'HUMAN', 'SYSTEM')),
    CONSTRAINT "CK_ConversationMessages_Sequence_NonNegative" CHECK ("Sequence" >= 0),
    CONSTRAINT "FK_ConversationMessages_ConversationChannelEndpoints_ConversationChannelEndpointId" FOREIGN KEY ("ConversationChannelEndpointId") REFERENCES "ConversationChannelEndpoints" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ConversationMessages_ConversationSessions_ConversationId" FOREIGN KEY ("ConversationId") REFERENCES "ConversationSessions" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_ConversationMessages_ConversationChannelEndpointId" ON "ConversationMessages" ("ConversationChannelEndpointId");

CREATE INDEX "IX_ConversationMessages_ConversationId_OccurredAt" ON "ConversationMessages" ("ConversationId", "OccurredAt");

CREATE INDEX "IX_ConversationMessages_Tenant_IsDeleted" ON "ConversationMessages" ("TenantId", "IsDeleted");

CREATE INDEX "IX_ConversationMessages_Tenant_RelatedAIRequestId" ON "ConversationMessages" ("TenantId", "RelatedAIRequestId");

CREATE INDEX "IX_ConversationMessages_Tenant_RelatedPendingActionId" ON "ConversationMessages" ("TenantId", "RelatedPendingActionId");

CREATE INDEX "IX_ConversationMessages_Tenant_SenderType_OccurredAt" ON "ConversationMessages" ("TenantId", "SenderType", "OccurredAt");

CREATE UNIQUE INDEX "UX_ConversationMessages_ConversationId_Sequence" ON "ConversationMessages" ("ConversationId", "Sequence");

CREATE UNIQUE INDEX "UX_ConversationMessages_Tenant_Endpoint_ProviderMessageId_NotNull" ON "ConversationMessages" ("TenantId", "ConversationChannelEndpointId", "ProviderMessageId") WHERE "ProviderMessageId" IS NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260420212000_DB0061_AddConversationMessage', '10.0.1');

COMMIT;

START TRANSACTION;
CREATE TABLE "ConversationControlTransitions" (
    "Id" uuid NOT NULL,
    "ConversationId" uuid NOT NULL,
    "PreviousMode" character varying(32),
    "NewMode" character varying(32) NOT NULL,
    "TransitionReason" character varying(256) NOT NULL,
    "TriggeredByType" character varying(16) NOT NULL,
    "TriggeredByUserId" uuid,
    "RelatedMessageId" uuid,
    "RelatedAIRequestId" uuid,
    "NoteRedacted" text,
    "ControlVersion" bigint NOT NULL,
    "OccurredAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "ModifiedAt" timestamp with time zone NOT NULL,
    "CreatedByUid" uuid NOT NULL,
    "ModifiedByUid" uuid NOT NULL,
    "CorrelationId" uuid NOT NULL,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_ConversationControlTransitions" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_ConversationControlTransitions_ControlVersion_Positive" CHECK ("ControlVersion" > 0),
    CONSTRAINT "CK_ConversationControlTransitions_NewMode" CHECK ("NewMode" IN ('AI_ACTIVE', 'HUMAN_ACTIVE', 'AI_PAUSED', 'RESOLVED')),
    CONSTRAINT "CK_ConversationControlTransitions_PreviousMode" CHECK ("PreviousMode" IS NULL OR "PreviousMode" IN ('AI_ACTIVE', 'HUMAN_ACTIVE', 'AI_PAUSED', 'RESOLVED')),
    CONSTRAINT "CK_ConversationControlTransitions_TriggeredByType" CHECK ("TriggeredByType" IN ('USER', 'HUMAN', 'AI', 'SYSTEM', 'POLICY')),
    CONSTRAINT "FK_ConversationControlTransitions_ConversationSessions_ConversationId" FOREIGN KEY ("ConversationId") REFERENCES "ConversationSessions" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_ConversationControlTransitions_ConversationId_OccurredAt" ON "ConversationControlTransitions" ("ConversationId", "OccurredAt");

CREATE INDEX "IX_ConversationControlTransitions_Tenant_IsDeleted" ON "ConversationControlTransitions" ("TenantId", "IsDeleted");

CREATE INDEX "IX_ConversationControlTransitions_Tenant_NewMode_OccurredAt" ON "ConversationControlTransitions" ("TenantId", "NewMode", "OccurredAt");

CREATE INDEX "IX_ConversationControlTransitions_Tenant_TriggeredByUserId_OccurredAt" ON "ConversationControlTransitions" ("TenantId", "TriggeredByUserId", "OccurredAt");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260420214345_DB0081_AddConversationControlTransition', '10.0.1');

COMMIT;

START TRANSACTION;
CREATE TABLE "MessageDeliveryAttempts" (
    "Id" uuid NOT NULL,
    "ConversationMessageId" uuid NOT NULL,
    "ConversationChannelEndpointId" uuid NOT NULL,
    "AttemptNumber" integer NOT NULL,
    "ProviderMessageId" character varying(256),
    "DeliveryStatus" character varying(32) NOT NULL,
    "HttpStatusCode" integer,
    "ErrorCode" character varying(128),
    "ErrorMessageRedacted" character varying(2000),
    "AttemptedAt" timestamp with time zone NOT NULL,
    "NextRetryAt" timestamp with time zone,
    "FinalizedAt" timestamp with time zone,
    "IsFinal" boolean NOT NULL DEFAULT FALSE,
    "MetadataJson" jsonb NOT NULL DEFAULT '{}',
    "CreatedAt" timestamp with time zone NOT NULL,
    "ModifiedAt" timestamp with time zone NOT NULL,
    "CreatedByUid" uuid NOT NULL,
    "ModifiedByUid" uuid NOT NULL,
    "CorrelationId" uuid NOT NULL,
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_MessageDeliveryAttempts" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_MessageDeliveryAttempts_AttemptNumber_Positive" CHECK ("AttemptNumber" >= 1),
    CONSTRAINT "CK_MessageDeliveryAttempts_DeliveryStatus" CHECK ("DeliveryStatus" IN ('QUEUED', 'ACCEPTED', 'SENT', 'DELIVERED', 'READ', 'FAILED')),
    CONSTRAINT "CK_MessageDeliveryAttempts_IsFinal_FinalizedAt_Consistency" CHECK (("IsFinal" AND "FinalizedAt" IS NOT NULL) OR (NOT "IsFinal" AND "FinalizedAt" IS NULL)),
    CONSTRAINT "FK_MessageDeliveryAttempts_ConversationChannelEndpoints_ConversationChannelEndpointId" FOREIGN KEY ("ConversationChannelEndpointId") REFERENCES "ConversationChannelEndpoints" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MessageDeliveryAttempts_ConversationMessages_ConversationMessageId" FOREIGN KEY ("ConversationMessageId") REFERENCES "ConversationMessages" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_MessageDeliveryAttempts_ConversationChannelEndpointId" ON "MessageDeliveryAttempts" ("ConversationChannelEndpointId");

CREATE INDEX "IX_MessageDeliveryAttempts_ConversationMessageId_AttemptedAt" ON "MessageDeliveryAttempts" ("ConversationMessageId", "AttemptedAt");

CREATE INDEX "IX_MessageDeliveryAttempts_Tenant_DeliveryStatus_NextRetryAt" ON "MessageDeliveryAttempts" ("TenantId", "DeliveryStatus", "NextRetryAt");

CREATE INDEX "IX_MessageDeliveryAttempts_Tenant_IsDeleted" ON "MessageDeliveryAttempts" ("TenantId", "IsDeleted");

CREATE UNIQUE INDEX "UX_MessageDeliveryAttempts_ConversationMessageId_AttemptNumber" ON "MessageDeliveryAttempts" ("ConversationMessageId", "AttemptNumber");

CREATE UNIQUE INDEX "UX_MessageDeliveryAttempts_Tenant_Endpoint_ProviderMessageId_NotNull" ON "MessageDeliveryAttempts" ("TenantId", "ConversationChannelEndpointId", "ProviderMessageId") WHERE "ProviderMessageId" IS NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260420214516_DB0071_AddMessageDeliveryAttempt', '10.0.1');

COMMIT;

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

