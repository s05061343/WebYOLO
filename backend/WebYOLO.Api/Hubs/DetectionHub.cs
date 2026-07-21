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

    public async Task Detect(string base64Image)
    {
        try
        {
            var commaIndex = base64Image.IndexOf(',');
            if (commaIndex >= 0)
            {
                base64Image = base64Image.Substring(commaIndex + 1);
            }
            
            var imageBytes = Convert.FromBase64String(base64Image);
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
