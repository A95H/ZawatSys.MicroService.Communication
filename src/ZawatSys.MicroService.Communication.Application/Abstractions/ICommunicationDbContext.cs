using Microsoft.EntityFrameworkCore;
using ZawatSys.MicroLib.Communication.Domain.Entities;
using ZawatSys.MicroLib.Shared.Common.Models;

namespace ZawatSys.MicroService.Communication.Application.Abstractions;

public interface ICommunicationDbContext
{
    DbSet<ConversationChannelEndpoint> ConversationChannelEndpoints { get; }

    DbSet<ExternalIdentityBinding> ExternalIdentityBindings { get; }

    DbSet<ConversationSession> ConversationSessions { get; }

    DbSet<ConversationControl> ConversationControls { get; }

    DbSet<ConversationControlTransition> ConversationControlTransitions { get; }

    DbSet<ConversationAssignment> ConversationAssignments { get; }

    DbSet<ConversationMessage> ConversationMessages { get; }

    DbSet<MessageDeliveryAttempt> MessageDeliveryAttempts { get; }

    DbSet<OutboxMessage> OutboxMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
