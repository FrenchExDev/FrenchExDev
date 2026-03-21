namespace FrenchExDev.Net.Diem.Media;

public sealed class ThumbnailOptions
{
    public int? Width { get; set; }
    public int? Height { get; set; }
    public ThumbnailMethod Method { get; set; } = ThumbnailMethod.Fit;
    public int Quality { get; set; } = 85;
}

public enum ThumbnailMethod { Fit, Center, Scale, Inflate }
