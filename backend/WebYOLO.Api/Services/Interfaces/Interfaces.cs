namespace WebYOLO.Api.Services.Interfaces;

public interface IImageProcessor
{
    float[] ProcessImage(byte[] imageBytes, int targetWidth, int targetHeight);
}

public interface IInferenceEngine
{
    float[] RunInference(float[] inputTensor, int[] dimensions);
    int[] GetInputDimensions();
}

public interface INmsProcessor
{
    IEnumerable<Models.DetectionResultDto> Process(float[] rawOutput, float confidenceThreshold);
}

public interface IDetectionAppService
{
    IEnumerable<Models.DetectionResultDto> DetectObjects(byte[] imageBytes);
}
