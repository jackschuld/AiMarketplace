using AiMarketplaceApi.Models;

namespace AiMarketplaceApi.Data.Seeders;

public static class LevelSeeder
{
    public static async Task SeedLevels(GameDbContext context)
    {
        if (!context.Levels.Any())
        {
            var levels = new List<Level>
            {
                new Level
                {
                    Name = "Vintage Camera",
                    InitialPrice = 500.00M,
                    TargetPrice = 400.00M,
                    ProductDescription = "A rare 1960s Leica M3 camera in excellent condition. Features a pristine lens and all original parts.",
                    VendorPersonality = "A passionate photography enthusiast who knows the true value of vintage cameras but needs quick cash for a new investment.",
                    RequiredPoints = 0  // First level, no points required
                },
                new Level
                {
                    Name = "Guitar strings",
                    InitialPrice = 45.00M,
                    TargetPrice = 25.00M,
                    ProductDescription = "An opened package of 9 out of 12 strings.",
                    VendorPersonality = "Naive and lazy seller who is a bit immature, but has no leverage and doesn't care about the strings anymore since he quit learning the guitar.",
                    RequiredPoints = 0
                },
                new Level
                {
                    Name = "Vintage Guitar",
                    InitialPrice = 1000.00M,
                    TargetPrice = 800.00M,
                    ProductDescription = "A 1970s Fender Stratocaster in sunburst finish. Some wear but excellent playability and tone.",
                    VendorPersonality = "A musician who loves this guitar but needs money for new recording equipment. Knowledgeable about vintage instruments and emotional about parting with it.",
                    RequiredPoints = 30  // Requires points from earlier levels
                },
                new Level
                {
                    Name = "Xbox One",
                    InitialPrice = 250.00M,
                    TargetPrice = 180.00M,
                    ProductDescription = "An older generation Xbox for gaming. Can play most modern games but will be slower than modern gens.",
                    VendorPersonality = "A gamer who's upgrading to the newest console. Knows the market value but is eager to sell.",
                    RequiredPoints = 60
                }
            };

            context.Levels.AddRange(levels);
            await context.SaveChangesAsync();
        }
    }
}
