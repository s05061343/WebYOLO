using Microsoft.AspNetCore.SignalR;
using WebYOLO.Api.Services.Interfaces;

namespace WebYOLO.Api.Hubs;

public class DetectionHub : Hub
{
    private readonly IDetectionAppService _detectionAppService;

    public DetectionHub(IDetectionAppService detectionAppService)
    {
        _detectionAppService = detectionAppService;
    }

    public async Task Detect(byte[] imageBytes)
    {
        try
        {
            var results = _detectionAppService.DetectObjects(imageBytes);
            
            await Clients.Caller.SendAsync("OnDetectionResult", new
            {
                status = "success",
                data = results
            });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("OnDetectionResult", new
            {
                status = "error",
                message = ex.Message
            });
        }
    }
}
