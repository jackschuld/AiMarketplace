using Microsoft.AspNetCore.Mvc;
using AiMarketplaceApi.Services;
using AiMarketplaceApi.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AiMarketplaceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VendorController : ControllerBase
    {
        private readonly VendorService _vendorService;

        public VendorController(VendorService vendorService)
        {
            _vendorService = vendorService;
        }

        [HttpPost("accept-bid")]
        public async Task<IActionResult> AcceptBid([FromBody] AcceptBidRequest request)
        {
            if (request == null || request.Level == null || request.ConversationHistory == null)
            {
                return BadRequest("Invalid request data.");
            }

            var response = await _vendorService.AcceptBidPriceAsync(request.AcceptedPrice, request.Level, request.ConversationHistory);
            return Ok(new { message = response });
        }
    }

    public class AcceptBidRequest
    {
        public decimal AcceptedPrice { get; set; }
        public Level Level { get; set; }
        public List<ChatMessage> ConversationHistory { get; set; }
    }
}
