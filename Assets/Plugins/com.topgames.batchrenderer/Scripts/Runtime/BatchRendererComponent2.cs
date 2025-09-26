#if UNITY_2022_1_OR_NEWER
using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using static BRGUtility;
using FrustumPlanes = BRGExtension.FrustumPlanes;
using AABB = BRGExtension.AABB;
using BRGExtension;
using Nebukam.ORCA;
using GPUAnimation.Runtime;
/// <summary>
/// Batch Renderer Group合批渲染海量物体组件
/// </summary>
public class BatchRendererComponent2 : MonoBehaviour
{
    public static BatchRendererComponent2 Instance { get; private set; }
/// <summary>
/// 表示一个渲染批次（SrpBatch），用于管理图形绘制的相关信息。
/// </summary>
private struct SrpBatch
{
    /// <summary>
    /// 批次 ID，唯一标识一个渲染批次。
    /// </summary>
    public BatchID BatchID;

    /// <summary>
    /// 绘制批次的关键字，包含与绘制相关的特定信息。
    /// </summary>
    public DrawBatchKey DrawKey;

    /// <summary>
    /// 图形缓冲区在浮点数（float4）中的偏移量，用于定位该批次在GPU缓冲区中的位置。
    /// </summary>
    public int GraphicsBufferOffsetInFloat4;

    /// <summary>
    /// 实例偏移量，指向在批次中实例的起始位置。
    /// </summary>
    public int InstanceOffset;

    /// <summary>
    /// 实例计数，表示此批次中包含的实例数量。
    /// </summary>
    public int InstanceCount;
}

    /// <summary>
    /// 该类负责管理渲染器资源的批处理，优化程序的渲染性能。
    /// </summary>
    [SerializeField] private List<RendererResource> m_RendererResources; // 渲染器资源的列表
    private BatchRendererGroup m_BRG; // 批量渲染器组
    private List<BatchID> m_BatchIndexes; // 批量索引列表
    private int m_TotalCapacity; // 总容量
    private int m_TotalVisibleCount; // 当前可见的物体数量
    public int TotalVisibleCount => m_TotalVisibleCount; // 获取当前可见物体数量的属性

    private Dictionary<DrawBatchKey, int> m_BatchesVisibleCount = new(); // 每个绘制批次的可见数量

    private Dictionary<DrawBatchKey, NativeList<int>> m_BatchesPerDrawKey = new(); // 每个绘制键对应的批次列表  //m_BatchesIndexRangePerDrawKey
    private NativeHashMap<DrawBatchKey, NativeList<int>> m_DrawBatchesNodeIndexes; // 每个绘制键的节点索引   //m_NodeIndexRangePerDrawKey
    private NativeArray<RendererNode> m_AllRendererNodes; // 所有渲染节点的数组
    private NativeHashMap<int, RendererNode> m_DirtyRendererNodes; // 脏渲染节点的哈希映射

    private Dictionary<DrawBatchKey, NativeArray<float4>> m_ObjectToWorldPerDrawKey = new(); // 每个绘制键的物体到世界坐标的转换矩阵
    private Dictionary<DrawBatchKey, NativeArray<float4>> m_WorldToObjectPerDrawKey = new(); // 每个绘制键的世界到物体坐标的转换矩阵
    private Dictionary<DrawBatchKey, GraphicsBuffer> m_InstanceDataPerDrawKey = new(); // 每个绘制键的实例数据缓冲区
    private Dictionary<DrawBatchKey, NativeArray<float4>> m_RenderBufferPerDrawKey = new(); // 每个绘制键的实例数据缓冲区
    private Dictionary<DrawBatchKey, NativeQueue<BatchDrawCommand>> m_BatchDrawCommandsPerDrawKey = new(); // 每个绘制键的批处理绘制命令队列  //新不存在

    private int m_MaxItemPerBatch; // 每个批次的最大项目数
    private NativeList<SrpBatch> m_DrawBatches; // 渲染批次的列表
    private NativeArray<float4> m_BrgHeader; // 批渲染组的头部信息

    private NativeHashMap<DrawBatchKey, NativeQueue<int>> m_InactiveRendererNodes; // 非活动渲染节点的哈希映射

    private void Awake()
    {
        // 将当前实例赋值给单例模式的实例
        Instance = this;

        // 初始化可见计数
        m_TotalVisibleCount = 0;

        // 创建并初始化用于存储渲染器节点的哈希映射
        m_DirtyRendererNodes = new NativeHashMap<int, RendererNode>(64, Allocator.Persistent);
        m_InactiveRendererNodes = new NativeHashMap<DrawBatchKey, NativeQueue<int>>(m_RendererResources.Count, Allocator.Persistent);

        // 创建可用于存储绘制批次的原生列表
        m_DrawBatches = new NativeList<SrpBatch>(128, Allocator.Persistent);

        // 初始化批次渲染组头部
        m_BrgHeader = new NativeArray<float4>(4, Allocator.Persistent);
        for (int i = 0; i < 4; i++)
        {
            m_BrgHeader[i] = Unity.Mathematics.float4.zero; // 设置为零
        }

        // 创建批次渲染组，并指定裁剪操作的回调
        m_BRG = new BatchRendererGroup(OnPerformCulling, IntPtr.Zero);
        m_DrawBatchesNodeIndexes = new NativeHashMap<DrawBatchKey, NativeList<int>>(m_RendererResources.Count, Allocator.Persistent);
        m_TotalCapacity = 0;

        // 遍历渲染资源，注册网格和材料，并初始化相关数据
        foreach (var item in m_RendererResources)
        {
            m_TotalCapacity += item.capacity; // 计算总容量
            var meshId = m_BRG.RegisterMesh(item.mesh); // 注册网格
            var matId = m_BRG.RegisterMaterial(item.material); // 注册材料
            item.RegisterResource(meshId, matId); // 注册资源

            m_InactiveRendererNodes.Add(item.Key, new NativeQueue<int>(Allocator.Persistent)); // 添加到非活动渲染节点
        }

        // 创建存储所有渲染器节点的原生数组
        m_AllRendererNodes = new NativeArray<RendererNode>(m_TotalCapacity, Allocator.Persistent);

        // 创建渲染器数据缓存
        CreateRendererDataCaches();

        // 生成绘制批次
        GenerateBatches();

        // 初始化批次头部  可不用
        InitializeBatchHeader();

        // 设置全局边界
        Bounds bounds = new Bounds(new Vector3(0, 0, 0), new Vector3(1048576.0f, 1048576.0f, 1048576.0f));
        m_BRG.SetGlobalBounds(bounds); // 设置渲染组的全局边界
    }

    private void Start()
    {
        RVOComponent.Instance?.SubscribeAgentDataApplyEvent(OnAgentDataApply);
    }


    private void Update()
    {
        if (m_TotalVisibleCount > 0)
        {
            FlushDirtyRendererNodes();
            JobHandle updateInstanceDataJobHandle = UpdateRendererNodeData();
            UploadInstanceDataJobs(updateInstanceDataJobHandle);
        }
    }


    private void OnDestroy()
    {
        foreach (var batchKey in m_DrawBatchesNodeIndexes.GetKeyArray(Allocator.Temp))
        {
            m_DrawBatchesNodeIndexes[batchKey].Dispose();
            m_WorldToObjectPerDrawKey[batchKey].Dispose();
            m_ObjectToWorldPerDrawKey[batchKey].Dispose();
            m_InstanceDataPerDrawKey[batchKey].Dispose();
            m_BatchesPerDrawKey[batchKey].Dispose();
            m_BatchDrawCommandsPerDrawKey[batchKey].Dispose();
        }
        m_DrawBatchesNodeIndexes.Dispose();
        m_AllRendererNodes.Dispose();
        m_DrawBatches.Dispose();
        m_BrgHeader.Dispose();
        foreach (var item in m_InactiveRendererNodes.GetKeyArray(Allocator.Temp))
        {
            m_InactiveRendererNodes[item].Dispose();
        }
        m_DirtyRendererNodes.Dispose();
        m_InactiveRendererNodes.Dispose();
        m_BRG.Dispose();
    }

    private void OnAgentDataApply(NativeArray<AgentData> arg0)
    {
        SetRendererData(arg0);
    }
    /// <summary>
    /// 获取可添加RendererNode个数
    /// </summary>
    /// <param name="resourceIndex"></param>
    /// <returns></returns>
    public int GetCapacityRemain(int resourceIndex)
    {
        if (m_RendererResources == null) return 0;
        var res = m_RendererResources[resourceIndex];
        return res.capacity - m_BatchesVisibleCount[res.Key];
    }
    /// <summary>
    /// 获取已注册的渲染Mesh个数
    /// </summary>
    /// <returns></returns>
    public int GetResourceCount()
    {
        return m_RendererResources == null ? 0 : m_RendererResources.Count;
    }

    /// <summary>
    /// 同步脏数据
    /// </summary>
    private void FlushDirtyRendererNodes()
    {
        if (m_DirtyRendererNodes.Count > 0)
        {
            var dirtyNodes = m_DirtyRendererNodes.GetValueArray(Allocator.TempJob);
            var job = new FlushDirtyRendererNodesJob()
            {
                Nodes = m_AllRendererNodes,
                DirtyNodes = dirtyNodes
            };
            job.Schedule(dirtyNodes.Length, 64).Complete();
            dirtyNodes.Dispose();
            m_DirtyRendererNodes.Clear();
        }
    }
    private void GenerateBatches()
    {
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS) // 如果不是在Unity编辑器中，并且是Android或iOS平台
#if DISABLE_HYBRIDCLR // 如果禁用HybridCLR
        int kBRGBufferMaxWindowSize = 16 * 256 * 256; // 设置BRG缓冲区最大窗口大小
#else
        int kBRGBufferMaxWindowSize = 16 * 128 * 128; // 设置BRG缓冲区最大窗口大小
#endif
#else
        int kBRGBufferMaxWindowSize = 16 * 1024 * 1024; // 在其他平台上设置BRG缓冲区最大窗口大小
#endif
        const int kItemSize = (2 * 3 + 2);  // 每个物体包含2个3x4矩阵、1个颜色值和1个动画ID，内存大小共8个float4
        m_MaxItemPerBatch = ((kBRGBufferMaxWindowSize / kSizeOfFloat4) - 4) / kItemSize;  // 计算每个批次的最大物体数量，-4是为了解决64个字节的BRG约束

        // 对于每个绘制键，创建批次
        foreach (var drawKey in m_DrawBatchesNodeIndexes.GetKeyArray(Allocator.Temp))
        {
            // 如果m_BatchesPerDrawKey中不存在当前drawKey，则添加一个新的NativeList
            if (!m_BatchesPerDrawKey.ContainsKey(drawKey))
            {
                m_BatchesPerDrawKey.Add(drawKey, new NativeList<int>(128, Allocator.Persistent));
            }

            var instanceCountPerDrawKey = m_DrawBatchesNodeIndexes[drawKey].Length; // 获取当前drawKey的实例数量
            // 为每个drawKey创建世界到对象的转换矩阵和对象到世界的转换矩阵
            m_WorldToObjectPerDrawKey.Add(drawKey, new NativeArray<float4>(instanceCountPerDrawKey * 3, Allocator.Persistent));
            m_ObjectToWorldPerDrawKey.Add(drawKey, new NativeArray<float4>(instanceCountPerDrawKey * 3, Allocator.Persistent));

            // 计算每个drawKey批次的最大物体数量
            var maxItemPerDrawKeyBatch = m_MaxItemPerBatch > instanceCountPerDrawKey ? instanceCountPerDrawKey : m_MaxItemPerBatch;
            // 根据实例字节数、最大物体数量和float4大小计算批次对齐大小
            int batchAlignedSizeInFloat4 = BufferSizeForInstances(kBytesPerInstance, maxItemPerDrawKeyBatch, kSizeOfFloat4, 4 * kSizeOfFloat4) / kSizeOfFloat4;
            var batchCountPerDrawKey = (instanceCountPerDrawKey + maxItemPerDrawKeyBatch - 1) / maxItemPerDrawKeyBatch; // 计算每个drawKey的批次数

            // 创建实例数据缓冲区
            var instanceDataCountInFloat4 = batchCountPerDrawKey * batchAlignedSizeInFloat4; // 计算实例数据的float4数量
            var instanceData = new GraphicsBuffer(GraphicsBuffer.Target.Raw, GraphicsBuffer.UsageFlags.LockBufferForWrite, instanceDataCountInFloat4, kSizeOfFloat4);
            m_InstanceDataPerDrawKey.Add(drawKey, instanceData); // 将实例数据缓冲区添加到对应drawKey

            // 生成SRP批次
            int left = instanceCountPerDrawKey; // 剩余的实例数量
            for (int i = 0; i < batchCountPerDrawKey; i++)
            {
                int instanceOffset = i * maxItemPerDrawKeyBatch; // 计算实例偏移
                int gpuOffsetInFloat4 = i * batchAlignedSizeInFloat4; // 计算GPU偏移

                var batchInstanceCount = left > maxItemPerDrawKeyBatch ? maxItemPerDrawKeyBatch : left; // 当前批次的实例数量
                var drawBatch = new SrpBatch
                {
                    DrawKey = drawKey,
                    GraphicsBufferOffsetInFloat4 = gpuOffsetInFloat4,
                    InstanceOffset = instanceOffset,
                    InstanceCount = batchInstanceCount
                };

                m_BatchesPerDrawKey[drawKey].Add(m_DrawBatches.Length); // 将当前批次索引添加到drawKey的批次列表
                m_DrawBatches.Add(drawBatch); // 将当前批次添加到绘制批次列表
                left -= batchInstanceCount; // 更新剩余实例数量
            }
        }

        // 获取着色器属性ID
        int objectToWorldID = Shader.PropertyToID("unity_ObjectToWorld");
        int worldToObjectID = Shader.PropertyToID("unity_WorldToObject");
        int colorID = Shader.PropertyToID("_Color");
        int gpuAnimClipId = Shader.PropertyToID("_ClipId");

        // 创建批次元数据数组
        var batchMetadata = new NativeArray<MetadataValue>(4, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < m_DrawBatches.Length; i++)
        {
            var drawBatch = m_DrawBatches[i]; // 获取当前批次
            var instanceData = m_InstanceDataPerDrawKey[drawBatch.DrawKey]; // 获取对应drawKey的实例数据

            var baseOffset = drawBatch.GraphicsBufferOffsetInFloat4 * kSizeOfFloat4; // 计算基础偏移
            int gpuAddressOffset = baseOffset + 64; // 计算GPU地址偏移
            batchMetadata[0] = CreateMetadataValue(objectToWorldID, gpuAddressOffset, true);       // 创建矩阵元数据
            gpuAddressOffset += kSizeOfPackedMatrix * drawBatch.InstanceCount; // 更新地址偏移
            batchMetadata[1] = CreateMetadataValue(worldToObjectID, gpuAddressOffset, true); // 创建逆矩阵元数据
            gpuAddressOffset += kSizeOfPackedMatrix * drawBatch.InstanceCount; // 更新地址偏移
            batchMetadata[2] = CreateMetadataValue(colorID, gpuAddressOffset, true); // 创建颜色元数据
            gpuAddressOffset += kSizeOfFloat4 * drawBatch.InstanceCount; // 更新地址偏移
            batchMetadata[3] = CreateMetadataValue(gpuAnimClipId, gpuAddressOffset, true); // 创建动画剪辑ID元数据

            // 根据BatchRendererGroup的缓冲区目标添加批次
            if (BatchRendererGroup.BufferTarget == BatchBufferTarget.ConstantBuffer)
            {
                drawBatch.BatchID = m_BRG.AddBatch(batchMetadata, instanceData.bufferHandle, (uint)BatchRendererGroup.GetConstantBufferOffsetAlignment(), (uint)BatchRendererGroup.GetConstantBufferMaxWindowSize());
            }
            else
            {
                drawBatch.BatchID = m_BRG.AddBatch(batchMetadata, instanceData.bufferHandle);
            }
            m_DrawBatches[i] = drawBatch; // 更新批次信息
        }
    }


    private void CreateRendererDataCaches()
    {
        // 清空可见批次计数的列表
        m_BatchesVisibleCount.Clear();
        int index = 0;  // 初始化索引，用于追踪节点的数量

        // 遍历所有的渲染器资源
        foreach (var rendererRes in m_RendererResources)
        {
            var drawKey = rendererRes.Key;  // 获取当前渲染器资源的关键字
            m_BatchesVisibleCount.Add(drawKey, 0);  // 为当前关键字添加初始可见批次计数，设置为0

            NativeList<int> perBatchNodes;  // 声明一个用于存储每个批次节点的本地列表

            // 检查当前关键字是否已存在于批次节点索引字典中
            if (!m_DrawBatchesNodeIndexes.ContainsKey(drawKey))
            {
                // 如果不存在，则创建一个新的本地列表并将其添加到字典中
                perBatchNodes = new NativeList<int>(2048, Allocator.Persistent);
                m_DrawBatchesNodeIndexes.Add(drawKey, perBatchNodes);

                // 创建一个新的持久化队列，用于存储每个绘制关键字的批次绘制命令
                NativeQueue<BatchDrawCommand> batchDrawCommands = new NativeQueue<BatchDrawCommand>(Allocator.Persistent);
                m_BatchDrawCommandsPerDrawKey.Add(drawKey, batchDrawCommands);
            }
            else
            {
                // 如果已经存在，则直接获取对应的本地列表
                perBatchNodes = m_DrawBatchesNodeIndexes[drawKey];
            }

            // 获取当前渲染器资源的基础颜色
            var baseColor = rendererRes.material.color;
            var initColor = float4(baseColor.r, baseColor.g, baseColor.b, baseColor.a);  // 将颜色转换为float4格式

            // 为当前渲染器资源的容量创建节点
            for (int i = 0; i < rendererRes.capacity; i++)
            {
                // 获取渲染器资源的包围盒
                var aabb = rendererRes.mesh.bounds.ConvertToAABB();

                // 创建一个新的渲染节点，包含唯一的ID、位置、旋转、缩放和包围盒
                var node = new RendererNode(new RendererNodeId(drawKey, index, 0), Unity.Mathematics.float3.zero, Unity.Mathematics.quaternion.identity, float3(1), aabb);

                node.color = initColor;  // 设置节点的颜色为初始化颜色
                perBatchNodes.Add(index);  // 将节点索引添加到当前批次的节点列表中
                m_AllRendererNodes[index++] = node;  // 将创建的节点添加到所有渲染节点数组中，并增加索引
            }
        }
    }


    public RendererNodeId AddRenderer(int resourceIdx, float3 pos, quaternion rot, float3 scale)
    {
        if (resourceIdx < 0 || resourceIdx >= m_RendererResources.Count)
        {
            Debug.LogWarning($"添加Renderer失败, resourceIdx越界");
            return RendererNodeId.Null;
        }
        var rendererRes = m_RendererResources[resourceIdx];
        var inactiveList = m_InactiveRendererNodes[rendererRes.Key];
        if (inactiveList.Count < 1)
        {
            var nodesIndexes = m_DrawBatchesNodeIndexes[rendererRes.Key];
            var jobs = new GetInactiveRendererNodeJob
            {
                Nodes = m_AllRendererNodes.AsReadOnly(),
                DirtyNodes = m_DirtyRendererNodes.AsReadOnly(),
                NodesIndexes = nodesIndexes,
                //RequireCount = 2048,
                Outputs = inactiveList.AsParallelWriter()
            };
            jobs.Schedule(nodesIndexes.Length, 16).Complete();
        }

        if (!inactiveList.TryDequeue(out int inactiveNodeIndex))
        {
            Debug.LogWarning("添加Renderer失败, Inactive renderer node is null");
            return RendererNodeId.Null;
        }
        var renderer = m_AllRendererNodes[inactiveNodeIndex];
        renderer.position = pos;
        renderer.rotation = rot;
        renderer.localScale = scale;
        renderer.active = true;
        //renderer.color = float4(1);
        renderer.culling = false;
        renderer.clipId = 0;
        m_DirtyRendererNodes.Add(renderer.Id.Index, renderer);
        m_BatchesVisibleCount[rendererRes.Key]++;
        m_TotalVisibleCount++;
        return renderer.Id;
    }
    /// <summary>
    /// 移除渲染节点
    /// </summary>
    /// <param name="id"></param>
    public void RemoveRenderer(RendererNodeId id)
    {
        if (m_DirtyRendererNodes.TryGetValue(id.Index, out var node))
        {
            node.active = false;
            m_DirtyRendererNodes[id.Index] = node;
            return;
        }
        node = m_AllRendererNodes[id.Index];
        node.active = false;
        m_DirtyRendererNodes.Add(id.Index, node);
        m_BatchesVisibleCount[id.BatchKey]--;
        m_TotalVisibleCount--;
    }
    /// <summary>
    /// 清除所有渲染节点
    /// </summary>
    public void ClearRenderers()
    {
        if (m_AllRendererNodes.Length > 0)
        {
            var job = new ClearRendererNodesJob()
            {
                Nodes = m_AllRendererNodes
            };
            job.Schedule(m_AllRendererNodes.Length, 64).Complete();

            m_TotalVisibleCount = 0;
            foreach (var drawKey in m_BatchesPerDrawKey.Keys)
            {
                m_BatchesVisibleCount[drawKey] = 0;
            }
        }
    }
    /// <summary>
    /// 通过JobSystem更新渲染数据
    /// </summary>
    /// <param name="agents"></param>
    public void SetRendererData(NativeArray<AgentData> agentsData)
    {
        //var tempAgents = new NativeArray<AgentData>(agentsData, Allocator.TempJob);
        var job = new UpdateRendererNodeDataJob
        {
            AgentDataArr = agentsData,
            Nodes = m_AllRendererNodes
        };
        job.Schedule(agentsData.Length, 64).Complete();
        //tempAgents.Dispose();
    }
    public void SetRendererData(RendererNodeId id, float3 pos, quaternion rot)
    {
        if (m_DirtyRendererNodes.TryGetValue(id.Index, out var node))
        {
            node.position = pos;
            node.rotation = rot;
            m_DirtyRendererNodes[id.Index] = node;
            return;
        }
        node = m_AllRendererNodes[id.Index];
        node.position = pos;
        node.rotation = rot;
        m_DirtyRendererNodes.Add(id.Index, node);
    }
    public void SetRendererClipId(RendererNodeId id, int animClipIndex)
    {
        if (m_DirtyRendererNodes.TryGetValue(id.Index, out var node))
        {
            var clipId = node.clipId;
            clipId.x = animClipIndex;
            node.clipId = clipId;
            m_DirtyRendererNodes[id.Index] = node;
            return;
        }
        node = m_AllRendererNodes[id.Index];
        var tempClipId = node.clipId;
        tempClipId.x = animClipIndex;
        tempClipId.y = Time.time;
        node.clipId = tempClipId;
        m_DirtyRendererNodes.Add(id.Index, node);
    }
    public void SetRendererData(RendererNodeId id, float3 pos, quaternion rot, float clipId)
    {
        if (m_DirtyRendererNodes.TryGetValue(id.Index, out var node))
        {
            var tempClipId = node.clipId;
            tempClipId.x = clipId;
            tempClipId.y = Time.time;
            node.clipId = tempClipId;
            node.position = pos;
            node.rotation = rot;
            m_DirtyRendererNodes[id.Index] = node;
            return;
        }
        node = m_AllRendererNodes[id.Index];
        node.position = pos;
        node.rotation = rot;
        var tmpClipId = node.clipId;
        tmpClipId.x = clipId;
        tmpClipId.y = Time.time;
        node.clipId = tmpClipId;
        m_DirtyRendererNodes.Add(id.Index, node);
    }
    public void SetRendererColor(RendererNodeId id, float4 color)
    {
        if (m_DirtyRendererNodes.TryGetValue(id.Index, out var node))
        {
            node.color = color;
            m_DirtyRendererNodes[id.Index] = node;
            return;
        }
        node = m_AllRendererNodes[id.Index];
        node.color = color;
        m_DirtyRendererNodes.Add(id.Index, node);
    }
    /// <summary>
    /// 获取动画片段信息
    /// </summary>
    /// <param name="id"></param>
    /// <param name="clipIndex"></param>
    /// <returns>xy是动画帧范围, z是动画时长, w是动画isLoop</returns>
    public Vector4 GetAnimationClipInfo(RendererNodeId id, int clipIndex)
    {
        var nodeMat = m_BRG.GetRegisteredMaterial(id.BatchKey.MaterialID);
        return GPUAnimationUtility.GetAnimationClipInfo(nodeMat, clipIndex);
    }
    public GPUBoneData GetAnimationAttachBone(RendererNodeId id, int clipIndex)
    {
        var nodeMat = m_BRG.GetRegisteredMaterial(id.BatchKey.MaterialID);
        return GPUAnimationUtility.GetAttachBoneTransform(nodeMat, clipIndex);
    }
    private void InitializeBatchHeader()
    {
        foreach (var srpBatch in m_DrawBatches)
        {
            var instanceData = m_InstanceDataPerDrawKey[srpBatch.DrawKey];
            instanceData.SetData(m_BrgHeader, 0, srpBatch.GraphicsBufferOffsetInFloat4, m_BrgHeader.Length);
        }
    }

    private void UploadInstanceDataJobs(JobHandle updateInstanceDataJobHandle)
    {
        NativeArray<JobHandle> handles = new NativeArray<JobHandle>(m_BatchesPerDrawKey.Count, Allocator.TempJob);
        int index = 0;
        

        foreach (var pair in m_BatchesPerDrawKey)
        {
            var drawKey = pair.Key;
            var instanceData = m_InstanceDataPerDrawKey[drawKey];
            var batchIds = pair.Value;
            var objectToWorldMatrices = m_ObjectToWorldPerDrawKey[drawKey];
            var worldToObjectMatrices = m_WorldToObjectPerDrawKey[drawKey];
            var nodesIndexes = m_DrawBatchesNodeIndexes[drawKey];
            // 检查这个 GraphicsBuffer 是否已经被锁定
           
                NativeArray<float4> output = instanceData.LockBufferForWrite<float4>(0, instanceData.count);
                // 添加到已锁定列表
                handles[index++] = new UploadInstanceDataJob
                {
                    ObjectToWorldMatrices = objectToWorldMatrices,
                    WorldToObjectMatrices = worldToObjectMatrices,
                    Nodes = m_AllRendererNodes.AsReadOnly(),
                    NodesIndexes = nodesIndexes,
                    BatchIds = batchIds,
                    Batches = m_DrawBatches,
                    Output = output
                }.Schedule(updateInstanceDataJobHandle);
            
        }

        JobHandle.CompleteAll(handles);
        if (handles.IsCreated)
        {
            handles.Dispose();
        }
        foreach (var pair in m_InstanceDataPerDrawKey)
        {
            var instanceData = pair.Value;
            
                instanceData.UnlockBufferAfterWrite<float4>(instanceData.count);
             
        }
        // 释放 JobHandles 的资源
        if (handles.IsCreated)
        {
            handles.Dispose();
        }
    }
    [BurstCompile]
    private struct GetInactiveRendererNodeJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<RendererNode>.ReadOnly Nodes;
        [ReadOnly]
        public NativeHashMap<int, RendererNode>.ReadOnly DirtyNodes;
        [ReadOnly]
        public NativeList<int> NodesIndexes;
        [ReadOnly]
        public int RequireCount;

        public NativeQueue<int>.ParallelWriter Outputs;

        public void Execute(int index)
        {
            int curIndex = NodesIndexes[index];
            if (DirtyNodes.ContainsKey(curIndex))
            {
                return;
            }
            var node = Nodes[curIndex];

            if (!node.active)
            {
                Outputs.Enqueue(curIndex);
            }
        }
    }
    [BurstCompile]
    private struct UploadInstanceDataJob : IJob
    {
        // 只读的原生数组，存储对象到世界坐标的矩阵
        [ReadOnly]
        public NativeArray<float4> ObjectToWorldMatrices;

        // 只读的原生数组，存储世界到对象坐标的矩阵
        [ReadOnly]
        public NativeArray<float4> WorldToObjectMatrices;

        // 只读的原生数组，存储渲染节点信息
        [ReadOnly]
        public NativeArray<RendererNode>.ReadOnly Nodes;

        // 只读的原生列表，存储节点索引
        [ReadOnly]
        public NativeList<int> NodesIndexes;

        // 只读的原生列表，存储批次 ID
        [ReadOnly]
        public NativeList<int> BatchIds;

        // 只读的原生列表，存储 SRP 批次信息
        [ReadOnly]
        public NativeList<SrpBatch> Batches;

        // 写入的原生数组，输出数据
        [WriteOnly]
        public NativeArray<float4> Output;

        // 执行任务的方法
        public void Execute()
        {
            // 批次的数量
            int batchCount = BatchIds.Length;

            // 遍历每个批次
            for (int i = 0; i < batchCount; i++)
            {
                // 获取当前批次的信息
                var num = BatchIds[i];
                var srpBatch = Batches[BatchIds[i]];

                // 批次中的实例数量
                var batchInstanceCount = srpBatch.InstanceCount;
                // 当前批次在 CPU 缓冲区的偏移量
                var cpuBufferOffset = srpBatch.InstanceOffset;

                // GPU 缓冲区中对象到世界坐标矩阵的偏移
                var objectToWorldGpuBufferOffset = srpBatch.GraphicsBufferOffsetInFloat4 + 4;
                // GPU 缓冲区中世界到对象坐标矩阵的偏移
                var worldToObjectGpuBufferOffset = objectToWorldGpuBufferOffset + batchInstanceCount * 3;
                // GPU 缓冲区中颜色数据的偏移
                var colorGpuBufferOffset = worldToObjectGpuBufferOffset + batchInstanceCount * 3;
                // GPU 缓冲区中动画片段 ID 的偏移
                var animClipIdGpuBufferOffset = colorGpuBufferOffset + batchInstanceCount;

                // 遍历当前批次中的每个实例
                for (int j = 0; j < batchInstanceCount; j++)
                {
                    // 获取节点索引
                    int nodeIndex = NodesIndexes[cpuBufferOffset + j];
                    var node = Nodes[nodeIndex];

                    // 如果节点不可见，则跳过
                    if (!node.Visible) continue;

                    // 计算矩阵偏移量
                    var matrixOffset = j * 3;
                    var matrixCpuBufferOffset = cpuBufferOffset * 3 + matrixOffset;

                    // 将对象到世界坐标矩阵写入输出数组
                    Output[objectToWorldGpuBufferOffset + matrixOffset] = ObjectToWorldMatrices[matrixCpuBufferOffset];
                    Output[objectToWorldGpuBufferOffset + matrixOffset + 1] = ObjectToWorldMatrices[matrixCpuBufferOffset + 1];
                    Output[objectToWorldGpuBufferOffset + matrixOffset + 2] = ObjectToWorldMatrices[matrixCpuBufferOffset + 2];

                    // 将世界到对象坐标矩阵写入输出数组
                    Output[worldToObjectGpuBufferOffset + matrixOffset] = WorldToObjectMatrices[matrixCpuBufferOffset];
                    Output[worldToObjectGpuBufferOffset + matrixOffset + 1] = WorldToObjectMatrices[matrixCpuBufferOffset + 1];
                    Output[worldToObjectGpuBufferOffset + matrixOffset + 2] = WorldToObjectMatrices[matrixCpuBufferOffset + 2];

                    // 将节点颜色写入输出数组
                    Output[colorGpuBufferOffset + j] = node.color;
                    // 将节点动画片段 ID 写入输出数组
                    Output[animClipIdGpuBufferOffset + j] = node.clipId;
                }
            }
        }
    }
    [BurstCompile] // 使用Burst编译器优化此作业
    private unsafe struct UpdateInstanceDataJob : IJobParallelFor
    {
        [ReadOnly] // 表示该字段只在读取时使用，避免在并行作业中被修改
        public NativeArray<RendererNode>.ReadOnly Nodes; // 节点数组，存储每个渲染节点的信息

        [ReadOnly] // 同上
        public NativeList<int> NodesIndexes; // 节点索引列表，用于访问Nodes中的特定节点

        [NativeDisableParallelForRestriction] // 禁用对并行作业的限制，允许在并行作业中写入
        public NativeArray<float4> ObjectToWorldMatrices; // 存储对象到世界矩阵的数组

        [NativeDisableParallelForRestriction] // 同上
        public NativeArray<float4> WorldToObjectMatrices; // 存储世界到对象矩阵的数组

        // 作业执行的主要方法
        public void Execute(int i)
        {
            int nodeIndex = NodesIndexes[i]; // 获取当前索引对应的节点索引
            var node = Nodes[nodeIndex]; // 获取当前节点

            // 如果节点不可见，则直接返回，不进行计算
            if (!node.Visible) return;

            // 获取对象到世界矩阵和世界到对象矩阵的指针
            var objectToWorldPtr = (float4*)ObjectToWorldMatrices.GetUnsafePtr();
            var worldToObjectPtr = (float4*)WorldToObjectMatrices.GetUnsafePtr();

            var matrixIdx = i * 3; // 计算当前节点的矩阵在数组中的起始索引
            var objectToWorldMatrix = node.BuildMatrix(); // 通过节点构建对象到世界矩阵

            // 将对象到世界矩阵的各个部分打包到对应的数组中
            objectToWorldMatrix.PackedMatrices(ref objectToWorldPtr[matrixIdx], ref objectToWorldPtr[matrixIdx + 1], ref objectToWorldPtr[matrixIdx + 2]);

            // 计算世界到对象矩阵（即对象到世界矩阵的逆矩阵）
            var worldToObjectMatrix = math.inverse(objectToWorldMatrix);
            // 将世界到对象矩阵的各个部分打包到对应的数组中
            worldToObjectMatrix.PackedMatrices(ref worldToObjectPtr[matrixIdx], ref worldToObjectPtr[matrixIdx + 1], ref worldToObjectPtr[matrixIdx + 2]);
        }
    }

    /// <summary>
    /// 更新渲染节点数据并返回一个工作句柄。
    /// </summary>
    /// <returns>合并后的工作句柄。</returns>
    private JobHandle UpdateRendererNodeData()
    {
        // 创建一个本地的 JobHandle 数组，大小为 m_DrawBatchesNodeIndexes 的计数，分配方式为 Temp。
        NativeArray<JobHandle> handles = new NativeArray<JobHandle>(m_DrawBatchesNodeIndexes.Count, Allocator.Temp);

        int index = 0;
        // 遍历 m_DrawBatchesNodeIndexes 的所有键（绘制批次的索引）。
        foreach (var drawKey in m_DrawBatchesNodeIndexes.GetKeyArray(Allocator.Temp))
        {
            // 如果当前绘制批次的可见计数小于1，则跳过。
            if (m_BatchesVisibleCount[drawKey] < 1) continue;

            // 获取当前绘制批次的节点索引、物体到世界的矩阵和世界到物体的矩阵。
            var nodesIndexes = m_DrawBatchesNodeIndexes[drawKey];
            var objectToWorldMatrices = m_ObjectToWorldPerDrawKey[drawKey];
            var worldToObjectMatrices = m_WorldToObjectPerDrawKey[drawKey];

            // 创建一个 UpdateInstanceDataJob 作业，并调度它。
            handles[index++] = new UpdateInstanceDataJob
            {
                // 传递所有渲染节点的只读集合
                Nodes = m_AllRendererNodes.AsReadOnly(),
                // 传递当前绘制批次的节点索引
                NodesIndexes = nodesIndexes,
                // 传递物体到世界的矩阵
                ObjectToWorldMatrices = objectToWorldMatrices,
                // 传递世界到物体的矩阵
                WorldToObjectMatrices = worldToObjectMatrices,
            }.Schedule(nodesIndexes.Length, 64); // 调度作业，使用节点索引的长度和每批的任务数量（64）
        }

        // 调度所有已排队的作业。
        JobHandle.ScheduleBatchedJobs();
        // 返回合并后的工作句柄
        return JobHandle.CombineDependencies(handles);
    }

    /// <summary>
    /// 执行剔除操作，返回一个工作句柄。
    /// </summary>
    /// <param name="rendererGroup">批量渲染器组。</param>
    /// <param name="cullingContext">剔除上下文。</param>
    /// <param name="cullingOutput">剔除输出。</param>
    /// <param name="userContext">用户上下文的指针。</param>
    /// <returns>工作句柄。</returns>
    public unsafe JobHandle OnPerformCulling(
        BatchRendererGroup rendererGroup,
        BatchCullingContext cullingContext,
        BatchCullingOutput cullingOutput,
        IntPtr userContext)
    {
        // 如果总可见计数大于0，执行剔除操作。
        if (m_TotalVisibleCount > 0)
        {
            // 创建一个用于存储绘制命令的剔除输出。
            BatchCullingOutputDrawCommands drawCommands = new BatchCullingOutputDrawCommands();
            // 进行线程剔除，使用剔除上下文中的剔除平面。
            CullingThreaded(ref drawCommands, cullingContext.cullingPlanes);

            // 将绘制命令写入到剔除输出中。
            cullingOutput.drawCommands[0] = drawCommands;
        }
        // 返回一个空的工作句柄。
        return new JobHandle();
    }

    [BurstCompile] // 启用 Burst 编译器以提高性能
    private unsafe struct CullingJob : IJobParallelFor // 定义一个并行作业，使用 IJobParallelFor 接口
    {
        [ReadOnly] // 指示该字段只读，防止在作业中修改
        public NativeArray<Plane> CullingPlanes; // 用于剔除的平面数组

        [DeallocateOnJobCompletion] // 作业完成后自动释放内存
        [ReadOnly] // 该字段只读
        public NativeArray<SrpBatch> Batches; // 存储批处理的信息

        [ReadOnly] // 该字段只读
        public NativeArray<RendererNode>.ReadOnly Nodes; // 渲染节点数组

        [ReadOnly] // 该字段只读
        public NativeList<int> NodesIndexes; // 节点索引列表

        [ReadOnly] // 该字段只读
        [NativeDisableContainerSafetyRestriction] // 禁用容器安全检查
        public NativeArray<float4> ObjectToWorldMatrices; // 对象到世界坐标的转换矩阵数组

        [ReadOnly] // 该字段只读
        public int DrawKeyOffset; // 用于绘制的键的偏移量

        [WriteOnly] // 指示该字段只写
        [NativeDisableUnsafePtrRestriction] // 禁用对 Unsafe 指针的限制
        public int* VisibleInstances; // 存储可见实例的索引（使用指针以提高性能）

        [WriteOnly] // 指示该字段只写
        public NativeQueue<BatchDrawCommand>.ParallelWriter DrawCommands; // 存储绘制命令的并行写入器

        public void Execute(int index) // 执行作业的主要逻辑
        {
            var batchesPtr = (SrpBatch*)Batches.GetUnsafeReadOnlyPtr(); // 获取批处理数组的指针
            var objectToWorldMatricesPtr = (float4*)ObjectToWorldMatrices.GetUnsafeReadOnlyPtr(); // 获取变换矩阵的指针
            ref var srpBatch = ref batchesPtr[index]; // 获取当前批处理的引用

            int visibleCount = 0; // 可见实例计数
            int batchOffset = DrawKeyOffset + srpBatch.InstanceOffset; // 计算当前批处理的偏移量
            int idx = 0; // 索引变量

            // 遍历当前批处理的实例
            for (int instanceIdx = 0; instanceIdx < srpBatch.InstanceCount; instanceIdx++)
            {
                idx = srpBatch.InstanceOffset + instanceIdx; // 计算当前实例的索引

                int nodeIndex = NodesIndexes[idx]; // 获取节点索引
                var node = Nodes[nodeIndex]; // 获取对应的渲染节点
                if (!node.active) continue; // 如果节点不活跃，则跳过

                // 假设只有一个剔除切分并且有 6 个剔除平面
                var matrixIdx = (srpBatch.InstanceOffset + instanceIdx) * 3; // 计算矩阵索引
                var worldAABB = Transform(ref objectToWorldMatricesPtr[matrixIdx], ref objectToWorldMatricesPtr[matrixIdx + 1], ref objectToWorldMatricesPtr[matrixIdx + 2], node.aabb); // 计算世界坐标系下的轴对齐包围盒（AABB）

                // 检查 AABB 是否与剔除平面相交
                node.culling = (Intersect(CullingPlanes, ref worldAABB) == FrustumPlanes.IntersectResult.Out);
                if (node.culling) continue; // 如果节点被剔除，则跳过

                // 如果节点不被剔除，记录可见实例的索引
                VisibleInstances[batchOffset + visibleCount] = instanceIdx;
                visibleCount++; // 更新可见计数
            }

            // 如果有可见实例
            if (visibleCount > 0)
            {
                var drawKey = srpBatch.DrawKey; // 获取绘制关键字
                                                // 将绘制命令添加到队列
                DrawCommands.Enqueue(new BatchDrawCommand
                {
                    visibleOffset = (uint)batchOffset, // 可见实例的偏移量
                    visibleCount = (uint)visibleCount, // 可见实例的数量
                    batchID = srpBatch.BatchID, // 批处理 ID
                    materialID = drawKey.MaterialID, // 材料 ID
                    meshID = drawKey.MeshID, // 网格 ID
                    submeshIndex = (ushort)drawKey.SubmeshIndex, // 子网格索引
                    splitVisibilityMask = 0xff, // 分割可见性掩码
                    flags = BatchDrawCommandFlags.None, // 绘制命令标志
                    sortingPosition = 0 // 排序位置
                });
            }
        }
    }

    // 这是一个私有的、不安全的方法，用于执行批量剔除的多线程处理
    private unsafe void CullingThreaded(ref BatchCullingOutputDrawCommands drawCommands, NativeArray<Plane> cullingPlanes)
    {
        // 分配可见实例数组的内存
        drawCommands.visibleInstances = Malloc<int>(m_TotalCapacity);
        // 创建一个临时的作业句柄数组，用于存储每个剔除作业的句柄
        NativeArray<JobHandle> cullingJobs = new NativeArray<JobHandle>(m_BatchesPerDrawKey.Count, Allocator.TempJob);

        int drawKeyIdx = 0; // 绘制关键字索引
        int totalInstanceCount = 0; // 总实例计数

        // 遍历每个绘制关键字及其对应的批次
        foreach (var pair in m_BatchesPerDrawKey)
        {
            // 如果当前关键字的可见批次数小于1，则跳过该关键字
            if (m_BatchesVisibleCount[pair.Key] < 1) continue;

            int instanceCountPerDrawKey = 0; // 当前绘制关键字的实例计数
                                             // 计算当前绘制关键字的所有批次实例总数
            foreach (var batchIdx in pair.Value)
            {
                instanceCountPerDrawKey += m_DrawBatches[batchIdx].InstanceCount;
            }

            // 获取当前批次的绘制关键字和相关批次
            var drawKey = pair.Key;
            var batchIds = pair.Value;
            var batchDrawCommands = m_BatchDrawCommandsPerDrawKey[drawKey];

            // 创建一个临时的批次数组
            var batchesPerDrawKey = new NativeArray<SrpBatch>(batchIds.Length, Allocator.TempJob);
            for (int i = 0; i < batchIds.Length; i++)
            {
                var srpBatch = batchIds[i];
                batchesPerDrawKey[i] = m_DrawBatches[batchIds[i]];
            }

            // 获取当前绘制关键字的节点索引
            var nodesIndexes = m_DrawBatchesNodeIndexes[drawKey];
            // 创建并调度剔除作业
            cullingJobs[drawKeyIdx] = new CullingJob
            {
                CullingPlanes = cullingPlanes, // 剔除平面
                Batches = batchesPerDrawKey, // 批次
                Nodes = m_AllRendererNodes.AsReadOnly(), // 渲染节点
                NodesIndexes = nodesIndexes, // 节点索引
                DrawKeyOffset = totalInstanceCount, // 当前绘制关键字的偏移
                ObjectToWorldMatrices = m_ObjectToWorldPerDrawKey[drawKey], // 对象到世界的矩阵

                VisibleInstances = drawCommands.visibleInstances, // 可见实例数组
                DrawCommands = batchDrawCommands.AsParallelWriter(), // 批次绘制命令
            }.Schedule(batchIds.Length, 64); // 使用64个并行作业调度

            totalInstanceCount += instanceCountPerDrawKey; // 更新总实例计数
            drawKeyIdx++; // 更新绘制关键字索引
        }

        // 合并作业句柄并完成所有剔除作业
        JobHandle.CombineDependencies(cullingJobs).Complete();
        // 如果作业数组被创建，则释放其内存
        if (cullingJobs.IsCreated)
        {
            cullingJobs.Dispose();
        }

        // 计算总的绘制命令数量
        var totalBatchDrawCommands = 0;
        foreach (var pair in m_BatchDrawCommandsPerDrawKey)
        {
            totalBatchDrawCommands += pair.Value.Count; // 累加每个绘制关键字的命令数量
        }

        // 设置绘制范围
        SetupDrawRanges(ref drawCommands, totalBatchDrawCommands);

        // 清空实例排序位置相关的数据
        drawCommands.instanceSortingPositions = null;
        drawCommands.instanceSortingPositionFloatCount = 0;

        // 更新绘制命令数量和绘制命令数组
        drawCommands.drawCommandCount = totalBatchDrawCommands;
        drawCommands.drawCommands = Malloc<BatchDrawCommand>(drawCommands.drawCommandCount);

        int drawCommandIdx = 0; // 绘制命令索引
                                // 遍历每个绘制命令队列，填充绘制命令数组
        foreach (var drawCommandsQueue in m_BatchDrawCommandsPerDrawKey.Values)
        {
            while (drawCommandsQueue.TryDequeue(out var drawCommand)) // 从队列中获取绘制命令
            {
                drawCommands.drawCommands[drawCommandIdx] = drawCommand; // 存储绘制命令
                drawCommandIdx++; // 更新绘制命令索引
            }
        }
    }

    // 私有方法，用于设置绘制范围
    private unsafe void SetupDrawRanges(ref BatchCullingOutputDrawCommands drawCommands, int drawCommandsCount)
    {
        // 假设只有一个绘制范围
        drawCommands.drawRangeCount = 1;
        drawCommands.drawRanges = Malloc<BatchDrawRange>(1);
        drawCommands.drawRanges[0] = new BatchDrawRange
        {
            drawCommandsBegin = 0, // 绘制命令开始索引
            drawCommandsCount = (uint)drawCommandsCount, // 绘制命令数量
            filterSettings = new BatchFilterSettings
            {
                renderingLayerMask = 1, // 渲染层掩码
                layer = 0, // 层
                motionMode = MotionVectorGenerationMode.Camera, // 运动模式
                shadowCastingMode = ShadowCastingMode.On, // 阴影投射模式
                receiveShadows = false, // 是否接收阴影
                staticShadowCaster = false, // 是否为静态阴影投射者
                allDepthSorted = false // 是否全部深度排序
            }
        };
    }

    // 使用Burst编译器进行优化的静态方法，用于检测包围盒与剔除平面的交集
    [BurstCompile]
    public static FrustumPlanes.IntersectResult Intersect(NativeArray<Plane> cullingPlanes, ref AABB a)
    {
        float3 m = a.Center; // 包围盒中心
        float3 extent = a.Extents; // 包围盒的扩展

        var inCount = 0; // 在剔除平面内的计数
                         // 遍历每个剔除平面
        for (int i = 0; i < cullingPlanes.Length; i++)
        {
            float3 normal = cullingPlanes[i].normal; // 获取平面的法线
            float dist = math.dot(normal, m) + cullingPlanes[i].distance; // 计算中心到平面的距离
            float radius = math.dot(extent, math.abs(normal)); // 计算包围盒在平面法线方向的扩展

            // 如果包围盒完全在平面之外，则返回不相交
            if (dist + radius <= 0)
                return FrustumPlanes.IntersectResult.Out;

            // 如果包围盒的一部分在平面内，则增加计数
            if (dist > radius)
                inCount++;
        }

        // 根据在剔除平面内的计数返回相交结果
        return (inCount == cullingPlanes.Length) ? FrustumPlanes.IntersectResult.In : FrustumPlanes.IntersectResult.Partial;
    }

    [BurstCompile]
    private static AABB Transform(ref float4 packed1, ref float4 packed2, ref float4 packed3, AABB localBounds)
    {
        AABB transformed;
        float3 c0 = packed1.xyz;
        float3 c1 = new float3(packed1.w, packed2.xy);
        float3 c2 = new float3(packed2.zw, packed3.x);
        float3 c3 = packed3.yzw;
        transformed.Extents = math.abs(c0 * localBounds.Extents.x) + math.abs(c1 * localBounds.Extents.y) + math.abs(c2 * localBounds.Extents.z);
        var b = localBounds.Center;
        transformed.Center = c0 * b.x + c1 * b.y + c2 * b.z + c3;
        return transformed;
    }

    [BurstCompile]
    private struct UpdateRendererNodeDataJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<AgentData> AgentDataArr;
        [NativeDisableParallelForRestriction]
        public NativeArray<RendererNode> Nodes;
        public void Execute(int index)
        {
            var agentDt = AgentDataArr[index];
            var node = Nodes[agentDt.rendererIndex];
            node.position = agentDt.worldPosition;
            node.rotation = agentDt.worldQuaternion;
            node.clipId = agentDt.clipId;
            Nodes[agentDt.rendererIndex] = node;
        }
    }

    /// <summary>
    /// 同步脏数据
    /// </summary>
    [BurstCompile]
    private struct FlushDirtyRendererNodesJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction]
        [WriteOnly]
        public NativeArray<RendererNode> Nodes;
        [ReadOnly]
        public NativeArray<RendererNode> DirtyNodes;
        public void Execute(int index)
        {
            var node = DirtyNodes[index];
            Nodes[node.Id.Index] = node;
        }
    }

    [BurstCompile]
    private struct ClearRendererNodesJob : IJobParallelFor
    {
        public NativeArray<RendererNode> Nodes;
        public void Execute(int index)
        {
            var node = Nodes[index];
            node.active = false;
            Nodes[index] = node;
        }
    }
}
#endif