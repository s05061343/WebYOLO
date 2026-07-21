using OpenCvSharp;
using WebYOLO.Api.Services.Interfaces;

namespace WebYOLO.Api.Services.Implementations;

public class OpenCvImageProcessor : IImageProcessor
{
    public float[] ProcessImage(byte[] imageBytes, int targetWidth, int targetHeight)
    {
        // Decode image from bytes
        using var srcMat = Cv2.ImDecode(imageBytes, ImreadModes.Color);
        if (srcMat.Empty())
        {
            throw new ArgumentException("Failed to decode image bytes.");
        }

        // Convert BGR to RGB
        using var rgbMat = new Mat();
        Cv2.CvtColor(srcMat, rgbMat, ColorConversionCodes.BGR2RGB);

        // Resize
        using var resizedMat = new Mat();
        Cv2.Resize(rgbMat, resizedMat, new Size(targetWidth, targetHeight));

        // Normalize and convert to Float (CHW format)
        // YOLOv8 expects values in range [0, 1] and shape [1, 3, targetHeight, targetWidth]
        using var floatMat = new Mat();
        resizedMat.ConvertTo(floatMat, MatType.CV_32FC3, 1.0 / 255.0);

        // Extract channels to CHW
        var h = floatMat.Rows;
        var w = floatMat.Cols;
        var channels = floatMat.Channels();
        var tensorData = new float[channels * h * w];

        var index = 0;
        var indexer = floatMat.GetGenericIndexer<Vec3f>();
        for (int c = 0; c < channels; c++)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    tensorData[index++] = indexer[y, x][c];
                }
            }
        }

        return tensorData;
    }
}
