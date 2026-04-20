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

