using Mapsui;

namespace RainRadar
{
    public class DataPoint
    {
        MQuad _quad;
        double _value;

        public DataPoint(MQuad quad, double value)
        {
            _quad = quad;
            _value = value;
        }

        public MQuad Quad => _quad;

        public double Value => _value;
    }
}
