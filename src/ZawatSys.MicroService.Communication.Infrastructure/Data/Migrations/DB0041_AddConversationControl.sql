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
