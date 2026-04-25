namespace ZawatSys.MicroService.Communication.Application.Control;

public static class CommunicationPermissions
{
    public const string ReadConversations = "communication.conversations.read";
    public const string ReplyToConversation = "communication.messages.reply";
    public const string TakeOverConversation = "communication.control.takeover";
    public const string PauseAiConversation = "communication.control.pause";
    public const string ResumeAiConversation = "communication.control.resume";
    public const string ReleaseConversation = "communication.control.release";
    public const string ReassignConversation = "communication.control.reassign";
    public const string ResolveConversation = "communication.control.resolve";
    public const string ReopenConversation = "communication.control.reopen";

    public static IReadOnlyList<string> GetAll() =>
    [
        ReadConversations,
        ReplyToConversation,
        TakeOverConversation,
        PauseAiConversation,
        ResumeAiConversation,
        ReleaseConversation,
        ReassignConversation,
        ResolveConversation,
        ReopenConversation
    ];
}
