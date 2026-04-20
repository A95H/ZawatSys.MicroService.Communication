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

