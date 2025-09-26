using Nebukam.ORCA;
using System;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;




[BurstCompile]
public struct GPUAnimationEventJob : IJobChunk
{
    [Obsolete]
    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    {
        var eventArray = chunk.GetNativeArray(EventHandle);
        var clipIdArray = chunk.GetNativeArray(ClipIdHandle);
        var renderDataArray = chunk.GetNativeArray(RenderDataHandle);

        int entityCount = chunk.ChunkEntityCount;

        for (int i = 0; i < entityCount; i++)
        {
            var renderData = renderDataArray[i];
            int renderDataId = renderData.RenderIndex;

            float4 clipId = clipIdArray[i].Value;
          
            int currentEventFrame = eventArray[i].Value;
            var eventData = eventArray[i];
            Unity.Mathematics.int2 @int = new Unity.Mathematics.int2(renderDataId, (int)clipId.x);

            if (ClipInfos.TryGetValue(@int, out GPUAnimationClipInfo clipInfo))
            {
              
                int animationCurrentFrame = GetAnimationCurrentFrame(clipInfo, clipId.y, CurrentTime);
              //  Debug.Log($"RenderDataId: {renderDataId}, ClipId.x: {(int)clipId.x}, animationCurrentFrame: {animationCurrentFrame}");
                if (currentEventFrame == animationCurrentFrame)
                    return;
                eventData.Value = animationCurrentFrame;
                eventArray[i] = eventData;


                int num2 = currentEventFrame + 1;
                if (animationCurrentFrame == num2)
                {
                    this.TryTriggerEvent(renderDataId, new int3(@int, animationCurrentFrame));
                    return;
                }
                if (animationCurrentFrame > num2)
                {
                    for (int q = num2; q <= animationCurrentFrame; q++)
                    {
                        this.TryTriggerEvent(renderDataId, new int3(@int, q));
                    }
                    return;
                }
                for (int j = num2; j <= clipInfo.EndFrame; j++)
                {
                    this.TryTriggerEvent(renderDataId, new int3(@int, j));
                }
                for (int k = clipInfo.StartFrame; k <= animationCurrentFrame; k++)
                {
                    this.TryTriggerEvent(renderDataId, new int3(@int, k));
                }
 
            }
        }
    }




    [BurstCompile]
    private void TryTriggerEvent(int agentId, int3 triggerKey)
    {
        var key = new Unity.Mathematics.int3(triggerKey.x, triggerKey.y, triggerKey.z);
        if (TriggerFrames.Contains(key))
        {
         
            var eventInfo = new GPUAnimationEventInfo
            {
                EntityId = agentId,
                RenderIndex = triggerKey.x,
                ClipIndex = triggerKey.y,
                TriggerAtFrame = triggerKey.z
            };

            Results.Enqueue(eventInfo);
        }
    }




    [BurstCompile]
     private int GetAnimationCurrentFrame(GPUAnimationClipInfo clipInfo, float startPlayTime, float time)
    {

        float num;
        float timeElapsed = time - startPlayTime;

        if (clipInfo.IsLoop)
        {
            num = timeElapsed * clipInfo.AnimSpeed % clipInfo.AnimLength;
        }
        else
        {
            num = timeElapsed * clipInfo.AnimSpeed;
        }

        float normalizedTime = math.saturate(num / clipInfo.AnimLength);
        float framePosition = math.lerp((float)clipInfo.StartFrame, (float)clipInfo.EndFrame, normalizedTime);
        int frameIndex = (int)math.ceil(framePosition) - clipInfo.StartFrame + 1;


    

        return frameIndex;
    }



    //private void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    //{
    //}




    public float CurrentTime;




    public ComponentTypeHandle<GPUAnimationEventFramed> EventHandle;




    [ReadOnly]
    public ComponentTypeHandle<GPUAnimationClipId> ClipIdHandle;




    [ReadOnly]
    public ComponentTypeHandle<ECSRenderData> RenderDataHandle;




    [ReadOnly]
    public NativeHashMap<int2, GPUAnimationClipInfo> ClipInfos;




    [ReadOnly]
    public NativeHashSet<int3> TriggerFrames;




    [WriteOnly]
    public NativeQueue<GPUAnimationEventInfo>.ParallelWriter Results;
}
