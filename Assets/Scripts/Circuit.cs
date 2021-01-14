using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Circuit : MonoBehaviour
{
    public List<Transform> waypoints = new List<Transform>();

    private void OnDrawGizmos()
    {
        DrawGizmos(false);
    }

    private void OnDrawGizmosSelected()
    {
        DrawGizmos(true);
    }

    void DrawGizmos(bool selected)
    {
        if (!selected) return;

        if (waypoints.Count > 1)
        {
            Vector3 previous = waypoints[0].transform.position;

            for (int i = 0; i < waypoints.Count; i++)
            {
                Vector3 next = waypoints[i].transform.position;
                
                Gizmos.DrawLine(previous, next);

                previous = next;
            }
            
            Gizmos.DrawLine(previous, waypoints[0].transform.position);
        }
    }
}
