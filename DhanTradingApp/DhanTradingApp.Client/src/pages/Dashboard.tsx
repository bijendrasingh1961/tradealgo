import React, { useState, useEffect } from 'react';
import * as signalR from '@microsoft/signalr';
import { TradingViewChart } from '../components/TradingViewChart';
import { tradingService } from '../services/api';

interface OptionChainItem {
  strikePrice: number;
  callOption: { securityId: string };
  putOption: { securityId: string };
}

export const Dashboard: React.FC = () => {
  const [underlying, setUnderlying] = useState('NIFTY');
  const [expiry, setExpiry] = useState('');
  const [optionChain, setOptionChain] = useState<OptionChainItem[]>([]);
  const [selectedStrike, setSelectedStrike] = useState<number | null>(null);
  const [quantity, setQuantity] = useState(1);
  const [chartData, setChartData] = useState<{ time: number; value: number }[]>([]);
  const [vwapData, setVwapData] = useState<{ time: number; value: number }[]>([]);
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);

  useEffect(() => {
    const newConnection = new signalR.HubConnectionBuilder()
      .withUrl('https://localhost:7236/tradingHub')
      .withAutomaticReconnect()
      .build();

    setConnection(newConnection);
  }, []);

  useEffect(() => {
    if (connection) {
      connection.start()
        .then(() => {
          connection.on('ReceiveStraddleUpdate', (data: { combinedPrice: number; vwap: number; timestamp: number }) => {
            setChartData(prev => [...prev.slice(-100), { time: data.timestamp, value: data.combinedPrice }]);
            setVwapData(prev => [...prev.slice(-100), { time: data.timestamp, value: data.vwap }]);
          });
        })
        .catch(e => console.log('Connection failed: ', e));
    }
  }, [connection]);

  const fetchOptionChain = async () => {
    if (underlying && expiry) {
      try {
        const data = await tradingService.getOptionChain(underlying, expiry);
        setOptionChain(data.data || []);
      } catch (err) {
        console.error('Failed to fetch option chain', err);
      }
    }
  };

  const handleSubscribe = () => {
    const item = optionChain.find(o => o.strikePrice === selectedStrike);
    if (connection && item) {
      setChartData([]);
      setVwapData([]);
      connection.invoke('SubscribeStraddle', item.callOption.securityId, item.putOption.securityId);
    }
  };

  const handleSellStraddle = async () => {
    const item = optionChain.find(o => o.strikePrice === selectedStrike);
    if (item) {
      try {
        await tradingService.sellStraddle({
          callSecurityId: item.callOption.securityId,
          putSecurityId: item.putOption.securityId,
          quantity: quantity
        });
        alert('Straddle Sell Orders Placed!');
      } catch (err) {
        alert('Failed to place orders');
      }
    }
  };

  return (
    <div className="flex flex-col h-screen bg-[#131722] text-[#d1d4dc]">
      <header className="p-4 border-b border-[#2b2b43] flex items-center justify-between">
        <h1 className="text-xl font-bold text-white">Dhan Algo Trader</h1>
        <div className="flex gap-4 items-end">
          <div className="flex flex-col">
            <span className="text-[10px]">Underlying</span>
            <input
              className="bg-[#1e222d] border border-[#363c4e] p-1 rounded w-24"
              value={underlying}
              onChange={e => setUnderlying(e.target.value)}
            />
          </div>
          <div className="flex flex-col">
            <span className="text-[10px]">Expiry (YYYY-MM-DD)</span>
            <input
              className="bg-[#1e222d] border border-[#363c4e] p-1 rounded w-32"
              value={expiry}
              onChange={e => setExpiry(e.target.value)}
            />
          </div>
          <button
            className="bg-gray-700 hover:bg-gray-600 px-3 py-1 rounded text-xs"
            onClick={fetchOptionChain}
          >
            Load Chain
          </button>
          <div className="flex flex-col">
            <span className="text-[10px]">Strike</span>
            <select
              className="bg-[#1e222d] border border-[#363c4e] p-1 rounded w-24"
              value={selectedStrike || ''}
              onChange={e => setSelectedStrike(Number(e.target.value))}
            >
              <option value="">Select</option>
              {optionChain.map(opt => (
                <option key={opt.strikePrice} value={opt.strikePrice}>{opt.strikePrice}</option>
              ))}
            </select>
          </div>
          <button
            className="bg-[#2962FF] hover:bg-[#1e4bd8] px-4 py-1 rounded text-white font-bold"
            onClick={handleSubscribe}
          >
            Watch
          </button>
        </div>
      </header>

      <main className="flex-1 p-4">
        <div className="bg-[#1e222d] rounded-lg border border-[#2b2b43] overflow-hidden h-full">
          <TradingViewChart data={chartData} vwapData={vwapData} />
        </div>
      </main>

      <footer className="p-4 border-t border-[#2b2b43] flex items-center justify-between">
        <div className="flex items-center gap-4">
          <span>Lots:</span>
          <input
            type="number"
            className="bg-[#1e222d] border border-[#363c4e] p-1 rounded w-16"
            value={quantity}
            onChange={e => setQuantity(parseInt(e.target.value))}
          />
        </div>
        <button
          className="bg-[#f23645] hover:bg-[#d1212f] px-8 py-2 rounded text-white font-bold text-lg"
          onClick={handleSellStraddle}
        >
          SELL STRADDLE
        </button>
      </footer>
    </div>
  );
};
