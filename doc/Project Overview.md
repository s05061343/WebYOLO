# Web 即時物件偵測系統 - 專案需求規格書 (PRD)

## 1. 專案概述 (Project Overview)
* **專案名稱**：Web-based Real-time Object Detection (PoC)
* **專案目標**：建立一套基於 Web 的即時物件偵測系統，驗證前端視訊串流、後端 WebSocket 即時通訊與 AI 模型推論的整合能力。
* **主要應用情境**：使用者透過瀏覽器開啟 Webcam，系統即時辨識畫面中的物件（如：人、手機等），並在畫面上同步繪製標籤與邊界框 (Bounding Box)。

---

## 2. 系統架構與技術選型 (System Architecture)

### 2.1 核心技術堆疊
* **前端 (Frontend)**：React.js
* **後端 (Backend)**：C# ASP.NET Core Web API
* **即時通訊 (Real-time Communication)**：SignalR (WebSocket)
* **AI 推論引擎 (AI Engine)**：Microsoft.ML.OnnxRuntime, OpenCvSharp4
* **AI 模型 (Model)**：YOLOv8 (ONNX 格式)

### 2.2 系統資料流
1. 前端透過 `MediaDevices API` 獲取視訊。
2. 前端定時抽取影像幀 (Frame)，轉為 Base64/Blob，透過 SignalR 傳送至後端。
3. 後端接收影像，轉為 Tensor 並交由 ONNX Runtime 推論。
4. 後端將推論結果 (類別、信心度、座標) 轉為 JSON，透過 SignalR 回傳。
5. 前端解析 JSON，使用 HTML5 `<canvas>` 在畫面上繪製結果。

---

## 3. 核心功能需求 (Functional Requirements)

### 3.1 前端功能 (React)
* **視訊擷取**：需實作介面授權並開啟使用者 Webcam，將畫面綁定至隱藏或底層的 `<video>` 標籤。
* **影像抽樣與傳輸**：
  * 實作影像抽樣機制（建議設定在 10 ~ 15 FPS）。
  * 將當前畫面繪製至離線 Canvas，壓縮並轉換編碼後，透過 SignalR 傳送。
* **畫面渲染**：
  * 在 `<video>` 上層疊加一個背景透明的 `<canvas>`。
  * 接收到後端座標資料後，即時清除前一幀的畫布，並繪製新的邊界框與文字標籤。

### 3.2 後端功能 (ASP.NET Core & SignalR)
* **SignalR Hub 建置**：
  * 建立 Hub 節點接收前端持續傳入的影像流。
  * 確保接收與推播過程採非同步 (Async) 處理，避免阻塞連線。
* **模型推論服務 (Inference Service)**：
  * 使用 OpenCvSharp4 進行影像前處理 (Resize, Normalize)。
  * 呼叫 ONNX Runtime 執行推論。
  * 實作 NMS (Non-Maximum Suppression) 過濾重疊且信心度較低的邊界框。
* **擴充性設計 (Configuration-driven)**：
  * 將模型檔案路徑 (`.onnx`) 與標籤對應表 (Labels array) 抽離至 `appsettings.json`。
  * 採用依賴注入 (Dependency Injection) 與介面化設計 (`IObjectDetectionService`)，確保未來替換客製化模型時不需修改核心邏輯。

### 3.3 AI 模型規格
* **初期階段**：使用官方預訓練 YOLOv8 模型 (COCO Dataset，80 種分類)。
* **未來擴充**：支援無縫抽換為使用者自行訓練並匯出之 ONNX 客製化模型。

---

## 4. 資料通訊格式 (Data Contracts)

### 後端回傳之 JSON 格式範例
```json
{
  "status": "success",
  "data": [
    {
      "label": "person",
      "confidence": 0.74,
      "boundingBox": {
        "x": 50,
        "y": 100,
        "width": 200,
        "height": 350
      }
    },
    {
      "label": "cell phone",
      "confidence": 0.753,
      "boundingBox": {
        "x": 60,
        "y": 300,
        "width": 120,
        "height": 80
      }
    }
  ]
}