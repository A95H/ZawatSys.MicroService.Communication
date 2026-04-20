using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZawatSys.MicroLib.Communication.Domain.Entities;

namespace ZawatSys.MicroService.Communication.Infrastructure.Data.Configuration;

public sealed class MessageDeliveryAttemptConfiguration : IEntityTypeConfiguration<MessageDeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<MessageDeliveryAttempt> builder)
    {
        builder.ToTable("MessageDeliveryAttempts", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_MessageDeliveryAttempts_DeliveryStatus",
                "\"DeliveryStatus\" IN ('QUEUED', 'ACCEPTED', 'SENT', 'DELIVERED', 'READ', 'FAILED')");

            tableBuilder.HasCheckConstraint(
                "CK_MessageDeliveryAttempts_AttemptNumber_Positive",
                "\"AttemptNumber\" >= 1");

            tableBuilder.HasCheckConstraint(
                "CK_MessageDeliveryAttempts_IsFinal_FinalizedAt_Consistency",
                "(\"IsFinal\" AND \"FinalizedAt\" IS NOT NULL) OR (NOT \"IsFinal\" AND \"FinalizedAt\" IS NULL)");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .IsRequired()
            .HasColumnType("uuid");

        builder.Property(e => e.ConversationMessageId)
            .IsRequired()
            .HasColumnType("uuid");

        builder.Property(e => e.ConversationChannelEndpointId)
            .IsRequired()
            .HasColumnType("uuid");

        builder.Property(e => e.AttemptNumber)
            .IsRequired()
            .HasColumnType("integer");

        builder.Property(e => e.ProviderMessageId)
            .HasMaxLength(256)
            .HasColumnType("character varying(256)");

        builder.Property(e => e.DeliveryStatus)
            .IsRequired()
            .HasMaxLength(32)
            .HasColumnType("character varying(32)");

        builder.Property(e => e.HttpStatusCode)
            .HasColumnType("integer");

        builder.Property(e => e.ErrorCode)
            .HasMaxLength(128)
            .HasColumnType("character varying(128)");

        builder.Property(e => e.ErrorMessageRedacted)
            .HasMaxLength(2000)
            .HasColumnType("character varying(2000)");

        builder.Property(e => e.AttemptedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.NextRetryAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.FinalizedAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.IsFinal)
            .IsRequired()
            .HasColumnType("boolean")
            .HasDefaultValue(false);

        builder.Property(e => e.MetadataJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");

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

        builder.HasOne(e => e.ConversationMessage)
            .WithMany()
            .HasForeignKey(e => e.ConversationMessageId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_MessageDeliveryAttempts_ConversationMessages_ConversationMessageId");

        builder.HasOne(e => e.ConversationChannelEndpoint)
            .WithMany()
            .HasForeignKey(e => e.ConversationChannelEndpointId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_MessageDeliveryAttempts_ConversationChannelEndpoints_ConversationChannelEndpointId");

        builder.HasIndex(e => new { e.ConversationMessageId, e.AttemptNumber })
            .IsUnique()
            .HasDatabaseName("UX_MessageDeliveryAttempts_ConversationMessageId_AttemptNumber");

        builder.HasIndex(e => new { e.TenantId, e.DeliveryStatus, e.NextRetryAt })
            .HasDatabaseName("IX_MessageDeliveryAttempts_Tenant_DeliveryStatus_NextRetryAt");

        builder.HasIndex(e => new { e.TenantId, e.ConversationChannelEndpointId, e.ProviderMessageId })
            .IsUnique()
            .HasFilter("\"ProviderMessageId\" IS NOT NULL")
            .HasDatabaseName("UX_MessageDeliveryAttempts_Tenant_Endpoint_ProviderMessageId_NotNull");

        builder.HasIndex(e => new { e.ConversationMessageId, e.AttemptedAt })
            .HasDatabaseName("IX_MessageDeliveryAttempts_ConversationMessageId_AttemptedAt");

        builder.HasIndex(e => new { e.TenantId, e.IsDeleted })
            .HasDatabaseName("IX_MessageDeliveryAttempts_Tenant_IsDeleted");

        builder.HasQueryFilter(e => !e.IsDeleted);

        builder.Ignore(e => e.DomainEvents);
    }
}
