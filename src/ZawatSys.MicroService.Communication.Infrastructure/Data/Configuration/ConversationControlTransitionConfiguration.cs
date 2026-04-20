using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZawatSys.MicroLib.Communication.Domain.Entities;

namespace ZawatSys.MicroService.Communication.Infrastructure.Data.Configuration;

public sealed class ConversationControlTransitionConfiguration : IEntityTypeConfiguration<ConversationControlTransition>
{
    public void Configure(EntityTypeBuilder<ConversationControlTransition> builder)
    {
        builder.ToTable("ConversationControlTransitions", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_ConversationControlTransitions_NewMode",
                "\"NewMode\" IN ('AI_ACTIVE', 'HUMAN_ACTIVE', 'AI_PAUSED', 'RESOLVED')");

            tableBuilder.HasCheckConstraint(
                "CK_ConversationControlTransitions_PreviousMode",
                "\"PreviousMode\" IS NULL OR \"PreviousMode\" IN ('AI_ACTIVE', 'HUMAN_ACTIVE', 'AI_PAUSED', 'RESOLVED')");

            tableBuilder.HasCheckConstraint(
                "CK_ConversationControlTransitions_TriggeredByType",
                "\"TriggeredByType\" IN ('USER', 'HUMAN', 'AI', 'SYSTEM', 'POLICY')");

            tableBuilder.HasCheckConstraint(
                "CK_ConversationControlTransitions_ControlVersion_Positive",
                "\"ControlVersion\" > 0");
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

        builder.Property(e => e.PreviousMode)
            .HasMaxLength(32)
            .HasColumnType("character varying(32)");

        builder.Property(e => e.NewMode)
            .IsRequired()
            .HasMaxLength(32)
            .HasColumnType("character varying(32)");

        builder.Property(e => e.TransitionReason)
            .IsRequired()
            .HasMaxLength(256)
            .HasColumnType("character varying(256)");

        builder.Property(e => e.TriggeredByType)
            .IsRequired()
            .HasMaxLength(16)
            .HasColumnType("character varying(16)");

        builder.Property(e => e.TriggeredByUserId)
            .HasColumnType("uuid");

        builder.Property(e => e.RelatedMessageId)
            .HasColumnType("uuid");

        builder.Property(e => e.RelatedAIRequestId)
            .HasColumnType("uuid");

        builder.Property(e => e.NoteRedacted)
            .HasColumnType("text");

        builder.Property(e => e.ControlVersion)
            .IsRequired()
            .HasColumnType("bigint");

        builder.Property(e => e.OccurredAt)
            .IsRequired()
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
            .HasConstraintName("FK_ConversationControlTransitions_ConversationSessions_ConversationId");

        builder.HasIndex(e => new { e.ConversationId, e.OccurredAt })
            .HasDatabaseName("IX_ConversationControlTransitions_ConversationId_OccurredAt");

        builder.HasIndex(e => new { e.TenantId, e.NewMode, e.OccurredAt })
            .HasDatabaseName("IX_ConversationControlTransitions_Tenant_NewMode_OccurredAt");

        builder.HasIndex(e => new { e.TenantId, e.TriggeredByUserId, e.OccurredAt })
            .HasDatabaseName("IX_ConversationControlTransitions_Tenant_TriggeredByUserId_OccurredAt");

        builder.HasIndex(e => new { e.TenantId, e.IsDeleted })
            .HasDatabaseName("IX_ConversationControlTransitions_Tenant_IsDeleted");

        builder.HasQueryFilter(e => !e.IsDeleted);

        builder.Ignore(e => e.DomainEvents);
    }
}
