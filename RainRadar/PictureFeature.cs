using Mapsui;
using Mapsui.Layers;
using System;

namespace RainRadar;

public class PictureFeature : BaseFeature, IFeature
{
    public Picture? Picture { get; }
    public MRect? Extent => Picture;

    public PictureFeature(PictureFeature pictureFeature) : base(pictureFeature)
    {
        Picture = pictureFeature.Picture == null ? null : new Picture(pictureFeature.Picture);
    }

    public PictureFeature(Picture? picture)
    {
        Picture = picture;
    }

    public void CoordinateVisitor(Action<double, double, CoordinateSetter> visit)
    {
        if (Picture != null)
            foreach (var point in new[] { Picture.Min, Picture.Max })
                visit(point.X, point.Y, (x, y) =>
                {
                    point.X = x;
                    point.Y = y;
                });
    }
}
