import React, { useEffect, useRef } from 'react';
import { DetectionResult } from '../hooks/useSignalRClient';

interface BoundingBoxRendererProps {
  results: DetectionResult[];
  videoWidth: number;
  videoHeight: number;
}

export const BoundingBoxRenderer: React.FC<BoundingBoxRendererProps> = ({
  results,
  videoWidth,
  videoHeight,
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    // Clear previous drawings
    ctx.clearRect(0, 0, videoWidth, videoHeight);

    // Draw new results
    results.forEach((result) => {
      const { x, y, width, height } = result.boundingBox;
      
      // Dynamic colors based on label (simplified)
      ctx.strokeStyle = '#00FFAA'; 
      ctx.lineWidth = 3;
      ctx.strokeRect(x, y, width, height);

      // Draw label background
      ctx.fillStyle = 'rgba(0, 255, 170, 0.8)';
      const text = `${result.label} (${(result.confidence * 100).toFixed(1)}%)`;
      const textWidth = ctx.measureText(text).width;
      ctx.fillRect(x, y - 25, textWidth + 10, 25);

      // Draw label text
      ctx.fillStyle = '#000000';
      ctx.font = '16px Inter, sans-serif';
      ctx.fontWeight = '600';
      ctx.fillText(text, x + 5, y - 7);
    });
  }, [results, videoWidth, videoHeight]);

  return (
    <canvas
      ref={canvasRef}
      width={videoWidth}
      height={videoHeight}
      style={{
        position: 'absolute',
        top: 0,
        left: 0,
        pointerEvents: 'none', // Allow clicks to pass through to the video if needed
        zIndex: 10,
      }}
    />
  );
};
