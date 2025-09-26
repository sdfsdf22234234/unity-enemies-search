
using System.Collections.Generic;
using UnityEngine;

public class EdgeObstacle : ShapeObstacleBase
{
    [SerializeField] List<Vector3> points;

    protected override bool IsEdge => true; 
    protected override void CalculatePoints()
    {
        if (points == null) return;
        var center = transform.position + m_Center;
        center.y = 0;
        var rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        var scale = transform.localScale;
        for (int i = 0; i < points.Count; i++)
        {
            var point = points[i];
            point.x *= scale.x;
            point.z *= scale.z;
            point = center + rotation * point;
            mCacheVertexList.Add(point);
        }
    }
}
