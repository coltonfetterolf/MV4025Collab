using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MGEntity : Entity
{

    const float range = 1000f;

    override public float GetRange() { return range;  }

}