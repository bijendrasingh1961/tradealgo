using DhanTradingApp.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DhanTradingApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TradingController : ControllerBase
    {
        private readonly IDhanApiService _apiService;

        public TradingController(IDhanApiService apiService)
        {
            _apiService = apiService;
        }

        [HttpGet("option-chain/{underlyingId}/{expiry}")]
        public async Task<IActionResult> GetOptionChain(string underlyingId, string expiry)
        {
            var result = await _apiService.GetOptionChain(underlyingId, expiry);
            return Content(result, "application/json");
        }

        [HttpPost("sell-straddle")]
        public async Task<IActionResult> SellStraddle([FromBody] SellStraddleRequest request)
        {
            var callResult = await _apiService.PlaceOrder("SELL", request.CallSecurityId, "NSE_FNO", request.Quantity, "MARGIN");
            var putResult = await _apiService.PlaceOrder("SELL", request.PutSecurityId, "NSE_FNO", request.Quantity, "MARGIN");

            return Ok(new { callResult, putResult });
        }
    }

    public class SellStraddleRequest
    {
        public string CallSecurityId { get; set; } = string.Empty;
        public string PutSecurityId { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }
}
