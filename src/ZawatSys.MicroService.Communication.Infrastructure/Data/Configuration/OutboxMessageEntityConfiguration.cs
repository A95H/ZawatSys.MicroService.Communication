using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZawatSys.MicroLib.Shared.Common.Models;

namespace ZawatSys.MicroService.Communication.Infrastructure.Data.Configuration;

public sealed class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(om => om.Id);

        builder.Property(om => om.Type)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(om => om.Content)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(om => om.OccurredOn)
            .IsRequired();

        builder.Property(om => om.Sent)
            .IsRequired();

        builder.Property(om => om.RetryCount)
            .IsRequired();

        builder.Property(om => om.Error)
            .HasMaxLength(2000);

        builder.Property(om => om.TenantId)
            .IsRequired();

        builder.HasIndex(om => new { om.Sent, om.OccurredOn })
            .HasDatabaseName("IX_OutboxMessages_Sent_OccurredOn");

        builder.Ignore(om => om.DomainEvents);
    }
}
