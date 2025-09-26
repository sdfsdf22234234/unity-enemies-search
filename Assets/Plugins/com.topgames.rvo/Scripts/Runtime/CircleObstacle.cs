using UnityEngine;

public class CircleObstacle : ShapeObstacleBase
{
    [SerializeField] float radius = 0.5f;
    [SerializeField][Range(3, 100)] int vertexCount = 6;
    protected override void CalculatePoints()
    {
        float anglePadding = 360f / vertexCount;

        var vec = Vector3.forward * radius * transform.localScale.x;

        var curPos = transform.position + m_Center;
        curPos.y = 0;
        for (int i = 0; i < vertexCount; i++)
        {
            mCacheVertexList.Add(curPos + vec);
            vec = Quaternion.AngleAxis(anglePadding, transform.up) * vec;
        }
    }
}
