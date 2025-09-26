using System;
 
using Unity.Burst;
using Unity.Mathematics;

 
[BurstCompile(CompileSynchronously = true)]
public struct GPUAnimTriggerInfo
{
    /// <summary>
    /// 代理的唯一标识符。
    /// 用于识别与此动画相关联的代理。
    /// </summary>
    public int AgentId;

    public int3 TriggerKey;
    ///// <summary>
    ///// 资源索引。
    ///// 指定与此动画相关的资源的索引。
    ///// </summary>
    //public int ResIndex;

    ///// <summary>
    ///// 剪辑索引。
    ///// 表示当前动画剪辑在动画列表中的索引位置。
    ///// </summary>
    //public int ClipIndex;

    ///// <summary>
    ///// 触发帧。
    ///// 指定在动画的哪个帧触发该事件。
    ///// </summary>
    //public int TriggerAtFrame;
}
