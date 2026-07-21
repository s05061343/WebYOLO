# WebYOLO

WebYOLO 是一個高效能的即時網頁端物件偵測系統。前端透過 React 擷取攝影機畫面，後端則利用 ASP.NET Core SignalR 接收影像，並結合 OpenCV 與 ONNX Runtime (YOLOv8) 進行低延遲的推論，最終將 Bounding Box 即時反饋渲染至前端畫面上。

## 🌟 核心特色 (Key Features)
* 📹 **零延遲攝影機串流**：前端使用 WebRTC (`getUserMedia`) 獲取原生視訊。
* ⚡ **背壓與防堵塞機制**：實作非同步狀態鎖 (Drop Frames)，確保前端畫面只在後端有能力處理時才傳送，防止記憶體堆積與 CPU 滿載。
* 🚀 **超高速影像前處理**：後端拔除 C# 迴圈，採用 `Cv2.Split` 搭配 `Marshal.Copy` 進行記憶體區塊拷貝，瞬間完成 CHW 張量轉換。
* 🎛️ **資源控制開關**：UI 內建美觀的 Toggle Switch，可隨時暫停/啟動偵測，徹底釋放運算資源。

---

## 🏗️ 系統架構 (Architecture)

### 前端 (Frontend)
* **框架**：React 18 + TypeScript + Vite
* **核心 Hook**：
  * `useWebcam`：處理硬體視訊權限與串流。
  * `useFrameSampler`：定時擷取 Canvas 並壓縮為 JPEG Blob。
  * `useSignalRClient`：管理 WebSocket 連線與 Base64 影像傳輸。

### 後端 (Backend)
* **框架**：ASP.NET Core 10 Web API
* **通訊**：SignalR (JSON Protocol, Base64 Payload)
* **影像處理**：OpenCvSharp4 (C++ Wrapper)
* **推論引擎**：Microsoft.ML.OnnxRuntime (YOLOv8n)

---

## 🛠️ 環境需求 (Prerequisites)

* [Node.js](https://nodejs.org/) (v18 或以上版本)
* [.NET 10 SDK](https://dotnet.microsoft.com/)
* YOLOv8 ONNX 模型檔 (`yolov8n.onnx`)

---

## 🚀 快速啟動 (Getting Started)

### 1. 準備模型檔
您必須手動下載預先訓練好的 YOLOv8 模型，並放入後端專案中。
1. 點擊下載：[yolov8n.onnx](https://github.com/ibaiGorordo/ONNX-YOLOv8-Object-Detection/raw/main/models/yolov8n.onnx)
2. 將下載的檔案命名為 `yolov8n.onnx`。
3. 將它放入目錄：`backend/WebYOLO.Api/yolov8n.onnx`

### 2. 啟動後端 (Backend)
開啟終端機並切換至後端目錄：
```bash
cd backend/WebYOLO.Api
dotnet run
```
> **注意**：專案已經預設綁定至 `http://localhost:5000`，且 CORS 已配置為允許前端存取。

### 3. 啟動前端 (Frontend)
開啟第二個終端機並切換至前端目錄：
```bash
cd frontend
npm install
npm run dev
```
啟動後，Vite 會顯示本地測試網址（通常為 `http://localhost:5173` 或 `5174`）。使用瀏覽器開啟該網址，並允許攝影機權限，即可開始體驗！

---

## 📂 專案結構 (Project Structure)
```text
WebYOLO/
├── backend/
│   ├── WebYOLO.sln
│   └── WebYOLO.Api/
│       ├── Hubs/                # SignalR 通訊端點 (DetectionHub)
│       ├── Services/            # OpenCV 前處理、NMS 後處理與 ONNX 推論邏輯
│       ├── Program.cs           # 依賴注入 (DI) 與中介軟體設定
│       └── yolov8n.onnx         # YOLO 模型檔 (需手動放入)
└── frontend/
    ├── src/
    │   ├── components/          # React 元件 (如 BoundingBoxRenderer)
    │   ├── hooks/               # 邏輯封裝 (Webcam, Sampler, SignalR)
    │   ├── App.tsx              # 主儀表板與狀態管理
    │   └── index.css            # 現代化深色 UI 樣式
    └── package.json
```
