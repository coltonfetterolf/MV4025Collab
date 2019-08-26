using System.Collections.Generic;

namespace ConvNetSharp.Core.Layers.Double
{
    [System.Serializable]
    public class InputLayer : InputLayer<double>
    {
        public InputLayer(Dictionary<string, object> data) : base(data)
        {
        }

        public InputLayer(int inputWidth, int inputHeight, int inputDepth) : base(inputWidth, inputHeight, inputDepth)
        {
        }
    }
}