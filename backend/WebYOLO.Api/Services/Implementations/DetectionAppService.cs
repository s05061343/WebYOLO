using WebYOLO.Api.Models;
using WebYOLO.Api.Services.Interfaces;

namespace WebYOLO.Api.Services.Implementations;

public class DetectionAppService : IDetectionAppService
{
    private readonly IImageProcessor _imageProcessor;
    private readonly IInferenceEngine _inferenceEngine;
    private readonly INmsProcessor _nmsProcessor;

    public DetectionAppService(
        IImageProcessor imageProcessor,
        IInferenceEngine inferenceEngine,
        INmsProcessor nmsProcessor)
    {
        _imageProcessor = imageProcessor;
        _inferenceEngine = inferenceEngine;
        _nmsProcessor = nmsProcessor;
    }

    public IEnumerable<DetectionResultDto> DetectObjects(byte[] imageBytes)
    {
        var dims = _inferenceEngine.GetInputDimensions();
        // target width and height are typically at index 3 and 2 for CHW format
        int targetWidth = dims[3];
        int targetHeight = dims[2];

        // 1. Pre-process
        var tensorData = _imageProcessor.ProcessImage(imageBytes, targetWidth, targetHeight);

        // 2. Inference
        var rawOutput = _inferenceEngine.RunInference(tensorData, dims);

        // 3. Post-process (NMS)
        var results = _nmsProcessor.Process(rawOutput, 0.25f); // 0.25 confidence threshold

        return results;
    }
}
