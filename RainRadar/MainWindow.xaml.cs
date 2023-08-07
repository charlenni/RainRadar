using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Rendering.Skia.SkiaWidgets;
using Mapsui.Styles;
using Mapsui.Utilities;
using Mapsui.Widgets.PerformanceWidget;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;

namespace RainRadar
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static readonly MPoint bottomLeft = new MPoint(3.604382997, 46.95361533);
        static readonly MPoint topLeft = new MPoint(2.095883211, 54.58546706);
        static readonly MPoint bottomRight = new MPoint(14.60482286, 47.07156997);
        static readonly MPoint topRight = new MPoint(15.69697166, 54.73806893);

        MPoint _center;
        readonly Performance _performance = new(10);

        static readonly Dictionary<double, SKColor> colorTable = new Dictionary<double, SKColor> {
            { double.MinValue, new SKColor(136, 136, 136) },
            { 0.06, new SKColor(255, 255, 255, 0) },
            { 0.12, new SKColor(0, 255, 255) },
            { 0.21, new SKColor(0, 136, 255) },
            { 0.36, new SKColor(0, 101, 189) },
            { 0.65, new SKColor(0, 255, 0) },
            { 1.15, new SKColor(0, 205, 0) },
            { 2.05, new SKColor(0, 136, 0) },
            { 3.65, new SKColor(255, 255, 0) },
            { 6.48, new SKColor(255, 190, 0) },
            { 11.53, new SKColor(255, 136, 0) },
            { 20.50, new SKColor(255, 0, 0) },
            { 36.46, new SKColor(204, 0, 0) },
            { 64.48, new SKColor(139, 0, 0) },
            { 115.31, new SKColor(255, 0, 255) },
            { 205.05, new SKColor(170, 0, 255) },
            { double.MaxValue, new SKColor(0, 0, 0) },
        }; 

        /* static readonly Dictionary<double, SKColor> colorTable = new Dictionary<double, SKColor> {
            { double.MinValue, new SKColor(136, 136, 136) },
            { 0.0, new SKColor(255, 255, 255, 0) },
            { 12.0, new SKColor(0, 255, 255) },
            { 24.0, new SKColor(0, 136, 255) },
            { 36.0, new SKColor(0, 101, 189) },
            { 48.0, new SKColor(0, 255, 0) },
            { 60.0, new SKColor(0, 205, 0) },
            { 72.0, new SKColor(0, 136, 0) },
            { 84.0, new SKColor(255, 255, 0) },
            { 96.0, new SKColor(255, 190, 0) },
            { 108.0, new SKColor(255, 136, 0) },
            { 120.0, new SKColor(255, 0, 0) },
            { 132.0, new SKColor(204, 0, 0) },
            { 144.0, new SKColor(139, 0, 0) },
            { 156.0, new SKColor(255, 0, 255) },
            { 168.0, new SKColor(170, 0, 255) },
            { double.MaxValue, new SKColor(0, 0, 0) },
        }; */

        public MainWindow()
        {
            InitializeComponent();

            var mapControl = new Mapsui.UI.Wpf.MapControl();

            mapControl.Renderer.StyleRenderers.Add(typeof(PictureStyle), new PictureStyleRenderer());

            mapControl.Map?.Layers.Add(Mapsui.Tiling.OpenStreetMap.CreateTileLayer());

            mapControl.Performance = _performance;
            mapControl.Map?.Widgets.Add(new PerformanceWidget(_performance));
            mapControl.Renderer.WidgetRenders[typeof(PerformanceWidget)] = new PerformanceWidgetRenderer(10, 10, 12, SKColors.Black, SKColors.White);

            mapControl.Map?.Layers.Add(CreateDataPointLayer(mapControl.Map));

            Content = mapControl;
        }

        private ILayer CreateDataPointLayer(Map map)
        {
            _center = SphericalMercator.FromLonLat(8, 51).ToMPoint();

            map.Home = (n) => { n.CenterOnAndZoomTo(_center, 150); };

            // Read data 
            var data = ReadSquareData("RainRadar.Resources.germany_data_900kmx900km.txt", 900, 900);

            // Create SKPicture from data
            var picture = CreatePicture(data);
            
            // Get extent of data
            var (left, top, right, bottom) = CalcRectForData();
            var rect = new MRect(left, top, right, bottom);

            // Place the picture on map
            var pictureFeature = new PictureFeature(new Picture(picture, rect));

            pictureFeature.Styles = new List<IStyle> { new PictureStyle { } };

            var memoryLayer = new MemoryLayer("DataPointLayer");

            memoryLayer.Style = null;

            var features = new List<PictureFeature> { pictureFeature };
            
            memoryLayer.Features = features;

            return memoryLayer;
        }

        private SKPicture CreatePicture(DataPoint[][] data)
        {
            // Calc rect for data
            var cullRect = CalcMaxRectForData(900, 900);

            var result = new SKPictureRecorder();

            var canvas = result.BeginRecording(new SKRect(0, 0, (float)cullRect.Width, (float)cullRect.Height));

            var paint = new SKPaint { Color = SKColors.Pink, IsStroke = false, StrokeWidth = 1, };

            var path = new SKPath();
            var startY = 0;

            SKColor color = SKColors.LightPink;
            SKColor lastColor = SKColors.LightPink;

            for (var x = 0; x < 900; x++)
            {
                lastColor = GetColorForValue(data[x][0].Value);
                startY = 0;

                for (var y = 0; y < 900; y++)
                {
                    color = GetColorForValue(data[x][y].Value);

                    if (color == lastColor &&  y - startY < 20) // If we add more than 20 points, then the there are rounding problems
                    {
                        continue;
                    }

                    if (lastColor.Alpha != 0)
                    {
                        paint.Color = lastColor;

                        path.MoveTo((float)(data[x][startY].Quad.BottomLeft.X - cullRect.Left), (float)(cullRect.Top - data[x][startY].Quad.BottomLeft.Y));
                        path.LineTo((float)(data[x][startY].Quad.BottomRight.X - cullRect.Left), (float)(cullRect.Top - data[x][startY].Quad.BottomRight.Y));
                        path.LineTo((float)(data[x][y].Quad.TopRight.X - cullRect.Left), (float)(cullRect.Top - data[x][y].Quad.TopRight.Y));
                        path.LineTo((float)(data[x][y].Quad.TopLeft.X - cullRect.Left), (float)(cullRect.Top - data[x][y].Quad.TopLeft.Y));
                        path.Close();

                        canvas.DrawPath(path, paint);

                        path.Reset();
                    }

                    startY = y;
                    lastColor = color;
                }

                if (startY < 899)
                {
                    paint.Color = lastColor;

                    path.MoveTo((float)(data[x][startY].Quad.BottomLeft.X - cullRect.Left), (float)(cullRect.Top - data[x][startY].Quad.BottomLeft.Y));
                    path.LineTo((float)(data[x][startY].Quad.BottomRight.X - cullRect.Left), (float)(cullRect.Top - data[x][startY].Quad.BottomRight.Y));
                    path.LineTo((float)(data[x][899].Quad.TopRight.X - cullRect.Left), (float)(cullRect.Top - data[x][899].Quad.TopRight.Y));
                    path.LineTo((float)(data[x][899].Quad.TopLeft.X - cullRect.Left), (float)(cullRect.Top - data[x][899].Quad.TopLeft.Y));
                    path.Close();

                    canvas.DrawPath(path, paint);

                    path.Reset();
                }
            }

            return result.EndRecording();
        }

        private SKColor GetColorForValue(double value)
        {
            return colorTable.Where(kv => kv.Key >= value).First().Value;
        }

        private (float, float, float, float) CalcRectForData()
        {
            var bl = SphericalMercator.FromLonLat(bottomLeft);
            var tl = SphericalMercator.FromLonLat(topLeft);
            var br = SphericalMercator.FromLonLat(bottomRight);
            var tr = SphericalMercator.FromLonLat(topRight);

            var left = (float)Math.Min(bl.X, tl.X);
            var right = (float)Math.Max(br.X, tr.X);
            var bottom = (float)Math.Min(bl.Y, br.Y);
            var top = (float)Math.Max(tl.Y, tr.Y);

            return (left, top, right, bottom);
        }

        private DataPoint[][] ReadSquareData(string resourceName, int dataPointsInX, int dataPointsInY)
        {
            var result = new DataPoint[dataPointsInX][];

            for (var i = 0; i < dataPointsInX; i++)
            {
                result[i] = new DataPoint[dataPointsInY];
            }

            var assembly = Assembly.GetExecutingAssembly();

            using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    return result;
                }

                var x = 0;

                using (StreamReader reader = new StreamReader(stream))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();

                        var values = ReadValuesOfLine(line);

                        for (var y = 0; y < dataPointsInY; y++) 
                        { 
                            var value = values[y] == -999.0 ? double.MinValue : values[y];
                            var quad = CalcMapsuiQuad(x, dataPointsInY - y - 1);

                            result[x][dataPointsInY - y - 1] = new DataPoint(quad, value);
                        }

                        x++;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Read all values from content of a string
        /// </summary>
        /// <param name="line">String with all values to read</param>
        /// <returns>Array of doubles with the values</returns>
        private double[] ReadValuesOfLine(string? line) 
        {
            var list = new List<double>();

            if (line == null)
            {
                return new double[0];
            }

            Match match = Regex.Match(line, @"(-?\d+(\.\d+)?)|(\.\d+)");

            while (match.Success)
            {
                if (double.TryParse(match.Value, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out double value))
                {
                    list.Add(value);
                }
                else
                {
                    list.Add(double.MinValue);
                }

                match = match.NextMatch();
            }

            return list.ToArray();
        }

        /// <summary>
        /// Calculates the biggest size of the data point grid
        /// </summary>
        /// <remarks>
        /// Because the datapoints are not evenly spaced in geographical space, it
        /// could be, that the size in the middle of the rect is bigger than on the 
        /// sides.
        /// </remarks>
        /// <param name="dataPointsInX">Number of data points in x direction</param>
        /// <param name="dataPointsInY">Number of data points in y direction</param>
        /// <returns></returns>
        private MRect CalcMaxRectForData(int dataPointsInX, int dataPointsInY)
        {
            var left = double.MaxValue;
            var right = double.MinValue;
            var top = double.MinValue;
            var bottom = double.MaxValue;

            for (var i = 0; i <= dataPointsInY; i++)
            {
                left = Math.Min(left, ConvertKartesianToGeographical(0, i).Item1);
                right = Math.Max(right, ConvertKartesianToGeographical(dataPointsInX, i).Item1);
            }

            for (var j = 0; j <= dataPointsInX; j++)
            {
                bottom = Math.Min(bottom, ConvertKartesianToGeographical(j, 0).Item2);
                top = Math.Max(top, ConvertKartesianToGeographical(j, dataPointsInY).Item2);
            }

            var bl = SphericalMercator.FromLonLat(left, bottom);
            var tr = SphericalMercator.FromLonLat(right, top);

            return new MRect(bl.x, bl.y, tr.x, tr.y);
        }

        static double _radius = 6370.040;
        static double _x0 = -523.4622;
        static double _y0 = -4658.645;
        static double _lambda0 = 10.0 * Math.PI / 180.0;
        static double _phi0 = 60.0 * Math.PI / 180.0;
        static double _factor = _radius * _radius * (1 + Math.Sin(_phi0)) * (1 + Math.Sin(_phi0));

        /// <summary>
        /// Convert kartesian coordinate to geographical coordinate
        /// </summary>
        /// <param name="j">X coordinate of data point</param>
        /// <param name="i">Y coordinate of data point</param>
        /// <returns>Geographical coordinate in lat/lon</returns>
        private (double, double) ConvertKartesianToGeographical(int j, int i)
        {
            var x = _x0 + j;
            var y = _y0 + i;

            var lambda = (Math.Atan(-x / y) + _lambda0) * 180.0 / Math.PI;
            var square = x * x + y * y;
            var phi = Math.Asin((_factor - square) / (_factor + square)) * 180.0 / Math.PI;

            return (lambda, phi);
        }

        /// <summary>
        /// Get all fourc corners of data point
        /// </summary>
        /// <param name="j">X coordinate for data point</param>
        /// <param name="i">Y coordinate for data point</param>
        /// <returns>Quad with all four corners</returns>
        private MQuad CalcMapsuiQuad(int j, int i)
        {
            var result = new MQuad();

            (var x, var y) = ConvertKartesianToGeographical(j, i);

            result.BottomLeft = SphericalMercator.FromLonLat(new MPoint(x, y));

            (x, y) = ConvertKartesianToGeographical(j + 1, i);

            result.BottomRight = SphericalMercator.FromLonLat(new MPoint(x, y));

            (x, y) = ConvertKartesianToGeographical(j, i + 1);

            result.TopLeft = SphericalMercator.FromLonLat(new MPoint(x, y));

            (x, y) = ConvertKartesianToGeographical(j + 1, i + 1);

            result.TopRight = SphericalMercator.FromLonLat(new MPoint(x, y));

            return result;
        }
    }
}
