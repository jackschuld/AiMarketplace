using AiMarketplaceApi.Models;

namespace AiMarketplaceApi.Models.DTOs;

public class LevelDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal InitialPrice { get; set; }
    public decimal TargetPrice { get; set; }
    public string ProductDescription { get; set; } = string.Empty;
    public string VendorPersonality { get; set; } = string.Empty;
    public int RequiredPoints { get; set; }
    public UserProgressDto? UserProgress { get; set; }
}

