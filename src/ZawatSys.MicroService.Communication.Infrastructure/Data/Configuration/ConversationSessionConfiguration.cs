using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZawatSys.MicroLib.Communication.Domain.Entities;

namespace ZawatSys.MicroService.Communication.Infrastructure.Data.Configuration;

public sealed class ConversationSessionConfiguration : IEntityTypeConfiguration<ConversationSession>
{
    public void Configure(EntityTypeBuilder<ConversationSession> builder)
    {
        builder.ToTable("ConversationSessions", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_ConversationSessions_SessionStatus",
                "\"SessionStatus\" IN ('OPEN', 'RESOLVED', 'CLOSED', 'ARCHIVED')");

            tableBuilder.HasCheckConstraint(
                "CK_ConversationSessions_LastMessageSequence_NonNegative",
                "\"LastMessageSequence\" >= 0");

            tableBuilder.HasCheckConstraint(
                "CK_ConversationSessions_IntegrationVersion_Positive",
                "\"IntegrationVersion\" > 0");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .IsRequired()
            .HasColumnType("uuid");

        builder.Property(e => e.ConversationChannelEndpointId)
            .IsRequired()
            .HasColumnType("uuid");

        builder.Property(e => e.ExternalIdentityBindingId)
            .IsRequired()
            .HasColumnType("uuid");

        builder.Property(e => e.Channel)
            .IsRequired()
            .HasMaxLength(64)
            .HasColumnType("character varying(64)");

        builder.Property(e => e.SessionStatus)
            .IsRequired()
            .HasMaxLength(32)
            .HasColumnType("character varying(32)");

        builder.Property(e => e.Locale)
            .HasMaxLength(16)
            .HasColumnType("character varying(16)");

        builder.Property(e => e.CurrentScenarioKey)
            .HasMaxLength(256)
            .HasColumnType("character varying(256)");

        builder.Property(e => e.CurrentScenarioInstanceId)
            .HasColumnType("uuid");

        builder.Property(e => e.CurrentScenarioStateCode)
            .HasMaxLength(64)
            .HasColumnType("character varying(64)");

        builder.Property(e => e.ScenarioSnapshotJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");

        builder.Property(e => e.CurrentPendingActionId)
            .HasColumnType("uuid");

        builder.Property(e => e.OpenedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.LastInboundMessageAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.LastOutboundMessageAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.LastUserMessageAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.LastHumanMessageAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.LastAIMessageAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.LastMessageSequence)
            .IsRequired()
            .HasColumnType("bigint")
            .HasDefaultValue(0L);

        builder.Property(e => e.ResolvedAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.ClosedAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.ResolutionCode)
            .HasMaxLength(64)
            .HasColumnType("character varying(64)");

        builder.Property(e => e.IntegrationVersion)
            .IsRequired()
            .HasColumnType("bigint")
            .HasDefaultValue(1L);

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

        builder.HasOne(e => e.ConversationChannelEndpoint)
            .WithMany()
            .HasForeignKey(e => e.ConversationChannelEndpointId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ConversationSessions_ConversationChannelEndpoints_ConversationChannelEndpointId");

        builder.HasOne(e => e.ExternalIdentityBinding)
            .WithMany()
            .HasForeignKey(e => e.ExternalIdentityBindingId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ConversationSessions_ExternalIdentityBindings_ExternalIdentityBindingId");

        builder.HasIndex(e => new { e.TenantId, e.ExternalIdentityBindingId })
            .IsUnique()
            .HasFilter("\"ClosedAt\" IS NULL")
            .HasDatabaseName("UX_ConversationSessions_Tenant_ExternalIdentityBinding_Active");

        builder.HasIndex(e => new { e.TenantId, e.SessionStatus, e.LastInboundMessageAt })
            .HasDatabaseName("IX_ConversationSessions_Tenant_SessionStatus_LastInboundMessageAt");

        builder.HasIndex(e => new { e.TenantId, e.CurrentPendingActionId })
            .HasDatabaseName("IX_ConversationSessions_Tenant_CurrentPendingActionId");

        builder.HasIndex(e => new { e.ExternalIdentityBindingId, e.OpenedAt })
            .HasDatabaseName("IX_ConversationSessions_ExternalIdentityBindingId_OpenedAt");

        builder.HasIndex(e => new { e.TenantId, e.IsDeleted })
            .HasDatabaseName("IX_ConversationSessions_Tenant_IsDeleted");

        builder.HasQueryFilter(e => !e.IsDeleted);

        builder.Ignore(e => e.DomainEvents);
    }
}
