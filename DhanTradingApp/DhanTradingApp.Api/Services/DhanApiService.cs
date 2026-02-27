using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using DhanTradingApp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace DhanTradingApp.Api.Services
{
    public interface IDhanApiService
    {
        Task<string> GetInstruments();
        Task<string> GetOptionChain(string underlyingId, string expiry);
        Task<string> PlaceOrder(string transactionType, string securityId, string exchangeSegment, int quantity, string productType);
    }

    public class DhanApiService : IDhanApiService
    {
        private readonly HttpClient _httpClient;
        private readonly TradingDbContext _context;

        private static readonly Dictionary<string, string> UnderlyingMap = new()
        {
            { "NIFTY", "13" },
            { "BANKNIFTY", "25" },
            { "FINNIFTY", "27" }
        };

        public DhanApiService(HttpClient httpClient, TradingDbContext context)
        {
            _httpClient = httpClient;
            _context = context;
        }

        private async Task SetAuthHeader()
        {
            var settings = await _context.DhanSettings.FirstOrDefaultAsync();
            if (settings != null)
            {
                _httpClient.DefaultRequestHeaders.Remove("access-token");
                _httpClient.DefaultRequestHeaders.Add("access-token", settings.AccessToken);
            }
        }

        public async Task<string> GetInstruments()
        {
            return "[]";
        }

        public async Task<string> GetOptionChain(string underlyingId, string expiry)
        {
            await SetAuthHeader();

            // Map underlying name to ID if needed
            string id = UnderlyingMap.ContainsKey(underlyingId.ToUpper()) ? UnderlyingMap[underlyingId.ToUpper()] : underlyingId;

            var requestBody = new
            {
                underlying_id = id,
                underlying_segment = "NSE_EQ", // Usually indices are in NSE_EQ or NSE_IDX
                expiry_date = expiry
            };

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://api.dhan.co/v2/optionchain", content);
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> PlaceOrder(string transactionType, string securityId, string exchangeSegment, int quantity, string productType)
        {
            await SetAuthHeader();
            var settings = await _context.DhanSettings.FirstOrDefaultAsync();

            var orderRequest = new
            {
                dhanClientId = settings?.ClientId,
                correlationId = Guid.NewGuid().ToString(),
                transactionType = transactionType,
                exchangeSegment = exchangeSegment,
                productType = productType,
                orderType = "MARKET",
                validity = "DAY",
                securityId = securityId,
                quantity = quantity
            };

            var content = new StringContent(JsonConvert.SerializeObject(orderRequest), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://api.dhan.co/v2/orders", content);
            return await response.Content.ReadAsStringAsync();
        }
    }
}
