using System.Collections.Generic;

namespace ConvNetSharp.Core.Layers.Double
{
    [System.Serializable]
    public class ReluLayer : ReluLayer<double>
    {
        public ReluLayer()
        {
        }

        public ReluLayer(Dictionary<string, object> data) : base(data)
        {
        }
    }
}