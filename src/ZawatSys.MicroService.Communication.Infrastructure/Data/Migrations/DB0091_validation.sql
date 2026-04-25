INSERT INTO "ConversationChannelEndpoints"
    ("Id", "Channel", "Provider", "EndpointKey", "DisplayName", "InboundEnabled", "OutboundEnabled", "IsDefault", "MetadataJson", "CreatedAt", "ModifiedAt", "CreatedByUid", "ModifiedByUid", "CorrelationId", "IsDeleted", "TenantId")
VALUES
    ('11111111-1111-1111-1111-111111111111', 'WhatsApp', 'Meta', 'primary', 'Primary', TRUE, TRUE, TRUE, '{}'::jsonb, NOW(), NOW(), 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', FALSE, '00000000-0000-0000-0000-000000000001'),
    ('22222222-2222-2222-2222-222222222222', 'WhatsApp', 'Meta', 'backup', 'Backup', TRUE, TRUE, FALSE, '{}'::jsonb, NOW(), NOW(), 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', FALSE, '00000000-0000-0000-0000-000000000001');

INSERT INTO "ExternalIdentityBindings"
    ("Id", "ConversationChannelEndpointId", "Channel", "ExternalUserId", "NormalizedExternalUserId", "BindingKind", "VerificationStatus", "IsBlocked", "MetadataJson", "CreatedAt", "ModifiedAt", "CreatedByUid", "ModifiedByUid", "CorrelationId", "IsDeleted", "TenantId")
VALUES
    ('33333333-3333-3333-3333-333333333333', '11111111-1111-1111-1111-111111111111', 'WhatsApp', '+15550001111', '15550001111', 'Verified', 'Verified', FALSE, '{}'::jsonb, NOW(), NOW(), 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', FALSE, '00000000-0000-0000-0000-000000000001');

INSERT INTO "ConversationSessions"
    ("Id", "ConversationChannelEndpointId", "ExternalIdentityBindingId", "Channel", "SessionStatus", "ScenarioSnapshotJson", "OpenedAt", "LastInboundMessageAt", "LastMessageSequence", "IntegrationVersion", "CreatedAt", "ModifiedAt", "CreatedByUid", "ModifiedByUid", "CorrelationId", "IsDeleted", "TenantId")
VALUES
    ('44444444-4444-4444-4444-444444444444', '11111111-1111-1111-1111-111111111111', '33333333-3333-3333-3333-333333333333', 'WhatsApp', 'OPEN', '{}'::jsonb, NOW() - INTERVAL '1 day', NOW() - INTERVAL '5 minutes', 2, 1, NOW(), NOW(), 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', FALSE, '00000000-0000-0000-0000-000000000001');

INSERT INTO "ConversationControls"
    ("Id", "ConversationId", "Mode", "IntegrationVersion", "CreatedAt", "ModifiedAt", "CreatedByUid", "ModifiedByUid", "CorrelationId", "IsDeleted", "TenantId")
VALUES
    ('55555555-5555-5555-5555-555555555555', '44444444-4444-4444-4444-444444444444', 'AI_PAUSED', 2, NOW(), NOW(), 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', FALSE, '00000000-0000-0000-0000-000000000001');

INSERT INTO "ConversationAssignments"
    ("Id", "ConversationId", "AssignedToUserId", "AssignmentRole", "AssignedByUserId", "AssignedAt", "IsActive", "CreatedAt", "ModifiedAt", "CreatedByUid", "ModifiedByUid", "CorrelationId", "IsDeleted", "TenantId")
VALUES
    ('66666666-6666-6666-6666-666666666666', '44444444-4444-4444-4444-444444444444', '77777777-7777-7777-7777-777777777777', 'Owner', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', NOW() - INTERVAL '10 minutes', TRUE, NOW(), NOW(), 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', FALSE, '00000000-0000-0000-0000-000000000001');

INSERT INTO "ConversationMessages"
    ("Id", "ConversationId", "ConversationChannelEndpointId", "Sequence", "Channel", "Direction", "SenderType", "MessageKind", "ProviderMessageId", "MetadataJson", "OccurredAt", "CreatedAt", "ModifiedAt", "CreatedByUid", "ModifiedByUid", "CorrelationId", "IsDeleted", "TenantId")
VALUES
    ('88888888-8888-8888-8888-888888888881', '44444444-4444-4444-4444-444444444444', '11111111-1111-1111-1111-111111111111', 1, 'WhatsApp', 'INBOUND', 'USER', 'TEXT', 'in-001', '{}'::jsonb, NOW() - INTERVAL '4 minutes', NOW(), NOW(), 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', FALSE, '00000000-0000-0000-0000-000000000001'),
    ('88888888-8888-8888-8888-888888888882', '44444444-4444-4444-4444-444444444444', '11111111-1111-1111-1111-111111111111', 2, 'WhatsApp', 'OUTBOUND', 'AI', 'TEXT', 'out-002', '{}'::jsonb, NOW() - INTERVAL '3 minutes', NOW(), NOW(), 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', FALSE, '00000000-0000-0000-0000-000000000001'),
    ('88888888-8888-8888-8888-888888888883', '44444444-4444-4444-4444-444444444444', '11111111-1111-1111-1111-111111111111', 3, 'WhatsApp', 'OUTBOUND', 'SYSTEM', 'SYSTEM_NOTICE', 'out-003', '{}'::jsonb, NOW() - INTERVAL '2 minutes', NOW(), NOW(), 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', FALSE, '00000000-0000-0000-0000-000000000001');

INSERT INTO "ConversationControlTransitions"
    ("Id", "ConversationId", "PreviousMode", "NewMode", "TransitionReason", "TriggeredByType", "ControlVersion", "OccurredAt", "CreatedAt", "ModifiedAt", "CreatedByUid", "ModifiedByUid", "CorrelationId", "IsDeleted", "TenantId")
VALUES
    ('99999999-9999-9999-9999-999999999991', '44444444-4444-4444-4444-444444444444', NULL, 'AI_ACTIVE', 'resume', 'SYSTEM', 1, NOW() - INTERVAL '9 minutes', NOW(), NOW(), 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', FALSE, '00000000-0000-0000-0000-000000000001'),
    ('99999999-9999-9999-9999-999999999992', '44444444-4444-4444-4444-444444444444', 'AI_ACTIVE', 'AI_PAUSED', 'resolve', 'HUMAN', 2, NOW() - INTERVAL '1 minute', NOW(), NOW(), 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', FALSE, '00000000-0000-0000-0000-000000000001');

INSERT INTO "MessageDeliveryAttempts"
    ("Id", "ConversationMessageId", "ConversationChannelEndpointId", "AttemptNumber", "ProviderMessageId", "DeliveryStatus", "AttemptedAt", "NextRetryAt", "FinalizedAt", "IsFinal", "MetadataJson", "CreatedAt", "ModifiedAt", "CreatedByUid", "ModifiedByUid", "CorrelationId", "IsDeleted", "TenantId")
VALUES
    ('aaaaaaaa-1111-1111-1111-111111111111', '88888888-8888-8888-8888-888888888882', '11111111-1111-1111-1111-111111111111', 1, 'provider-out-001', 'FAILED', NOW() - INTERVAL '1 minute', NOW() + INTERVAL '5 minutes', NULL, FALSE, '{}'::jsonb, NOW(), NOW(), 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', FALSE, '00000000-0000-0000-0000-000000000001');

EXPLAIN SELECT *
FROM "ConversationSessions"
WHERE "TenantId" = '00000000-0000-0000-0000-000000000001'
  AND "ExternalIdentityBindingId" = '33333333-3333-3333-3333-333333333333'
  AND "ClosedAt" IS NULL
  AND NOT "IsDeleted";

EXPLAIN SELECT *
FROM "ConversationMessages"
WHERE "ConversationId" = '44444444-4444-4444-4444-444444444444'
  AND "Direction" = 'OUTBOUND'
ORDER BY "Sequence";

EXPLAIN SELECT *
FROM "MessageDeliveryAttempts"
WHERE "TenantId" = '00000000-0000-0000-0000-000000000001'
  AND NOT "IsFinal"
  AND "NextRetryAt" IS NOT NULL
ORDER BY "NextRetryAt";

DO $$
BEGIN
    BEGIN
        INSERT INTO "ConversationChannelEndpoints"
            ("Id", "Channel", "Provider", "EndpointKey", "DisplayName", "InboundEnabled", "OutboundEnabled", "IsDefault", "MetadataJson", "CreatedAt", "ModifiedAt", "CreatedByUid", "ModifiedByUid", "CorrelationId", "IsDeleted", "TenantId")
        VALUES
            ('12121212-1212-1212-1212-121212121212', 'WhatsApp', 'Meta', 'second-default', 'Second Default', TRUE, TRUE, TRUE, '{}'::jsonb, NOW(), NOW(), 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', FALSE, '00000000-0000-0000-0000-000000000001');
        RAISE EXCEPTION 'expected unique violation for default endpoint';
    EXCEPTION
        WHEN unique_violation THEN
            RAISE NOTICE 'validated default endpoint uniqueness';
    END;

    BEGIN
        INSERT INTO "ConversationAssignments"
            ("Id", "ConversationId", "AssignedToUserId", "AssignmentRole", "AssignedByUserId", "AssignedAt", "IsActive", "CreatedAt", "ModifiedAt", "CreatedByUid", "ModifiedByUid", "CorrelationId", "IsDeleted", "TenantId")
        VALUES
            ('13131313-1313-1313-1313-131313131313', '44444444-4444-4444-4444-444444444444', '14141414-1414-1414-1414-141414141414', 'Owner', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', NOW(), TRUE, NOW(), NOW(), 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', FALSE, '00000000-0000-0000-0000-000000000001');
        RAISE EXCEPTION 'expected unique violation for active owner';
    EXCEPTION
        WHEN unique_violation THEN
            RAISE NOTICE 'validated single active owner uniqueness';
    END;

    BEGIN
        INSERT INTO "ConversationControlTransitions"
            ("Id", "ConversationId", "PreviousMode", "NewMode", "TransitionReason", "TriggeredByType", "ControlVersion", "OccurredAt", "CreatedAt", "ModifiedAt", "CreatedByUid", "ModifiedByUid", "CorrelationId", "IsDeleted", "TenantId")
        VALUES
            ('15151515-1515-1515-1515-151515151515', '44444444-4444-4444-4444-444444444444', 'AI_PAUSED', 'RESOLVED', 'invalid-resolve-mode', 'HUMAN', 3, NOW(), NOW(), NOW(), 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', FALSE, '00000000-0000-0000-0000-000000000001');
        RAISE EXCEPTION 'expected check violation for RESOLVED control mode';
    EXCEPTION
        WHEN check_violation THEN
            RAISE NOTICE 'validated RESOLVED is not a control mode';
    END;
END $$;
