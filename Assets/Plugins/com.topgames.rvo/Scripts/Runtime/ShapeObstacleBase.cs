
using Nebukam.ORCA;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public abstract class ShapeObstacleBase : MonoBehaviour
{
    [SerializeField] private bool m_AutoApplyObstacle = true;
    [SerializeField] protected Vector3 m_Center = Vector3.zero;
    protected List<float3> mCacheVertexList = new List<float3>();
    protected Obstacle mObstacles;

    private bool m_Applied;
    protected virtual bool IsEdge => false;
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        if (mCacheVertexList != null && mCacheVertexList.Count > 2)
        {
            Gizmos.color = Color.red;
            for (int i = 0; i < mCacheVertexList.Count - 1; ++i)
            {
                Gizmos.DrawLine(mCacheVertexList[i], mCacheVertexList[i + 1]);
            }
            int vCount = mCacheVertexList.Count;
            Gizmos.DrawLine(mCacheVertexList[vCount - 1], mCacheVertexList[0]);
            float3 centerPos = transform.position + m_Center;
            centerPos.y = 0;
            for (int i = 0; i < mCacheVertexList.Count; i++)
            {
                var point = mCacheVertexList[i];
                point.y = 0;
                var dirA = Unity.Mathematics.math.normalize(point - centerPos);
                float angle = UnsignedAngle(Vector3.SignedAngle(Vector3.forward, dirA, Vector3.up));
                Handles.Label(mCacheVertexList[i], $"[{i}]:{angle}");
            }
        }
    }
    private void OnValidate()
    {
        GenerateVertex();
    }
#endif

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (!EditorApplication.isPlaying) return;
#endif
        if (m_AutoApplyObstacle)
        {
            AddObstacles();
        }
    }
    private void OnDisable()
    {
#if UNITY_EDITOR
        if (!EditorApplication.isPlaying) return;
#endif
        RemoveObstacles();
    }
    /// <summary>
    /// 移除障碍物
    /// </summary>
    public void RemoveObstacles()
    {
        if (mObstacles != null)
        {
            RVOComponent.Instance.RemoveObstacle(mObstacles);
        }
        m_Applied = false;
    }
    public Obstacle AddObstacles()
    {
        if (m_Applied) return mObstacles;

        var points = GenerateVertex();
        InternalApplyObstacles(points);
        m_Applied = true;
        mCacheVertexList.Clear();
        return mObstacles;
    }
    private List<float3> GenerateVertex()
    {
        mCacheVertexList.Clear();

        CalculatePoints();
        SortVertices(ref mCacheVertexList);
        return mCacheVertexList;
    }
    private void SortVertices(ref List<float3> vertices)
    {
        float3 centerPos = transform.position + m_Center;
        centerPos.y = 0;

        var sortByDir = Vector3.forward;
        int sortSign = -1;
        vertices.Sort((a, b) =>
        {
            a.y = b.y = 0;
            var dirA = Unity.Mathematics.math.normalize(a - centerPos);
            var dirB = Unity.Mathematics.math.normalize(b - centerPos);

            return sortSign * UnsignedAngle(Vector3.SignedAngle(sortByDir, dirA, Vector3.up)).CompareTo(UnsignedAngle(Vector3.SignedAngle(sortByDir, dirB, Vector3.up)));
        });
    }
    private float UnsignedAngle(float angle)
    {
        angle %= 360f;

        if (angle < 0f)
            angle += 360f;

        return angle;
    }
    protected virtual void CalculatePoints()
    {

    }
    /// <summary>
    /// 把障碍物顶点添加到RVO系统
    /// </summary>
    /// <param name="obsPoints"></param>
    protected virtual void InternalApplyObstacles(List<float3> obsPoints)
    {
        mObstacles = RVOComponent.Instance.AddObstacle(obsPoints, IsEdge);
    }
}
