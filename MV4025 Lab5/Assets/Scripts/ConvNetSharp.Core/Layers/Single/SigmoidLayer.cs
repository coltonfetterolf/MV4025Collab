using System.Collections.Generic;

namespace ConvNetSharp.Core.Layers.Single
{
    [System.Serializable]
    public class SigmoidLayer : SigmoidLayer<float>
    {
        public SigmoidLayer(Dictionary<string, object> data) : base(data)
        {
        }

        public SigmoidLayer()
        {
        }
    }
}