using OpenAI.Interfaces;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels;
using AiMarketplaceApi.Models;
using AiMarketplaceApi.Data;
using System.Text.RegularExpressions;
using System;
using System.Linq;

namespace AiMarketplaceApi.Services;

public class VendorService : IVendorService
{
    private readonly IOpenAIService _openAIService;

    public VendorService(IOpenAIService openAIService)
    {
        _openAIService = openAIService;
    }

    public async Task<(string response, bool isAccepted, decimal? offeredPrice, decimal? vendorOfferedPrice)> GetVendorResponseAsync(
        string userMessage, 
        Level level, 
        decimal? lastOfferedPrice,
        decimal? vendorOfferedPrice,
        List<AiMarketplaceApi.Models.ChatMessage> conversationHistory)
    {
        LogDebugInfo(level, lastOfferedPrice, vendorOfferedPrice, conversationHistory);

        conversationHistory ??= new List<AiMarketplaceApi.Models.ChatMessage>();
        var conversationContext = BuildConversationContext(conversationHistory);

        decimal? extractedPrice = ExtractPrice(userMessage);

        if (IsFirstMessage(conversationHistory))
        {
            var greetingPrompt = CreateGreetingPrompt(level, userMessage);
            return await GenerateResponse(greetingPrompt, userMessage, extractedPrice, false, null);
        }

        var basePrompt = CreateBasePrompt(level, vendorOfferedPrice, conversationContext, userMessage);

        if (!extractedPrice.HasValue)
        {
            var systemPrompt = CreateNegotiationPrompt(basePrompt, level, vendorOfferedPrice, extractedPrice);
            return await GenerateResponse(systemPrompt, userMessage, null, false, null);
        }

        return await HandlePriceOffer(extractedPrice.Value, level, vendorOfferedPrice, lastOfferedPrice, basePrompt, userMessage);
    }

    private void LogDebugInfo(Level level, decimal? lastOfferedPrice, decimal? vendorOfferedPrice, List<AiMarketplaceApi.Models.ChatMessage> conversationHistory)
    {
        Console.WriteLine($"Level: {level?.Id}"); // Debug log
        Console.WriteLine($"ConversationHistory Count: {conversationHistory?.Count}"); // Debug log
        Console.WriteLine($"Vendor Last Offered: {vendorOfferedPrice}"); // Debug log
        Console.WriteLine($"Buyer Last Offered: {lastOfferedPrice}"); // Debug log
    }

    private string BuildConversationContext(List<AiMarketplaceApi.Models.ChatMessage> conversationHistory)
    {
        return string.Join("\n", conversationHistory
            .Where(m => m != null) // Filter out any null messages
            .OrderBy(m => m.Timestamp)
            .Select(m => $"{(m.IsUser ? "Buyer" : "Vendor")}: {m.Content ?? ""}"));
    }

    private decimal? ExtractPrice(string message)
    {
        var patterns = new[]
        {
            @"\$(\d+(?:\.\d{2})?)",           // $400 or $400.00
            @"(\d+(?:\.\d{2})?)\s*dollars?"   // 400 dollars or 400.00 dollars
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(message, pattern);
            if (match.Success && decimal.TryParse(match.Groups[1].Value, out decimal price))
            {
                return price;
            }
        }

        return null;
    }

    private bool IsFirstMessage(List<AiMarketplaceApi.Models.ChatMessage> conversationHistory)
    {
        return conversationHistory.Count(m => !m.IsUser) == 0;
    }

    private string CreateGreetingPrompt(Level level, string userMessage)
    {
        return $@"You are a vendor selling: {level.ProductDescription}
            Personality: {level.VendorPersonality}
            Initial asking price: ${level.InitialPrice}

            This is your FIRST message to the buyer. Keep it SHORT and FRIENDLY.
            Briefly introduce yourself and the item ONCE.
            Don't repeat the full product description.
            Be concise and natural - just a quick greeting under 30 words.";
    }

    private string CreateBasePrompt(Level level, decimal? vendorOfferedPrice, string conversationContext, string userMessage)
    {
        return $@"You are a vendor selling: {level.ProductDescription}
            Personality: {level.VendorPersonality}
            Initial asking price: ${level.InitialPrice}
            Minimum acceptable price (target): ${level.TargetPrice}
            Your last counter-offer: {(vendorOfferedPrice.HasValue ? $"${vendorOfferedPrice}" : "None")}

            IMPORTANT STYLE GUIDELINES:
            1. Keep responses SHORT (30-50 words maximum)
            2. NEVER repeat the full product description - refer to it briefly as 'this item' or 'it'
            3. Be natural and concise, like a real person texting
            4. Avoid business-speak or overly formal language
            5. No need to say hello in every message
            6. If the buyer is getting closer to an acceptable price, show more interest.
            Previous conversation:
            {conversationContext}

            Current buyer message: {userMessage}";
    }

    private string CreateNegotiationPrompt(string basePrompt, Level level, decimal? vendorOfferedPrice, decimal? extractedPrice)
    {
        return $@"{basePrompt}

IMPORTANT NEGOTIATION RULES:
1. NEVER go below your target price of ${level.TargetPrice}
2. NEVER OFFER a price above your previous counter-offer ${vendorOfferedPrice} (if you made one)
3. ACCEPT AN OFFER of any price at or above ${level.InitialPrice} or ${vendorOfferedPrice} (if you made one)
4. If the buyer mentions your exact counter-offer price or says they agree/accept, IMMEDIATELY ACCEPT THE DEAL
5. Your negotiation strategy should reflect your personality: {level.VendorPersonality}
6. Maintain conversation context and reference previous offers
7. If accepting an offer, clearly state 'DEAL ACCEPTED' at the end";
    }

    private async Task<(string response, bool isAccepted, decimal? offeredPrice, decimal? vendorOfferedPrice)> HandlePriceOffer(
        decimal offeredPrice, 
        Level level, 
        decimal? vendorOfferedPrice, 
        decimal? lastOfferedPrice, 
        string basePrompt, 
        string userMessage)
    {
        if (offeredPrice > level.InitialPrice)
        {
            var acceptancePrompt = CreateAcceptancePrompt(basePrompt, offeredPrice, "more than your asking price");
            return await GenerateResponse(acceptancePrompt, userMessage, offeredPrice, true, null);
        }

        if (offeredPrice == level.InitialPrice)
        {
            var acceptancePrompt = CreateAcceptancePrompt(basePrompt, offeredPrice, "exactly your asking price");
            return await GenerateResponse(acceptancePrompt, userMessage, offeredPrice, true, null);
        }

        decimal counterOffer = CalculateCounterOffer(offeredPrice, level, vendorOfferedPrice, lastOfferedPrice);
        var negotiationPrompt = CreateNegotiationPrompt(basePrompt, level, vendorOfferedPrice, offeredPrice);
        return await GenerateResponse(negotiationPrompt, userMessage, offeredPrice, false, counterOffer);
    }

    private string CreateAcceptancePrompt(string basePrompt, decimal offeredPrice, string condition)
    {
        return $@"{basePrompt}

The buyer offered {condition}.
Briefly accept with a hint they could have paid less.
KEEP IT UNDER 30 WORDS and end with 'DEAL ACCEPTED'";
    }

    private decimal CalculateCounterOffer(decimal offeredPrice, Level level, decimal? vendorOfferedPrice, decimal? lastOfferedPrice)
    {
        decimal counterOffer;
        if (vendorOfferedPrice.HasValue)
        {
            decimal maxCounterOffer = vendorOfferedPrice.Value;
            counterOffer = CalculateNewCounterOffer(offeredPrice, level, lastOfferedPrice, maxCounterOffer);
        }
        else
        {
            counterOffer = CalculateFirstCounterOffer(offeredPrice, level);
        }
        return Math.Max(counterOffer, level.TargetPrice);
    }

    private decimal CalculateNewCounterOffer(decimal offeredPrice, Level level, decimal? lastOfferedPrice, decimal maxCounterOffer)
    {
        if (lastOfferedPrice.HasValue && lastOfferedPrice.Value < offeredPrice)
        {
            decimal middleGround = Math.Round((offeredPrice + maxCounterOffer) / 2, 2);
            decimal targetDifference = middleGround - level.TargetPrice;
            decimal priceRange = level.InitialPrice - level.TargetPrice;
            decimal percentOfRange = (targetDifference / priceRange) * 100;

            if (percentOfRange <= 10)
            {
                return level.TargetPrice;
            }
            else
            {
                return Math.Max(middleGround, level.TargetPrice);
            }
        }
        else
        {
            return CalculateCounterOfferBasedOnPersonality(level, maxCounterOffer);
        }
    }

    private decimal CalculateCounterOfferBasedOnPersonality(Level level, decimal maxCounterOffer)
    {
        if (level.VendorPersonality.ToLower().Contains("eager") || 
            level.VendorPersonality.ToLower().Contains("need") ||
            level.VendorPersonality.ToLower().Contains("desperate"))
        {
            return Math.Max(Math.Round(maxCounterOffer - (maxCounterOffer - level.TargetPrice) * 0.2m, 2), level.TargetPrice);
        }
        else if (level.VendorPersonality.ToLower().Contains("stubborn") ||
                level.VendorPersonality.ToLower().Contains("firm") ||
                level.VendorPersonality.ToLower().Contains("confident"))
        {
            return Math.Max(Math.Round(maxCounterOffer - (maxCounterOffer - level.TargetPrice) * 0.05m, 2), level.TargetPrice);
        }
        else
        {
            return Math.Max(Math.Round(maxCounterOffer - (maxCounterOffer - level.TargetPrice) * 0.1m, 2), level.TargetPrice);
        }
    }

    private decimal CalculateFirstCounterOffer(decimal offeredPrice, Level level)
    {
        decimal percentOfInitial = offeredPrice / level.InitialPrice * 100;

        if (percentOfInitial >= 80)
        {
            return Math.Max(Math.Round((level.TargetPrice + offeredPrice) / 2, 2), level.TargetPrice);
        }
        else if (percentOfInitial >= 50)
        {
            return CalculateCounterOfferBasedOnPersonality(level, level.InitialPrice);
        }
        else
        {
            if (level.VendorPersonality.ToLower().Contains("patient") || 
                level.VendorPersonality.ToLower().Contains("kind") ||
                level.VendorPersonality.ToLower().Contains("helpful"))
            {
                return Math.Max(Math.Round(level.InitialPrice - (level.InitialPrice - level.TargetPrice) * 0.3m, 2), level.TargetPrice);
            }
            else
            {
                return level.InitialPrice;
            }
        }
    }

    private async Task<(string response, bool isAccepted, decimal? offeredPrice, decimal? vendorOfferedPrice)> GenerateResponse(
        string prompt, 
        string userMessage, 
        decimal? offeredPrice, 
        bool isAccepted, 
        decimal? vendorOfferedPrice)
    {
        var response = await _openAIService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = new List<OpenAI.ObjectModels.RequestModels.ChatMessage>
            {
                OpenAI.ObjectModels.RequestModels.ChatMessage.FromSystem(prompt),
                OpenAI.ObjectModels.RequestModels.ChatMessage.FromUser(userMessage)
            },
            Model = OpenAI.ObjectModels.Models.Gpt_3_5_Turbo
        });

        var responseText = response.Choices.First().Message.Content ?? "No response";
        return (responseText + (isAccepted ? "\n\nDEAL ACCEPTED" : ""), isAccepted, offeredPrice, vendorOfferedPrice);
    }

    public async Task<string> AcceptBidPriceAsync(decimal acceptedPrice, Level level, List<AiMarketplaceApi.Models.ChatMessage> conversationHistory)
    {
        var conversationContext = BuildConversationContext(conversationHistory);

        var acceptancePrompt = $@"You are a vendor selling: {level.ProductDescription}
            Personality: {level.VendorPersonality}
            The buyer has accepted your offer of ${acceptedPrice}.
            Previous conversation:
            {conversationContext}
            Respond with brief enthusiasm appropriate to your personality.
            KEEP IT UNDER 30 WORDS and end with 'DEAL ACCEPTED'";

        var response = await _openAIService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = new List<OpenAI.ObjectModels.RequestModels.ChatMessage>
            {
                OpenAI.ObjectModels.RequestModels.ChatMessage.FromSystem(acceptancePrompt)
            },
            Model = OpenAI.ObjectModels.Models.Gpt_3_5_Turbo
        });

        var responseText = response.Choices.First().Message.Content ?? "No response";
        return responseText + "\n\nDEAL ACCEPTED";
    }
} 