using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZawatSys.MicroLib.Communication.Domain.Entities;

namespace ZawatSys.MicroService.Communication.Infrastructure.Data.Configuration;

public sealed class ConversationMessageConfiguration : IEntityTypeConfiguration<ConversationMessage>
{
    public void Configure(EntityTypeBuilder<ConversationMessage> builder)
    {
        builder.ToTable("ConversationMessages", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_ConversationMessages_Direction",
                "\"Direction\" IN ('INBOUND', 'OUTBOUND', 'INTERNAL')");

            tableBuilder.HasCheckConstraint(
                "CK_ConversationMessages_SenderType",
                "\"SenderType\" IN ('USER', 'AI', 'HUMAN', 'SYSTEM')");

            tableBuilder.HasCheckConstraint(
                "CK_ConversationMessages_MessageKind",
                "\"MessageKind\" IN ('TEXT', 'MEDIA', 'BUTTON_REPLY', 'COMMAND', 'SYSTEM_NOTICE', 'SUGGESTION')");

            tableBuilder.HasCheckConstraint(
                "CK_ConversationMessages_Sequence_NonNegative",
                "\"Sequence\" >= 0");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .IsRequired()
            .HasColumnType("uuid");

        builder.Property(e => e.ConversationId)
            .IsRequired()
            .HasColumnType("uuid");

        builder.Property(e => e.ConversationChannelEndpointId)
            .IsRequired()
            .HasColumnType("uuid");

        builder.Property(e => e.Sequence)
            .IsRequired()
            .HasColumnType("bigint");

        builder.Property(e => e.Channel)
            .IsRequired()
            .HasMaxLength(64)
            .HasColumnType("character varying(64)");

        builder.Property(e => e.Direction)
            .IsRequired()
            .HasMaxLength(16)
            .HasColumnType("character varying(16)");

        builder.Property(e => e.SenderType)
            .IsRequired()
            .HasMaxLength(16)
            .HasColumnType("character varying(16)");

        builder.Property(e => e.SenderUserId)
            .HasColumnType("uuid");

        builder.Property(e => e.SenderDisplayName)
            .HasMaxLength(256)
            .HasColumnType("character varying(256)");

        builder.Property(e => e.MessageKind)
            .IsRequired()
            .HasMaxLength(32)
            .HasColumnType("character varying(32)");

        builder.Property(e => e.ProviderMessageId)
            .HasMaxLength(256)
            .HasColumnType("character varying(256)");

        builder.Property(e => e.ProviderCorrelationKey)
            .HasMaxLength(256)
            .HasColumnType("character varying(256)");

        builder.Property(e => e.ReplyToMessageId)
            .HasColumnType("uuid");

        builder.Property(e => e.TextNormalized)
            .HasColumnType("text");

        builder.Property(e => e.TextRedacted)
            .HasColumnType("text");

        builder.Property(e => e.MetadataJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");

        builder.Property(e => e.IsInternalOnly)
            .IsRequired()
            .HasColumnType("boolean")
            .HasDefaultValue(false);

        builder.Property(e => e.RelatedPendingActionId)
            .HasColumnType("uuid");

        builder.Property(e => e.RelatedAIRequestId)
            .HasColumnType("uuid");

        builder.Property(e => e.OccurredAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.ProcessedAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.ModifiedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.CreatedByUid)
            .IsRequired()
            .HasColumnType("uuid");

        builder.Property(e => e.ModifiedByUid)
            .IsRequired()
            .HasColumnType("uuid");

        builder.Property(e => e.CorrelationId)
            .IsRequired()
            .HasColumnType("uuid");

        builder.Property(e => e.IsDeleted)
            .IsRequired()
            .HasColumnType("boolean")
            .HasDefaultValue(false);

        builder.Property<uint>("xmin")
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid");

        builder.HasOne(e => e.Conversation)
            .WithMany()
            .HasForeignKey(e => e.ConversationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ConversationMessages_ConversationSessions_ConversationId");

        builder.HasOne(e => e.ConversationChannelEndpoint)
            .WithMany()
            .HasForeignKey(e => e.ConversationChannelEndpointId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ConversationMessages_ConversationChannelEndpoints_ConversationChannelEndpointId");

        builder.HasIndex(e => new { e.ConversationId, e.Sequence })
            .IsUnique()
            .HasDatabaseName("UX_ConversationMessages_ConversationId_Sequence");

        builder.HasIndex(e => new { e.TenantId, e.ConversationChannelEndpointId, e.ProviderMessageId })
            .IsUnique()
            .HasFilter("\"ProviderMessageId\" IS NOT NULL")
            .HasDatabaseName("UX_ConversationMessages_Tenant_Endpoint_ProviderMessageId_NotNull");

        builder.HasIndex(e => new { e.ConversationId, e.OccurredAt })
            .HasDatabaseName("IX_ConversationMessages_ConversationId_OccurredAt");

        builder.HasIndex(e => new { e.ConversationId, e.Direction, e.Sequence })
            .HasDatabaseName("IX_ConversationMessages_ConversationId_Direction_Sequence");

        builder.HasIndex(e => new { e.TenantId, e.SenderType, e.OccurredAt })
            .HasDatabaseName("IX_ConversationMessages_Tenant_SenderType_OccurredAt");

        builder.HasIndex(e => new { e.TenantId, e.RelatedPendingActionId })
            .HasDatabaseName("IX_ConversationMessages_Tenant_RelatedPendingActionId");

        builder.HasIndex(e => new { e.TenantId, e.RelatedAIRequestId })
            .HasDatabaseName("IX_ConversationMessages_Tenant_RelatedAIRequestId");

        builder.HasIndex(e => new { e.TenantId, e.IsDeleted })
            .HasDatabaseName("IX_ConversationMessages_Tenant_IsDeleted");

        builder.HasQueryFilter(e => !e.IsDeleted);

        builder.Ignore(e => e.DomainEvents);
    }
}
