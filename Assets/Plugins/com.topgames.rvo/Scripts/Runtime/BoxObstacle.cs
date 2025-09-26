using UnityEngine;

/// <summary>
/// BoxCollider转化为RVO障碍物
/// </summary>
public class BoxObstacle : ShapeObstacleBase
{
    [SerializeField] UnityEngine.Vector2 boxSize = UnityEngine.Vector2.one;

    protected override void CalculatePoints()
    {
        var curPos = transform.position + m_Center;
        curPos.y = 0;
        var halfSize = new UnityEngine.Vector2(transform.lossyScale.x, transform.lossyScale.z) * boxSize * 0.5f;
        var rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        var pointA = curPos + rotation * (Vector3.right * (halfSize.x + m_Center.x) + Vector3.forward * (halfSize.y + m_Center.y));
        var pointB = curPos + rotation * (Vector3.left * (halfSize.x - m_Center.x) + Vector3.forward * (halfSize.y + m_Center.y));
        var pointC = curPos + rotation * (Vector3.left * (halfSize.x - m_Center.x) + Vector3.back * (halfSize.y - m_Center.y));
        var pointD = curPos + rotation * (Vector3.right * (halfSize.x + m_Center.x) + Vector3.back * (halfSize.y - m_Center.y));

        mCacheVertexList.Add(pointA);
        mCacheVertexList.Add(pointB);
        mCacheVertexList.Add(pointC);
        mCacheVertexList.Add(pointD);
    }
}
