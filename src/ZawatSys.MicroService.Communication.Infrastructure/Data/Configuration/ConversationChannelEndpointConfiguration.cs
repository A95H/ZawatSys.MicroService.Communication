using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZawatSys.MicroLib.Communication.Domain.Entities;

namespace ZawatSys.MicroService.Communication.Infrastructure.Data.Configuration;

public sealed class ConversationChannelEndpointConfiguration : IEntityTypeConfiguration<ConversationChannelEndpoint>
{
    public void Configure(EntityTypeBuilder<ConversationChannelEndpoint> builder)
    {
        builder.ToTable("ConversationChannelEndpoints");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .IsRequired()
            .HasColumnType("uuid");

        builder.Property(e => e.Channel)
            .IsRequired()
            .HasMaxLength(64)
            .HasColumnType("character varying(64)");

        builder.Property(e => e.Provider)
            .IsRequired()
            .HasMaxLength(128)
            .HasColumnType("character varying(128)");

        builder.Property(e => e.EndpointKey)
            .IsRequired()
            .HasMaxLength(256)
            .HasColumnType("character varying(256)");

        builder.Property(e => e.DisplayName)
            .IsRequired()
            .HasMaxLength(256)
            .HasColumnType("character varying(256)");

        builder.Property(e => e.WebhookSecretRef)
            .HasMaxLength(512)
            .HasColumnType("character varying(512)");

        builder.Property(e => e.AccessTokenSecretRef)
            .HasMaxLength(512)
            .HasColumnType("character varying(512)");

        builder.Property(e => e.VerificationSecretRef)
            .HasMaxLength(512)
            .HasColumnType("character varying(512)");

        builder.Property(e => e.InboundEnabled)
            .IsRequired()
            .HasColumnType("boolean")
            .HasDefaultValue(true);

        builder.Property(e => e.OutboundEnabled)
            .IsRequired()
            .HasColumnType("boolean")
            .HasDefaultValue(true);

        builder.Property(e => e.IsDefault)
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

        builder.HasIndex(e => new { e.TenantId, e.Channel, e.EndpointKey })
            .IsUnique()
            .HasDatabaseName("UX_ConversationChannelEndpoints_Tenant_Channel_EndpointKey");

        builder.HasIndex(e => new { e.TenantId, e.Channel, e.InboundEnabled })
            .HasDatabaseName("IX_ConversationChannelEndpoints_Tenant_Channel_InboundEnabled");

        builder.HasIndex(e => new { e.TenantId, e.Channel, e.IsDefault })
            .HasDatabaseName("IX_ConversationChannelEndpoints_Tenant_Channel_IsDefault");

        builder.HasIndex(e => new { e.TenantId, e.IsDeleted })
            .HasDatabaseName("IX_ConversationChannelEndpoints_Tenant_IsDeleted");

        builder.HasQueryFilter(e => !e.IsDeleted);

        builder.Ignore(e => e.DomainEvents);
    }
}
