using System;
using AOT;
using Nebukam.ORCA;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;



[DisableAutoCreation]
public partial struct RVOSyncSystem : ISystem
{



    private void OnCreate(ref SystemState state)
    {
        if (ECSGraphicsComponent.Instance == null)
            throw new NullReferenceException("ECSGraphicsComponent.Instance is null");
        entities = ECSGraphicsComponent.Instance.Entities;
    }




    private void OnUpdate(ref SystemState state)
    {
        Unity.Collections.NativeArray<AgentData> agentData = default;

   
        if (RVOComponent.Instance != null && !entities.IsEmpty)
        {
          
            if (RVOComponent.Instance.TryGetAgentData(out agentData))
            {
               
                var commandBuffer = new Unity.Entities.EntityCommandBuffer(
                    Unity.Collections.Allocator.TempJob
                );

            
                m_CommandBuffer = commandBuffer;

              
                var parallelWriter = commandBuffer.AsParallelWriter();

              
                var job = new SyncRVOECSGraphicsJob
                {
                    Agents = agentData,
                    Entities = entities,
                    EntityBuffer = parallelWriter
                };

               
                var jobHandle = job.Schedule(agentData.Length, 64, state.Dependency);

              
                jobHandle.Complete();

               
                m_CommandBuffer.Playback(state.EntityManager);

             
                m_CommandBuffer.Dispose();
            }
        }
    }




    //[MonoPInvokeCallback(typeof(SystemBaseDelegates.Function))]
    //internal static void __codegen__OnCreate(IntPtr self, IntPtr state)
    //{
    //}




    //[MonoPInvokeCallback(typeof(SystemBaseDelegates.Function))]
    //internal static void __codegen__OnUpdate(IntPtr self, IntPtr state)
    //{
    //}




    private EntityCommandBuffer m_CommandBuffer;




    private NativeHashMap<int, Entity> entities;
}
