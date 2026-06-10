using System.Collections.Generic;
using UnityEngine;

public class RoadNode : MonoBehaviour
{
    public List<RoadSegment> connectedSegments = new();

    public bool isIntersection = true;
    public bool allowLeft = true;
    public bool allowRight = true;
    public bool allowStraight = true;
}