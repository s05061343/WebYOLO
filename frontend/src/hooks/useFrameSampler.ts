import { useEffect, useRef, useState } from 'react';

export const useFrameSampler = (videoRef: React.RefObject<HTMLVideoElement | null>, fps: number = 10, isActive: boolean = true) => {
  const [latestFrame, setLatestFrame] = useState<Blob | null>(null);
  const canvasRef = useRef<HTMLCanvasElement>(document.createElement('canvas'));

  useEffect(() => {
    if (!videoRef.current || !isActive) {
      if (!isActive) setLatestFrame(null); // Clear last frame
      return;
    }

    const intervalMs = 1000 / fps;
    let intervalId: number;

    const sampleFrame = () => {
      const video = videoRef.current;
      if (video && video.readyState >= 2) {
        const canvas = canvasRef.current;
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;
        const ctx = canvas.getContext('2d');
        if (ctx) {
          ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
          // Convert to blob (JPEG)
          canvas.toBlob(
            (blob) => {
              if (blob) {
                setLatestFrame(blob);
              }
            },
            'image/jpeg',
            0.8
          );
        }
      }
    };

    // Wait until video is playing
    const handlePlay = () => {
      intervalId = window.setInterval(sampleFrame, intervalMs);
    };

    const handlePause = () => {
      if (intervalId) window.clearInterval(intervalId);
    };

    videoRef.current.addEventListener('play', handlePlay);
    videoRef.current.addEventListener('pause', handlePause);

    // If already playing
    if (videoRef.current.readyState >= 3 && !videoRef.current.paused) {
      intervalId = window.setInterval(sampleFrame, intervalMs);
    }

    return () => {
      if (intervalId) window.clearInterval(intervalId);
      if (videoRef.current) {
        videoRef.current.removeEventListener('play', handlePlay);
        videoRef.current.removeEventListener('pause', handlePause);
      }
    };
  }, [videoRef, fps, isActive]);

  return { latestFrame };
};
