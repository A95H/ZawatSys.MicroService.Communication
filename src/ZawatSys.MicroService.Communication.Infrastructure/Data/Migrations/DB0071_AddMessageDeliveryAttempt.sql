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

