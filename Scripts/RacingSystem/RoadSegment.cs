using UnityEngine;

public class RoadSegment : MonoBehaviour
{
    public RoadNode startNode;
    public RoadNode endNode;
    
    [Header("Spline Selection")]
    public int splineIndex = 0;
    public bool useAllSplines = false;
}