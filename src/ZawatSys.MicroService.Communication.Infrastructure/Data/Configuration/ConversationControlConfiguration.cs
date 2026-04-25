using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZawatSys.MicroLib.Communication.Domain.Entities;

namespace ZawatSys.MicroService.Communication.Infrastructure.Data.Configuration;

public sealed class ConversationControlConfiguration : IEntityTypeConfiguration<ConversationControl>
{
    public void Configure(EntityTypeBuilder<ConversationControl> builder)
    {
        builder.ToTable("ConversationControls", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_ConversationControls_Mode",
                "\"Mode\" IN ('AI_ACTIVE', 'HUMAN_ACTIVE', 'AI_PAUSED')");

            tableBuilder.HasCheckConstraint(
                "CK_ConversationControls_IntegrationVersion_Positive",
                "\"IntegrationVersion\" > 0");
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

        builder.Property(e => e.Mode)
            .IsRequired()
            .HasMaxLength(32)
            .HasColumnType("character varying(32)");

        builder.Property(e => e.AssignedToUserId)
            .HasColumnType("uuid");

        builder.Property(e => e.AssignedQueueCode)
            .HasMaxLength(64)
            .HasColumnType("character varying(64)");

        builder.Property(e => e.HandoffReason)
            .HasMaxLength(512)
            .HasColumnType("character varying(512)");

        builder.Property(e => e.PauseReason)
            .HasMaxLength(512)
            .HasColumnType("character varying(512)");

        builder.Property(e => e.LastUserActivityAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.LastHumanActivityAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.LastAIActivityAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.LastTakenOverAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.LastResumedAt)
            .HasColumnType("timestamp with time zone");

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

        builder.HasOne(e => e.Conversation)
            .WithMany()
            .HasForeignKey(e => e.ConversationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ConversationControls_ConversationSessions_ConversationId");

        builder.HasIndex(e => e.ConversationId)
            .IsUnique()
            .HasFilter("NOT \"IsDeleted\"")
            .HasDatabaseName("UX_ConversationControls_ConversationId");

        builder.HasIndex(e => new { e.TenantId, e.Mode, e.ModifiedAt })
            .HasDatabaseName("IX_ConversationControls_Tenant_Mode_ModifiedAt");

        builder.HasIndex(e => new { e.TenantId, e.AssignedToUserId, e.Mode })
            .HasDatabaseName("IX_ConversationControls_Tenant_AssignedToUserId_Mode");

        builder.HasIndex(e => new { e.TenantId, e.AssignedQueueCode, e.Mode })
            .HasDatabaseName("IX_ConversationControls_Tenant_AssignedQueueCode_Mode");

        builder.HasIndex(e => new { e.TenantId, e.IsDeleted })
            .HasDatabaseName("IX_ConversationControls_Tenant_IsDeleted");

        builder.HasQueryFilter(e => !e.IsDeleted);

        builder.Ignore(e => e.DomainEvents);
    }
}
