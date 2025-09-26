using System;
using System.Collections.Generic;
using BRGExtension;
using GPUAnimation.Runtime;
using Nebukam.ORCA;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;



public class BatchRendererComponent : MonoBehaviour
{

    public static BatchRendererComponent Instance { get; private set; }
    private bool UseConstantBuffer
    {
        get
        {
            return BatchRendererGroup.BufferTarget == BatchBufferTarget.ConstantBuffer;
        }
    }



    private bool UserComputeShaderUpload
    {
        get
        {
            return this.m_MemoryCopyCS != null && SystemInfo.supportsComputeShaders;
        }
    }



    public int TotalVisibleCount
    {
        get
        {
            return this.m_TotalVisibleCount;
        }
    }




    public bool EnableLOD
    {
        get
        {
            return this.m_EnableLOD;
        }
        set
        {
            this.m_EnableLOD = value;
        }
    }
    private void Awake()
    {

        Instance = this;

        this.m_FastWriteModeOnDX12 &= (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12);
        Debug.Log(string.Format("#######BRG use fast mode on dx12: {0}######", this.m_FastWriteModeOnDX12));
        this.m_TotalVisibleCount = 0;
        this.m_DirtyRendererNodes = new NativeHashMap<int, RendererNode>(64, Allocator.Persistent);
        int count = this.m_RendererResources.Count;
        this.m_InactiveRendererNodes = new NativeHashMap<DrawBatchKey, NativeQueue<int>>(count, Allocator.Persistent);
        this.m_DrawBatches = new NativeList<BatchRendererComponent.SrpBatch>(128, Allocator.Persistent);
        this.m_BRG = new BatchRendererGroup(new BatchRendererGroup.OnPerformCulling(this.OnPerformCulling), IntPtr.Zero);
        this.m_NodeIndexRangePerDrawKey = new NativeHashMap<DrawBatchKey, int2>(count, Allocator.Persistent);
        this.m_BatchesIndexRangePerDrawKey = new NativeHashMap<DrawBatchKey, int2>(count, Allocator.Persistent);
        this.m_LodDistances = new NativeArray<float4>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        this.m_LodLevels = new NativeArray<int>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        this.m_TotalCapacity = 0;
        for (int i = 0; i < count; i++)
        {
            RendererResource rendererResource = this.m_RendererResources[i];
            BatchMeshID meshId = this.m_BRG.RegisterMesh(rendererResource.mesh);
            BatchMaterialID matId = this.m_BRG.RegisterMaterial(rendererResource.material);
            rendererResource.RegisterResource(i, meshId, matId, rendererResource.material.renderQueue > 2500);
            int index = i;
            ref NativeArray<int> ptr = ref this.m_LodLevels;
            int index2 = i;
            ValueTuple<float4, int> lods = rendererResource.GetLods(this.m_DistanceScaleLOD);
            this.m_LodDistances[index] = lods.Item1;
            ptr[index2] = lods.Item2;
            this.m_InactiveRendererNodes.Add(rendererResource.Key, new NativeQueue<int>(Allocator.Persistent));
            this.m_TotalCapacity += rendererResource.capacity;
        }
        this.m_AllRendererNodes = new NativeArray<RendererNode>(this.m_TotalCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        this.GenerateBatches();
        Bounds globalBounds = new Bounds(new Vector3(0f, 0f, 0f), new Vector3(1048576f, 1048576f, 1048576f));
        this.m_BRG.SetGlobalBounds(globalBounds);
        this.m_AllRendererNodesPreAnimFrame = new NativeArray<int>(this.m_TotalCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        this.m_AnimClipsInfos = new NativeHashMap<int2, GPUAnimClipInfo>(64, Allocator.Persistent);
        this.m_AnimEventFrames = new NativeHashMap<int3, int>(32, Allocator.Persistent);
        this.m_ClipEventsMaxCount = 0;
        for (int j = 0; j < count; j++)
        {
            RendererResource rendererResource2 = this.m_RendererResources[j];
            if (!(rendererResource2.gpuAnimEventData == null))
            {
                for (int k = 0; k < rendererResource2.gpuAnimEventData.ClipEvents.Count; k++)
                {
                    GPUAnimEvents gpuanimEvents = rendererResource2.gpuAnimEventData.ClipEvents[k];
                    bool flag = false;
                    foreach (KeyValuePair<int, string> keyValuePair in gpuanimEvents)
                    {
                        if (keyValuePair.Key != -1 && !string.IsNullOrEmpty(keyValuePair.Value))
                        {
                            flag = true;
                            this.m_AnimEventFrames.Add(new int3(j, k, keyValuePair.Key), keyValuePair.Key);
                        }
                    }
                    if (flag)
                    {
                        if (this.m_ClipEventsMaxCount < gpuanimEvents.Count)
                        {
                            this.m_ClipEventsMaxCount = gpuanimEvents.Count;
                        }
                        int2 key = new int2(j, k);
                        float4 @float = GPUAnimationUtility.GetAnimationClipInfo(rendererResource2.material, k);
                        this.m_AnimClipsInfos.Add(key, new GPUAnimClipInfo
                        {
                            StartFrame = (int)@float.x,
                            EndFrame = (int)@float.y,
                            AnimLength = @float.z,
                            IsLoop = (@float.w > 0.01f),
                            AnimSpeed = rendererResource2.material.GetFloat("_AnimSpeed")
                        });
                    }
                }
            }
        }



    }

    //private void Start()
    //{

      
    //}


    private void Update()
    {
        this.FlushDirtyRendererNodes();
        this.TrySyncRVOAgents();
        if (this.m_TotalVisibleCount > 0)
        {
            if (this.m_FastWriteModeOnDX12)
            {
                this.UploadInstanceDataJobsDX12();
                return;
            }
            this.UploadInstanceDataJobs();
        }
    }


    private void OnDestroy()
    {
        foreach (DrawBatchKey key in this.m_NodeIndexRangePerDrawKey.GetKeyArray(Allocator.Temp))
        {
            this.m_InstanceDataPerDrawKey[key].Dispose();
            this.m_RenderBufferPerDrawKey[key].Dispose();
        }
        this.m_LodDistances.Dispose();
        this.m_LodLevels.Dispose();
        this.m_NodeIndexRangePerDrawKey.Dispose();
        this.m_BatchesIndexRangePerDrawKey.Dispose();
        this.m_AllRendererNodes.Dispose();
        this.m_DrawBatches.Dispose();
        foreach (DrawBatchKey key2 in this.m_InactiveRendererNodes.GetKeyArray(Allocator.Temp))
        {
            this.m_InactiveRendererNodes[key2].Dispose();
        }
        this.m_DirtyRendererNodes.Dispose();
        this.m_InactiveRendererNodes.Dispose();
        this.m_AnimClipsInfos.Dispose();
        this.m_AnimEventFrames.Dispose();
        this.m_AllRendererNodesPreAnimFrame.Dispose();
        this.m_BRG.Dispose();
    }


    private void TrySyncRVOAgents()
    {
        NativeArray<AgentData> agentDataArr;
        RVOComponent rvoComponent = RVOComponent.Instance;

        if (rvoComponent != null && rvoComponent.TryGetAgentData(out agentDataArr))
        {
            new BatchRendererComponent.SyncRvoAgentsParallelJob
            {
                RotateSmoothStep = Time.deltaTime * 10f,
                AgentDataArr = agentDataArr,
                Nodes = this.m_AllRendererNodes
            }.Schedule(agentDataArr.Length, 64, default(JobHandle)).Complete();
        }
    }


    public int GetCapacityRemain(int resourceIndex)
    {
        if (this.m_RendererResources == null)
        {
            return 0;
        }
        return this.m_RendererResources[resourceIndex].capacity - this.m_BatchesVisibleCount[resourceIndex];
    }


    public int GetResourceCount()
    {
        if (this.m_RendererResources != null)
        {
            return this.m_RendererResources.Count;
        }
        return 0;
    }


    private void FlushDirtyRendererNodes()
    {
        int count = this.m_DirtyRendererNodes.Count;
        if (count > 0)
        {
            new BatchRendererComponent.FlushDirtyRendererNodesJob
            {
                Nodes = this.m_AllRendererNodes,
                DirtyNodes = this.m_DirtyRendererNodes.GetValueArray(Allocator.TempJob)
            }.Schedule(count, 8, default(JobHandle)).Complete();
            this.m_DirtyRendererNodes.Clear();
        }
    }


    private void GenerateBatches()
    {
        uint num = 8388608U;
        uint num2 = 16U;
        if (this.UseConstantBuffer)
        {
            num = (uint)BatchRendererGroup.GetConstantBufferMaxWindowSize();
            num2 = (uint)BatchRendererGroup.GetConstantBufferOffsetAlignment();
        }
        GraphicsBuffer.UsageFlags usageFlags = this.m_FastWriteModeOnDX12 ? GraphicsBuffer.UsageFlags.LockBufferForWrite : GraphicsBuffer.UsageFlags.None;
        int num3 = 0;
        int num4 = 0;
        int num5 = 0;
        int num6 = 0;
        this.m_BatchesVisibleCount = new int[this.m_RendererResources.Count];
        for (int i = 0; i < this.m_RendererResources.Count; i++)
        {
            RendererResource rendererResource = this.m_RendererResources[i];
            DrawBatchKey key = rendererResource.Key;
            this.m_BatchesVisibleCount[i] = 0;
            this.m_NodeIndexRangePerDrawKey.Add(key, new int2(num5, rendererResource.capacity));
            num5 += rendererResource.capacity;
            int y = this.m_NodeIndexRangePerDrawKey[key].y;
            int num7 = (int)((num - (uint)BRGUtility.kSizeOfMatrix) / (uint)BRGUtility.kBytesPerInstance);
            if (num7 > y)
            {
                num7 = y;
            }
            int num8 = (y + num7 - 1) / num7;
            int num9 = BRGUtility.kSizeOfMatrix + num7 * BRGUtility.kBytesPerInstance + (int)num2 - 1 & (int)(~(int)(num2 - 1U));
            int num10 = num8 * num9;
            GraphicsBuffer graphicsBuffer;
            if (this.UseConstantBuffer)
            {
                graphicsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Constant, usageFlags, num10 / BRGUtility.kSizeOfFloat4, BRGUtility.kSizeOfFloat4);
            }
            else
            {
                graphicsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, usageFlags, num10 / BRGUtility.kSizeOfFloat4, BRGUtility.kSizeOfFloat4);
            }
            NativeArray<float4> value = new NativeArray<float4>(num10 / BRGUtility.kSizeOfFloat4, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            int nameID = Shader.PropertyToID("unity_ObjectToWorld");
            int nameID2 = Shader.PropertyToID("unity_WorldToObject");
            int nameID3 = Shader.PropertyToID("_BaseColor");
            int nameID4 = Shader.PropertyToID("_ClipId");
            int nameID5 = Shader.PropertyToID("_UserdataVec4");
            int num11 = y;
            int num12 = 0;
            NativeArray<MetadataValue> batchMetadata = new NativeArray<MetadataValue>(5, Allocator.Temp, NativeArrayOptions.ClearMemory);
            for (int j = 0; j < num8; j++)
            {
                int num13 = (num11 < num7) ? num11 : num7;
                int num14 = j * num9 / BRGUtility.kSizeOfFloat4;
                int num15 = num14 + 4;
                int num16 = num15 + num13 * 3;
                int num17 = num16 + num13 * 3;
                int num18 = num17 + num13;
                int num19 = num18 + num13;
                BatchRendererComponent.SrpBatch srpBatch = new BatchRendererComponent.SrpBatch
                {
                    DrawKey = key,
                    InstanceOffset = num12,
                    BeginGlobalNodeIndex = num6,
                    drawRendererCount = num13,
                    localToWorldIdx = num15,
                    worldToLocalIdx = num16,
                    baseColorIdx = num17,
                    clipIdIdx = num18,
                    userdataVec4Idx = num19
                };
                Vector4 v = rendererResource.material.color;
                for (int k = 0; k < num13; k++)
                {
                    AABB meshAABB = rendererResource.mesh.bounds.ConvertToAABB();
                    this.m_AllRendererNodes[num6] = new RendererNode(new RendererNodeId(key, num6, i), float3.zero, quaternion.identity, math.float3(1), meshAABB)
                    {
                        batchIndex = this.m_DrawBatches.Length,
                        color = v
                    };
                    num6++;
                }
                value[num14] = float4.zero;
                value[num14 + 1] = float4.zero;
                value[num14 + 2] = float4.zero;
                value[num14 + 3] = float4.zero;
                if (this.UseConstantBuffer)
                {
                    batchMetadata[0] = BRGUtility.CreateMetadataValue(nameID, (num15 - num14) * BRGUtility.kSizeOfFloat4, true);
                    batchMetadata[1] = BRGUtility.CreateMetadataValue(nameID2, (num16 - num14) * BRGUtility.kSizeOfFloat4, true);
                    batchMetadata[2] = BRGUtility.CreateMetadataValue(nameID3, (num17 - num14) * BRGUtility.kSizeOfFloat4, true);
                    batchMetadata[3] = BRGUtility.CreateMetadataValue(nameID4, (num18 - num14) * BRGUtility.kSizeOfFloat4, true);
                    batchMetadata[4] = BRGUtility.CreateMetadataValue(nameID5, (num19 - num14) * BRGUtility.kSizeOfFloat4, true);
                }
                else
                {
                    batchMetadata[0] = BRGUtility.CreateMetadataValue(nameID, num15 * BRGUtility.kSizeOfFloat4, true);
                    batchMetadata[1] = BRGUtility.CreateMetadataValue(nameID2, num16 * BRGUtility.kSizeOfFloat4, true);
                    batchMetadata[2] = BRGUtility.CreateMetadataValue(nameID3, num17 * BRGUtility.kSizeOfFloat4, true);
                    batchMetadata[3] = BRGUtility.CreateMetadataValue(nameID4, num18 * BRGUtility.kSizeOfFloat4, true);
                    batchMetadata[4] = BRGUtility.CreateMetadataValue(nameID5, num19 * BRGUtility.kSizeOfFloat4, true);
                }
                int bufferOffset = 0;
                int windowSize = 0;
                if (this.UseConstantBuffer)
                {
                    windowSize = BRGUtility.kSizeOfMatrix + num13 * BRGUtility.kBytesPerInstance;
                    bufferOffset = num14 * BRGUtility.kSizeOfFloat4;
                }
                srpBatch.BatchID = this.m_BRG.AddBatch(batchMetadata, graphicsBuffer.bufferHandle, (uint)bufferOffset, (uint)windowSize);
                this.m_DrawBatches.Add(srpBatch);
                num12 += num13;
                num11 -= num13;
            }
            num4 += num12;
            this.m_InstanceDataPerDrawKey.Add(key, graphicsBuffer);
            this.m_RenderBufferPerDrawKey.Add(key, value);
            this.m_BatchesIndexRangePerDrawKey[key] = new int2(num3, num8);
            num3 += num8;
            batchMetadata.Dispose();
        }
    }


    public RendererNodeId AddRenderer(int resourceIdx, float3 pos, quaternion rot, float3 scale)
    {
        if (resourceIdx < 0 || resourceIdx >= this.m_RendererResources.Count)
        {
            Debug.LogWarning("添加Renderer失败, resourceIdx越界");
            return RendererNodeId.Null;
        }
        RendererResource rendererResource = this.m_RendererResources[resourceIdx];
        NativeQueue<int> nativeQueue = this.m_InactiveRendererNodes[rendererResource.Key];
        if (nativeQueue.Count < 1)
        {
            this.FlushDirtyRendererNodes();
            int2 @int = this.m_NodeIndexRangePerDrawKey[rendererResource.Key];
            new BatchRendererComponent.GetInactiveRendererNodeJob
            {
                Nodes = this.m_AllRendererNodes,
                NodesIndexRange = @int,
                Outputs = nativeQueue.AsParallelWriter()
            }.Schedule(@int.y, 16, default(JobHandle)).Complete();
        }
        int index;
        if (!nativeQueue.TryDequeue(out index))
        {
            Debug.LogWarning("添加Renderer失败, Inactive renderer node is null");
            return RendererNodeId.Null;
        }
        RendererNode item = this.m_AllRendererNodes[index];
        item.position = pos;
        item.rotation = rot;
        item.localScale = scale;
        item.active = true;
        item.clipId = 0;
        this.m_DirtyRendererNodes.Add(item.Id.Index, item);
        this.m_BatchesVisibleCount[resourceIdx]++;
        this.m_TotalVisibleCount++;
        return item.Id;
    }


    public void RemoveRenderer(RendererNodeId id)
    {
        if (id == RendererNodeId.Null)
        {
            return;
        }
        RendererNode rendererNode;
        if (this.m_DirtyRendererNodes.TryGetValue(id.Index, out rendererNode))
        {
            rendererNode.active = false;
            this.m_DirtyRendererNodes[id.Index] = rendererNode;
            this.m_BatchesVisibleCount[id.ResourceIndex]--;
            this.m_TotalVisibleCount--;
            return;
        }
        rendererNode = this.m_AllRendererNodes[id.Index];
        rendererNode.active = false;
        this.m_DirtyRendererNodes.Add(id.Index, rendererNode);
        this.m_BatchesVisibleCount[id.ResourceIndex]--;
        this.m_TotalVisibleCount--;
    }


    public void RemoveAllRenderers()
    {
        this.FlushDirtyRendererNodes();
        if (this.m_AllRendererNodes.Length > 0 && this.m_TotalVisibleCount > 0)
        {
            new BatchRendererComponent.ClearRendererNodesJob
            {
                Nodes = this.m_AllRendererNodes
            }.Schedule(this.m_AllRendererNodes.Length, 64, default(JobHandle)).Complete();
            this.m_TotalVisibleCount = 0;
            for (int i = 0; i < this.m_BatchesVisibleCount.Length; i++)
            {
                this.m_BatchesVisibleCount[i] = 0;
            }
        }
    }


    public void SetRendererData(RendererNodeId id, float3 pos, quaternion rot)
    {
        RendererNode rendererNode;
        if (this.m_DirtyRendererNodes.TryGetValue(id.Index, out rendererNode))
        {
            rendererNode.position = pos;
            rendererNode.rotation = rot;
            this.m_DirtyRendererNodes[id.Index] = rendererNode;
            return;
        }
        rendererNode = this.m_AllRendererNodes[id.Index];
        rendererNode.position = pos;
        rendererNode.rotation = rot;
        this.m_DirtyRendererNodes.Add(id.Index, rendererNode);
    }


    public void SetRendererClipId(RendererNodeId id, int animClipIndex)
    {
        RendererNode rendererNode;
        if (this.m_DirtyRendererNodes.TryGetValue(id.Index, out rendererNode))
        {
            this.SetAnimationIndex(ref rendererNode, animClipIndex);
            this.m_DirtyRendererNodes[id.Index] = rendererNode;
            return;
        }
        rendererNode = this.m_AllRendererNodes[id.Index];
        this.SetAnimationIndex(ref rendererNode, animClipIndex);
        this.m_DirtyRendererNodes.Add(id.Index, rendererNode);
    }


    public void SetRendererData(RendererNodeId id, float3 pos, quaternion rot, int clipId)
    {
        RendererNode rendererNode;
        if (this.m_DirtyRendererNodes.TryGetValue(id.Index, out rendererNode))
        {
            this.SetAnimationIndex(ref rendererNode, clipId);
            rendererNode.position = pos;
            rendererNode.rotation = rot;
            this.m_DirtyRendererNodes[id.Index] = rendererNode;
            return;
        }
        rendererNode = this.m_AllRendererNodes[id.Index];
        rendererNode.position = pos;
        rendererNode.rotation = rot;
        this.SetAnimationIndex(ref rendererNode, clipId);
        this.m_DirtyRendererNodes.Add(id.Index, rendererNode);
    }


    public void SetRendererColor(RendererNodeId id, float4 color)
    {
        RendererNode rendererNode;
        if (this.m_DirtyRendererNodes.TryGetValue(id.Index, out rendererNode))
        {
            rendererNode.color = color;
            this.m_DirtyRendererNodes[id.Index] = rendererNode;
            return;
        }
        rendererNode = this.m_AllRendererNodes[id.Index];
        rendererNode.color = color;
        this.m_DirtyRendererNodes.Add(id.Index, rendererNode);
    }


    private void SetAnimationIndex(ref RendererNode node, int animIndex)
    {
        float4 clipId = node.clipId;
        clipId.z = clipId.x;
        clipId.w = clipId.y;
        clipId.x = (float)animIndex;
        clipId.y = Time.time + 0.2f;
        node.clipId = clipId;
    }


    public bool TryTriggerGPUAnimationEvents(NativeArray<AgentData> agents, out NativeQueue<GPUAnimTriggerInfo> triggerResults)
    {
        int length = agents.Length;
        if (length == 0)
        {
            triggerResults = default(NativeQueue<GPUAnimTriggerInfo>);
            return false;
        }
        triggerResults = new NativeQueue<GPUAnimTriggerInfo>(Allocator.TempJob);
        new BatchRendererComponent.GPUAnimTriggerJob
        {
            Agents = agents,
            AllNodes = this.m_AllRendererNodes,
            AllNodesPreAnimFrame = this.m_AllRendererNodesPreAnimFrame,
            ClipInfos = this.m_AnimClipsInfos,
            TriggerFrames = this.m_AnimEventFrames,
            CurrentTime = Time.time,
            Results = triggerResults.AsParallelWriter()
        }.Schedule(length, 64, default(JobHandle)).Complete();
        if (triggerResults.Count == 0)
        {
            triggerResults.Dispose();
            return false;
        }
        return true;
    }

    public bool TryTriggerGPUAnimationEvents(out NativeQueue<GPUAnimTriggerInfo> triggerResults)
    {
        //int length = agents.Length;
        //if (length == 0)
        //{
        //    triggerResults = default(NativeQueue<GPUAnimTriggerInfo>);
        //    return false;
        //}
        triggerResults = default(NativeQueue<GPUAnimTriggerInfo>);
        var rvoComponent = RVOComponent.Instance;
        if (rvoComponent == null)
            return false;
        NativeArray<AgentData> agentData;
        if (!rvoComponent.TryGetAgentData(out agentData))
            return false;

        triggerResults = new NativeQueue<GPUAnimTriggerInfo>(Allocator.TempJob);
        new BatchRendererComponent.GPUAnimTriggerJob
        {
            Agents = agentData,
            AllNodes = this.m_AllRendererNodes,
            AllNodesPreAnimFrame = this.m_AllRendererNodesPreAnimFrame,
            ClipInfos = this.m_AnimClipsInfos,
            TriggerFrames = this.m_AnimEventFrames,
            CurrentTime = Time.time,
            Results = triggerResults.AsParallelWriter()
        }.Schedule(agentData.Length, 64, default(JobHandle)).Complete();
        if (triggerResults.Count == 0)
        {
            triggerResults.Dispose();
            return false;
        }
        return true;
    }
  
  
    public bool HasAnimationEvent(RendererNodeId id, int clipIndex, string eventName)
    {
        if (this.m_AnimClipsInfos.ContainsKey(new int2(id.ResourceIndex, clipIndex)))
        {
            foreach (KeyValuePair<int, string> keyValuePair in this.m_RendererResources[id.ResourceIndex].gpuAnimEventData.ClipEvents[clipIndex])
            {
                if (keyValuePair.Key >= 0 && keyValuePair.Value.CompareTo(eventName) == 0)
                {
                    return true;
                }
            }
            return false;
        }
        return false;
    }


    public float4 GetAnimationClipInfo(RendererNodeId id, int clipIndex)
    {
        return GPUAnimationUtility.GetAnimationClipInfo(this.m_BRG.GetRegisteredMaterial(id.BatchKey.MaterialID), clipIndex);
    }
    public float GetAnimTransDuration(RendererNodeId id)
    {
        var mat = this.m_BRG.GetRegisteredMaterial(id.BatchKey.MaterialID);

        float animationOffset = mat.HasProperty("_AnimTransDuration") ? mat.GetFloat("_AnimTransDuration") : 0.0f;
        return animationOffset;
    }

    public GPUBoneData GetAnimationAttachBone(RendererNodeId id, int boneId)
    {
        RendererNode rendererNode;
        RendererNode rendererNode2;
        if (this.m_DirtyRendererNodes.TryGetValue(id.Index, out rendererNode))
        {
            rendererNode2 = rendererNode;
        }
        else
        {
            rendererNode2 = this.m_AllRendererNodes[id.Index];
        }
        return this.GetAnimationAttachBone(id, boneId, rendererNode2.clipId, rendererNode2.localScale.x);
    }


    public GPUBoneData GetAnimationAttachBone(RendererNodeId id, int boneId, float4 clipId, float scale)
    {
        return GPUAnimationUtility.GetAttachBoneTransform(this.m_BRG.GetRegisteredMaterial(id.BatchKey.MaterialID), clipId.x, clipId.y, boneId, scale);
    }


    public int GetAnimationClipsCount(RendererNodeId id)
    {
        GPUAnimationEventData gpuAnimEventData = this.m_RendererResources[id.ResourceIndex].gpuAnimEventData;
        if (!(gpuAnimEventData != null))
        {
            return 0;
        }
        return gpuAnimEventData.ClipEvents.Count;
    }


    private void UploadInstanceDataJobs()
    {
        NativeArray<JobHandle> jobs = new NativeArray<JobHandle>(this.m_BatchesIndexRangePerDrawKey.Count, Allocator.TempJob, NativeArrayOptions.ClearMemory);
        int num = 0;
        foreach (KVPair<DrawBatchKey, int2> kvpair in this.m_BatchesIndexRangePerDrawKey)
        {
            DrawBatchKey key = kvpair.Key;
            int2 @int = this.m_NodeIndexRangePerDrawKey[key];
            BatchRendererComponent.UploadParallelJob jobData = new BatchRendererComponent.UploadParallelJob
            {
                Nodes = this.m_AllRendererNodes,
                StartNodeIndex = @int.x,
                Batches = this.m_DrawBatches,
                Output = this.m_RenderBufferPerDrawKey[key]
            };
            jobs[num] = jobData.Schedule(@int.y, 64, default(JobHandle));
            num++;
        }
        JobHandle.CombineDependencies(jobs).Complete();
        jobs.Dispose();
        foreach (KeyValuePair<DrawBatchKey, NativeArray<float4>> keyValuePair in this.m_RenderBufferPerDrawKey)
        {
            this.m_InstanceDataPerDrawKey[keyValuePair.Key].SetData<float4>(keyValuePair.Value);
        }
    }


    private void UploadInstanceDataJobsDX12()
    {
        NativeArray<JobHandle> jobs = new NativeArray<JobHandle>(this.m_BatchesIndexRangePerDrawKey.Count, Allocator.TempJob, NativeArrayOptions.ClearMemory);
        int num = 0;
        foreach (KVPair<DrawBatchKey, int2> kvpair in this.m_BatchesIndexRangePerDrawKey)
        {
            DrawBatchKey key = kvpair.Key;
            GraphicsBuffer graphicsBuffer = this.m_InstanceDataPerDrawKey[key];
            int2 @int = this.m_NodeIndexRangePerDrawKey[key];
            BatchRendererComponent.UploadParallelJob jobData = new BatchRendererComponent.UploadParallelJob
            {
                Nodes = this.m_AllRendererNodes,
                StartNodeIndex = @int.x,
                Batches = this.m_DrawBatches,
                Output = graphicsBuffer.LockBufferForWrite<float4>(0, graphicsBuffer.count)
            };
            jobs[num] = jobData.Schedule(@int.y, 64, default(JobHandle));
            num++;
        }
        JobHandle.CombineDependencies(jobs).Complete();
        jobs.Dispose();
        foreach (KeyValuePair<DrawBatchKey, GraphicsBuffer> keyValuePair in this.m_InstanceDataPerDrawKey)
        {
            GraphicsBuffer value = keyValuePair.Value;
            value.UnlockBufferAfterWrite<float4>(value.count);
        }
    }


    public JobHandle OnPerformCulling(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext, BatchCullingOutput cullingOutput, IntPtr userContext)
    {
        if (this.m_TotalVisibleCount > 0)
        {
            NativeArray<int3> rendererCullingInfo = new NativeArray<int3>(this.m_TotalCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            JobHandle dependsOn = new BatchRendererComponent.CullingParallelJob
            {
                planes = cullingContext.cullingPlanes,
                renderers = this.m_AllRendererNodes,
                rendererCullingInfo = rendererCullingInfo,
                srpBatches = this.m_DrawBatches,
                NodeIndexRanges = this.m_NodeIndexRangePerDrawKey,
                lodParameters = cullingContext.lodParameters,
                lodDistances = this.m_LodDistances,
                lodLevels = this.m_LodLevels,
                enableLod = this.m_EnableLOD,
                lodBias = QualitySettings.lodBias
            }.Schedule(this.m_TotalCapacity, 64, default(JobHandle));
            return new BatchRendererComponent.DrawCommandOutputJob
            {
                allDepthSorted = this.m_AllDepthSorted,
                receiveShadows = this.m_ReceiveShadows,
                shadowMode = this.m_ShadowMode,
                staticShadowCaster = this.m_StaticShadowCaster,
                srpBatches = this.m_DrawBatches,
                totalVisibleCount = this.m_TotalVisibleCount,
                rendererCullingInfo = rendererCullingInfo,
                drawCommandsArr = cullingOutput.drawCommands,
                renderers = this.m_AllRendererNodes
            }.Schedule(dependsOn);
        }
        return default(JobHandle);
    }


    [Tooltip("DX12是否启用LockBufferForWrite模式加速上传数据到GPU(对于旧硬件可能会更慢)")]
    [SerializeField]
    private bool m_FastWriteModeOnDX12 = true;


    [SerializeField]
    private bool m_AllDepthSorted;


    [SerializeField]
    private bool m_StaticShadowCaster;


    [SerializeField]
    private bool m_ReceiveShadows;


    [SerializeField]
    private bool m_EnableLOD = true;


    [Range(0.1f, 100f)]
    [SerializeField]
    private float m_DistanceScaleLOD = 1f;


    [SerializeField]
    private ShadowCastingMode m_ShadowMode = ShadowCastingMode.On;


    [SerializeField]
    private ComputeShader m_MemoryCopyCS;


    [SerializeField]
    private List<RendererResource> m_RendererResources;


    private BatchRendererGroup m_BRG;


    private int m_TotalCapacity;


    private int m_TotalVisibleCount;


    private NativeArray<float4> m_LodDistances;


    private NativeArray<int> m_LodLevels;


    private int[] m_BatchesVisibleCount;


    private NativeHashMap<DrawBatchKey, int2> m_BatchesIndexRangePerDrawKey;


    private NativeHashMap<DrawBatchKey, int2> m_NodeIndexRangePerDrawKey;


    private NativeArray<RendererNode> m_AllRendererNodes;


    private NativeHashMap<int, RendererNode> m_DirtyRendererNodes;


    private Dictionary<DrawBatchKey, GraphicsBuffer> m_InstanceDataPerDrawKey = new Dictionary<DrawBatchKey, GraphicsBuffer>();


    private Dictionary<DrawBatchKey, NativeArray<float4>> m_RenderBufferPerDrawKey = new Dictionary<DrawBatchKey, NativeArray<float4>>();


    private NativeList<BatchRendererComponent.SrpBatch> m_DrawBatches;


    private NativeHashMap<DrawBatchKey, NativeQueue<int>> m_InactiveRendererNodes;


    private NativeHashMap<int2, GPUAnimClipInfo> m_AnimClipsInfos;


    private NativeHashMap<int3, int> m_AnimEventFrames;


    private NativeArray<int> m_AllRendererNodesPreAnimFrame;


    private int m_ClipEventsMaxCount;


    private struct SrpBatch
    {

        public BatchID BatchID;


        public DrawBatchKey DrawKey;


        public int BeginGlobalNodeIndex;


        public int InstanceOffset;


        public int drawRendererCount;


        public int localToWorldIdx;


        public int worldToLocalIdx;


        public int baseColorIdx;


        public int clipIdIdx;


        public int userdataVec4Idx;
    }


    [BurstCompile]
    private struct GetInactiveRendererNodeJob : IJobParallelFor
    {

        public void Execute(int index)
        {
            int num = this.NodesIndexRange.x + index;
            if (!this.Nodes[num].active)
            {
                this.Outputs.Enqueue(num);
            }
        }


        [ReadOnly]
        public NativeArray<RendererNode> Nodes;


        [ReadOnly]
        public int2 NodesIndexRange;


        public NativeQueue<int>.ParallelWriter Outputs;
    }


    [BurstCompile]
    private struct UploadParallelJob : IJobParallelFor
    {

        public void Execute(int i)
        {
            int index = this.StartNodeIndex + i;
            RendererNode rendererNode = this.Nodes[index];
            if (!rendererNode.active)
            {
                return;
            }
            BatchRendererComponent.SrpBatch srpBatch = this.Batches[rendererNode.batchIndex];
            int localToWorldIdx = srpBatch.localToWorldIdx;
            int worldToLocalIdx = srpBatch.worldToLocalIdx;
            int baseColorIdx = srpBatch.baseColorIdx;
            int clipIdIdx = srpBatch.clipIdIdx;
            int userdataVec4Idx = srpBatch.userdataVec4Idx;
            int num = i - srpBatch.InstanceOffset;
            int num2 = num * 3;
            float4x3 float4x;
            float4x3 float4x2;
            rendererNode.BuildPackedMatrices(out float4x, out float4x2);
            this.Output[localToWorldIdx + num2] = float4x.c0;
            this.Output[localToWorldIdx + num2 + 1] = float4x.c1;
            this.Output[localToWorldIdx + num2 + 2] = float4x.c2;
            this.Output[worldToLocalIdx + num2] = float4x2.c0;
            this.Output[worldToLocalIdx + num2 + 1] = float4x2.c1;
            this.Output[worldToLocalIdx + num2 + 2] = float4x2.c2;
            this.Output[baseColorIdx + num] = rendererNode.color;
            this.Output[clipIdIdx + num] = rendererNode.clipId;
            this.Output[userdataVec4Idx + num] = rendererNode.userdataVec4;
        }


        [ReadOnly]
        public NativeArray<RendererNode> Nodes;


        public int StartNodeIndex;


        [ReadOnly]
        public NativeList<BatchRendererComponent.SrpBatch> Batches;


        [NativeDisableParallelForRestriction]
        [WriteOnly]
        public NativeArray<float4> Output;
    }


    [BurstCompile]
    private struct DrawCommandOutputJob : IJob
    {

        public unsafe void Execute()
        {
            NativeList<BatchDrawCommand> list = new NativeList<BatchDrawCommand>(this.srpBatches.Length, Allocator.TempJob);
            BatchCullingOutputDrawCommands batchCullingOutputDrawCommands = default(BatchCullingOutputDrawCommands);
            batchCullingOutputDrawCommands.visibleInstances = BRGUtility.Malloc<int>(this.totalVisibleCount);
            int num = 0;
            uint visibleOffset = 0U;
            uint num2 = 0U;
            int num3 = 0;
            int num4 = -1;
            for (int i = 0; i < this.rendererCullingInfo.Length; i++)
            {
                int3 @int = this.rendererCullingInfo[i];
                if (@int.x >= 0)
                {
                    batchCullingOutputDrawCommands.visibleInstances[num] = @int.x;
                    BatchRendererComponent.SrpBatch srpBatch = this.srpBatches[num3];
                    DrawBatchKey drawKey = srpBatch.DrawKey;
                    if (@int.y != num3 || @int.z != num4)
                    {
                        if (num2 > 0U)
                        {
                            BatchDrawCommand batchDrawCommand = new BatchDrawCommand
                            {
                                visibleOffset = visibleOffset,
                                visibleCount = num2,
                                batchID = srpBatch.BatchID,
                                meshID = drawKey.MeshID,
                                materialID = drawKey.MaterialID,
                                submeshIndex = (ushort)num4,
                                splitVisibilityMask = 255,
                                flags = BatchDrawCommandFlags.None,
                                sortingPosition = 0
                            };
                            list.Add(batchDrawCommand);
                        }
                        visibleOffset = (uint)num;
                        num2 = 0U;
                        num3 = @int.y;
                        num4 = (int)((ushort)@int.z);
                    }
                    num2 += 1U;
                    num++;
                }
            }
            if (num2 > 0U)
            {
                BatchRendererComponent.SrpBatch srpBatch2 = this.srpBatches[num3];
                DrawBatchKey drawKey2 = srpBatch2.DrawKey;
                BatchDrawCommand batchDrawCommand2 = new BatchDrawCommand
                {
                    visibleOffset = visibleOffset,
                    visibleCount = num2,
                    batchID = srpBatch2.BatchID,
                    meshID = drawKey2.MeshID,
                    materialID = drawKey2.MaterialID,
                    submeshIndex = (ushort)num4,
                    splitVisibilityMask = 255,
                    flags = BatchDrawCommandFlags.None,
                    sortingPosition = 0
                };
                list.Add(batchDrawCommand2);
            }
            int length = list.Length;
            this.SetupDrawRanges(ref batchCullingOutputDrawCommands, length, this.shadowMode, this.receiveShadows, this.staticShadowCaster, this.allDepthSorted);
            batchCullingOutputDrawCommands.instanceSortingPositions = null;
            batchCullingOutputDrawCommands.instanceSortingPositionFloatCount = 0;
            batchCullingOutputDrawCommands.drawCommandCount = length;
            batchCullingOutputDrawCommands.drawCommands = BRGUtility.Malloc<BatchDrawCommand>(length);
            UnsafeUtility.MemCpy((void*)batchCullingOutputDrawCommands.drawCommands, (void*)list.GetUnsafePtr<BatchDrawCommand>(), (long)(length * BRGUtility.kSizeOfBatchDrawCommand));
            this.drawCommandsArr[0] = batchCullingOutputDrawCommands;
            list.Dispose();
        }


        [BurstCompile]
        private unsafe void SetupDrawRanges(ref BatchCullingOutputDrawCommands drawCommands, int drawCommandsCount, ShadowCastingMode shadowMode, bool receiveShadows, bool staticShadowCaster, bool allDepthSorted)
        {
            drawCommands.drawRangeCount = 1;
            drawCommands.drawRanges = BRGUtility.Malloc<BatchDrawRange>(1);
            *drawCommands.drawRanges = new BatchDrawRange
            {
                drawCommandsBegin = 0U,
                drawCommandsCount = (uint)drawCommandsCount,
                filterSettings = new BatchFilterSettings
                {
                    renderingLayerMask = 1U,
                    layer = 0,
                    motionMode = MotionVectorGenerationMode.Camera,
                    shadowCastingMode = shadowMode,
                    receiveShadows = receiveShadows,
                    staticShadowCaster = staticShadowCaster,
                    allDepthSorted = allDepthSorted
                }
            };
        }


        [ReadOnly]
        [DeallocateOnJobCompletion]
        public NativeArray<int3> rendererCullingInfo;


        [ReadOnly]
        public NativeList<BatchRendererComponent.SrpBatch> srpBatches;


        [ReadOnly]
        public NativeArray<RendererNode> renderers;


        public int totalVisibleCount;


        public NativeArray<BatchCullingOutputDrawCommands> drawCommandsArr;


        public ShadowCastingMode shadowMode;


        public bool receiveShadows;


        public bool staticShadowCaster;


        public bool allDepthSorted;
    }


    [BurstCompile]
    private struct DrawCommandOutputWithSortingPosJob : IJob
    {

        public unsafe void Execute()
        {
            NativeList<BatchDrawCommand> list = new NativeList<BatchDrawCommand>(this.srpBatches.Length, Allocator.TempJob);
            NativeList<int> list2 = new NativeList<int>(this.totalVisibleCount, Allocator.TempJob);
            NativeList<float> list3 = new NativeList<float>(this.totalVisibleCount * 3, Allocator.TempJob);
            int num = 0;
            uint num2 = 0U;
            uint num3 = 0U;
            int num4 = 0;
            int num5 = -1;
            for (int i = 0; i < this.rendererCullingInfo.Length; i++)
            {
                int3 @int = this.rendererCullingInfo[i];
                if (@int.x >= 0)
                {
                    list2.Add(@int.x);
                    BatchRendererComponent.SrpBatch srpBatch = this.srpBatches[num4];
                    DrawBatchKey drawKey = srpBatch.DrawKey;
                    float3 center = this.renderers[srpBatch.BeginGlobalNodeIndex + @int.x].worldAabb.Center;
                    list3.Add(center.x);
                    list3.Add(center.y);
                    list3.Add(center.z);
                    if (@int.y != num4 || @int.z != num5)
                    {
                        if (num3 > 0U)
                        {
                            BatchDrawCommand batchDrawCommand = new BatchDrawCommand
                            {
                                visibleOffset = num2,
                                visibleCount = num3,
                                batchID = srpBatch.BatchID,
                                meshID = drawKey.MeshID,
                                materialID = drawKey.MaterialID,
                                submeshIndex = (ushort)num5,
                                splitVisibilityMask = 255,
                                flags = drawKey.drawCommandFlags,
                                sortingPosition = (int)(num2 * 3U)
                            };
                            list.Add(batchDrawCommand);
                        }
                        num2 = (uint)num;
                        num3 = 0U;
                        num4 = @int.y;
                        num5 = (int)((ushort)@int.z);
                    }
                    num3 += 1U;
                    num++;
                }
            }
            if (num3 > 0U)
            {
                BatchRendererComponent.SrpBatch srpBatch2 = this.srpBatches[num4];
                DrawBatchKey drawKey2 = srpBatch2.DrawKey;
                BatchDrawCommand batchDrawCommand2 = new BatchDrawCommand
                {
                    visibleOffset = num2,
                    visibleCount = num3,
                    batchID = srpBatch2.BatchID,
                    meshID = drawKey2.MeshID,
                    materialID = drawKey2.MaterialID,
                    submeshIndex = (ushort)num5,
                    splitVisibilityMask = 255,
                    flags = drawKey2.drawCommandFlags,
                    sortingPosition = (int)(num2 * 3U)
                };
                list.Add(batchDrawCommand2);
            }
            int length = list2.Length;
            BatchCullingOutputDrawCommands batchCullingOutputDrawCommands = new BatchCullingOutputDrawCommands
            {
                visibleInstanceCount = length,
                visibleInstances = BRGUtility.Malloc<int>(length)
            };
            UnsafeUtility.MemCpy((void*)batchCullingOutputDrawCommands.visibleInstances, (void*)list2.GetUnsafePtr<int>(), (long)(length * BRGUtility.kSizeOfInt));
            this.SetupDrawRanges(ref batchCullingOutputDrawCommands, list.Length, this.shadowMode, this.receiveShadows, this.staticShadowCaster, this.allDepthSorted);
            int length2 = list3.Length;
            batchCullingOutputDrawCommands.instanceSortingPositionFloatCount = length2;
            batchCullingOutputDrawCommands.instanceSortingPositions = BRGUtility.Malloc<float>(length2);
            UnsafeUtility.MemCpy((void*)batchCullingOutputDrawCommands.instanceSortingPositions, (void*)list3.GetUnsafePtr<float>(), (long)(length2 * BRGUtility.kSizeOfFloat));
            int length3 = list.Length;
            batchCullingOutputDrawCommands.drawCommandCount = length3;
            batchCullingOutputDrawCommands.drawCommands = BRGUtility.Malloc<BatchDrawCommand>(length3);
            UnsafeUtility.MemCpy((void*)batchCullingOutputDrawCommands.drawCommands, (void*)list.GetUnsafePtr<BatchDrawCommand>(), (long)(length3 * BRGUtility.kSizeOfBatchDrawCommand));
            this.drawCommandsArr[0] = batchCullingOutputDrawCommands;
            list.Dispose();
            list2.Dispose();
            list3.Dispose();
        }


        [BurstCompile]
        private unsafe void SetupDrawRanges(ref BatchCullingOutputDrawCommands drawCommands, int drawCommandsCount, ShadowCastingMode shadowMode, bool receiveShadows, bool staticShadowCaster, bool allDepthSorted)
        {
            drawCommands.drawRangeCount = 1;
            drawCommands.drawRanges = BRGUtility.Malloc<BatchDrawRange>(1);
            *drawCommands.drawRanges = new BatchDrawRange
            {
                drawCommandsBegin = 0U,
                drawCommandsCount = (uint)drawCommandsCount,
                filterSettings = new BatchFilterSettings
                {
                    renderingLayerMask = 1U,
                    layer = 0,
                    motionMode = MotionVectorGenerationMode.Camera,
                    shadowCastingMode = shadowMode,
                    receiveShadows = receiveShadows,
                    staticShadowCaster = staticShadowCaster,
                    allDepthSorted = allDepthSorted
                }
            };
        }


        [DeallocateOnJobCompletion]
        [ReadOnly]
        public NativeArray<int3> rendererCullingInfo;


        [ReadOnly]
        public NativeList<BatchRendererComponent.SrpBatch> srpBatches;


        [ReadOnly]
        public NativeArray<RendererNode> renderers;


        public int totalVisibleCount;


        public NativeArray<BatchCullingOutputDrawCommands> drawCommandsArr;


        public ShadowCastingMode shadowMode;


        public bool receiveShadows;


        public bool staticShadowCaster;


        public bool allDepthSorted;
    }


    [BurstCompile]
    private struct CullingParallelJob : IJobParallelFor
    {

        public void Execute(int index)
        {
            RendererNode rendererNode = this.renderers[index];
            if (!rendererNode.active || BRGExtension.FrustumPlanes.CheckCulling(this.planes, rendererNode.worldAabb))
            {
                this.rendererCullingInfo[index] = new int3(-1, 0, 0);
                return;
            }
            BatchRendererComponent.SrpBatch srpBatch = this.srpBatches[rendererNode.batchIndex];
            DrawBatchKey drawKey = srpBatch.DrawKey;
            int x = this.NodeIndexRanges[drawKey].x;
            int resourceIndex = rendererNode.Id.ResourceIndex;
            int num = this.lodLevels[resourceIndex];
            int z = 0;
            if (this.enableLod && num > 1)
            {
                float distSq = math.select(math.distancesq(rendererNode.position, this.lodParameters.cameraPosition), 1f, this.lodParameters.isOrthographic);
                z = this.SelectLODLevel(this.lodParameters, this.lodBias, this.lodDistances[resourceIndex], this.lodLevels[resourceIndex], distSq);
            }
            this.rendererCullingInfo[index] = new int3(index - x - srpBatch.InstanceOffset, rendererNode.batchIndex, z);
        }


        [BurstCompile]
        private int SelectLODLevel(LODParameters lodParm, float lodBias, float4 lods, int lodCount, float distSq)
        {
            int result = 0;
            float num = this.CalculateLodDistanceScale(lodParm.fieldOfView, lodBias, lodParm.isOrthographic, lodParm.orthoSize);
            distSq *= num;
            switch (lodCount)
            {
                case 2:
                    result = math.select(0, 1, distSq > lods.x);
                    break;
                case 3:
                    result = ((distSq > lods.y) ? 2 : math.select(0, 1, distSq > lods.x));
                    break;
                case 4:
                    result = ((distSq > lods.y) ? math.select(2, 3, distSq > lods.z) : math.select(0, 1, distSq > lods.x));
                    break;
            }
            return result;
        }


        private float CalculateLodDistanceScale(float fieldOfView, float globalLodBias, bool isOrthographic, float orthoSize)
        {
            float result;
            if (isOrthographic)
            {
                result = 2f * orthoSize / globalLodBias;
            }
            else
            {
                float num = math.tan(math.radians(fieldOfView * 0.5f));
                result = 2f * num / globalLodBias;
            }
            return result;
        }


        [ReadOnly]
        public NativeArray<Plane> planes;


        [ReadOnly]
        public NativeArray<RendererNode> renderers;


        [ReadOnly]
        public NativeHashMap<DrawBatchKey, int2> NodeIndexRanges;


        [ReadOnly]
        public NativeList<BatchRendererComponent.SrpBatch> srpBatches;


        [ReadOnly]
        public bool enableLod;


        [ReadOnly]
        public float lodBias;


        [ReadOnly]
        public NativeArray<float4> lodDistances;


        [ReadOnly]
        public NativeArray<int> lodLevels;


        [ReadOnly]
        public LODParameters lodParameters;


        [WriteOnly]
        public NativeArray<int3> rendererCullingInfo;
    }


    [BurstCompile]
    private struct SyncRvoAgentsParallelJob : IJobParallelFor
    {

        public void Execute(int index)
        {
            AgentData agentData = this.AgentDataArr[index];
            RendererNode rendererNode = this.Nodes[agentData.rendererIndex];
            bool flag = !rendererNode.position.Equals(agentData.worldPosition) || !rendererNode.rotation.Equals(agentData.worldQuaternion);
            rendererNode.clipId = agentData.clipId;
            rendererNode.userdataVec4 = agentData.userdataVec4;
            if (flag)
            {
                rendererNode.position = agentData.worldPosition;
                rendererNode.rotation = math.nlerp(rendererNode.rotation, agentData.worldQuaternion, this.RotateSmoothStep);
                rendererNode.UpdateTransform();
            }
            this.Nodes[agentData.rendererIndex] = rendererNode;
        }


        [ReadOnly]
        public float RotateSmoothStep;


        [ReadOnly]
        public NativeArray<AgentData> AgentDataArr;


        [NativeDisableParallelForRestriction]
        public NativeArray<RendererNode> Nodes;
    }


    [BurstCompile(CompileSynchronously = true)]
    private struct FlushDirtyRendererNodesJob : IJobParallelFor
    {

        public void Execute(int i)
        {
            RendererNode rendererNode = this.DirtyNodes[i];
            if (rendererNode.active)
            {
                rendererNode.UpdateTransform();
            }
            this.Nodes[rendererNode.Id.Index] = rendererNode;
        }


        [NativeDisableParallelForRestriction]
        [WriteOnly]
        public NativeArray<RendererNode> Nodes;


        [DeallocateOnJobCompletion]
        [ReadOnly]
        public NativeArray<RendererNode> DirtyNodes;
    }


    [BurstCompile(CompileSynchronously = true)]
    private struct ClearRendererNodesJob : IJobParallelFor
    {

        public void Execute(int index)
        {
            RendererNode value = this.Nodes[index];
            value.active = false;
            this.Nodes[index] = value;
        }


        public NativeArray<RendererNode> Nodes;
    }


    [BurstCompile(CompileSynchronously = true)]
    private struct GPUAnimTriggerJob : IJobParallelFor
    {

        public void Execute(int index)
        {
            AgentData agentData = this.Agents[index];
            RendererNode rendererNode = this.AllNodes[agentData.rendererIndex];
            if (rendererNode.active)
            {
                int2 @int = new int2(rendererNode.Id.ResourceIndex, (int)rendererNode.clipId.x);
                GPUAnimClipInfo gpuanimClipInfo;
                if (this.ClipInfos.TryGetValue(@int, out gpuanimClipInfo))
                {
                    float4 clipId = rendererNode.clipId;
                    int animationCurrentFrame = this.GetAnimationCurrentFrame(gpuanimClipInfo, clipId.y, this.CurrentTime);
                    int num = this.AllNodesPreAnimFrame[agentData.rendererIndex];
                    this.AllNodesPreAnimFrame[agentData.rendererIndex] = animationCurrentFrame;
                    if (num == animationCurrentFrame)
                    {
                        return;
                    }
                    int num2 = num + 1;
                    if (animationCurrentFrame == num2)
                    {
                        this.TryTriggerEvent(agentData.id, new int3(@int, animationCurrentFrame));
                        return;
                    }
                    if (animationCurrentFrame > num2)
                    {
                        for (int i = num2; i <= animationCurrentFrame; i++)
                        {
                            this.TryTriggerEvent(agentData.id, new int3(@int, i));
                        }
                        return;
                    }
                    for (int j = num2; j <= gpuanimClipInfo.EndFrame; j++)
                    {
                        this.TryTriggerEvent(agentData.id, new int3(@int, j));
                    }
                    for (int k = gpuanimClipInfo.StartFrame; k <= animationCurrentFrame; k++)
                    {
                        this.TryTriggerEvent(agentData.id, new int3(@int, k));
                    }
                }
            }
        }


        [BurstCompile]
        private void TryTriggerEvent(int agentId, int3 triggerKey)
        {
            if (this.TriggerFrames.ContainsKey(triggerKey))
            {
                this.Results.Enqueue(new GPUAnimTriggerInfo
                {
                    AgentId = agentId,
                    TriggerKey = triggerKey
                });
            }
        }


        [BurstCompile]
        private int GetAnimationCurrentFrame(GPUAnimClipInfo clipInfo, float startPlayTime, float time)
        {
            float num;
            if (clipInfo.IsLoop)
            {
                num = (time - startPlayTime) * clipInfo.AnimSpeed % clipInfo.AnimLength;
            }
            else
            {
                num = (time - startPlayTime) * clipInfo.AnimSpeed;
            }
            return (int)math.ceil(math.lerp((float)clipInfo.StartFrame, (float)clipInfo.EndFrame, math.saturate(num / clipInfo.AnimLength))) - clipInfo.StartFrame + 1;
        }


        public float CurrentTime;


        [ReadOnly]
        public NativeArray<AgentData> Agents;


        [ReadOnly]
        public NativeArray<RendererNode> AllNodes;


        [ReadOnly]
        public NativeHashMap<int2, GPUAnimClipInfo> ClipInfos;


        [ReadOnly]
        public NativeHashMap<int3, int> TriggerFrames;


        [NativeDisableParallelForRestriction]
        public NativeArray<int> AllNodesPreAnimFrame;


        [NativeDisableParallelForRestriction]
        [WriteOnly]
        public NativeQueue<GPUAnimTriggerInfo>.ParallelWriter Results;
    }
}
