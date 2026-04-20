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

