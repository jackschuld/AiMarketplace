using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AiMarketplaceApi.Data;
using AiMarketplaceApi.Models;
using AiMarketplaceApi.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using AiMarketplaceApi.Models.DTOs;

namespace AiMarketplaceApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GameController : ControllerBase
{
    private readonly GameDbContext _context;
    private readonly IVendorService _vendorService;
    private readonly IPointsCalculator _pointsCalculator;

    public GameController(GameDbContext context, IVendorService vendorService, IPointsCalculator pointsCalculator)
    {
        _context = context;
        _vendorService = vendorService;
        _pointsCalculator = pointsCalculator;
    }

    [HttpGet("levels")]
    public async Task<ActionResult<IEnumerable<LevelDto>>> GetLevels()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        var user = await _context.Users
            .Include(u => u.UserLevels)
            .FirstAsync(u => u.Id == userId);

        var levels = await _context.Levels
            .Include(l => l.UserLevels.Where(ul => ul.UserId == userId))
            .ToListAsync();

        return levels.Select(l => new LevelDto
        {
            Id = l.Id,
            Name = l.Name,
            InitialPrice = l.InitialPrice,
            TargetPrice = l.TargetPrice,
            ProductDescription = l.ProductDescription,
            VendorPersonality = l.VendorPersonality,
            RequiredPoints = l.RequiredPoints,
            UserProgress = l.UserLevels.FirstOrDefault() == null ? null : new UserProgressDto
            {
                IsCompleted = l.UserLevels.First().IsCompleted,
                Stars = l.UserLevels.First().Stars,
                Points = l.UserLevels.First().Points,
                LastOfferedPrice = l.UserLevels.First().LastOfferedPrice
            }
        }).ToList();
    }

    [HttpGet("levels/{id}")]
    public async Task<ActionResult<Level>> GetLevel(int id)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        
        var level = await _context.Levels
            .Include(l => l.UserLevels.Where(ul => ul.UserId == userId))
                .ThenInclude(ul => ul.ChatMessages)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (level == null)
        {
            return NotFound();
        }

        return level;
    }

    [HttpPost("levels")]
    public async Task<ActionResult<Level>> CreateLevel(Level level)
    {
        level.UserLevels = new List<UserLevel>();
        
        _context.Levels.Add(level);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetLevel), new { id = level.Id }, level);
    }

    [HttpPost("levels/{levelId}/messages")]
    public async Task<ActionResult<ChatMessageDto>> SendMessage(int levelId, [FromBody] SendMessageDto messageDto)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        
        var userLevel = await _context.UserLevels
            .Include(ul => ul.Level)
            .Include(ul => ul.ChatMessages)
            .FirstOrDefaultAsync(ul => ul.LevelId == levelId && ul.UserId == userId);

        if (userLevel == null)
        {
            var level = await _context.Levels.FindAsync(levelId);
            if (level == null)
            {
                return NotFound($"Level {levelId} not found.");
            }

            userLevel = new UserLevel
            {
                UserId = userId,
                LevelId = levelId,
                Level = level,
                IsCompleted = false,
                Points = 0,
                ChatMessages = new List<ChatMessage>()
            };
            _context.UserLevels.Add(userLevel);
            await _context.SaveChangesAsync();
        }

        var userMessage = new ChatMessage
        {
            Content = messageDto.Content,
            IsUser = true,
            Timestamp = DateTime.UtcNow,
            UserLevelId = userLevel.Id
        };
        _context.ChatMessages.Add(userMessage);

        (string vendorResponse, bool isAccepted, decimal? offeredPrice, decimal? vendorOfferedPrice) = await _vendorService
            .GetVendorResponseAsync(
                messageDto.Content, 
                userLevel.Level, 
                userLevel.LastOfferedPrice,
                userLevel.VendorOfferedPrice,
                userLevel.ChatMessages.ToList()
            );

        var vendorMessage = new ChatMessage
        {
            Content = vendorResponse,
            IsUser = false,
            Timestamp = DateTime.UtcNow,
            UserLevelId = userLevel.Id
        };
        _context.ChatMessages.Add(vendorMessage);

        if (offeredPrice.HasValue)
        {
            userLevel.LastOfferedPrice = offeredPrice.Value;
        }

        if (vendorOfferedPrice.HasValue)
        {
            userLevel.VendorOfferedPrice = vendorOfferedPrice.Value;
        }

        if (isAccepted)
        {
            userLevel.IsCompleted = true;
            
            // Different star ratings based on the offered price
            if (offeredPrice > userLevel.Level.InitialPrice)
            {
                // Overpaid - 0 stars
                userLevel.Stars = 0;
                userLevel.Points = 0;
            }
            else if (offeredPrice == userLevel.Level.InitialPrice)
            {
                // Paid full price - 1 star
                userLevel.Stars = 1;
                userLevel.Points = 10;
            }
            else if (offeredPrice == userLevel.Level.TargetPrice)
            {
                // Paid exactly target price - 3 stars (perfect negotiation)
                userLevel.Stars = 3;
                userLevel.Points = 30;
            }
            else
            {
                // Negotiated a price between target and initial
                var priceRange = userLevel.Level.InitialPrice - userLevel.Level.TargetPrice;
                var priceAboveTarget = offeredPrice.Value - userLevel.Level.TargetPrice;
                var percentageOfRange = (priceAboveTarget / priceRange) * 100;
                
                userLevel.Stars = percentageOfRange switch
                {
                    <= 10 => 3,  // Within 10% of target price
                    <= 30 => 2,  // Within 30% of target price
                    _ => 1       // Any successful negotiation
                };

                userLevel.Points = userLevel.Stars.Value * 10; // 10 points per star
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new ChatMessageDto
        {
            Id = vendorMessage.Id,
            Content = vendorResponse,
            IsUser = false,
            Timestamp = vendorMessage.Timestamp,
            IsAccepted = isAccepted,
            Stars = isAccepted ? userLevel.Stars : null
        });
    }

    [HttpGet("levels/{levelId}/messages")]
    public async Task<ActionResult<IEnumerable<ChatMessageDto>>> GetMessages(int levelId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        
        var userLevel = await _context.UserLevels
            .Include(ul => ul.ChatMessages)
            .FirstOrDefaultAsync(ul => ul.LevelId == levelId && ul.UserId == userId);

        if (userLevel == null)
        {
            return new List<ChatMessageDto>();
        }

        return userLevel.ChatMessages
            .OrderBy(m => m.Timestamp)
            .Select(m => new ChatMessageDto
            {
                Id = m.Id,
                Content = m.Content,
                IsUser = m.IsUser,
                Timestamp = m.Timestamp,
                IsAccepted = false, // Only set true for the final acceptance message
                Stars = null // Only set for the final acceptance message
            })
            .ToList();
    }

    [HttpPut("levels/{id}")]
    public async Task<ActionResult<Level>> UpdateLevel(int id, Level levelUpdate)
    {
        var level = await _context.Levels.FindAsync(id);
        
        if (level == null)
        {
            return NotFound();
        }

        level.Name = levelUpdate.Name;
        level.InitialPrice = levelUpdate.InitialPrice;
        level.TargetPrice = levelUpdate.TargetPrice;
        level.ProductDescription = levelUpdate.ProductDescription;
        level.VendorPersonality = levelUpdate.VendorPersonality;
        level.RequiredPoints = levelUpdate.RequiredPoints;

        await _context.SaveChangesAsync();

        return level;
    }
} 