using OpenCvSharp;
using OpenCvSharp.Dnn;
using WebYOLO.Api.Models;
using WebYOLO.Api.Services.Interfaces;

namespace WebYOLO.Api.Services.Implementations;

public class YoloNmsProcessor : INmsProcessor
{
    private readonly string[] _labels;

    public YoloNmsProcessor(IConfiguration configuration)
    {
        // Load labels from configuration or use fallback
        var labelsConfig = configuration.GetSection("YoloLabels").Get<string[]>();
        _labels = labelsConfig ?? new[] { "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat", "traffic light", "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat", "dog", "horse", "sheep", "cow", "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase", "frisbee", "skis", "snowboard", "sports ball", "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket", "bottle", "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple", "sandwich", "orange", "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "couch", "potted plant", "bed", "dining table", "toilet", "tv", "laptop", "mouse", "remote", "keyboard", "cell phone", "microwave", "oven", "toaster", "sink", "refrigerator", "book", "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush" };
    }

    public IEnumerable<DetectionResultDto> Process(float[] rawOutput, float confidenceThreshold)
    {
        // YOLOv8 output shape is [1, 84, 8400] for COCO (4 coords + 80 classes, 8400 anchors)
        int numClasses = 80;
        int numAnchors = 8400;
        
        var boxes = new List<Rect2d>();
        var scores = new List<float>();
        var classIds = new List<int>();

        for (int i = 0; i < numAnchors; i++)
        {
            float maxScore = 0f;
            int classId = -1;

            // Find max class score for this anchor
            for (int c = 0; c < numClasses; c++)
            {
                // rawOutput is flat, conceptually [84, 8400]
                // index = c_idx * numAnchors + anchor_idx
                float score = rawOutput[(4 + c) * numAnchors + i];
                if (score > maxScore)
                {
                    maxScore = score;
                    classId = c;
                }
            }

            if (maxScore >= confidenceThreshold)
            {
                float xc = rawOutput[0 * numAnchors + i];
                float yc = rawOutput[1 * numAnchors + i];
                float w = rawOutput[2 * numAnchors + i];
                float h = rawOutput[3 * numAnchors + i];

                float x = xc - (w / 2);
                float y = yc - (h / 2);

                boxes.Add(new Rect2d(x, y, w, h));
                scores.Add(maxScore);
                classIds.Add(classId);
            }
        }

        // Apply NMS
        CvDnn.NMSBoxes(boxes, scores, confidenceThreshold, 0.45f, out int[] indices);

        var results = new List<DetectionResultDto>();
        foreach (int idx in indices)
        {
            var box = boxes[idx];
            var cid = classIds[idx];
            var label = cid >= 0 && cid < _labels.Length ? _labels[cid] : $"Class {cid}";

            results.Add(new DetectionResultDto
            {
                Label = label,
                Confidence = scores[idx],
                BoundingBox = new BoundingBoxDto
                {
                    X = (float)box.X,
                    Y = (float)box.Y,
                    Width = (float)box.Width,
                    Height = (float)box.Height
                }
            });
        }

        return results;
    }
}
