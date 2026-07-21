using System.Runtime.InteropServices;
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

        Cv2.Split(floatMat, out Mat[] splitMats);
        var channelSize = h * w;
        for (int i = 0; i < channels; i++)
        {
            Marshal.Copy(splitMats[i].Data, tensorData, i * channelSize, channelSize);
            splitMats[i].Dispose();
        }

        return tensorData;
    }
}
