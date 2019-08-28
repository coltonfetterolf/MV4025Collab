using System.Collections.Generic;

namespace ConvNetSharp.Core.Layers.Single
{
    [System.Serializable]
    public class ReluLayer : ReluLayer<float>
    {
        public ReluLayer()
        {
            
        }

        public ReluLayer(Dictionary<string, object> data) : base(data)
        {
        }
    }
}