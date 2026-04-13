using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using KiteConnect;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace KiteNiftyStrategy
{
    /// <summary>
    /// Configuration model for Kite API credentials.
    /// Values are loaded from config.json.
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// Your Zerodha Kite API Key.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Your Zerodha Kite API Secret.
        /// </summary>
        public string ApiSecret { get; set; } = string.Empty;
    }

    /// <summary>
    /// Main application class for the Nifty 50 Options Strategy.
    /// Strategy: Sell Nifty 50 CE and PE at 9:30 AM, set 90% SL, and trail remaining leg to cost if one SL is hit.
    /// </summary>
    class Program
    {
        private static Kite? _kite;
        private static AppConfig? _config;
        private static string _accessToken = string.Empty;

        // Constants for the strategy
        private const int LOT_SIZE = 25; // Nifty lot size (as of 2024)
        private const int QUANTITY = LOT_SIZE * 4; // 4 lots as per requirement
        private const string EXCHANGE = Constants.Exchange.NFO;

        // Tracking active SL order IDs to avoid false triggers from other user trades
        private static string _ceSLOrderId = string.Empty;
        private static string _peSLOrderId = string.Empty;

        /// <summary>
        /// Entry point of the application.
        /// </summary>
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("=== Nifty 9:30 AM Short Straddle Strategy ===");

                // 1. Load configuration and initialize API
                LoadConfiguration();
                InitializeKite();

                if (_kite == null) return;

                // 2. Collect user inputs for strikes
                Console.WriteLine("\n[Input Required]");
                Console.Write("Enter CE Strike Price (e.g., 22500): ");
                string ceStrike = Console.ReadLine() ?? "";
                Console.Write("Enter PE Strike Price (e.g., 22000): ");
                string peStrike = Console.ReadLine() ?? "";

                // 3. Discover instruments and find current week expiry
                Console.WriteLine("\n[Instrument Discovery]");
                var instruments = _kite.GetInstruments(EXCHANGE);
                var currentWeekExpiry = FindCurrentWeekExpiry(instruments);
                Console.WriteLine($"Detected Current Week Expiry: {currentWeekExpiry:yyyy-MM-dd}");

                // Identify the exact trading symbols from Kite's master list
                string ceSymbol = FindSymbol(instruments, ceStrike, "CE", currentWeekExpiry);
                string peSymbol = FindSymbol(instruments, peStrike, "PE", currentWeekExpiry);

                if (string.IsNullOrEmpty(ceSymbol) || string.IsNullOrEmpty(peSymbol))
                {
                    Console.WriteLine("Error: Could not find matching instruments for the given strikes and expiry.");
                    return;
                }

                Console.WriteLine($"Trading CE: {ceSymbol}");
                Console.WriteLine($"Trading PE: {peSymbol}");

                // 4. Wait until the scheduled start time (9:30 AM)
                WaitUntilTime(new TimeSpan(9, 30, 0));

                // 5. Execute initial Sell Market orders
                ExecuteStrategy(ceSymbol, peSymbol);

                // 6. Monitor positions until square-off time (3:20 PM)
                Console.WriteLine("\n[Monitoring]");
                Console.WriteLine("Monitoring for SL hits or 3:20 PM square-off...");
                MonitorPositions(ceSymbol, peSymbol);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nCritical Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                Console.WriteLine("\nApplication finished. Press any key to exit.");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Loads API credentials from config.json using Microsoft.Extensions.Configuration.
        /// </summary>
        private static void LoadConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config.json", optional: false, reloadOnChange: true);

            var configuration = builder.Build();
            _config = configuration.Get<AppConfig>();

            if (_config == null || string.IsNullOrEmpty(_config.ApiKey) || string.IsNullOrEmpty(_config.ApiSecret))
            {
                throw new Exception("Invalid configuration. Please check config.json and ensure ApiKey/ApiSecret are set.");
            }
        }

        /// <summary>
        /// Initializes the Kite Connect client and handles the daily manual authentication flow.
        /// </summary>
        private static void InitializeKite()
        {
            if (_config == null) return;

            _kite = new Kite(_config.ApiKey, Debug: false);

            // Manual login is required by Zerodha once a day
            Console.WriteLine("\n[Authentication]");
            Console.WriteLine("1. Open this URL in your browser: " + _kite.GetLoginURL());
            Console.WriteLine("2. Login and copy the 'request_token' from the redirected URL.");
            Console.Write("Enter Request Token: ");
            string requestToken = Console.ReadLine() ?? "";

            // Exchange request_token for a persistent access_token
            User user = _kite.GenerateSession(requestToken, _config.ApiSecret);
            _accessToken = user.AccessToken;
            _kite.SetAccessToken(_accessToken);

            Console.WriteLine("Authentication successful!");
        }

        /// <summary>
        /// Finds the closest expiry date for Nifty options in the instrument list.
        /// </summary>
        /// <param name="instruments">List of all NFO instruments.</param>
        /// <returns>The earliest expiry date that is today or in the future.</returns>
        private static DateTime FindCurrentWeekExpiry(List<Instrument> instruments)
        {
            var expiries = instruments
                .Where(i => i.Name == "NIFTY")
                .Select(i => i.Expiry)
                .Where(e => e.HasValue)
                .Select(e => e!.Value)
                .Distinct()
                .OrderBy(e => e)
                .ToList();

            DateTime today = DateTime.Today;
            return expiries.FirstOrDefault(e => e >= today);
        }

        /// <summary>
        /// Finds the exact trading symbol (e.g., NIFTY2453022500CE) for a given strike and type.
        /// </summary>
        private static string FindSymbol(List<Instrument> instruments, string strike, string type, DateTime expiry)
        {
            if (!decimal.TryParse(strike, out decimal strikeDecimal)) return string.Empty;

            var match = instruments.FirstOrDefault(i =>
                i.Name == "NIFTY" &&
                i.Expiry == expiry &&
                i.Strike == strikeDecimal &&
                i.InstrumentType == type);

            return match.TradingSymbol ?? string.Empty;
        }

        /// <summary>
        /// Blocks execution until the target time of the day is reached.
        /// </summary>
        /// <param name="targetTime">The time of day to wait for (e.g., 9:30 AM).</param>
        private static void WaitUntilTime(TimeSpan targetTime)
        {
            DateTime now = DateTime.Now;
            DateTime target = DateTime.Today.Add(targetTime);

            if (now > target)
            {
                Console.WriteLine($"\nNote: Target time {targetTime} has already passed. Proceeding immediately.");
                return;
            }

            Console.WriteLine($"\nWaiting until {targetTime}. Current time: {now:HH:mm:ss}");
            while (DateTime.Now < target)
            {
                // Sleep to avoid high CPU usage
                Thread.Sleep(1000);
            }
            Console.WriteLine("Target time reached! Executing orders...");
        }

        /// <summary>
        /// Executes the strategy: Places Market Sell orders and then sets up Limit SL orders.
        /// </summary>
        private static void ExecuteStrategy(string ceSymbol, string peSymbol)
        {
            if (_kite == null) return;

            // 1. Place Market Sell orders for both legs
            Console.WriteLine($"\n[Executing Trade]");

            var ceOrderResponse = _kite.PlaceOrder(
                Exchange: EXCHANGE,
                TradingSymbol: ceSymbol,
                TransactionType: Constants.Transaction.Sell,
                Quantity: QUANTITY,
                OrderType: Constants.OrderType.Market,
                Product: Constants.Product.MIS
            );

            var peOrderResponse = _kite.PlaceOrder(
                Exchange: EXCHANGE,
                TradingSymbol: peSymbol,
                TransactionType: Constants.Transaction.Sell,
                Quantity: QUANTITY,
                OrderType: Constants.OrderType.Market,
                Product: Constants.Product.MIS
            );

            Console.WriteLine($"Market Sell orders submitted. CE Order ID: {ceOrderResponse.OrderId}, PE Order ID: {peOrderResponse.OrderId}");

            // 2. Wait for orders to be filled at the exchange
            Thread.Sleep(2000);

            // 3. Get average entry prices to calculate SL
            decimal ceEntryPrice = GetAverageEntryPrice(ceOrderResponse.OrderId);
            decimal peEntryPrice = GetAverageEntryPrice(peOrderResponse.OrderId);

            if (ceEntryPrice == 0 || peEntryPrice == 0)
            {
                Console.WriteLine("Warning: Could not retrieve entry prices. SL orders might fail.");
            }

            Console.WriteLine($"CE Filled at: {ceEntryPrice}");
            Console.WriteLine($"PE Filled at: {peEntryPrice}");

            // 4. Place initial Stop Loss (SL) Limit orders (Entry + 90%)
            _ceSLOrderId = PlaceSLOrder(ceSymbol, ceEntryPrice);
            _peSLOrderId = PlaceSLOrder(peSymbol, peEntryPrice);
        }

        /// <summary>
        /// Retrieves the average fill price for a specific order by checking its history.
        /// </summary>
        private static decimal GetAverageEntryPrice(string orderId)
        {
            if (_kite == null || string.IsNullOrEmpty(orderId)) return 0;

            try
            {
                var history = _kite.GetOrderHistory(orderId);
                // In Tech.Zerodha.KiteConnect 5.x, constants are structured
                var completedOrder = history.LastOrDefault(o => o.Status == Constants.OrderStatus.Complete);
                return completedOrder.OrderId != null ? completedOrder.AveragePrice : 0;
            }
            catch { return 0; }
        }

        /// <summary>
        /// Places a Stop Loss (SL) Limit order.
        /// Trigger Price = Entry + 90%
        /// Limit Price = Trigger + 1% (Buffer for slippage)
        /// </summary>
        private static string PlaceSLOrder(string symbol, decimal entryPrice)
        {
            if (_kite == null || entryPrice == 0) return "";

            // Calculate SL levels
            decimal triggerPrice = Math.Round(entryPrice * 1.90m, 1); // 90% SL above entry
            decimal limitPrice = Math.Round(triggerPrice * 1.01m, 1); // 1% Buffer for execution

            var orderResponse = _kite.PlaceOrder(
                Exchange: EXCHANGE,
                TradingSymbol: symbol,
                TransactionType: Constants.Transaction.Buy,
                Quantity: QUANTITY,
                Price: limitPrice,
                TriggerPrice: triggerPrice,
                OrderType: Constants.OrderType.SL,
                Product: Constants.Product.MIS
            );

            Console.WriteLine($"Placed SL Order for {symbol}: Trigger={triggerPrice}, Limit={limitPrice}");
            return orderResponse.OrderId;
        }

        /// <summary>
        /// Continuously monitors the order book for SL hits and the time for square-off.
        /// </summary>
        private static void MonitorPositions(string ceSymbol, string peSymbol)
        {
            if (_kite == null) return;

            bool ceSLHit = false;
            bool peSLHit = false;
            DateTime squareOffTime = DateTime.Today.Add(new TimeSpan(15, 20, 0));

            while (DateTime.Now < squareOffTime)
            {
                try
                {
                    var orders = _kite.GetOrders();

                    // 1. Check if CE Stop Loss was triggered and filled
                    if (!ceSLHit && !string.IsNullOrEmpty(_ceSLOrderId))
                    {
                        var slOrder = orders.FirstOrDefault(o => o.OrderId == _ceSLOrderId);
                        if (slOrder.OrderId != null && slOrder.Status == Constants.OrderStatus.Complete)
                        {
                            Console.WriteLine("\n[Alert] CE Stop Loss Hit!");
                            ceSLHit = true;
                            TrailToCost(peSymbol, _peSLOrderId); // Trail the other leg
                        }
                    }

                    // 2. Check if PE Stop Loss was triggered and filled
                    if (!peSLHit && !string.IsNullOrEmpty(_peSLOrderId))
                    {
                        var slOrder = orders.FirstOrDefault(o => o.OrderId == _peSLOrderId);
                        if (slOrder.OrderId != null && slOrder.Status == Constants.OrderStatus.Complete)
                        {
                            Console.WriteLine("\n[Alert] PE Stop Loss Hit!");
                            peSLHit = true;
                            TrailToCost(ceSymbol, _ceSLOrderId); // Trail the other leg
                        }
                    }

                    // If both legs are closed, we can stop monitoring
                    if (ceSLHit && peSLHit)
                    {
                        Console.WriteLine("Both legs closed via SL. Strategy ended.");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Monitoring warning: {ex.Message}");
                }

                Thread.Sleep(5000); // Wait 5 seconds between polls to stay within rate limits
            }

            // If time is 3:20 PM, close everything
            if (DateTime.Now >= squareOffTime)
            {
                Console.WriteLine("\n[Square-Off] 3:20 PM reached.");
                SquareOff();
            }
        }

        /// <summary>
        /// Trails the open SL order of the remaining leg to its original entry price (cost).
        /// </summary>
        /// <param name="symbol">The trading symbol of the leg to trail.</param>
        /// <param name="slOrderId">The original SL order ID to modify.</param>
        private static void TrailToCost(string symbol, string slOrderId)
        {
            if (_kite == null || string.IsNullOrEmpty(slOrderId)) return;

            try
            {
                var orders = _kite.GetOrders();

                // Find the currently open SL order for this symbol
                var openSLOrder = orders.FirstOrDefault(o => o.OrderId == slOrderId && (o.Status == "OPEN" || o.Status == "TRIGGER PENDING"));

                if (openSLOrder.OrderId != null)
                {
                    // Find the original Sell order to get the entry price
                    var sellOrder = orders.LastOrDefault(o => o.Tradingsymbol == symbol && o.TransactionType == Constants.Transaction.Sell && o.Status == Constants.OrderStatus.Complete);

                    if (sellOrder.OrderId != null)
                    {
                        decimal entryPrice = sellOrder.AveragePrice;
                        decimal triggerPrice = entryPrice;
                        decimal limitPrice = Math.Round(entryPrice * 1.01m, 1);

                        // Modify the existing SL order
                        _kite.ModifyOrder(
                            OrderId: openSLOrder.OrderId,
                            TriggerPrice: triggerPrice,
                            Price: limitPrice,
                            OrderType: Constants.OrderType.SL
                        );
                        Console.WriteLine($"Trailed {symbol} Stop Loss to Cost: {entryPrice}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while trailing {symbol} to cost: {ex.Message}");
            }
        }

        /// <summary>
        /// Automatically cancels all pending orders and closes all open positions for the account.
        /// </summary>
        private static void SquareOff()
        {
            if (_kite == null) return;

            try
            {
                // 1. Cancel all pending/trigger-pending orders
                var orders = _kite.GetOrders();
                var openOrders = orders.Where(o => o.Status == "OPEN" || o.Status == "TRIGGER PENDING");
                foreach (var order in openOrders)
                {
                    _kite.CancelOrder(order.OrderId);
                    Console.WriteLine($"Cancelled pending order: {order.OrderId}");
                }

                // 2. Close all open positions with Market orders
                var positionResponse = _kite.GetPositions();
                var netPositions = positionResponse.Net;
                foreach (var position in netPositions)
                {
                    if (position.Quantity != 0)
                    {
                        // Opposite transaction to square off
                        var transactionType = position.Quantity > 0 ? Constants.Transaction.Sell : Constants.Transaction.Buy;

                        _kite.PlaceOrder(
                            Exchange: position.Exchange,
                            TradingSymbol: position.TradingSymbol,
                            TransactionType: transactionType,
                            Quantity: Math.Abs(position.Quantity),
                            OrderType: Constants.OrderType.Market,
                            Product: position.Product
                        );
                        Console.WriteLine($"Squared off position: {position.TradingSymbol}, Quantity: {position.Quantity}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during Square-Off: {ex.Message}");
            }
        }
    }
}
