﻿using System.Collections.Generic;

namespace ConvNetSharp.Core.Layers.Double
{
    [System.Serializable]
    public class SigmoidLayer : SigmoidLayer<double>
    {
        public SigmoidLayer(Dictionary<string, object> data) : base(data)
        {
        }

        public SigmoidLayer()
        {
        }
    }
}