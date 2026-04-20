using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZawatSys.MicroLib.Communication.Domain.Entities;

namespace ZawatSys.MicroService.Communication.Infrastructure.Data.Configuration;

public sealed class ExternalIdentityBindingConfiguration : IEntityTypeConfiguration<ExternalIdentityBinding>
{
    public void Configure(EntityTypeBuilder<ExternalIdentityBinding> builder)
    {
        builder.ToTable("ExternalIdentityBindings", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_ExternalIdentityBindings_BindingKind",
                "\"BindingKind\" IN ('Verified', 'Inferred', 'Guest', 'Lead')");

            tableBuilder.HasCheckConstraint(
                "CK_ExternalIdentityBindings_VerificationStatus",
                "\"VerificationStatus\" IN ('Unverified', 'Verified', 'Suspended')");

            tableBuilder.HasCheckConstraint(
                "CK_ExternalIdentityBindings_BlockReason_When_Blocked",
                "NOT \"IsBlocked\" OR length(trim(coalesce(\"BlockReason\", ''))) > 0");
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

        builder.Property(e => e.Channel)
            .IsRequired()
            .HasMaxLength(64)
            .HasColumnType("character varying(64)");

        builder.Property(e => e.ExternalUserId)
            .IsRequired()
            .HasMaxLength(256)
            .HasColumnType("character varying(256)");

        builder.Property(e => e.NormalizedExternalUserId)
            .IsRequired()
            .HasMaxLength(256)
            .HasColumnType("character varying(256)");

        builder.Property(e => e.ExternalDisplayName)
            .HasMaxLength(256)
            .HasColumnType("character varying(256)");

        builder.Property(e => e.ContactId)
            .HasColumnType("uuid");

        builder.Property(e => e.SubjectId)
            .HasColumnType("uuid");

        builder.Property(e => e.AccountId)
            .HasColumnType("uuid");

        builder.Property(e => e.EntityType)
            .HasMaxLength(128)
            .HasColumnType("character varying(128)");

        builder.Property(e => e.EntityId)
            .HasColumnType("uuid");

        builder.Property(e => e.BindingKind)
            .IsRequired()
            .HasMaxLength(32)
            .HasColumnType("character varying(32)");

        builder.Property(e => e.VerificationStatus)
            .IsRequired()
            .HasMaxLength(32)
            .HasColumnType("character varying(32)");

        builder.Property(e => e.IsBlocked)
            .IsRequired()
            .HasColumnType("boolean")
            .HasDefaultValue(false);

        builder.Property(e => e.BlockReason)
            .HasMaxLength(512)
            .HasColumnType("character varying(512)");

        builder.Property(e => e.LastResolvedAt)
            .HasColumnType("timestamp with time zone");

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

        builder.HasOne(e => e.ConversationChannelEndpoint)
            .WithMany()
            .HasForeignKey(e => e.ConversationChannelEndpointId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ExternalIdentityBindings_ConversationChannelEndpoints_ConversationChannelEndpointId");

        builder.HasIndex(e => new { e.TenantId, e.ConversationChannelEndpointId, e.NormalizedExternalUserId })
            .IsUnique()
            .HasDatabaseName("UX_ExternalIdentityBindings_Tenant_Endpoint_NormalizedExternalUserId");

        builder.HasIndex(e => new { e.TenantId, e.ContactId })
            .HasDatabaseName("IX_ExternalIdentityBindings_Tenant_ContactId");

        builder.HasIndex(e => new { e.TenantId, e.SubjectId })
            .HasDatabaseName("IX_ExternalIdentityBindings_Tenant_SubjectId");

        builder.HasIndex(e => new { e.TenantId, e.AccountId })
            .HasDatabaseName("IX_ExternalIdentityBindings_Tenant_AccountId");

        builder.HasIndex(e => new { e.TenantId, e.EntityType, e.EntityId })
            .HasDatabaseName("IX_ExternalIdentityBindings_Tenant_EntityType_EntityId");

        builder.HasIndex(e => new { e.TenantId, e.IsBlocked })
            .HasDatabaseName("IX_ExternalIdentityBindings_Tenant_IsBlocked");

        builder.HasIndex(e => new { e.TenantId, e.IsDeleted })
            .HasDatabaseName("IX_ExternalIdentityBindings_Tenant_IsDeleted");

        builder.HasQueryFilter(e => !e.IsDeleted);

        builder.Ignore(e => e.DomainEvents);
    }
}
