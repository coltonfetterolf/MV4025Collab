using System.Collections.Generic;

namespace ConvNetSharp.Core.Layers.Double
{
    [System.Serializable]
    public class PoolLayer : PoolLayer<double>
    {
        public PoolLayer(Dictionary<string, object> data) : base(data)
        {
        }

        public PoolLayer(int width, int height) : base(width, height)
        {
        }
    }
}