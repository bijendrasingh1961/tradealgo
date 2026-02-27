using Microsoft.AspNetCore.SignalR;
using DhanTradingApp.Api.Services;

namespace DhanTradingApp.Api.Hubs
{
    public class TradingHub : Hub
    {
        private readonly DhanWebSocketService _wsService;

        public TradingHub(DhanWebSocketService wsService)
        {
            _wsService = wsService;
        }

        public async Task SubscribeStraddle(string callId, string putId)
        {
            await _wsService.SubscribeStraddle(Context.ConnectionId, callId, putId);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _wsService.Unsubscribe(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
