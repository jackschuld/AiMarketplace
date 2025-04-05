using AiMarketplaceApi.Models;
using BCrypt.Net;

namespace AiMarketplaceApi.Data.Seeders;

public static class UserSeeder
{
    public static async Task SeedTestUser(GameDbContext context)
    {
        if (!context.Users.Any(u => u.Email == "schuldjack@gmail.com"))
        {
            var testUser = new User
            {
                Email = "schuldjack@gmail.com",
                Username = "jack",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Qaz-11-wsx"),
                TotalPoints = 0
            };

            context.Users.Add(testUser);
            await context.SaveChangesAsync();
        }
    }
}
