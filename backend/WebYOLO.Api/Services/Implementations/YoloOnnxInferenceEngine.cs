using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using WebYOLO.Api.Services.Interfaces;

namespace WebYOLO.Api.Services.Implementations;

public class YoloOnnxInferenceEngine : IInferenceEngine, IDisposable
{
    private readonly InferenceSession _session;
    private readonly string _inputName;

    public YoloOnnxInferenceEngine(IConfiguration configuration)
    {
        var modelPath = configuration["ModelPath"] ?? "yolov8n.onnx";
        if (!File.Exists(modelPath))
        {
            // fallback, or handled upstream
        }
        
        var options = new SessionOptions();
        options.AppendExecutionProvider_CPU(); // Ensure CPU provider is used
        
        // Let it throw if file not found, proper handling is outside scope of POC or should be ensured before run
        _session = new InferenceSession(modelPath, options);
        _inputName = _session.InputMetadata.Keys.First();
    }

    public int[] GetInputDimensions()
    {
        var dims = _session.InputMetadata[_inputName].Dimensions;
        // Typically [-1, 3, 640, 640], we replace -1 with 1 for batch size
        return new int[] { 1, dims[1], dims[2], dims[3] };
    }

    public float[] RunInference(float[] inputTensorData, int[] dimensions)
    {
        var tensor = new DenseTensor<float>(inputTensorData, dimensions);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputName, tensor)
        };

        using var results = _session.Run(inputs);
        var output = results.First().AsTensor<float>().ToArray();
        return output;
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
