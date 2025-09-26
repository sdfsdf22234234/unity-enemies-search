#if UNITY_2022_1_OR_NEWER
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Burst;
using AABB = BRGExtension.AABB;
using UnityEngine;

/// <summary>
/// 表示一个渲染器节点的结构体，用于存储与渲染相关的信息，包括位置、旋转、缩放、颜色、以及包围盒等数据。
/// </summary>
[BurstCompile]
public struct RendererNode
{
    /// <summary>
    /// 渲染器节点的唯一标识符。
    /// </summary>
    public RendererNodeId Id { get; private set; }

    /// <summary>
    /// 渲染器节点的构造函数，用于初始化节点的属性。
    /// </summary>
    /// <param name="id">节点的唯一标识符。</param>
    /// <param name="position">节点在世界空间中的位置。</param>
    /// <param name="rotation">节点的旋转。</param>
    /// <param name="localScale">节点的局部缩放。</param>
    /// <param name="meshAABB">节点的包围盒（AABB）。</param>
    public RendererNode(RendererNodeId id, float3 position, quaternion rotation, float3 localScale, AABB meshAABB)
    {
        int m_ResourceIndex = id.ResourceIndex;
        this.Id = id; // 设置节点的ID
        this.position = position; // 设置节点的位置
        this.rotation = rotation; // 设置节点的旋转
        this.localScale = localScale; // 设置节点的缩放
        this.aabb = meshAABB; // 设置节点的包围盒
        this.color = float4(1); // 设置节点的颜色，默认为白色
        this.active = false; // 初始化节点为非激活状态
        this.clipId = 0; // 初始化剪切ID为0
        this.userdataVec4 = 0;
        this.localToWorld = Unity.Mathematics.float4x4.TRS(position, rotation, localScale);
        this.batchIndex = 0;
        this.worldAabb = BRGExtension.AABB.Transform(this.localToWorld, aabb);

        this.culling = false;

    }
   

    /// <summary>
    /// 构建矩阵
    /// </summary>
    /// <returns></returns>
    [BurstCompile]
    public float4x4 BuildMatrix()
    {
        return Unity.Mathematics.float4x4.TRS(position, rotation, localScale);
    }
    /// <summary>
    /// 构建节点的局部和世界矩阵。
    /// </summary>
    /// <param name="localMatrix">输出的局部矩阵。</param>
    /// <param name="worldMatrix">输出的世界矩阵。</param>
    [BurstCompile]
    public void BuildPackedMatrices(out float4x3 localMatrix, out float4x3 worldMatrix)
    {
        // 计算局部到世界的反变换矩阵（scaleMatrix），通常用于获取局部空间的缩放信息。
 
        float4x4 scaleMatrix = fastinverse(localToWorld);

        // 初始化 localMatrix 和 worldMatrix 的每一列为零。
        // localMatrix 用于存储局部矩阵的三列。
        localMatrix.c0 = 0;
        localMatrix.c1 = 0;
        localMatrix.c2 = 0;

        // worldMatrix 用于存储世界矩阵的三列。
        worldMatrix.c0 = 0;
        worldMatrix.c1 = 0;
        worldMatrix.c2 = 0;

        // 使用 BRGUtility 类的 PackedMatrices 方法将 localToWorld 矩阵打包到 localMatrix 的三列中。
        // localMatrix.c0, localMatrix.c1, localMatrix.c2 分别存储 localToWorld 的 x, y, z 分量。
        BRGUtility.PackedMatrices(ref localToWorld, ref localMatrix.c0, ref localMatrix.c1, ref localMatrix.c2);

        // 使用 BRGUtility 类的 PackedMatrices 方法将 scaleMatrix 打包到 worldMatrix 的三列中。
        // worldMatrix.c0, worldMatrix.c1, worldMatrix.c2 分别存储 scaleMatrix 的 x, y, z 分量。
        BRGUtility.PackedMatrices(ref scaleMatrix, ref worldMatrix.c0, ref worldMatrix.c1, ref worldMatrix.c2);
    }

    /// <summary>
    /// 更新节点的变换信息。
    /// </summary>
    [BurstCompile]
    internal void UpdateTransform()
    {
        float3 center = new float3(position.x, position.y, position.z);
        float4x4 transformationMatrix = Unity.Mathematics.float4x4.TRS(center, rotation, localScale);
        this.localToWorld = transformationMatrix;
        AABB localAABB = this.aabb;
        this.worldAabb = BRGExtension.AABB.Transform(transformationMatrix, aabb);
    }

    /// <summary>
    /// 表示节点是否处于激活状态。
    /// </summary>
    public bool active;

    /// <summary>
    /// 节点在世界空间中的位置。
    /// </summary>
    public float3 position;

    /// <summary>
    /// 节点的旋转。
    /// </summary>
    public quaternion rotation;

    /// <summary>
    /// 节点的局部缩放。
    /// </summary>
    public float3 localScale;

    /// <summary>
    /// 节点的颜色。
    /// </summary>
    public float4 color;

    /// <summary>
    /// 剪切ID，用于图形裁剪。
    /// </summary>
    public float4 clipId;  // x=AnimIndex,y=PlayStartTime,z=PreviousAnimIndex,w=PreviousPlayStartTime

    /// <summary>
    /// 用户自定义的4维向量数据。
    /// </summary>
    public float4 userdataVec4;

    /// <summary>
    /// 节点的包围盒（AABB），在局部空间中。
    /// </summary>
    public AABB aabb;

    /// <summary>
    /// 节点的包围盒（AABB），在世界空间中。
    /// </summary>
    public AABB worldAabb;

    /// <summary>
    /// 将局部坐标转换为世界坐标的矩阵。
    /// </summary>
    public float4x4 localToWorld;

    /// <summary>
    /// 节点的批次索引，用于批处理渲染。
    /// </summary>
    public int batchIndex;
    /// <summary>
    /// 是否被裁剪,在视口外时为true
    /// </summary>
    public bool culling;
    public bool Visible
    {
        get
        {
            return active && !culling;
        }
    }
    /// <summary>
    /// 表示一个空的渲染器节点。
    /// </summary>
    public static readonly RendererNode Empty = new RendererNode();
}
#endif
