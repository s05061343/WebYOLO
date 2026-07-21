import { useEffect, useState, useRef } from 'react';
import * as signalR from '@microsoft/signalr';

export interface BoundingBox {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface DetectionResult {
  label: string;
  confidence: number;
  boundingBox: BoundingBox;
}

export const useSignalRClient = (hubUrl: string, latestFrame: Blob | null) => {
  const [connectionState, setConnectionState] = useState<string>('Disconnected');
  const [detectionResults, setDetectionResults] = useState<DetectionResult[]>([]);
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect()
      .build();

    connectionRef.current = connection;

    connection.on('OnDetectionResult', (response: any) => {
      if (response && response.status === 'success') {
        setDetectionResults(response.data);
      }
    });

    const startConnection = async () => {
      try {
        await connection.start();
        setConnectionState('Connected');
      } catch (err) {
        console.error('SignalR Connection Error: ', err);
        setConnectionState('Error');
      }
    };

    connection.onreconnecting(() => setConnectionState('Reconnecting'));
    connection.onreconnected(() => setConnectionState('Connected'));
    connection.onclose(() => setConnectionState('Disconnected'));

    startConnection();

    return () => {
      connection.stop();
    };
  }, [hubUrl]);

  useEffect(() => {
    const sendFrame = async () => {
      if (
        latestFrame &&
        connectionRef.current &&
        connectionRef.current.state === signalR.HubConnectionState.Connected
      ) {
        const arrayBuffer = await latestFrame.arrayBuffer();
        const uint8Array = new Uint8Array(arrayBuffer);
        
        try {
          await connectionRef.current.invoke('Detect', uint8Array);
        } catch (err) {
          console.error('Error sending frame to SignalR', err);
        }
      }
    };

    sendFrame();
  }, [latestFrame]);

  return { connectionState, detectionResults };
};
