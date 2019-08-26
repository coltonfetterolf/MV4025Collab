using System.Collections.Generic;

namespace ConvNetSharp.Core.Layers.Single
{
    [System.Serializable]
    public class RegressionLayer : RegressionLayer<float>
    {
        public RegressionLayer(Dictionary<string, object> data) : base(data)
        {
        }

        public RegressionLayer()
        {
        }
    }
}