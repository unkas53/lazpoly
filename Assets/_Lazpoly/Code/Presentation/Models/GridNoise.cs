namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// Soft noise over the unit square, for the textures drawn in code (<see cref="LawnSkin"/>, <see cref="RoadSkin"/>):
    /// random numbers from 0 to 1 on a grid, blended smoothly between its points. The same seed gives the same noise.
    /// </summary>
    public sealed class GridNoise
    {
        private readonly float[] _values;
        private readonly int _across, _along;

        /// <summary>A grid of <paramref name="across"/> by <paramref name="along"/> cells over the square.</summary>
        public GridNoise(int across, int along, int seed)
        {
            _across = across;
            _along = along;
            _values = new float[(across + 2) * (along + 2)];
            var random = new System.Random(seed);
            for (int i = 0; i < _values.Length; i++) _values[i] = (float)random.NextDouble();
        }

        /// <summary>The noise at a point of the square (u, v from 0 to 1; outside it the edge's value).</summary>
        public float At(float u, float v)
        {
            float x = Clamp01(u) * _across, y = Clamp01(v) * _along;
            int ix = System.Math.Min(_across, (int)x), iy = System.Math.Min(_along, (int)y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int row = iy * (_across + 2) + ix, next = row + _across + 2;
            float low = _values[row] + (_values[row + 1] - _values[row]) * fx;
            float high = _values[next] + (_values[next + 1] - _values[next]) * fx;
            return low + (high - low) * fy;
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
