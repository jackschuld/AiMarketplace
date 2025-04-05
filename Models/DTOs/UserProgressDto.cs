using AiMarketplaceApi.Models;

namespace AiMarketplaceApi.Models.DTOs;

public class UserProgressDto
{
    public bool IsCompleted { get; set; }
    public int? Stars { get; set; }
    public int Points { get; set; }
    public decimal? LastOfferedPrice { get; set; }
}
