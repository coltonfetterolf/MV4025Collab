using System.Collections.Generic;

namespace ConvNetSharp.Core.Layers.Double
{
    [System.Serializable]
    public class SoftmaxLayer : SoftmaxLayer<double>
    {
        public SoftmaxLayer(Dictionary<string, object> data) : base(data)
        {
        }

        public SoftmaxLayer(int classCount) : base(classCount)
        {
        }
    }
}