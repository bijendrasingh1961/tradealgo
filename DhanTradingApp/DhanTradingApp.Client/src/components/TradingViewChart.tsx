import React, { useEffect, useRef } from 'react';
import { createChart, ColorType, ISeriesApi } from 'lightweight-charts';

interface ChartProps {
  data: { time: number; value: number }[];
  vwapData: { time: number; value: number }[];
}

export const TradingViewChart: React.FC<ChartProps> = ({ data, vwapData }) => {
  const chartContainerRef = useRef<HTMLDivElement>(null);
  const lineSeriesRef = useRef<ISeriesApi<"Line"> | null>(null);
  const vwapSeriesRef = useRef<ISeriesApi<"Line"> | null>(null);

  useEffect(() => {
    if (!chartContainerRef.current) return;

    const chart = createChart(chartContainerRef.current, {
      layout: {
        background: { type: ColorType.Solid, color: '#131722' },
        textColor: '#d1d4dc',
      },
      grid: {
        vertLines: { color: '#2b2b43' },
        horzLines: { color: '#2b2b43' },
      },
      width: chartContainerRef.current.clientWidth,
      height: 500,
    });

    const lineSeries = chart.addLineSeries({
      color: '#2962FF',
      lineWidth: 2,
    });
    lineSeriesRef.current = lineSeries;

    const vwapSeries = chart.addLineSeries({
      color: '#ff9800',
      lineWidth: 1,
      lineStyle: 2, // Dashed
    });
    vwapSeriesRef.current = vwapSeries;

    chart.timeScale().fitContent();

    const handleResize = () => {
      chart.applyOptions({ width: chartContainerRef.current?.clientWidth });
    };

    window.addEventListener('resize', handleResize);

    return () => {
      window.removeEventListener('resize', handleResize);
      chart.remove();
    };
  }, []);

  useEffect(() => {
    if (lineSeriesRef.current && data.length > 0) {
      lineSeriesRef.current.setData(data);
    }
    if (vwapSeriesRef.current && vwapData.length > 0) {
      vwapSeriesRef.current.setData(vwapData);
    }
  }, [data, vwapData]);

  return <div ref={chartContainerRef} style={{ position: 'relative', width: '100%' }} />;
};
