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

