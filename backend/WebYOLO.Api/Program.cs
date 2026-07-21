using WebYOLO.Api.Hubs;
using WebYOLO.Api.Services.Implementations;
using WebYOLO.Api.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddSignalR(options =>
{
    // YOLO image blobs can be large
    options.MaximumReceiveMessageSize = 5 * 1024 * 1024; // 5 MB
});

// Dependency Injection
builder.Services.AddSingleton<IImageProcessor, OpenCvImageProcessor>();
builder.Services.AddSingleton<IInferenceEngine, YoloOnnxInferenceEngine>();
builder.Services.AddSingleton<INmsProcessor, YoloNmsProcessor>();
builder.Services.AddSingleton<IDetectionAppService, DetectionAppService>();

var app = builder.Build();

app.UseCors("AllowFrontend");

app.MapHub<DetectionHub>("/detectionHub");

app.Run();
