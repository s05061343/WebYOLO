import { useState, useEffect, useRef } from 'react';

export const useWebcam = () => {
  const [stream, setStream] = useState<MediaStream | null>(null);
  const [error, setError] = useState<Error | null>(null);
  const videoRef = useRef<HTMLVideoElement>(null);

  useEffect(() => {
    let activeStream: MediaStream | null = null;

    const startWebcam = async () => {
      try {
        activeStream = await navigator.mediaDevices.getUserMedia({
          video: { width: 640, height: 640, facingMode: 'user' },
        });
        setStream(activeStream);
        if (videoRef.current) {
          videoRef.current.srcObject = activeStream;
        }
      } catch (err) {
        setError(err instanceof Error ? err : new Error('Unknown error accessing webcam'));
      }
    };

    startWebcam();

    return () => {
      if (activeStream) {
        activeStream.getTracks().forEach((track) => track.stop());
      }
    };
  }, []);

  return { stream, error, videoRef };
};
