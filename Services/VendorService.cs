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
        Console.WriteLine($"Level: {level?.Id}"); // Debug log
        Console.WriteLine($"ConversationHistory Count: {conversationHistory?.Count}"); // Debug log
        Console.WriteLine($"Vendor Last Offered: {vendorOfferedPrice}"); // Debug log
        Console.WriteLine($"Buyer Last Offered: {lastOfferedPrice}"); // Debug log

        conversationHistory ??= new List<AiMarketplaceApi.Models.ChatMessage>();
        
        // Build conversation history for context
        var conversationContext = string.Join("\n", conversationHistory
            .Where(m => m != null) // Filter out any null messages
            .OrderBy(m => m.Timestamp)
            .Select(m => $"{(m.IsUser ? "Buyer" : "Vendor")}: {m.Content ?? ""}"));
        
        Console.WriteLine($"Conversation Context Length: {conversationContext.Length}"); // Debug log
        
        // Extract price from message
        var priceMatch = Regex.Match(userMessage, @"\$?(\d+(?:\.\d{2})?)", RegexOptions.IgnoreCase);
        decimal? extractedPrice = null;
        
        if (priceMatch.Success)
        {
            extractedPrice = decimal.Parse(priceMatch.Groups[1].Value);
            Console.WriteLine($"Extracted Price: {extractedPrice}"); // Debug log
        }
        
        // Check if the player is accepting the vendor's previous offer based on context and price
        bool isAcceptingVendorOffer = false;
        if (vendorOfferedPrice.HasValue && 
            (extractedPrice.HasValue && Math.Abs(extractedPrice.Value - vendorOfferedPrice.Value) < 0.01m ||
             userMessage.ToLower().Contains("yes") || 
             userMessage.ToLower().Contains("ok") || 
             userMessage.ToLower().Contains("deal") || 
             userMessage.ToLower().Contains("accept") || 
             userMessage.ToLower().Contains("agree") || 
             userMessage.ToLower().Contains("sounds fair") || 
             userMessage.ToLower().Contains("i'll take it") ||
             userMessage.ToLower().Contains("that works") ||
             (userMessage.ToLower().Contains(vendorOfferedPrice.Value.ToString()) && 
              (userMessage.ToLower().Contains("will do") || 
               userMessage.ToLower().Contains("i can do") || 
               userMessage.ToLower().Contains("let's do")))))
        {
            Console.WriteLine("Detected acceptance of vendor offer"); // Debug log
            isAcceptingVendorOffer = true;
        }
        
        // At the beginning of your GetVendorResponseAsync method
        bool isFirstMessage = conversationHistory.Count(m => !m.IsUser) == 0;

        if (isFirstMessage)
        {
            var greetingPrompt = $@"You are a vendor selling: {level.ProductDescription}
Personality: {level.VendorPersonality}
Initial asking price: ${level.InitialPrice}

This is your FIRST message to the buyer. Keep it SHORT and FRIENDLY.
Briefly introduce yourself and the item ONCE.
Don't repeat the full product description.
Be concise and natural - just a quick greeting under 30 words.";

            var response = await _openAIService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
            {
                Messages = new List<OpenAI.ObjectModels.RequestModels.ChatMessage>
                {
                    OpenAI.ObjectModels.RequestModels.ChatMessage.FromSystem(greetingPrompt),
                    OpenAI.ObjectModels.RequestModels.ChatMessage.FromUser(userMessage)
                },
                Model = OpenAI.ObjectModels.Models.Gpt_3_5_Turbo
            });
            return (response.Choices.First().Message.Content ?? "No response", false, extractedPrice, null);
        }

        // Update the base prompt to encourage brevity
        var basePrompt = $@"You are a vendor selling: {level.ProductDescription}
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

Previous conversation:
{conversationContext}

Current buyer message: {userMessage}";

        // If player is accepting the vendor's offer
        if (isAcceptingVendorOffer)
        {
            var acceptancePrompt = $@"{basePrompt}

The buyer has ACCEPTED your counter-offer!
Respond with brief enthusiasm appropriate to your personality.
KEEP IT UNDER 30 WORDS and end with 'DEAL ACCEPTED'";
            
            var response = await _openAIService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
            {
                Messages = new List<OpenAI.ObjectModels.RequestModels.ChatMessage>
                {
                    OpenAI.ObjectModels.RequestModels.ChatMessage.FromSystem(acceptancePrompt),
                    OpenAI.ObjectModels.RequestModels.ChatMessage.FromUser(userMessage)
                },
                Model = OpenAI.ObjectModels.Models.Gpt_3_5_Turbo
            });
            var responseText = response.Choices.First().Message.Content ?? "No response";
            return (responseText + "\n\nDEAL ACCEPTED", true, vendorOfferedPrice.Value, null);
        }

        // Regular negotiation system prompt
        var systemPrompt = $@"{basePrompt}

IMPORTANT NEGOTIATION RULES:
1. NEVER go below your target price of ${level.TargetPrice}
2. NEVER OFFER a price above your previous counter-offer ${vendorOfferedPrice} (if you made one)
3. ACCEPT AN OFFER of any price at or above ${level.InitialPrice} or ${vendorOfferedPrice} (if you made one)
4. If the buyer mentions your exact counter-offer price or says they agree/accept, IMMEDIATELY ACCEPT THE DEAL
5. Your negotiation strategy should reflect your personality: {level.VendorPersonality}
6. Maintain conversation context and reference previous offers
7. If accepting an offer, clearly state 'DEAL ACCEPTED' at the end";

        // If no price was offered, focus on engaging in negotiation
        if (!extractedPrice.HasValue)
        {
            var response = await _openAIService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
            {
                Messages = new List<OpenAI.ObjectModels.RequestModels.ChatMessage>
                {
                    OpenAI.ObjectModels.RequestModels.ChatMessage.FromSystem(systemPrompt),
                    OpenAI.ObjectModels.RequestModels.ChatMessage.FromUser(userMessage)
                },
                Model = OpenAI.ObjectModels.Models.Gpt_3_5_Turbo
            });
            return (response.Choices.First().Message.Content ?? "No response", false, null, null);
        }

        decimal offeredPrice = extractedPrice.Value;

        // Handle price above initial price - accept but give 0 stars for overpaying
        if (offeredPrice > level.InitialPrice)
        {
            var acceptancePrompt = $@"{basePrompt}

The buyer offered more than your asking price.
Briefly accept with a hint they could have paid less.
KEEP IT UNDER 30 WORDS and end with 'DEAL ACCEPTED'";
            
            var response = await _openAIService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
            {
                Messages = new List<OpenAI.ObjectModels.RequestModels.ChatMessage>
                {
                    OpenAI.ObjectModels.RequestModels.ChatMessage.FromSystem(acceptancePrompt),
                    OpenAI.ObjectModels.RequestModels.ChatMessage.FromUser(userMessage)
                },
                Model = OpenAI.ObjectModels.Models.Gpt_3_5_Turbo
            });
            var responseText = response.Choices.First().Message.Content ?? "No response";
            return (responseText + "\n\nDEAL ACCEPTED", true, offeredPrice, null);
        }

        // Accept the initial price exactly
        if (offeredPrice == level.InitialPrice)
        {
            var acceptancePrompt = $@"You are a vendor selling: {level.ProductDescription}
Personality: {level.VendorPersonality}
Initial asking price: ${level.InitialPrice}

The buyer has offered exactly your asking price of ${level.InitialPrice}.
Accept happily, but hint that they might have been able to negotiate a better deal.
End with 'DEAL ACCEPTED'";
            
            var response = await _openAIService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
            {
                Messages = new List<OpenAI.ObjectModels.RequestModels.ChatMessage>
                {
                    OpenAI.ObjectModels.RequestModels.ChatMessage.FromSystem(acceptancePrompt),
                    OpenAI.ObjectModels.RequestModels.ChatMessage.FromUser(userMessage)
                },
                Model = OpenAI.ObjectModels.Models.Gpt_3_5_Turbo
            });
            var responseText = response.Choices.First().Message.Content ?? "No response";
            return (responseText + "\n\nDEAL ACCEPTED", true, offeredPrice, null);
        }

        // If price is below target, calculate a counter-offer based on personality and progress
        decimal counterOffer;
        
        // First check if we've already made a counter-offer
        if (vendorOfferedPrice.HasValue)
        {
            // Never go above previous counter-offer
            decimal maxCounterOffer = vendorOfferedPrice.Value;
            
            // Calculate new counter-offer based on buyer's progress
            if (lastOfferedPrice.HasValue && lastOfferedPrice.Value < offeredPrice)
            {
                // Buyer is moving up - be more willing to meet in the middle
                decimal middleGround = Math.Round((offeredPrice + maxCounterOffer) / 2, 2);
                
                // Check if this is close to target price (within 10%)
                decimal targetDifference = middleGround - level.TargetPrice;
                decimal priceRange = level.InitialPrice - level.TargetPrice;
                decimal percentOfRange = (targetDifference / priceRange) * 100;
                
                if (percentOfRange <= 10)
                {
                    // They're close, just go to target price
                    counterOffer = level.TargetPrice;
                }
                else
                {
                    // They're making progress but not close yet
                    counterOffer = Math.Max(middleGround, level.TargetPrice);
                }
            }
            else
            {
                // Buyer isn't making progress - hold firm or make minimal movement
                // How much to move depends on personality
                if (level.VendorPersonality.ToLower().Contains("eager") || 
                    level.VendorPersonality.ToLower().Contains("need") ||
                    level.VendorPersonality.ToLower().Contains("desperate"))
                {
                    // More eager to sell - move more
                    counterOffer = Math.Max(Math.Round(maxCounterOffer - (maxCounterOffer - level.TargetPrice) * 0.2m, 2), level.TargetPrice);
                }
                else if (level.VendorPersonality.ToLower().Contains("stubborn") ||
                        level.VendorPersonality.ToLower().Contains("firm") ||
                        level.VendorPersonality.ToLower().Contains("confident"))
                {
                    // Stubborn - barely move
                    counterOffer = Math.Max(Math.Round(maxCounterOffer - (maxCounterOffer - level.TargetPrice) * 0.05m, 2), level.TargetPrice);
                }
                else
                {
                    // Default - moderate movement
                    counterOffer = Math.Max(Math.Round(maxCounterOffer - (maxCounterOffer - level.TargetPrice) * 0.1m, 2), level.TargetPrice);
                }
            }
        }
        else
        {
            // First counter-offer - based on how far buyer is from target and personality
            decimal percentOfInitial = offeredPrice / level.InitialPrice * 100;
            
            if (percentOfInitial >= 80)
            {
                // Close to initial price - moderate counter
                counterOffer = Math.Max(Math.Round((level.TargetPrice + offeredPrice) / 2, 2), level.TargetPrice);
            }
            else if (percentOfInitial >= 50)
            {
                // In the middle range - counter based on personality
                if (level.VendorPersonality.ToLower().Contains("eager") || 
                    level.VendorPersonality.ToLower().Contains("need") ||
                    level.VendorPersonality.ToLower().Contains("desperate"))
                {
                    // More eager to sell - come down more
                    counterOffer = Math.Max(Math.Round(level.InitialPrice - (level.InitialPrice - level.TargetPrice) * 0.6m, 2), level.TargetPrice);
                }
                else if (level.VendorPersonality.ToLower().Contains("stubborn") ||
                        level.VendorPersonality.ToLower().Contains("firm") ||
                        level.VendorPersonality.ToLower().Contains("confident"))
                {
                    // Stubborn - barely move from initial
                    counterOffer = Math.Max(Math.Round(level.InitialPrice - (level.InitialPrice - level.TargetPrice) * 0.2m, 2), level.TargetPrice);
                }
                else
                {
                    // Default - moderate movement
                    counterOffer = Math.Max(Math.Round(level.InitialPrice - (level.InitialPrice - level.TargetPrice) * 0.4m, 2), level.TargetPrice);
                }
            }
            else
            {
                // Far from initial price - counter strongly or dismiss based on personality
                if (level.VendorPersonality.ToLower().Contains("patient") || 
                    level.VendorPersonality.ToLower().Contains("kind") ||
                    level.VendorPersonality.ToLower().Contains("helpful"))
                {
                    // Patient - willing to educate and counter
                    counterOffer = Math.Max(Math.Round(level.InitialPrice - (level.InitialPrice - level.TargetPrice) * 0.3m, 2), level.TargetPrice);
                }
                else
                {
                    // Default for low offers - stay firm at initial price
                    counterOffer = level.InitialPrice;
                }
            }
        }
        
        // Ensure counter is never below target price
        counterOffer = Math.Max(counterOffer, level.TargetPrice);
        
        // Create negotiation prompt based on personality
        var negotiationPrompt = $@"{basePrompt}

The buyer offered ${offeredPrice}, which is below your minimum acceptable price.
Do not explicitly say the ${level.TargetPrice} but hint at your range so they don't keep guessing too low.
Based on your personality, respond naturally and counter with ${counterOffer}.
If the buyer's offer is very low, you may show frustration or disappointment according to your personality.
If the buyer is getting closer to an acceptable price, show more interest.
Counter with ${counterOffer} naturally, without explaining your reasoning at length.
Be BRIEF - focus on the price, not product details.
Make your counter-offer of ${counterOffer} clear but don't be robotic about it.
KEEP IT UNDER 50 WORDS.
DO NOT reveal that you're following specific negotiation rules or formulas.";

        Console.WriteLine($"Debug - Initial Price: ${level.InitialPrice}");
        Console.WriteLine($"Debug - Target Price: ${level.TargetPrice}");
        Console.WriteLine($"Debug - Buyer Offer: ${offeredPrice}");
        Console.WriteLine($"Debug - Calculated Counter-Offer: ${counterOffer}");

        var negotiationResponse = await _openAIService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = new List<OpenAI.ObjectModels.RequestModels.ChatMessage>
            {
                OpenAI.ObjectModels.RequestModels.ChatMessage.FromSystem(negotiationPrompt),
                OpenAI.ObjectModels.RequestModels.ChatMessage.FromUser(userMessage)
            },
            Model = OpenAI.ObjectModels.Models.Gpt_3_5_Turbo
        });
        return (negotiationResponse.Choices.First().Message.Content ?? "No response", false, offeredPrice, counterOffer);
    }

    private int CalculateStars(decimal initialPrice, decimal targetPrice, decimal offeredPrice)
    {
        if (offeredPrice >= initialPrice) return 1;  // Overpaid
        if (offeredPrice == targetPrice) return 3;   // Perfect negotiation
        if (offeredPrice > targetPrice) return 2;    // Good deal
        return 1;  // Should never happen as this is only called for winning offers
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
} 