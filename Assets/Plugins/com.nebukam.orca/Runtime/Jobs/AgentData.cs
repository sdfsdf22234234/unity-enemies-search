// Copyright (c) 2021 Timothé Lapetite - nebukam@gmail.com
// 
// 许可声明：
// 允许任何人免费使用、复制、修改、合并、发布、分发、再许可和/或销售本软件及其相关文档（“软件”），
// 并允许软件的任何受让人也这样做，前提是遵守以下条件：
// 
// 以上版权声明和本许可声明应包含在所有副本或重要部分的 软件中。
// 
// 软件是按“原样”提供的，不附有任何形式的保证，无论是明示还是暗示，包括但不限于对适销性、特定用途适用性和不侵权的担保。
// 作者或版权持有者在任何情况下不对因使用或其他处理软件而产生的任何索赔、损害或其他责任负责。
// 

using System;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace Nebukam.ORCA
{
    /// <summary>
    /// 表示代理数据的结构体，设计为适合在工作线程中使用。
    /// </summary>
    [BurstCompile] // 使用Burst编译器进行优化
    [Serializable]
    public struct AgentData : IEquatable<AgentData>
    {
        [UnityEngine.HideInInspector] // 在Unity编辑器中隐藏此字段
        public int index; // 代理的索引

        [UnityEngine.HideInInspector]
        public int kdIndex; // KD树中的索引，用于空间分割和快速查询

        [UnityEngine.HideInInspector]
        public float2 position; // 代理的二维位置

        [UnityEngine.HideInInspector]
        public float baseline; // 基准高度（可能用于代理的高度计算）

        [UnityEngine.HideInInspector]
        public float2 prefVelocity; // 代理的期望速度

        [UnityEngine.HideInInspector]
        public float2 velocity; // 代理的实际速度

        public float height; // 代理的高度

        public float radius; // 代理的半径

        public float radiusObst; // 代理的障碍物半径

        public float maxSpeed; // 代理的最大速度

        public int maxNeighbors; // 最大邻居数（影响避让策略）

        public float neighborDist; // 邻居距离（影响避让范围）

        [UnityEngine.HideInInspector]
        public float neighborElev; // 邻居高度（可能用于三维空间的避让）

        public float timeHorizon; // 时间视野（用于预测）

        public float timeHorizonObst; // 时间视野（用于预测障碍物）

        public float avoidWeight; // 避让权重（影响避让行为的强度）

        public ORCALayer layerOccupation; // 代理所在的层（可能用于分层处理）

        public ORCALayer layerIgnore; // 代理忽略的层（用于设置避让策略）

        public bool navigationEnabled; // 是否启用导航

        public bool collisionEnabled; // 是否启用碰撞检测

        [UnityEngine.HideInInspector]
        public float3 worldPosition; // 代理的世界位置（三维）

        [UnityEngine.HideInInspector]
        public float3 worldVelocity; // 代理的世界速度（三维）

        [UnityEngine.HideInInspector]
        public quaternion worldQuaternion; // 代理的世界旋转（三维）

        [UnityEngine.HideInInspector]
        public Unity.Mathematics.float2 targetPosition; // 目标位置（二位）

        [UnityEngine.HideInInspector]
        public Unity.Mathematics.quaternion targetQuaternion; // 目标旋转（三维）

        [UnityEngine.HideInInspector]
        public int rendererIndex; // 渲染器的索引（用于可视化）

        [UnityEngine.HideInInspector]
        public float4 clipId; // 动画剪辑的ID

        [HideInInspector]
        public float4 userdataVec4;

        //[UnityEngine.HideInInspector]
        //public int catalog; //  代理的目录索引（用于分类）

        [UnityEngine.HideInInspector]
        public float searchRadius; // 搜索半径（用于发现邻居）

        [UnityEngine.HideInInspector]
        public int searchCount; // 搜索计数（记录发现的邻居数量）

        [UnityEngine.HideInInspector]
        public int id; // 代理的唯一标识符

        [HideInInspector]
        public bool arrived;

        [HideInInspector]
        public bool stopMoveSelf;

        [HideInInspector]
        public ORCALayer camp;

        [HideInInspector]
        public ORCALayer searchTag;

        [HideInInspector]
        public ORCALayer ignoreSearchTag;
        // 判断两个 AgentData 是否相等
        public bool Equals(AgentData other)
        {
            return this.id == other.id;
        }

        // 返回 AgentData 的哈希值
        public override int GetHashCode()
        {
            return id.GetHashCode();
        }
        public readonly bool CheckIgnoreSearch(ORCALayer otherTag)
        {
            return (otherTag & this.ignoreSearchTag) == otherTag;
        }
        public readonly bool IsFriendlyCamp(ORCALayer otherCampLayer)
        {
            return (otherCampLayer & this.camp) != 0;
        }


    }

    /// <summary>
    /// 代理在一次模拟步骤之后的结果数据。
    /// </summary>
    [BurstCompile]
    public struct AgentDataResult
    {
        public float2 position; // 代理的新位置（二位）

        public float2 velocity; // 代理的新速度（二位）

        public bool updated; // 指示代理是否已更新
    }
}
