using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AiMarketplaceApi.Models;

public class ChatMessage
{
    public int Id { get; set; }
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    [Required]
    public bool IsUser { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public int UserLevelId { get; set; }
    
    [JsonIgnore]
    public UserLevel UserLevel { get; set; } = null!;
} 