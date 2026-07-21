# Web 即時物件偵測系統 - 詳細設計文件 (Detailed Design Document)

## 1. 系統設計準則與選型確認
* **資料傳輸**：前端擷取影像後轉為 `Blob` (二進位串流)，透過 SignalR 傳輸至後端，以優化傳輸效能與降低頻寬負擔。
* **前端狀態管理**：純粹使用 React Hooks (`useState`, `useEffect`, `useRef`, `useCallback` 等) 進行模組間的資料流動。
* **後端測試框架**：採用 .NET 內建的 **MSTest** 作為單元測試框架。
* **後端架構原則**：嚴格遵循 **SOLID 原則**，透過介面隔離各項功能，確保模組具備高度可測試性與可抽換性。

---

## 2. 前端模組詳細設計 (React)

前端採 Custom Hooks 與 Pure Component 的方式進行解耦。

### 2.1 視訊擷取模組 (`useWebcam` Hook)
* **職責 (SRP)**：負責向瀏覽器請求攝影機權限，並取得 `MediaStream`。
* **輸入/輸出**：
  * 輸入：無 (或影片解析度設定檔)。
  * 輸出：`{ stream: MediaStream | null, error: Error | null }`
* **獨立測試方式**：Mock `navigator.mediaDevices.getUserMedia`，驗證授權成功與失敗時的狀態變化。

### 2.2 影像抽樣模組 (`useFrameSampler` Hook)
* **職責 (SRP)**：接收 `MediaStream` 或 `HTMLVideoElement`，透過內部計時器 (如 `requestAnimationFrame` 或 `setInterval`)，以指定 FPS 將畫面繪製至隱藏的 `<canvas>`，並轉換為 `Blob`。
* **輸入/輸出**：
  * 輸入：`videoElementRef`, `fps` (目標幀率)。
  * 輸出：`{ latestFrame: Blob | null }`
* **獨立測試方式**：Mock HTML5 Canvas API 與計時器，驗證在指定時間間隔內是否正確觸發 Blob 轉換邏輯。

### 2.3 即時通訊模組 (`useSignalRClient` Hook)
* **職責 (SRP)**：負責建立與管理 SignalR 連線，傳送 `Blob` 資料至後端，並監聽後端回傳的 JSON 推論結果。
* **輸入/輸出**：
  * 輸入：`latestFrame` (由 `useFrameSampler` 提供), `hubUrl`。
  * 輸出：`{ connectionState: string, detectionResults: DetectionResult[] }`
* **獨立測試方式**：使用 `@microsoft/signalr` 的 Mock 物件，驗證連線建立、發送位元組資料與接收事件的正確性，無需真實啟動後端伺服器。

### 2.4 畫面渲染模組 (`BoundingBoxRenderer` 元件)
* **職責 (SRP)**：純 UI 元件，負責將推論結果繪製在疊加於視訊上方的 `<canvas>`。
* **輸入/輸出**：
  * Props：`results: DetectionResult[]`, `videoWidth`, `videoHeight`
* **獨立測試方式**：傳入靜態的假座標資料，檢查 Canvas API 繪製函式 (如 `strokeRect`, `fillText`) 是否被正確呼叫且參數無誤。

---

## 3. 後端模組詳細設計 (ASP.NET Core)

後端嚴格遵循 SOLID，特別是依賴反轉 (DIP) 與單一職責 (SRP)。

### 3.1 核心資料模型 (Data Transfer Objects)
* `DetectionResultDto`：包含類別名稱 (Label)、信心度 (Confidence)、與邊界框座標 (X, Y, Width, Height)。

### 3.2 介面設計 (Interface Segregation)

#### 1. `IImageProcessor` (影像前處理)
* **職責**：將前端傳入的二進位影像，轉換為 AI 模型所需的張量 (Tensor) 格式。
* **方法**：`float[] ProcessImage(byte[] imageBytes, int targetWidth, int targetHeight);`
* **實作 (`OpenCvImageProcessor`)**：使用 `OpenCvSharp4` 進行解碼 (Decode)、縮放 (Resize) 與正規化 (Normalize)。

#### 2. `IInferenceEngine` (AI 推論引擎)
* **職責**：執行 AI 模型推論，不涉及具體業務邏輯，僅做數學運算。
* **方法**：`float[] RunInference(float[] inputTensor);`
* **實作 (`YoloOnnxInferenceEngine`)**：使用 `Microsoft.ML.OnnxRuntime` 載入 YOLOv8 模型並輸出原始的推論矩陣。

#### 3. `INmsProcessor` (非極大值抑制與後處理)
* **職責**：將推論引擎產出的原始矩陣，過濾重疊框並轉換為具體物件。
* **方法**：`IEnumerable<DetectionResultDto> Process(float[] rawOutput, float confidenceThreshold);`
* **實作 (`YoloNmsProcessor`)**：解析 YOLO 輸出的特定維度格式，執行 NMS 演算法。

#### 4. `IDetectionAppService` (應用協調服務)
* **職責**：作為 Facade，負責協調上述三個模組，處理單一影像的完整生命週期。
* **方法**：`IEnumerable<DetectionResultDto> DetectObjects(byte[] imageBytes);`
* **實作細節**：透過建構子注入 `IImageProcessor`, `IInferenceEngine`, `INmsProcessor`。

### 3.3 通訊端點 (`DetectionHub`)
* **職責**：繼承 `Hub`，負責 SignalR 的網路通訊。
* **實作邏輯**：
  1. 接收前端的 `byte[]`。
  2. 呼叫注入的 `IDetectionAppService.DetectObjects(byte[])`。
  3. 將結果序列化為 JSON 格式，透過 `Clients.Caller.SendAsync` 回傳。
* **獨立測試方式**：Mock `IDetectionAppService`，驗證 Hub 是否正確呼叫該服務並推播結果，不依賴任何真實連線或模型。

---

## 4. 模組獨立測試策略總結

1. **前端測試**：採用 Vitest/Jest。各個 Hook 分離測試，DOM 與 Canvas API 透過 `jsdom` 或 Mock 函式攔截，不需開啟真實攝影機。
2. **後端單元測試**：採用 .NET 內建之 **MSTest** 搭配 Mock 套件 (如 `Moq` 或 `NSubstitute`)。
   * 針對 `IDetectionAppService` 測試時，Mock 所有子元件，驗證資料流向。
   * 針對 `INmsProcessor` 測試時，提供預先算好的浮點數陣列，驗證是否正確過濾出 Bounding Box，完全無需載入 ONNX 模型。
   * 針對 `IImageProcessor` 可提供靜態測試圖片 (轉為 byte[]) 驗證輸出張量大小是否正確。
