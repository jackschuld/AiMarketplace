using AiMarketplaceApi.Models;

namespace AiMarketplaceApi.Services;

public interface IVendorService
{
    Task<(string response, bool isAccepted, decimal? offeredPrice, decimal? vendorOfferedPrice)> GetVendorResponseAsync(
        string userMessage, 
        Level level, 
        decimal? lastOfferedPrice, 
        decimal? vendorOfferedPrice,
        List<AiMarketplaceApi.Models.ChatMessage> conversationHistory);
}
