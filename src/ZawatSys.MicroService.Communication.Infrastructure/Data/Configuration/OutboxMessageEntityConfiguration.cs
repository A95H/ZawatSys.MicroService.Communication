using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ZawatSys.MicroLib.Shared.Common.Models;

namespace ZawatSys.MicroService.Communication.Infrastructure.Data.Configuration;

public sealed class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        var contentComparer = new ValueComparer<JsonDocument>(
            (left, right) =>
                string.Equals(
                    left == null ? null : left.RootElement.GetRawText(),
                    right == null ? null : right.RootElement.GetRawText(),
                    StringComparison.Ordinal),
            value => value == null ? 0 : value.RootElement.GetRawText().GetHashCode(StringComparison.Ordinal),
            value => value == null ? null! : JsonDocument.Parse(value.RootElement.GetRawText()));

        builder.ToTable("OutboxMessages");

        builder.HasKey(om => om.Id);

        builder.Property(om => om.Type)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(om => om.Content)
            .IsRequired()
            .HasConversion(
                value => value.RootElement.GetRawText(),
                value => JsonDocument.Parse(value))
            .Metadata.SetValueComparer(contentComparer);

        builder.Property(om => om.Content)
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
