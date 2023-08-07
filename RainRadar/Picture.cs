using Mapsui;
using SkiaSharp;
using System;

namespace RainRadar;

public class Picture : MRect
{
    public Picture(Picture picture) : base(picture.Min.X, picture.Min.Y, picture.Max.X, picture.Max.Y)
    {
        Data = picture.Data;
        TickFetched = picture.TickFetched;
    }

    public Picture(SKPicture data, MRect rect) : base(rect)
    {
        Data = data;
        TickFetched = DateTime.Now.Ticks;
    }

    public SKPicture Data { get; }
    public long TickFetched { get; }
}
