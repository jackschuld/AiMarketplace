namespace AiMarketplaceApi.Models.DTOs;

public class ChatMessageDto
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsUser { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsAccepted { get; set; }
    public int? Stars { get; set; }
}

// Also add this DTO for sending messages
public class SendMessageDto
{
    public string Content { get; set; } = string.Empty;
}
