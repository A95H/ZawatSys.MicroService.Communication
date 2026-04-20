using Microsoft.EntityFrameworkCore;
using ZawatSys.MicroLib.Communication.Domain.Entities;
using ZawatSys.MicroLib.Shared.Common.Models;

namespace ZawatSys.MicroService.Communication.Infrastructure.Data;

public sealed class CommunicationDbContext : DbContext
{
    public CommunicationDbContext(DbContextOptions<CommunicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ConversationChannelEndpoint> ConversationChannelEndpoints => Set<ConversationChannelEndpoint>();
    public DbSet<ExternalIdentityBinding> ExternalIdentityBindings => Set<ExternalIdentityBinding>();
    public DbSet<ConversationSession> ConversationSessions => Set<ConversationSession>();
    public DbSet<ConversationControl> ConversationControls => Set<ConversationControl>();
    public DbSet<ConversationControlTransition> ConversationControlTransitions => Set<ConversationControlTransition>();
    public DbSet<ConversationAssignment> ConversationAssignments => Set<ConversationAssignment>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();
    public DbSet<MessageDeliveryAttempt> MessageDeliveryAttempts => Set<MessageDeliveryAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommunicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
