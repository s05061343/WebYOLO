namespace WebYOLO.Api.Models;

public class BoundingBoxDto
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
}

public class DetectionResultDto
{
    public string Label { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public BoundingBoxDto BoundingBox { get; set; } = new BoundingBoxDto();
}
