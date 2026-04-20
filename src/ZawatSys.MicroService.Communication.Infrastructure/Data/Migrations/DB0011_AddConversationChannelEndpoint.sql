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

