using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Road : MonoBehaviour
{
    public enum JointKey
    {
        IRoad = 0,
        LRoadL = 90,
        LRoadR = -90,
        URoadL = 180,
        URoadR = -180,
    }
    
    [System.Serializable]
    public struct Joint
    {
        public JointKey key;
        public Transform transform;
    }

    public Vector2 unit;
    
    public List<Joint> joints;
    
    public List<Transform> wayPoints;
}
