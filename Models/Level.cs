using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AiMarketplaceApi.Models;

public class Level
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    public decimal InitialPrice { get; set; }
    
    [Required]
    public decimal TargetPrice { get; set; }
    
    [Required]
    public string ProductDescription { get; set; } = string.Empty;
    
    [Required]
    public string VendorPersonality { get; set; } = string.Empty;
    
    [Required]
    public int RequiredPoints { get; set; }  // Points needed to unlock this level
    
    // Navigation property for EF relationships
    public List<UserLevel> UserLevels { get; set; } = new();
}