// Models/UserLevel.cs
namespace AiMarketplaceApi.Models;

public class UserLevel
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int LevelId { get; set; }
    public Level Level { get; set; } = null!;
    public bool IsCompleted { get; set; }
    public int? Stars { get; set; }
    public decimal? LastOfferedPrice { get; set; }
    public decimal? VendorOfferedPrice { get; set; } // Vendor's counter-offer price
    public int Points { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    
    // Navigation property for chat history
    public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
}