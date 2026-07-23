# WebYOLO 擴充開發文件
> 版本：v1.0 | 日期：2026-07-23 | 狀態：草稿

---

## 壹、現有專案基礎

| 層級 | 技術 | 說明 |
|---|---|---|
| 前端 | React 18 + TypeScript + Vite | 目前做攝影機即時串流 + bbox 渲染 |
| 後端 | ASP.NET Core 10 + SignalR | 接收 Base64 影像，ONNX 推論後回傳 |
| 推論 | YOLOv8n ONNX (C# OnnxRuntime) | 目前在後端直接推論，無追蹤 |

**擴充方向**：拆出獨立的 Python AI 微服務，負責追蹤、測速、車牌辨識，C# 後端改為轉發與資料儲存，前端加入新結果渲染。

---

## 貳、目標功能

| 功能 | 優先級 | 備註 |
|---|---|---|
| 車輛偵測 + 分類（轎車/卡車/機車） | P0 | YOLOv8 開箱即用 |
| 車流量統計（計數線） | P0 | 計算進出畫面車輛數 |
| 車輛跨幀追蹤（Track ID） | P1 | ByteTrack，需跨幀狀態 |
| 車速估算 | P1 | 透視變換 + 幀間距離/時間 |
| 車牌辨識（LPR） | P2 | 車牌 YOLO crop → OCR |

---

## 參、系統架構

```
[影像來源]
  └─ 高公局 MJPEG：https://cctvn.freeway.gov.tw/abs2mjpg/bmjpg?camera=<ID>
        │
        ▼
[Python FastAPI AI 微服務]  :port 8000
  ├─ 影像拉取（定時 polling 或接收 POST 上傳）
  ├─ YOLOv8 偵測 + 分類
  ├─ ByteTrack 追蹤（維護 session 狀態）
  ├─ 透視變換 → 車速估算
  ├─ 車牌 crop → OCR
  └─ 回傳 JSON
        │
        ▼
[C# ASP.NET Core 後端]  :port 5000
  ├─ 定時觸發 AI 分析 或 接收前端請求後觸發
  ├─ HttpClient 轉發至 FastAPI
  ├─ 解析 JSON → 存入資料庫
  └─ SignalR 推播前端
        │
        ▼
[React 前端]  :port 5173
  ├─ 顯示 CCTV 畫面（<img> 載入 MJPEG URL）
  ├─ Canvas overlay 繪製 bbox / 車速 / 車牌
  └─ 側邊欄：即時統計 + 歷史記錄
```

### Port 分配

| 服務 | Port |
|---|---|
| React 前端 (Vite dev) | 5173 |
| C# 後端 (ASP.NET) | 5000 |
| Python AI 微服務 (FastAPI) | 8000 |

---

## 肆、資料交換格式

### AI 微服務回傳 JSON 結構

```json
{
  "camera_id": "C00001",
  "timestamp": "2026-07-23T16:00:00+08:00",
  "frame_index": 42,
  "fps_estimated": 2.5,
  "vehicles": [
    {
      "vehicle_id": 7,
      "class": "car",
      "confidence": 0.91,
      "bbox": {
        "x": 120, "y": 85,
        "width": 80, "height": 50
      },
      "speed_kmh": 98.5,
      "speed_confidence": "low",
      "plate_number": "ABC-1234",
      "plate_confidence": 0.87
    }
  ],
  "statistics": {
    "total_count": 5,
    "car": 3,
    "truck": 1,
    "motorcycle": 1
  },
  "warnings": [
    "fps_unstable: actual fps 2.1, speed estimation accuracy degraded"
  ]
}
```

### 欄位說明

| 欄位 | 型別 | 說明 |
|---|---|---|
| `vehicle_id` | int | ByteTrack 追蹤 ID（跨幀持續） |
| `class` | string | `car` / `truck` / `motorcycle` / `bus` |
| `bbox` | object | 原始影像座標（未縮放） |
| `speed_kmh` | float \| null | 估算車速，FPS 不穩時為 null |
| `speed_confidence` | string | `high` / `low` / `unavailable` |
| `plate_number` | string \| null | 辨識結果，失敗時為 null |
| `plate_confidence` | float \| null | OCR 信心值 0~1 |
| `warnings` | array | 非致命性警告訊息 |

> **設計決策**：`speed_confidence` 和 `warnings` 明確標示估算品質，
> 讓前端決定是否顯示，而非強制呈現不可靠數值。

---

## 伍、各階段開發任務

### 第一階段：Python AI 微服務骨架

**目標**：建立 FastAPI 專案，定義路由，回傳模擬資料

```
ai_service/
├── main.py              # FastAPI 入口
├── routers/
│   └── analyze.py       # POST /api/analyze 路由
├── models/
│   └── schemas.py       # Pydantic 資料模型
├── services/
│   ├── detector.py      # YOLOv8 偵測
│   ├── tracker.py       # ByteTrack 包裝
│   ├── speed_calc.py    # 透視變換 + 速度計算
│   └── lpr.py           # 車牌辨識
├── config/
│   └── cameras.json     # 攝影機 ROI 設定（見下方說明）
├── requirements.txt
└── README.md
```

**路由規格**：

```
POST /api/analyze
  Body: multipart/form-data
    - image: 圖片檔案
    - camera_id: string（用於查詢 ROI 設定）

GET /api/cameras
  回傳所有已設定的攝影機清單

GET /health
  健康檢查
```

---

### 第二階段：AI 核心模組

#### 2-1 YOLOv8 偵測 + 分類

- 模型：`yolov8n.pt` 或更大的 `yolov8m.pt`（依硬體決定）
- 過濾類別：COCO 中的 `car(2)`, `motorcycle(3)`, `bus(5)`, `truck(7)`
- 轉為 Python 套件：`ultralytics`

```python
# services/detector.py 核心邏輯
from ultralytics import YOLO

model = YOLO("yolov8n.pt")
results = model(image, classes=[2, 3, 5, 7])
```

#### 2-2 ByteTrack 跨幀追蹤

- 套件：`ultralytics` 內建（`model.track(...)`）或獨立 `bytetracker`
- **狀態管理**：每個 `camera_id` 維護一個獨立的 tracker 實例

```python
# services/tracker.py
trackers: dict[str, YOLO] = {}  # camera_id -> tracker 實例

def get_tracker(camera_id: str) -> YOLO:
    if camera_id not in trackers:
        trackers[camera_id] = YOLO("yolov8n.pt")
    return trackers[camera_id]
```

> ⚠️ **已知限制（待解決）**：MJPEG FPS 不穩定，ByteTrack 的 Kalman Filter
> 假設幀間時間固定，FPS 變動大時 Track ID 容易丟失。
> **未來改善**：換用穩定 FPS 的影像來源（RTSP/HLS）。

#### 2-3 車速計算

**前提**：需為每個攝影機預先設定 ROI（透視變換參考點）

`config/cameras.json` 格式：
```json
{
  "C00001": {
    "name": "國1 北向 50k",
    "roi": {
      "image_points": [[120,200],[520,200],[580,400],[60,400]],
      "real_world_meters": [0, 10],
      "lane_width_meters": 3.75
    },
    "fps_hint": 2
  }
}
```

速度計算流程：
```
1. 讀取 camera_id 對應的 image_points
2. cv2.getPerspectiveTransform → 鳥瞰座標轉換矩陣
3. 將 bbox 中心點轉換為鳥瞰座標（公尺）
4. 比對上一幀同 vehicle_id 的座標差 → 距離 d
5. 幀間時間 t = 1 / fps_estimated
6. v = (d / t) * 3.6  # 轉 km/h
```

> ⚠️ **已知限制**：`cameras.json` 中的 `image_points` 目前需工程師手動設定。
> **未來改善**：前端提供 Canvas 互動介面，讓使用者在畫面上點選四個標定點。

#### 2-4 車牌辨識

流程：
```
1. 使用 YOLOv8（或 Haar Cascade）定位車牌區域
2. 裁切車牌 ROI（最小建議 80×25 px）
3. 送入 OCR 辨識
4. 用台灣車牌格式驗證：正規表達式 [A-Z0-9]{2,4}-[A-Z0-9]{2,4}
5. 信心值低於閾值（建議 0.6）則回傳 null，不強制輸出
```

OCR 選擇（依優先序）：
- **EasyOCR**：`pip install easyocr`，安裝簡單，Windows 相容性好
- **PaddleOCR**：準確率較高，但 Windows 環境建置較複雜
- **TrOCR**（HuggingFace）：Transformer-based，車牌效果佳

> ⚠️ **已知限制**：高公局攝影機解析度（通常 640×480）下，車牌約 15~20px 寬，
> OCR 辨識率極低。**此功能需更高解析度影像才有實用價值。**
> 目前實作重心放在架構完整性，未來換清晰影像來源後再調整 OCR 參數。

---

### 第三階段：C# 後端整合

#### 3-1 HttpClient 轉發

```csharp
// 接收前端請求，轉發至 FastAPI
public async Task<AnalysisResult> ForwardToAIService(
    IFormFile image, string cameraId)
{
    using var content = new MultipartFormDataContent();
    content.Add(new StreamContent(image.OpenReadStream()), "image", image.FileName);
    content.Add(new StringContent(cameraId), "camera_id");

    var response = await _httpClient.PostAsync("http://localhost:8000/api/analyze", content);
    var json = await response.Content.ReadAsStringAsync();

    // 使用捨棄運算子略過不需要的欄位
    var result = JsonSerializer.Deserialize<AnalysisResult>(json, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });
    return result!;
}
```

#### 3-2 資料庫 Schema（SQL Server）

```sql
CREATE TABLE VehicleDetections (
    Id            BIGINT IDENTITY PRIMARY KEY,
    CameraId      NVARCHAR(20)   NOT NULL,
    VehicleId     INT            NOT NULL,   -- ByteTrack ID
    Class         NVARCHAR(20)   NOT NULL,
    Confidence    FLOAT          NOT NULL,
    SpeedKmh      FLOAT          NULL,
    SpeedConf     NVARCHAR(20)   NULL,       -- high/low/unavailable
    PlateNumber   NVARCHAR(20)   NULL,
    PlateConf     FLOAT          NULL,
    BboxX         INT            NOT NULL,
    BboxY         INT            NOT NULL,
    BboxW         INT            NOT NULL,
    BboxH         INT            NOT NULL,
    Timestamp     DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);

CREATE TABLE FrameStatistics (
    Id          BIGINT IDENTITY PRIMARY KEY,
    CameraId    NVARCHAR(20) NOT NULL,
    TotalCount  INT          NOT NULL,
    CarCount    INT          NOT NULL,
    TruckCount  INT          NOT NULL,
    MotoCount   INT          NOT NULL,
    FpsEstimate FLOAT        NULL,
    Timestamp   DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);
```

#### 3-3 SignalR 推播格式

```csharp
// 推播給前端
await Clients.All.SendAsync("OnAnalysisResult", new {
    cameraId,
    timestamp,
    vehicles = result.Vehicles,
    statistics = result.Statistics,
    warnings = result.Warnings
});
```

---

### 第四階段：前端視覺化

#### 4-1 影像顯示

高公局 MJPEG 可直接用 `<img>` 載入（瀏覽器原生支援 MJPEG 刷新）：

```tsx
<img
  src={`https://cctvn.freeway.gov.tw/abs2mjpg/bmjpg?camera=${cameraId}`}
  alt="CCTV Live"
  ref={imgRef}
/>
```

> ⚠️ **CORS 限制**：直接在前端載入可能遇到 CORS 問題。
> **解決方案**：透過 C# 後端 Proxy 轉發影像串流，前端改連後端 URL。

#### 4-2 Canvas Bbox 疊加

座標縮放公式（原始解析度 → 前端顯示尺寸）：

```ts
const scaleX = displayWidth / originalWidth;
const scaleY = displayHeight / originalHeight;

ctx.strokeRect(
  bbox.x * scaleX,
  bbox.y * scaleY,
  bbox.width * scaleX,
  bbox.height * scaleY
);

// 車速標籤
ctx.fillText(
  `${vehicle.speed_kmh?.toFixed(0) ?? '?'} km/h`,
  bbox.x * scaleX,
  bbox.y * scaleY - 5
);
```

#### 4-3 新增 UI 元件清單

| 元件 | 說明 |
|---|---|
| `CctvPlayer` | 顯示 MJPEG 影像 + Canvas overlay |
| `VehicleList` | 即時偵測到的車輛列表（ID / 速度 / 車牌） |
| `StatisticsPanel` | 車流量統計圖表 |
| `WarningBanner` | 顯示 warnings 陣列的提示列 |
| `CameraSelector` | 選擇不同攝影機（未來用） |

---

### 第五階段：效能與穩定性

- **動態 ROI 觸發**：只在車輛 bbox 進入畫面下方 60% 才執行 LPR（解析度較佳區域）
- **異常處理**：Track ID 丟失時自動重新初始化 tracker
- **Retry 機制**：MJPEG 抓取失敗時，等待 N 秒後重試
- **錯誤日誌**：FastAPI 端寫入 `logs/error.log`，C# 端用 Serilog

---

## 陸、待解決問題清單（Future Work）

> 這些是已知的技術限制，暫時用當前方案繞過，未來應逐步解決。

| # | 問題 | 當前處理方式 | 未來改善方案 |
|---|---|---|---|
| 1 | MJPEG FPS 不穩（1~5 fps） | 車速標示 `speed_confidence: low`，前端顯示警示 | 換用 RTSP/HLS 穩定串流 |
| 2 | 影像解析度低，LPR 效果差 | 架構完整但準確率低，回傳 null 而非錯誤結果 | 換更高解析度影像來源 |
| 3 | ROI 標定需手動設定 JSON | `cameras.json` 由工程師設定 | 前端互動式 Canvas 標定 UI |
| 4 | 多攝影機追蹤器記憶體管理 | 無 TTL 機制，長期運行可能佔用過多記憶體 | 實作 LRU 快取 + TTL 清理機制 |
| 5 | MJPEG CORS 限制 | 先跑 proxy 方案或本機開發略過 | C# 後端實作 MJPEG Proxy |
| 6 | 無攝影機 Camera ID 清單 | 先硬編碼測試用 ID | 串接 TDX API 動態取得清單 + 地理資訊 |

---

## 柒、開發環境與套件

### Python AI 微服務 (`requirements.txt`)

```
fastapi>=0.111.0
uvicorn[standard]>=0.30.0
ultralytics>=8.2.0       # YOLOv8 + ByteTrack
opencv-python>=4.9.0
easyocr>=1.7.1
Pillow>=10.3.0
pydantic>=2.7.0
requests>=2.32.0
```

### C# 後端（新增 NuGet）

```
Microsoft.Extensions.Http    # HttpClient factory
Serilog.AspNetCore           # 結構化日誌
```

### 啟動順序

```bash
# 1. 啟動 Python AI 微服務
cd ai_service
uvicorn main:app --reload --port 8000

# 2. 啟動 C# 後端
cd backend/WebYOLO.Api
dotnet run

# 3. 啟動前端
cd frontend
npm run dev
```

---

## 捌、參考資源

- [YOLOv8 官方文件](https://docs.ultralytics.com/)
- [ByteTrack 論文](https://arxiv.org/abs/2110.06864)
- [OpenCV getPerspectiveTransform](https://docs.opencv.org/4.x/da/d54/group__imgproc__transform.html)
- [EasyOCR GitHub](https://github.com/JaidedAI/EasyOCR)
- [TDX 運輸資料流通服務](https://tdx.transportdata.tw/)
- [高公局開放資料（CCTV 靜態資訊）](https://data.gov.tw/)
