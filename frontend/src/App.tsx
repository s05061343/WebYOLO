import React, { useMemo, useState } from 'react';
import { useWebcam } from './hooks/useWebcam';
import { useFrameSampler } from './hooks/useFrameSampler';
import { useSignalRClient } from './hooks/useSignalRClient';
import { BoundingBoxRenderer } from './components/BoundingBoxRenderer';

function App() {
  const [isDetecting, setIsDetecting] = useState<boolean>(true);
  const { stream, error, videoRef } = useWebcam();
  // We use 10 FPS for frame sampling to avoid overloading backend
  const { latestFrame } = useFrameSampler(videoRef, 10, isDetecting);
  
  // Note: Adjust the hubUrl based on your backend environment
  const hubUrl = 'http://localhost:5000/detectionHub';
  const { connectionState, detectionResults } = useSignalRClient(hubUrl, latestFrame);

  // Group items to show in the stats panel
  const detectedSummary = useMemo(() => {
    const summary: Record<string, number> = {};
    if (isDetecting) {
      detectionResults.forEach((r) => {
        summary[r.label] = (summary[r.label] || 0) + 1;
      });
    }
    return Object.entries(summary);
  }, [detectionResults, isDetecting]);

  const getStatusClass = () => {
    if (connectionState === 'Connected') return 'connected';
    if (connectionState === 'Reconnecting') return 'reconnecting';
    if (connectionState === 'Error') return 'error';
    return '';
  };

  return (
    <div className="dashboard">
      <header className="header">
        <h1>WebYOLO Dashboard</h1>
        <div className="header-controls">
          <button 
            className={`toggle-btn ${isDetecting ? 'active' : ''}`}
            onClick={() => setIsDetecting(!isDetecting)}
          >
            {isDetecting ? '🟢 Detecting' : '⚪ Paused'}
          </button>
          <div className={`status-badge ${getStatusClass()}`}>
            <div className={`status-dot ${getStatusClass()}`} />
            <span>{connectionState}</span>
          </div>
        </div>
      </header>

      <main className="main-content">
        <div className="video-container">
          {error && <div className="error-overlay">Camera Error: {error.message}</div>}
          <video
            ref={videoRef}
            autoPlay
            playsInline
            muted
          />
          {stream && (
            <BoundingBoxRenderer
              results={detectionResults}
              videoWidth={640} // Default from hook
              videoHeight={640}
            />
          )}
        </div>

        <aside className="stats-panel">
          <div className="stat-box">
            <span className="stat-label">Total Objects</span>
            <span className="stat-value">{detectionResults.length}</span>
          </div>

          <div className="stat-box" style={{ flexGrow: 1 }}>
            <span className="stat-label" style={{ marginBottom: '1rem' }}>Live Detections</span>
            <div className="object-list">
              {detectedSummary.length === 0 ? (
                <div style={{ color: 'var(--text-secondary)' }}>No objects detected...</div>
              ) : (
                detectedSummary.map(([label, count]) => (
                  <div key={label} className="object-item">
                    <span style={{ textTransform: 'capitalize' }}>{label}</span>
                    <span style={{ fontWeight: 600 }}>{count}</span>
                  </div>
                ))
              )}
            </div>
          </div>
        </aside>
      </main>
    </div>
  );
}

export default App;
