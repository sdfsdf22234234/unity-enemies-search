using System;
using System.Runtime.InteropServices;

using Nebukam.ORCA;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;


 
[BurstCompile]
public struct SyncRVOECSGraphicsJob : IJobParallelFor
{

 
    public void Execute(int index)
    {
        var agent = Agents[index];

     
        Entity entity = Entities[agent.rendererIndex];
        float3 position = new float3(
                  agent.position.x,  // x 轴保持不变
                  0,                 // y 轴设为 0（高度）
                  agent.position.y   // 2D 的 y 映射到 3D 的 z
              );
        LocalTransform transform = LocalTransform.FromPositionRotation(
            position,
            agent.worldQuaternion
        );


         EntityBuffer.SetComponent(index, entity, transform);
    }


 
    [ReadOnly]
    public NativeArray<AgentData> Agents;


 
    [ReadOnly]
    public NativeHashMap<int, Entity> Entities;

 
    [WriteOnly]
    public EntityCommandBuffer.ParallelWriter EntityBuffer;
}
