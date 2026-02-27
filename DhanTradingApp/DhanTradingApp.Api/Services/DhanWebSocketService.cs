using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;
using DhanTradingApp.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using DhanTradingApp.Api.Hubs;
using System.Collections.Concurrent;

namespace DhanTradingApp.Api.Services
{
    public class StraddleData
    {
        public float CombinedPrice { get; set; }
        public float VWAP { get; set; }
        public long Timestamp { get; set; }
    }

    public class UserSubscription
    {
        public string CallId { get; set; } = string.Empty;
        public string PutId { get; set; } = string.Empty;
        public float CallLtp { get; set; }
        public float PutLtp { get; set; }
        public double CumulativePriceVolume { get; set; }
        public double CumulativeVolume { get; set; }
        public DateTime LastResetDate { get; set; } = DateTime.MinValue;
    }

    public class DhanWebSocketService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHubContext<TradingHub> _hubContext;
        private ClientWebSocket? _webSocket;

        private readonly ConcurrentDictionary<string, UserSubscription> _subscriptions = new();
        private readonly ConcurrentDictionary<string, HashSet<string>> _instrumentWatchers = new();

        public DhanWebSocketService(IServiceProvider serviceProvider, IHubContext<TradingHub> hubContext)
        {
            _serviceProvider = serviceProvider;
            _hubContext = hubContext;
        }

        public async Task SubscribeStraddle(string connectionId, string callId, string putId)
        {
            var sub = new UserSubscription { CallId = callId, PutId = putId };
            _subscriptions.AddOrUpdate(connectionId, sub, (_, _) => sub);

            _instrumentWatchers.AddOrUpdate(callId, new HashSet<string> { connectionId }, (_, hs) => { lock(hs) { hs.Add(connectionId); } return hs; });
            _instrumentWatchers.AddOrUpdate(putId, new HashSet<string> { connectionId }, (_, hs) => { lock(hs) { hs.Add(connectionId); } return hs; });

            if (_webSocket?.State == WebSocketState.Open)
            {
                var subRequest = new
                {
                    RequestCode = 15,
                    InstrumentCount = 2,
                    InstrumentList = new[]
                    {
                        new { ExchangeSegment = "NSE_FNO", SecurityId = callId },
                        new { ExchangeSegment = "NSE_FNO", SecurityId = putId }
                    }
                };

                var message = JsonConvert.SerializeObject(subRequest);
                await _webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        public void Unsubscribe(string connectionId)
        {
            if (_subscriptions.TryRemove(connectionId, out var sub))
            {
                if (_instrumentWatchers.TryGetValue(sub.CallId, out var callHs)) { lock(callHs) { callHs.Remove(connectionId); } }
                if (_instrumentWatchers.TryGetValue(sub.PutId, out var putHs)) { lock(putHs) { putHs.Remove(connectionId); } }
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
                    var settings = await dbContext.DhanSettings.FirstOrDefaultAsync();

                    if (settings == null || string.IsNullOrEmpty(settings.AccessToken))
                    {
                        await Task.Delay(5000, stoppingToken);
                        continue;
                    }

                    _webSocket = new ClientWebSocket();
                    var uri = new Uri($"wss://api-feed.dhan.co?version=2&token={settings.AccessToken}&clientId={settings.ClientId}&authType=2");

                    await _webSocket.ConnectAsync(uri, stoppingToken);

                    var buffer = new byte[1024 * 8];
                    var ms = new MemoryStream();

                    while (_webSocket.State == WebSocketState.Open && !stoppingToken.IsCancellationRequested)
                    {
                        WebSocketReceiveResult result;
                        do
                        {
                            result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), stoppingToken);
                            ms.Write(buffer, 0, result.Count);
                        } while (!result.EndOfMessage);

                        if (result.MessageType == WebSocketMessageType.Binary)
                        {
                            ProcessBinaryData(ms.ToArray());
                        }
                        ms.SetLength(0);
                    }
                }
                catch (Exception)
                {
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }

        private void ProcessBinaryData(byte[] fullData)
        {
            int offset = 0;
            while (offset + 8 <= fullData.Length)
            {
                byte responseCode = fullData[offset];
                if (responseCode == 4)
                {
                    if (offset + 50 > fullData.Length) break;

                    string securityId = BitConverter.ToInt32(fullData, offset + 4).ToString();
                    float ltp = BitConverter.ToSingle(fullData, offset + 8);
                    short lastTradedQty = BitConverter.ToInt16(fullData, offset + 12);

                    UpdateAndBroadcast(securityId, ltp, lastTradedQty);
                    offset += 50;
                }
                else
                {
                    int msgLen = BitConverter.ToInt16(fullData, offset + 1);
                    if (msgLen <= 8) { offset += 8; continue; }
                    offset += msgLen;
                }
            }
        }

        private void UpdateAndBroadcast(string securityId, float ltp, int qty)
        {
            if (_instrumentWatchers.TryGetValue(securityId, out var connections))
            {
                lock(connections)
                {
                    foreach (var connId in connections)
                    {
                        if (_subscriptions.TryGetValue(connId, out var sub))
                        {
                            // Daily Reset Check
                            if (DateTime.Today > sub.LastResetDate)
                            {
                                sub.CumulativePriceVolume = 0;
                                sub.CumulativeVolume = 0;
                                sub.LastResetDate = DateTime.Today;
                            }

                            if (sub.CallId == securityId) sub.CallLtp = ltp;
                            if (sub.PutId == securityId) sub.PutLtp = ltp;

                            if (sub.CallLtp > 0 && sub.PutLtp > 0)
                            {
                                float combinedPrice = sub.CallLtp + sub.PutLtp;
                                sub.CumulativePriceVolume += (combinedPrice * qty);
                                sub.CumulativeVolume += qty;

                                float vwap = sub.CumulativeVolume > 0 ? (float)(sub.CumulativePriceVolume / sub.CumulativeVolume) : combinedPrice;

                                _hubContext.Clients.Client(connId).SendAsync("ReceiveStraddleUpdate", new StraddleData
                                {
                                    CombinedPrice = combinedPrice,
                                    VWAP = vwap,
                                    Timestamp = DateTimeOffset.Now.ToUnixTimeSeconds()
                                });
                            }
                        }
                    }
                }
            }
        }
    }
}
