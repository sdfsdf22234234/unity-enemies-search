//using System;
//using System.Collections.Generic;
//using System.Runtime.CompilerServices;
//using System.Runtime.InteropServices;
//using Unity.Burst;
//using Unity.Burst.Intrinsics;
//using Unity.Collections;
//using Unity.Collections.LowLevel.Unsafe;
//using Unity.Entities;
//using Unity.Jobs;
//using Unity.Mathematics;
//using Unity.Rendering;
//using Unity.Transforms;
//using UnityEngine;
//using UnityEngine.Rendering;
//using UnityEngine.Scripting;

//[CompilerGenerated]
//[DisableAutoCreation]
//public partial class WebGLGraphicsSystem : SystemBase
//{
//    [Preserve]
//    protected override void OnCreate()
//    {
//        // 检查 WebGL 2.0 支持
//        CheckWebGL2Support();

//        // 初始化查询 - 查找所有需要渲染的实体
//        m_Query = GetEntityQuery(
//            ComponentType.ReadOnly<LocalToWorld>(),
//            ComponentType.ReadOnly<MaterialMeshInfo>(),
//            ComponentType.ReadOnly<RenderBounds>(),
//            ComponentType.ReadOnly<GPUAnimationClipId>()
//        );

//        // 初始化批处理数据结构 - 针对 WebGL 2.0 优化
//        m_BatchesMatrices = new NativeHashMap<int2, NativeList<Matrix4x4>>(100, Allocator.Persistent);
//        m_BatchesClipIds = new NativeHashMap<int2, NativeList<float4>>(100, Allocator.Persistent);
//        m_BatchesRenderParams = new Dictionary<int2, RenderParams>();
//        _TempClipIdArr = new List<Vector4>();
//        _TempPlanes = new Plane[6];

//        // 获取主摄像机
//        m_Camera = Camera.main;
//        if (m_Camera == null)
//        {
//            m_Camera = UnityEngine.Object.FindObjectOfType<Camera>();
//        }

//        // 初始化视锥体平面
//        m_Planes = new NativeArray<Plane>(6, Allocator.Persistent);

//        // 初始化组件类型句柄
//        m_TransformHandle = GetComponentTypeHandle<LocalToWorld>(true);
//        m_MeshInfoHandle = GetComponentTypeHandle<MaterialMeshInfo>(true);
//        m_BoundsHandle = GetComponentTypeHandle<RenderBounds>(true);
//        m_ClipIdHandle = GetComponentTypeHandle<GPUAnimationClipId>(true);

//        Debug.Log("WebGLGraphicsSystem 初始化完成 - WebGL 2.0 模式");
//    }

//    [Preserve]
//    protected override void OnDestroy()
//    {
//        // 清理资源
//        if (m_BatchesMatrices.IsCreated)
//            m_BatchesMatrices.Dispose();
//        if (m_BatchesClipIds.IsCreated)
//            m_BatchesClipIds.Dispose();
//        if (m_Planes.IsCreated)
//            m_Planes.Dispose();
//    }

//    [Preserve]
//    protected override void OnUpdate()
//    {
//        if (m_Camera == null)
//            return;

//        // 更新视锥体平面
//        UpdateFrustumPlanes();

//        // 更新组件类型句柄
//        m_TransformHandle.Update(this);
//        m_MeshInfoHandle.Update(this);
//        m_BoundsHandle.Update(this);
//        m_ClipIdHandle.Update(this);

//        // 清空批处理数据
//        ClearBatches();

//        // 创建渲染作业
//        var renderJob = new RenderDataJob
//        {
//            cullingPlanes = m_Planes,
//            BatchesMatrices = m_BatchesMatrices,
//            BatchesClipIds = m_BatchesClipIds,
//            TransformHandle = m_TransformHandle,
//            MeshInfoHandle = m_MeshInfoHandle,
//            BoundsHandle = m_BoundsHandle,
//            ClipIdHandle = m_ClipIdHandle
//        };

//        // 调度渲染作业
//        var jobHandle = renderJob.ScheduleParallel(m_Query, Dependency);
//        jobHandle.Complete();

//        // 执行实际渲染
//        RenderMesh();
//    }


// private void RenderMesh()
//{
//    // 遍历所有批处理
//    var matrixKeys = m_BatchesMatrices.GetKeyArray(Allocator.Temp);
//    for (int i = 0; i < matrixKeys.Length; i++)
//    {
//        var batchKey = matrixKeys[i];
        
//        if (m_BatchesMatrices.TryGetValue(batchKey, out var matrices) && 
//            m_BatchesClipIds.TryGetValue(batchKey, out var clipIds))
//        {
//            // 优化：检查是否已缓存此批次的材质信息
//            if (!m_BatchesRenderParams.TryGetValue(batchKey, out var renderParams))
//            {
//                // 只在第一次遇到此批次时查询材质网格信息
//                var entities = m_Query.ToEntityArray(Allocator.Temp);
//                if (entities.Length > 0)
//                {
//                    var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
//                    var materialMeshInfo = entityManager.GetComponentData<MaterialMeshInfo>(entities[0]);
//                    var renderMeshArray = entityManager.GetSharedComponentManaged<RenderMeshArray>(entities[0]);
                    
//                    var material = renderMeshArray.GetMaterial(materialMeshInfo);
                    
//                    renderParams = new RenderParams
//                    {
//                        material = material,
//                        layer = 0,
//                        shadowCastingMode = ShadowCastingMode.On,
//                        receiveShadows = true
//                    };
                    
//                    // 缓存材质信息，避免重复查询
//                    m_BatchesRenderParams[batchKey] = renderParams;
//                }
//                entities.Dispose();
//            }

//            if (renderParams.material != null)
//            {
//                // 获取 mesh 信息（这个查询相对轻量）
//                var entities = m_Query.ToEntityArray(Allocator.Temp);
//                if (entities.Length > 0)
//                {
//                    var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
//                    var materialMeshInfo = entityManager.GetComponentData<MaterialMeshInfo>(entities[0]);
//                    var renderMeshArray = entityManager.GetSharedComponentManaged<RenderMeshArray>(entities[0]);
//                    var mesh = renderMeshArray.GetMesh(materialMeshInfo);

//                    // 复制动画数据到临时数组
//                    Copy2RenderCache(clipIds, _TempClipIdArr);

//                    // 设置渲染参数 - WebGL 2.0 优化
//                    renderParams.material.SetVectorArray("_ClipIds", _TempClipIdArr);
//                    renderParams.material.SetMatrixArray("_Matrices", matrices.AsArray().ToArray());

//                    // 执行批处理渲染 - WebGL 2.0 支持实例化渲染
//                    Graphics.RenderMeshInstanced<Matrix4x4>(renderParams, mesh, 0, matrices.AsArray(), matrices.Length);
//                }
//                entities.Dispose();
//            }
//        }
//    }
//    matrixKeys.Dispose();
//}
      
//    private void Copy2RenderCache(NativeList<float4> clipIds, List<Vector4> dstClipIds)
//    {
//        dstClipIds.Clear();
//        for (int i = 0; i < clipIds.Length; i++)
//        {
//            var clipId = clipIds[i];
//            dstClipIds.Add(new Vector4(clipId.x, clipId.y, clipId.z, clipId.w));
//        }
//    }

//    private void UpdateFrustumPlanes()
//    {
//        if (m_Camera == null) return;

//        // 计算视锥体平面
//        var frustumPlanes = GeometryUtility.CalculateFrustumPlanes(m_Camera);
        
//        for (int i = 0; i < 6; i++)
//        {
//            _TempPlanes[i] = frustumPlanes[i];
//            m_Planes[i] = frustumPlanes[i];
//        }
//    }

//    /// <summary>
//    /// 检查 WebGL 2.0 支持
//    /// </summary>
//    private void CheckWebGL2Support()
//    {
//        #if UNITY_WEBGL && !UNITY_EDITOR
//        // 检查是否支持 WebGL 2.0
//        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.WebGL2)
//        {
//            Debug.Log("WebGL 2.0 已启用 - 支持实例化渲染和高级特性");
//        }
//        else if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.WebGL)
//        {
//            Debug.LogWarning("WebGL 1.0 模式 - 某些高级特性可能不可用");
//        }
//        #endif
//    }

//    private void ClearBatches()
//    {
//        // 清空所有批处理数据
//        var matrixValues = m_BatchesMatrices.GetValueArray(Allocator.Temp);
//        for (int i = 0; i < matrixValues.Length; i++)
//        {
//            matrixValues[i].Clear();
//        }
//        matrixValues.Dispose();

//        var clipIdValues = m_BatchesClipIds.GetValueArray(Allocator.Temp);
//        for (int i = 0; i < clipIdValues.Length; i++)
//        {
//            clipIdValues[i].Clear();
//        }
//        clipIdValues.Dispose();

//        m_BatchesRenderParams.Clear();
//    }


//    [Preserve]
//    public WebGLGraphicsSystem()
//    {
//    }

//    private EntityQuery m_Query;
//    private NativeHashMap<int2, NativeList<Matrix4x4>> m_BatchesMatrices;
//    private NativeHashMap<int2, NativeList<float4>> m_BatchesClipIds;
//    private List<Vector4> _TempClipIdArr;
//    private Dictionary<int2, RenderParams> m_BatchesRenderParams;
//    private Camera m_Camera;
//    private NativeArray<Plane> m_Planes;
//    private Plane[] _TempPlanes;
    
//    // 组件类型句柄
//    private ComponentTypeHandle<LocalToWorld> m_TransformHandle;
//    private ComponentTypeHandle<MaterialMeshInfo> m_MeshInfoHandle;
//    private ComponentTypeHandle<RenderBounds> m_BoundsHandle;
//    private ComponentTypeHandle<GPUAnimationClipId> m_ClipIdHandle;

//    [BurstCompile]
//    private partial struct ECSNodeAspect : IAspect, IAspectCreate<WebGLGraphicsSystem.ECSNodeAspect>
//    {
//        public ECSNodeAspect(RefRO<GPUAnimationClipId> ecsnodeaspect_clipidRef, RefRO<MaterialMeshInfo> ecsnodeaspect_meshinfoRef, RefRO<RenderBounds> ecsnodeaspect_aabbRef, RefRO<LocalToWorld> ecsnodeaspect_transformRef)
//        {
//            ClipId = ecsnodeaspect_clipidRef;
//            MeshInfo = ecsnodeaspect_meshinfoRef;
//            AABB = ecsnodeaspect_aabbRef;
//            Transform = ecsnodeaspect_transformRef;
//        }

//        public void AddComponentRequirementsTo(ref UnsafeList<ComponentType> all)
//        {
//            all.Add(ComponentType.ReadOnly<LocalToWorld>());
//            all.Add(ComponentType.ReadOnly<MaterialMeshInfo>());
//            all.Add(ComponentType.ReadOnly<RenderBounds>());
//            all.Add(ComponentType.ReadOnly<GPUAnimationClipId>());
//        }

//        public ECSNodeAspect CreateAspect(Entity entity, ref SystemState system)
//        {
//            // 注意：这个方法通常不会被直接调用
//            // ECS Aspect 系统会自动处理 RefRO 的创建
//            // 这里提供一个基本的实现，但实际使用中会通过 ResolvedChunk 访问
//            throw new NotImplementedException("CreateAspect 应该通过 ResolvedChunk 访问，而不是直接调用");
//        }

//        public readonly RefRO<LocalToWorld> Transform;
//        public readonly RefRO<MaterialMeshInfo> MeshInfo;
//        public readonly RefRO<RenderBounds> AABB;
//        public readonly RefRO<GPUAnimationClipId> ClipId;

//        public struct ResolvedChunk
//        {
//            public WebGLGraphicsSystem.ECSNodeAspect this[int index]
//            {
//                get
//                {
//                    return new WebGLGraphicsSystem.ECSNodeAspect(
//                        new RefRO<GPUAnimationClipId>(ECSNodeAspect_ClipIdNaC, index),
//                        new RefRO<MaterialMeshInfo>(ECSNodeAspect_MeshInfoNaC, index),
//                        new RefRO<RenderBounds>(ECSNodeAspect_AABBNaC, index),
//                        new RefRO<LocalToWorld>(ECSNodeAspect_TransformNaC, index)
//                    );
//                }
//            }

//            public NativeArray<GPUAnimationClipId> ECSNodeAspect_ClipIdNaC;
//            public NativeArray<MaterialMeshInfo> ECSNodeAspect_MeshInfoNaC;
//            public NativeArray<RenderBounds> ECSNodeAspect_AABBNaC;
//            public NativeArray<LocalToWorld> ECSNodeAspect_TransformNaC;
//            public int Length;
//        }

//        public struct TypeHandle
//        {
//            public TypeHandle(ref SystemState state)
//            {
//                ECSNodeAspect_ClipIdCAc = default;
//                ECSNodeAspect_MeshInfoCAc = default;
//                ECSNodeAspect_AABBCAc = default;
//                ECSNodeAspect_TransformCAc = default;
//            }

//            public void Update(ref SystemState state)
//            {
//                ECSNodeAspect_ClipIdCAc.Update(ref state);
//                ECSNodeAspect_MeshInfoCAc.Update(ref state);
//                ECSNodeAspect_AABBCAc.Update(ref state);
//                ECSNodeAspect_TransformCAc.Update(ref state);
//            }

//            public WebGLGraphicsSystem.ECSNodeAspect.ResolvedChunk Resolve(ArchetypeChunk chunk)
//            {
//                return new WebGLGraphicsSystem.ECSNodeAspect.ResolvedChunk
//                {
//                    ECSNodeAspect_ClipIdNaC = chunk.GetNativeArray(ref ECSNodeAspect_ClipIdCAc),
//                    ECSNodeAspect_MeshInfoNaC = chunk.GetNativeArray(ref ECSNodeAspect_MeshInfoCAc),
//                    ECSNodeAspect_AABBNaC = chunk.GetNativeArray(ref ECSNodeAspect_AABBCAc),
//                    ECSNodeAspect_TransformNaC = chunk.GetNativeArray(ref ECSNodeAspect_TransformCAc),
//                    Length = chunk.Count
//                };
//            }

//            [ReadOnly]
//            private ComponentTypeHandle<GPUAnimationClipId> ECSNodeAspect_ClipIdCAc;
//            [ReadOnly]
//            private ComponentTypeHandle<MaterialMeshInfo> ECSNodeAspect_MeshInfoCAc;
//            [ReadOnly]
//            private ComponentTypeHandle<RenderBounds> ECSNodeAspect_AABBCAc;
//            [ReadOnly]
//            private ComponentTypeHandle<LocalToWorld> ECSNodeAspect_TransformCAc;
//        }
//    }

//    [BurstCompile]
//    private struct RenderDataJob : IJobChunk
//    {
//        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
//        {
//            // 直接获取组件数组
//            var transformArray = chunk.GetNativeArray(ref TransformHandle);
//            var meshInfoArray = chunk.GetNativeArray(ref MeshInfoHandle);
//            var boundsArray = chunk.GetNativeArray(ref BoundsHandle);
//            var clipIdArray = chunk.GetNativeArray(ref ClipIdHandle);

//            int entityCount = chunk.Count;

//            for (int i = 0; i < entityCount; i++)
//            {
//                // 检查视锥体剔除
//                if (Intersect(cullingPlanes, boundsArray[i]) == Unity.Rendering.FrustumPlanes.IntersectResult.Out)
//                    continue;

//                // 创建批处理键（材质ID + 网格ID）
//                var batchKey = new int2(meshInfoArray[i].MaterialID.GetHashCode(), meshInfoArray[i].MeshID.GetHashCode());

//                // 获取或创建批处理列表
//                if (!BatchesMatrices.TryGetValue(batchKey, out var matrices))
//                {
//                    matrices = new NativeList<Matrix4x4>(Allocator.Temp);
//                    BatchesMatrices.Add(batchKey, matrices);
//                }

//                if (!BatchesClipIds.TryGetValue(batchKey, out var clipIds))
//                {
//                    clipIds = new NativeList<float4>(Allocator.Temp);
//                    BatchesClipIds.Add(batchKey, clipIds);
//                }

//                // 添加变换矩阵和动画ID
//                matrices.Add((Matrix4x4)transformArray[i].Value);
//                clipIds.Add(clipIdArray[i].Value);
//            }
//        }

//        private Unity.Rendering.FrustumPlanes.IntersectResult Intersect(NativeArray<Plane> cullingPlanes, RenderBounds renderBounds)
//        {
//            // 完善的视锥体剔除实现 - 支持精确的AABB剔除和性能优化
//            var center = renderBounds.Value.Center;
//            var extents = renderBounds.Value.Extents;
            
//            // 预计算AABB的边界
//            var min = center - extents;
//            var max = center + extents;
            
//            // 快速边界检查 - 如果AABB太小，直接跳过
//            var volume = extents.x * extents.y * extents.z;
//            if (volume < 0.0001f)
//            {
//                return Unity.Rendering.FrustumPlanes.IntersectResult.In;
//            }
            
//            // 检查所有6个视锥体平面
//            for (int i = 0; i < 6; i++)
//            {
//                var plane = cullingPlanes[i];
//                var normal = new float3(plane.normal.x, plane.normal.y, plane.normal.z);
//                var distance = plane.distance;
                
//                // 计算AABB在平面法向量上的投影半径
//                var radius = math.dot(extents, math.abs(normal));
                
//                // 计算AABB中心到平面的距离
//                var centerDistance = math.dot(center, normal) + distance;
                
//                // 如果AABB完全在平面的负半空间，则被剔除
//                if (centerDistance + radius < 0)
//                {
//                    return Unity.Rendering.FrustumPlanes.IntersectResult.Out;
//                }
                
//                // 如果AABB与平面相交，标记为部分相交
//                // 这里我们简化处理，只区分完全在内部和完全在外部
//            }
            
//            // 所有平面都通过，对象在视锥体内
//            return Unity.Rendering.FrustumPlanes.IntersectResult.In;
//        }

//        [ReadOnly]
//        public NativeArray<Plane> cullingPlanes;

//        [NativeDisableContainerSafetyRestriction]
//        [WriteOnly]
//        public NativeHashMap<int2, NativeList<Matrix4x4>> BatchesMatrices;

//        [WriteOnly]
//        [NativeDisableContainerSafetyRestriction]
//        public NativeHashMap<int2, NativeList<float4>> BatchesClipIds;

//        // 组件类型句柄
//        [ReadOnly] public ComponentTypeHandle<LocalToWorld> TransformHandle;
//        [ReadOnly] public ComponentTypeHandle<MaterialMeshInfo> MeshInfoHandle;
//        [ReadOnly] public ComponentTypeHandle<RenderBounds> BoundsHandle;
//        [ReadOnly] public ComponentTypeHandle<GPUAnimationClipId> ClipIdHandle;
//    }

//}
