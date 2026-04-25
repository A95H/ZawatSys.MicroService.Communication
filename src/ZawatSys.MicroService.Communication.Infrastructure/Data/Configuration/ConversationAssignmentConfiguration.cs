using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZawatSys.MicroLib.Communication.Domain.Entities;

namespace ZawatSys.MicroService.Communication.Infrastructure.Data.Configuration;

public sealed class ConversationAssignmentConfiguration : IEntityTypeConfiguration<ConversationAssignment>
{
    public void Configure(EntityTypeBuilder<ConversationAssignment> builder)
    {
        builder.ToTable("ConversationAssignments", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_ConversationAssignments_AssignmentRole",
                "\"AssignmentRole\" IN ('Owner', 'Observer')");

            tableBuilder.HasCheckConstraint(
                "CK_ConversationAssignments_ReleaseReason_When_Released",
                "\"ReleasedAt\" IS NOT NULL OR length(trim(coalesce(\"ReleaseReason\", ''))) = 0");

            tableBuilder.HasCheckConstraint(
                "CK_ConversationAssignments_IsActive_ReleasedAt_Consistency",
                "\"ReleasedAt\" IS NULL OR NOT \"IsActive\"");
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

        builder.Property(e => e.AssignedToUserId)
            .HasColumnType("uuid");

        builder.Property(e => e.AssignedQueueCode)
            .HasMaxLength(64)
            .HasColumnType("character varying(64)");

        builder.Property(e => e.AssignmentRole)
            .IsRequired()
            .HasMaxLength(32)
            .HasColumnType("character varying(32)");

        builder.Property(e => e.AssignedByUserId)
            .IsRequired()
            .HasColumnType("uuid");

        builder.Property(e => e.AssignedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.ReleasedAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.ReleaseReason)
            .HasMaxLength(512)
            .HasColumnType("character varying(512)");

        builder.Property(e => e.IsActive)
            .IsRequired()
            .HasColumnType("boolean")
            .HasDefaultValue(true);

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
            .HasConstraintName("FK_ConversationAssignments_ConversationSessions_ConversationId");

        builder.HasIndex(e => new { e.ConversationId, e.AssignedAt })
            .HasDatabaseName("IX_ConversationAssignments_ConversationId_AssignedAt");

        builder.HasIndex(e => new { e.ConversationId, e.AssignmentRole })
            .IsUnique()
            .HasFilter("\"IsActive\" = TRUE AND \"AssignmentRole\" = 'Owner' AND NOT \"IsDeleted\"")
            .HasDatabaseName("UX_ConversationAssignments_ConversationId_AssignmentRole_ActiveOwner");

        builder.HasIndex(e => new { e.TenantId, e.AssignedToUserId, e.IsActive })
            .HasDatabaseName("IX_ConversationAssignments_Tenant_AssignedToUserId_IsActive");

        builder.HasIndex(e => new { e.TenantId, e.AssignedQueueCode, e.IsActive })
            .HasDatabaseName("IX_ConversationAssignments_Tenant_AssignedQueueCode_IsActive");

        builder.HasIndex(e => new { e.TenantId, e.IsDeleted })
            .HasDatabaseName("IX_ConversationAssignments_Tenant_IsDeleted");

        builder.HasQueryFilter(e => !e.IsDeleted);

        builder.Ignore(e => e.DomainEvents);
    }
}
