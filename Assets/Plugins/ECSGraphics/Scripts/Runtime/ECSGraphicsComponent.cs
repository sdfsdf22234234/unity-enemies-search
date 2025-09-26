using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using GPUAnimation.Runtime;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;



public class ECSGraphicsComponent : MonoBehaviour
{




    public static ECSGraphicsComponent Instance;



    public NativeHashMap<int, Entity> Entities
    {


        get
        {
            return m_Entities;
        }
    }




    public int ECSRendersCount
    {


        get
        {
            return m_ECSRenders?.Length ?? 0;
        }
    }




    private void Awake()
    {

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        InitECSGraphics();

    }




    private void Start()
    {
        World defaultWorld = World.DefaultGameObjectInjectionWorld;
        if (defaultWorld == null)
        {
            throw new InvalidOperationException("Default world not found");
        }

        // Get the entity manager
        EntityManager entityManager = defaultWorld.EntityManager;

        // Create a query to find entities with specific components
        _renderDataQuery = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<GPUAnimationEventFramed>(),
            ComponentType.ReadOnly<GPUAnimationClipId>(),
            ComponentType.ReadOnly<ECSRenderData>()
        );
        if (m_AutoSyncRVO)
        {
            SystemHandle rvoSyncSystem = defaultWorld.GetOrCreateSystem<RVOSyncSystem>();
            var simulationGroup = defaultWorld.GetExistingSystemManaged<SimulationSystemGroup>();
            if (simulationGroup != null)
            {
                simulationGroup.AddSystemToUpdateList(rvoSyncSystem);
            }
            
        }



    }




    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        if (m_AutoSyncRVO)
        {
            World defaultWorld = World.DefaultGameObjectInjectionWorld;
            if (defaultWorld != null && defaultWorld.IsCreated)
            {
                SystemHandle rvoSyncSystem = defaultWorld.GetExistingSystem<RVOSyncSystem>();

                var simulationGroup = defaultWorld.GetExistingSystemManaged<SimulationSystemGroup>();
                if (simulationGroup != null)
                {
                    simulationGroup.RemoveSystemFromUpdateList(rvoSyncSystem);

                    defaultWorld.DestroySystem(rvoSyncSystem);
                }
                else
                {
                    throw new InvalidOperationException("Simulation system group not found");
                }
            }
        }

        // Dispose of native collections to prevent memory leaks
        m_Entities.Dispose();
        m_AnimClipsInfos.Dispose();
        m_AnimEventFrames.Dispose();
    }




    private void InitECSGraphics()
    {
        // 初始化实体哈希映射
        m_Entities = new NativeHashMap<int, Entity>(1024, Allocator.Persistent);

        if (m_ECSRenders == null || m_ECSRenders.Length == 0)
        {
            Debug.LogError("ECSRenders is null or empty");
            return;
        }

        // 获取默认世界和实体管理器
        World defaultWorld = World.DefaultGameObjectInjectionWorld;
        if (defaultWorld == null)
        {
            Debug.LogError("Default world not found");
            return;
        }

        EntityManager entityManager = defaultWorld.EntityManager;

        // 设置渲染过滤设置
        var renderFilterSettings = RenderFilterSettings.Default;
        renderFilterSettings.Layer = 0; // 重置层
        renderFilterSettings.ReceiveShadows = false;

        // 收集所有材质和网格
        var materials = new List<Material>();
        var meshes = new List<Mesh>();

        // 处理每个 ECSRender
        for (int i = 0; i < m_ECSRenders.Length; i++)
        {
            var render = m_ECSRenders[i];
            if (render == null) continue;

            //render.InitLODs();

            if (render.LODMaterials != null && render.LODMeshes != null)
            {
                render.RenderMeshArrayIndex = materials.Count;
                materials.AddRange(render.LODMaterials);
                meshes.AddRange(render.LODMeshes);
            }
        }

        // 创建渲染网格数组
        var renderMeshArray = new RenderMeshArray(
            materials.ToArray(),
            meshes.ToArray()
        );

        // 创建渲染网格描述
        var renderMeshDescription = new RenderMeshDescription
        {
            FilterSettings = renderFilterSettings,
            LightProbeUsage = m_LightProbeUsage
        };

        // 创建实体
        m_EntityTemp = entityManager.CreateEntity();

        // 设置材质网格信息
        var materialMeshInfo = MaterialMeshInfo.FromRenderMeshArrayIndices(
            0, // materialIndex
            0, // meshIndex
            0  // submeshIndex
        );

        // 添加渲染组件
        RenderMeshUtility.AddComponents(
            m_EntityTemp,
            entityManager,
            renderMeshDescription,
            renderMeshArray,
            materialMeshInfo
        );

        // 添加变换组件
        // 添加变换组件
        entityManager.AddComponentData(m_EntityTemp, new LocalTransform());

        // 添加材质颜色组件
       // entityManager.AddComponentData(m_EntityTemp, new MaterialBaseColor { Value = new float4(0.7f, 0.7f, 0.7f, 1.0f) });
       // entityManager.AddComponent(m_EntityTemp, ComponentType.ReadWrite<MaterialBaseColor>());

        entityManager.AddComponentData(m_EntityTemp, new GPUAnimationClipId());

        entityManager.AddComponentData(m_EntityTemp, new GPUAnimationUserdata());
  

        var componentTypes = entityManager.GetComponentTypes(m_EntityTemp);
      
        // 初始化 GPU 动画事件
        InitGPUAnimationEvents();
 
    }




    private void InitGPUAnimationEvents()
    {
        // 初始化动画剪辑信息哈希映射
        m_AnimClipsInfos = new NativeHashMap<int2, GPUAnimationClipInfo>(64, Allocator.Persistent);

        // 初始化动画事件帧哈希集
        m_AnimEventFrames = new NativeHashSet<int3>(32, Allocator.Persistent);

        // 遍历所有渲染器
        for (int i = 0; i < m_ECSRenders?.Length; i++)
        {
            ECSRender render = m_ECSRenders[i];
            if (render == null)
                continue;

            // 检查是否有GPU动画事件
            if (render.GPUAnimationEvents != null && render.HasGPUAnimationEvent)
            {
                // 遍历所有动画剪辑
                for (int j = 0; j < render.GPUAnimationEvents.ClipEvents?.Count; j++)
                {
                    var clipEvents = render.GPUAnimationEvents.ClipEvents[j];
                    if (clipEvents == null)
                        continue;

                    bool hasEvents = false;

                    // 遍历剪辑中的所有事件
                    foreach (var kvp in clipEvents)
                    {
                        int eventId = kvp.Key;
                        if (eventId > 0)
                        {
                            hasEvents = true;

                            // 添加动画事件帧
                            int3 eventKey = new int3(i, j, eventId);
                            m_AnimEventFrames.Add(eventKey);
                        }
                    }

                    // 如果有事件，则添加动画剪辑信息
                    if (hasEvents)
                    {
                        int2 clipKey = new int2(i, j);

                        // 获取动画剪辑信息
                        Material material = render.Material;
                        var clipInfo = GPUAnimationUtility.GetAnimationClipInfo(material, j);

                        // 解析剪辑信息
                        //int startFrame = (int)clipInfo.x;
                        //int frameCount = (int)clipInfo.y;
                        //float frameRate = clipInfo.z;
                        bool isLoop = clipInfo.w > 0.01f;

                        // 获取动画速度
                        float speed = material.GetFloat(GPUANIMATION_SPEED);

                        // 创建剪辑信息对象
                        GPUAnimationClipInfo animClipInfo = new GPUAnimationClipInfo
                        {  
                            StartFrame = (int)clipInfo.x,
                            EndFrame = (int)clipInfo.y,  // 结束帧 = 起始帧 + 帧数 - 1
                            AnimLength = clipInfo.z,     // 动画长度 = 帧数 / 帧率
                            AnimSpeed = speed,
                            IsLoop = isLoop
                        };

                        // 添加到映射
                        m_AnimClipsInfos.Add(clipKey, animClipInfo);
                    }
                }
            }
        }
    }




    public int Add(int renderId, float3 pos, quaternion rot, float scale = 1f, int parentId = -1)
    {
        if (renderId >= 0 && m_ECSRenders != null && renderId < m_ECSRenders.Length)
        {
            World defaultWorld = World.DefaultGameObjectInjectionWorld;
            if (defaultWorld == null)
            {
                throw new InvalidOperationException("Default world not found");
            }

            EntityManager entityManager = defaultWorld.EntityManager;

            ECSRender render = m_ECSRenders[renderId];
            if (render == null)
            {
                throw new InvalidOperationException("Render data not found");
            }
            Entity entity = entityManager.Instantiate(m_EntityTemp);

            // Set material and mesh information
            var materialMeshInfo = MaterialMeshInfo.FromRenderMeshArrayIndices(
                render.RenderMeshArrayIndex,
                render.RenderMeshArrayIndex,
                0);
            entityManager.SetComponentData(entity, materialMeshInfo);

           
            var bounds = render.AABB;
            var renderBounds = new RenderBounds();
            renderBounds.Value.Center = bounds.Center;
            renderBounds.Value.Extents = bounds.Extents;
            entityManager.SetComponentData(entity, renderBounds);

         
            var transform = LocalTransform.FromPositionRotationScale(pos, rot, scale);
            entityManager.SetComponentData(entity, transform);

            
            var filterSettings = RenderFilterSettings.Default;
            filterSettings.ReceiveShadows = render.ReceiveShadows;
            filterSettings.ShadowCastingMode = render.ShadowMode;
            entityManager.SetSharedComponent(entity, filterSettings);


          
            entityManager.AddComponentData(entity, new ECSRenderData
            {
                EntityIndex = entity.Index,
                RenderIndex = renderId
            });

            // Add GPU animation event component if needed
            if (render.HasGPUAnimationEvent)
            {
                entityManager.AddComponentData(entity, new GPUAnimationEventFramed { Value = 0 });
            }

     
            m_Entities.Add(entity.Index, entity);

            if (parentId != -1)
            {
                SetParent(entity.Index, parentId);
            }

            if (render.LODRenders != null && render.LODRenders.Length > 1)
            {
                entityManager.AddComponentData(entity, new MeshLODGroupComponent
                {
                    LODDistances0 = render.LODDistances0,
                    LODDistances1 = render.LODDistances1,
                    LocalReferencePoint = float3.zero,
                    ParentGroup = Entity.Null,
                    ParentMask = 0
                });

             
                entityManager.AddComponentData(entity, new MeshLODComponent
                {
                    Group = entity,
                    LODMask = 1
                });

                uint4x4 lodEntities = new uint4x4();
                int lodCount = render.LODRenders.Length;

                for (int i = 1; i < lodCount; i++)
                {
                    Entity lodEntity = entityManager.Instantiate(m_EntityTemp);
                    var lodMaterialMeshInfo = MaterialMeshInfo.FromRenderMeshArrayIndices(
                        i + render.RenderMeshArrayIndex,
                        i + render.RenderMeshArrayIndex,
                        0);
                    entityManager.SetComponentData(lodEntity, lodMaterialMeshInfo);
                    var reb = new RenderBounds();
                    reb.Value.Center = bounds.Center;
                    reb.Value.Extents = bounds.Extents;

                    entityManager.SetComponentData(lodEntity, reb);
                    var lodTransform = LocalTransform.FromPositionRotationScale(
                        float3.zero,
                        quaternion.identity,
                        1.0f);
                    entityManager.SetComponentData(lodEntity, lodTransform);

                    entityManager.SetSharedComponent(lodEntity, filterSettings);
                    entityManager.AddComponentData(lodEntity, new MeshLODComponent
                    {
                        Group = entity,
                        LODMask = 1 << i
                    });

                    lodEntities[i - 1 / 4][i - 1 % 4] = (uint)lodEntity.Index;
                    m_Entities.Add(lodEntity.Index, lodEntity);
                    SetParent(lodEntity.Index, entity.Index);
                }


                var lODsEntities = new LODsEntities();
                lODsEntities.LodEntitiesIds.c0 = (int4)lodEntities.c0;
                lODsEntities.LodEntitiesIds.c1 = (int4)lodEntities.c1;
                lODsEntities.LodCount = lodCount - 1;
                entityManager.AddComponentData(entity, lODsEntities);
            }

            return entity.Index;
        }
        else
        {
         
            Debug.LogError($"Invalid renderId: {renderId}");
            return -1;
        }
    }




    public void SetParent(int entityId, int entityParentId)
    {
        if (m_Entities.TryGetValue(entityId, out Entity childEntity) &&
       m_Entities.TryGetValue(entityParentId, out Entity parentEntity))
        {
            SetParent(childEntity, parentEntity);
        }
    }




    public void SetParent(Entity childEntity, Entity parentEntity)
    {
        World defaultWorld = World.DefaultGameObjectInjectionWorld;
        if (defaultWorld == null)
        {
            throw new InvalidOperationException("Default world not found");
        } 
        EntityManager entityManager = defaultWorld.EntityManager;
 
        if (entityManager.HasComponent<Parent>(childEntity))
        {
      
            entityManager.SetComponentData(childEntity, new Parent { Value = parentEntity });
        }
        else
        {
           
            entityManager.AddComponentData(childEntity, new Parent { Value = parentEntity });
        }
    }




    public void SetPositionAndRotation(int entityId, float3 pos, UnityEngine.Quaternion rot)
    {
        if (m_Entities.TryGetValue(entityId, out Entity entity))
        {
            World defaultWorld = World.DefaultGameObjectInjectionWorld;
            if (defaultWorld == null)
            {
                throw new InvalidOperationException("Default world not found");
            }
            EntityManager entityManager = defaultWorld.EntityManager;
            quaternion rotation = rot;
            LocalTransform transform = LocalTransform.FromPositionRotation(pos, rotation);
            entityManager.SetComponentData(entity, transform);
        }
    }




    public void SetPosition(int entityId, float3 pos)
    {
        if (m_Entities.TryGetValue(entityId, out Entity entity))
        {
            World defaultWorld = World.DefaultGameObjectInjectionWorld;
            if (defaultWorld == null)
            {
                throw new InvalidOperationException("Default world not found");
            }
            EntityManager entityManager = defaultWorld.EntityManager;
            LocalTransform transform = LocalTransform.FromPosition(pos);
            entityManager.SetComponentData(entity, transform);
        }
    }




    public LocalTransform GetTransform(int entityId)
    {
        if (m_Entities.TryGetValue(entityId, out Entity entity))
        {
          
            return GetTransform(entity);
        }
        else
        {
          
            return LocalTransform.Identity;
        }
    }




    public LocalTransform GetTransform(Entity entity)
    {
        World defaultWorld = World.DefaultGameObjectInjectionWorld;
        if (defaultWorld == null)
        {
            throw new InvalidOperationException("Default world not found");
        }

        EntityManager entityManager = defaultWorld.EntityManager;
        return entityManager.GetComponentData<LocalTransform>(entity);
    }




    public float3 GetPosition(int entityId)
    {
          // 先获取实体的变换组件
        LocalTransform transform = GetTransform(entityId);
        
        // 然后返回变换组件中的位置属性
        return transform.Position;
    }




    public float3 GetPosition(Entity entity)
    {
        // 先获取实体的变换组件
        LocalTransform transform = GetTransform(entity);

        // 然后返回变换组件中的位置属性
        return transform.Position;
    }




    public quaternion GetRotation(int entityId)
    {
        return GetTransform(entityId).Rotation;
     
     
    }




    public quaternion GetRotation(Entity entity)
    {
        return GetTransform(entity).Rotation;
    }




    public float GetScale(int entityId,int scale)
    {
        // 先获取实体的变换组件
        LocalTransform transform = GetTransform(entityId);

        // 然后返回变换组件中的位置属性
        return transform.Scale;
    }




    public float GetScale(Entity entity)
    {
        // 先获取实体的变换组件
        LocalTransform transform = GetTransform(entity);

        // 然后返回变换组件中的位置属性
        return transform.Scale;
    }




    public void SetRotation(int entityId, quaternion rot)
    {
        if (m_Entities.TryGetValue(entityId, out Entity entity))
        {
            World defaultWorld = World.DefaultGameObjectInjectionWorld;
            if (defaultWorld == null)
            {
                throw new InvalidOperationException("Default world not found");
            }

            EntityManager entityManager = defaultWorld.EntityManager;
            LocalTransform currentTransform = entityManager.GetComponentData<LocalTransform>(entity);
            LocalTransform newTransform = new LocalTransform
            {
                Position = currentTransform.Position,
                Rotation = rot,
                Scale = currentTransform.Scale
            };

            // 设置新的变换数据
            entityManager.SetComponentData(entity, newTransform);
          //  entityManager.SetComponentData(entity, transform);
        }
    }




    public void SetScale(int entityId, float scale)
    {
        if (m_Entities.TryGetValue(entityId, out Entity entity))
        {
            World defaultWorld = World.DefaultGameObjectInjectionWorld;
            if (defaultWorld == null)
            {
                throw new InvalidOperationException("Default world not found");
            }
            EntityManager entityManager = defaultWorld.EntityManager;

            LocalTransform currentTransform = entityManager.GetComponentData<LocalTransform>(entity);
            LocalTransform newTransform = new LocalTransform
            {
                Position = currentTransform.Position,
                Rotation = currentTransform.Rotation,
                Scale = scale
            };


           // LocalTransform transform = LocalTransform.FromScale(scale);
            entityManager.SetComponentData(entity, newTransform);
        }
    }




    public void SetMainColor(int entityId, float4 color)
    {
        if (m_Entities.TryGetValue(entityId, out Entity entity))
        {
            World defaultWorld = World.DefaultGameObjectInjectionWorld;
            if (defaultWorld == null)
            {
                throw new InvalidOperationException("Default world not found");
            }
            EntityManager entityManager = defaultWorld.EntityManager;
            if (entityManager.HasComponent<MaterialBaseColor>(entity))
            {
                entityManager.SetComponentData(entity, new MaterialBaseColor { Value = color });
            }
            else
            {
                entityManager.AddComponentData(entity, new MaterialBaseColor { Value = color });
            }
        }
    }




    public void SetMainColor(Entity entity, float4 color)
    {
        World defaultWorld = World.DefaultGameObjectInjectionWorld;
        if (defaultWorld == null)
        {
            throw new InvalidOperationException("Default world not found");
        }

        EntityManager entityManager = defaultWorld.EntityManager;

        if (entityManager.HasComponent<MaterialBaseColor>(entity))
        {
            entityManager.SetComponentData(entity, new MaterialBaseColor { Value = color });
        }
        else
        {
            entityManager.AddComponentData(entity, new MaterialBaseColor { Value = color });
        }
    }




    public void Remove(int entityId)
    {
        if (m_Entities.TryGetValue(entityId, out Entity entity))
        {
            Remove(entity);
        }
    }




    public void Remove(Entity entity)
    {
        World defaultWorld = World.DefaultGameObjectInjectionWorld;
        if (defaultWorld == null)
        {
            throw new InvalidOperationException("Default world not found");
        }
        EntityManager entityManager = defaultWorld.EntityManager;

        if (entityManager.HasBuffer<Child>(entity))
        {
            DynamicBuffer<Child> childrenBuffer = entityManager.GetBuffer<Child>(entity);
            var children = childrenBuffer.ToNativeArray(Allocator.Temp);
            for (int i = children.Length - 1; i >= 0; i--)
            {
                Entity childEntity = children[i].Value;
                if (childEntity != Entity.Null)
                {
                    Remove(childEntity);
                }
            }
        }

        entityManager.DestroyEntity(entity);
        m_Entities.Remove(entity.Index);
    }




    public void RemoveAll()
    {
        if (!m_Entities.IsEmpty)
        {
            World defaultWorld = World.DefaultGameObjectInjectionWorld;
            if (defaultWorld == null)
            {
                throw new InvalidOperationException("Default world not found");
            }

            EntityManager entityManager = defaultWorld.EntityManager;
            var allEntities = m_Entities.GetValueArray(Allocator.Temp);
            entityManager.DestroyEntity(allEntities);
            m_Entities.Clear();
        }
    }




    public float4 GetGPUAnimationClipInfo(Entity entity, int clipIndex)
    {
        Material entityMaterial = GetEntityMaterial(entity);
         
        var clipInfo = GPUAnimation.Runtime.GPUAnimationUtility.GetAnimationClipInfo(entityMaterial, clipIndex);
        return new float4(clipInfo.x, clipInfo.y, clipInfo.z, clipInfo.w);
    }




    public float4 GetGPUAnimationClipInfo(int entityId, int clipIndex)
    {
        Entity entity = m_Entities[entityId];

        Material entityMaterial = GetEntityMaterial(entity);
        var clipInfo = GPUAnimation.Runtime.GPUAnimationUtility.GetAnimationClipInfo(entityMaterial, clipIndex);

        return new float4(clipInfo.x, clipInfo.y, clipInfo.z, clipInfo.w);
    }




    public GPUBoneData GetGPUAnimationBone(int entityId, int boneId)
    {
        Unity.Entities.Entity entity = m_Entities[entityId];
        GPUAnimation.Runtime.GPUBoneData boneData = GetGPUAnimationBone(entity, boneId);

        return new GPUAnimation.Runtime.GPUBoneData(boneData.Position, boneData.Rotation, boneData.Scale, boneData.CurrentFrame);
      
    }




    private GPUBoneData GetGPUAnimationBone(Entity entity, int boneId)
    {
        GPUBoneData result = new GPUBoneData(float3.zero, quaternion.identity, float3.zero, 0);
      

 
        Material entityMaterial = GetEntityMaterial(entity);

      
        GPUAnimationClipId clipData = GetComponentData<GPUAnimationClipId>(entity);

        // 保存之前的动画索引 m_ClipId z:上一个索引,w，上一个开始时间,x 当前索引,y,当前播放时间
        GPUBoneData boneData = GPUAnimation.Runtime.GPUAnimationUtility.GetAttachBoneTransform(
            entityMaterial,
            clipData.Value.x,
            clipData.Value.y,
            boneId,
            1.0f);

        result = new GPUBoneData(boneData.Position, boneData.Rotation, boneData.Scale, boneData.CurrentFrame);
      

        // Get the entity's transform
        LocalTransform entityTransform = GetTransform(entity);

        
        var Position = entityTransform.TransformPoint(result.Position);

        // Transform the bone rotation from local space to world space
       var Rotation = entityTransform.TransformRotation(result.Rotation);

        // Transform the bone scale by the entity's scale
        float entityScale = entityTransform.Scale;
        var Scale = new float3(
            result.Scale.x * entityScale,
            result.Scale.y * entityScale,
            result.Scale.z * entityScale);


        result = new GPUBoneData(Position, Rotation, Scale, 0);


        return result;
    }




    public int GetGPUAnimationClipsCount(int entityId)
    {
        Unity.Entities.Entity entity = m_Entities[entityId];

        ECSRenderData renderData = GetComponentData<ECSRenderData>(entity);
        int renderIndex = renderData.RenderIndex;

        if (m_ECSRenders == null)
            throw new NullReferenceException();

        if (renderIndex >= m_ECSRenders.Length)
            throw new IndexOutOfRangeException();

        ECSRender render = m_ECSRenders[renderIndex];
        if (render == null)
            throw new NullReferenceException();

        if (render.GPUAnimationEvents != null)
        {
            return render.GPUAnimationEvents.ClipEvents.Count;
        }

        return 0;
    }




    public int GetGPUAnimationClipsCount(Entity entity)
    {
        ECSRenderData renderData = GetComponentData<ECSRenderData>(entity);
        int renderIndex = renderData.RenderIndex;

        if (m_ECSRenders == null)
            throw new NullReferenceException();

        if (renderIndex >= m_ECSRenders.Length)
            throw new IndexOutOfRangeException();

        ECSRender render = m_ECSRenders[renderIndex];
        if (render == null)
            throw new NullReferenceException();

        if (render.GPUAnimationEvents != null)
        {
            return render.GPUAnimationEvents.ClipEvents.Count;
        }

        return 0;
    }




    public void SetAttachmentBoneIndex(int attachedEntityId, int attachBoneId)
    {
        Entity attachedEntity = m_Entities[attachedEntityId];

        World defaultWorld = World.DefaultGameObjectInjectionWorld;
        if (defaultWorld == null)
        {
            throw new InvalidOperationException("Default world not found");
        }

        EntityManager entityManager = defaultWorld.EntityManager;
        entityManager.SetComponentData(attachedEntity, new GPUAnimationAttachBoneIndex
        {
            Value = attachBoneId
        });
    }




    public void SetAttachmentBoneIndex(Entity attachedEntity, int attachBoneId)
    {
        World defaultWorld = World.DefaultGameObjectInjectionWorld;
        if (defaultWorld == null)
        {
            throw new InvalidOperationException("Default world not found");
        }

        EntityManager entityManager = defaultWorld.EntityManager;
        entityManager.SetComponentData(attachedEntity, new GPUAnimationAttachBoneIndex
        {
            Value = attachBoneId
        });
    }




    public int AddAttachment(int renderId, int parentEntityId, int attachBoneId, float3 localpos, quaternion localrot, float scale = 1f)
    {
        if (renderId < 0 || m_ECSRenders == null || renderId >= m_ECSRenders.Length)
        {
            Debug.LogError($"Invalid renderId: {renderId}");
            return -1;
        }
        if (!m_Entities.TryGetValue(parentEntityId, out Entity parentEntity))
        {
            return -1;
        }

        World defaultWorld = World.DefaultGameObjectInjectionWorld;
        if (defaultWorld == null)
        {
            throw new InvalidOperationException("Default world not found");
        }

        EntityManager entityManager = defaultWorld.EntityManager;

        ECSRender render = m_ECSRenders[renderId];
        Entity entity = entityManager.Instantiate(m_EntityTemp);
        var materialMeshInfo = MaterialMeshInfo.FromRenderMeshArrayIndices(
            render.RenderMeshArrayIndex,
            render.RenderMeshArrayIndex,
            0);
        entityManager.SetComponentData(entity, materialMeshInfo);

        // Set render bounds
        var bounds = render.AABB;
        var renderBounds = new RenderBounds();
        renderBounds.Value.Center = bounds.Center;
        renderBounds.Value.Extents = bounds.Extents;
        entityManager.SetComponentData(entity, renderBounds);

  
        var filterSettings = RenderFilterSettings.Default;
        filterSettings.ReceiveShadows = render.ReceiveShadows;
        filterSettings.ShadowCastingMode = render.ShadowMode;
        entityManager.SetSharedComponent(entity, filterSettings);

 
        entityManager.AddComponentData(entity, new ECSRenderData
        {
            EntityIndex = entity.Index,
            RenderIndex = renderId
        });

 
        SetParent(entity, parentEntity);
 
        entityManager.AddComponentData(entity, new GPUAnimationAttachBoneIndex { Value = attachBoneId });
 
        var transform = LocalTransform.FromPositionRotationScale(localpos, localrot, scale);
        entityManager.SetComponentData(entity, transform);
 
        m_Entities.Add(entity.Index, entity);
 
        if (entityManager.HasComponent<GPUAnimationAttachments>(parentEntity))
        {
      
            var attachmentData = entityManager.GetComponentData<GPUAnimationAttachments>(parentEntity);
         
 
            entityManager.SetComponentData(parentEntity, attachmentData);
        }
        else
        {
        
            entityManager.AddComponentData(parentEntity, new GPUAnimationAttachments
            {
                AttachedEntitiesIds = new int4x2 { c0 = new int4(entity.Index) },
                AttachedCount = 1
            });
        }

 
        if (render.LODRenders != null && render.LODRenders.Length > 1)
        {
          
          
            entityManager.AddComponentData(entity, new MeshLODGroupComponent
            {
                LODDistances0 = render.LODDistances0,
                LODDistances1 = render.LODDistances1,
                LocalReferencePoint = float3.zero,
                ParentGroup = Entity.Null,
                ParentMask = 0
            });

      
            entityManager.AddComponentData(entity, new MeshLODComponent
            {
                Group = entity,
                LODMask = 1
            });

     
            var lodEntities = new uint4x4();
            int lodCount = render.LODRenders.Length;
            for (int i = 1; i < lodCount; i++)
            {
                Entity lodEntity = entityManager.Instantiate(m_EntityTemp);
                // 设置此LOD的材质和网格信息
                var lodMaterialMeshInfo = MaterialMeshInfo.FromRenderMeshArrayIndices(
                    i + render.RenderMeshArrayIndex,
                    i + render.RenderMeshArrayIndex,
                    0);
                entityManager.SetComponentData(lodEntity, lodMaterialMeshInfo);
                // 设置边界
                var lodRenderBounds = new RenderBounds();
                lodRenderBounds.Value.Center = bounds.Center;
                lodRenderBounds.Value.Extents = bounds.Extents;
                entityManager.SetComponentData(lodEntity, lodRenderBounds);
                // 设置初始变换
                var lodTransform = LocalTransform.FromPositionRotationScale(
                    float3.zero,
                    quaternion.identity,
                    1.0f);
                entityManager.SetComponentData(lodEntity, lodTransform);

                // 设置渲染过滤器设置
                entityManager.SetSharedComponent(lodEntity, filterSettings);

                // 添加MeshLODComponent到LOD实体
                entityManager.AddComponentData(lodEntity, new MeshLODComponent
                {
                    Group = entity,
                    LODMask = 1 << i
                });

                // 存储LOD实体索引
                int arrayIndex = (i - 1) / 4;
                int elementIndex = (i - 1) % 4;
                uint4 uintArray = lodEntities[arrayIndex];
                uintArray[elementIndex] = (uint)lodEntity.Index;
                lodEntities[arrayIndex] = uintArray;

                // 将LOD实体添加到跟踪字典
                m_Entities.Add(lodEntity.Index, lodEntity);

                // 设置主实体为此LOD实体的父实体
                SetParent(lodEntity, entity);

            }
            entityManager.AddComponentData(entity, new LODsEntities
            {
                LodEntitiesIds = new int4x2
                {
                    c0 = (int4)lodEntities.c0,
                    c1 = (int4)lodEntities.c1
                },
                LodCount = lodCount - 1
            });
          

        }

        return entity.Index;
    }




    public void RemoveAttachment(int entityId, int attachedEntityId)
    {
        if (m_Entities.TryGetValue(entityId, out Entity entity) &&
       m_Entities.TryGetValue(attachedEntityId, out Entity attachedEntity))
        {
           
            RemoveAttachment(entity, attachedEntity);
        }
    }




    public void RemoveAttachment(Entity entity, Entity attachedEntity)
    {
        World defaultWorld = World.DefaultGameObjectInjectionWorld;
        if (defaultWorld == null)
        {
            throw new InvalidOperationException("Default world not found");
        }

        // 获取实体管理器
        EntityManager entityManager = defaultWorld.EntityManager;

        // 检查实体是否有GPU动画附件
        if (entityManager.HasComponent<GPUAnimationAttachments>(entity))
        {
            // 获取附件数据
            GPUAnimationAttachments attachments = entityManager.GetComponentData<GPUAnimationAttachments>(entity);

            // 在附件列表中查找被附加的实体
            int foundIndex = -1;
            for (int i = 0; i < attachments.AttachedCount; i++)
            {
                // 计算在int4x2矩阵中的位置
                // 前4个元素存在第一行，后4个元素存在第二行
                int rowIndex = i / 4;
                int colIndex = i % 4;

                // 获取该位置的实体ID
                int entityId = 0;
                if (rowIndex == 0)
                    entityId = attachments.AttachedEntitiesIds[0][colIndex];
                else
                    entityId = attachments.AttachedEntitiesIds[1][colIndex];

                // 如果这是我们要查找的附加实体
                if (entityId == attachedEntity.Index)
                {
                    foundIndex = i;
                    break;
                }
            }

            // 如果找到了附件
            if (foundIndex >= 0)
            {
                // 创建更新后的附件数据
                GPUAnimationAttachments updatedAttachments = attachments;

                // 将找到位置之后的所有实体向前移动一个位置
                for (int i = foundIndex; i < updatedAttachments.AttachedCount - 1; i++)
                {
                    int currentRowIndex = i / 4;
                    int currentColIndex = i % 4;
                    int nextRowIndex = (i + 1) / 4;
                    int nextColIndex = (i + 1) % 4;

                    // 获取下一个实体ID
                    int nextEntityId = 0;
                    if (nextRowIndex == 0)
                        nextEntityId = updatedAttachments.AttachedEntitiesIds[0][nextColIndex];
                    else
                        nextEntityId = updatedAttachments.AttachedEntitiesIds[1][nextColIndex];

                    // 设置到当前位置
                    if (currentRowIndex == 0)
                        updatedAttachments.AttachedEntitiesIds[0][currentColIndex] = nextEntityId;
                    else
                        updatedAttachments.AttachedEntitiesIds[1][currentColIndex] = nextEntityId;
                }

                // 减少附件计数
                updatedAttachments.AttachedCount = Math.Max(0, Math.Min(8, updatedAttachments.AttachedCount - 1));

                // 更新组件数据
                entityManager.SetComponentData(entity, updatedAttachments);

                // 完全移除附加的实体
                Remove(attachedEntity);
            }
        }
    }




    public void PlayGPUAnimation(int entityId, int animClipIndex, float animCrossFadeDuration)
    {
        if (m_Entities.TryGetValue(entityId, out Entity entity))
        {
            PlayGPUAnimation(entity, animClipIndex, animCrossFadeDuration);
        }
    }




    public void PlayGPUAnimation(Entity entity, int animClipIndex, float animCrossFadeDuration)
    {
        World defaultWorld = World.DefaultGameObjectInjectionWorld;
        if (defaultWorld == null)
        {
            throw new InvalidOperationException("Default world not found");
        }
        EntityManager entityManager = defaultWorld.EntityManager;
        GPUAnimationClipId clipData = entityManager.GetComponentData<GPUAnimationClipId>(entity);
        // 保存之前的动画索引 m_ClipId z:上一个索引,w，上一个开始时间,x 当前索引,y,当前播放时间
        clipData.Value.x = animClipIndex;
        clipData.Value.y = UnityEngine.Time.time + animCrossFadeDuration;

        
        entityManager.SetComponentData(entity, clipData);

        // If the entity has LOD entities, update those as well
        if (entityManager.HasComponent<LODsEntities>(entity))
        {
            LODsEntities lodEntities = entityManager.GetComponentData<LODsEntities>(entity);

            // Update animation for each LOD entity
            for (int i = 0; i < lodEntities.LodCount; i++)
            {

                // 计算LOD实体在int4x2数组中的位置
                int rowIndex = i / 4;
                int colIndex = i % 4;

                // 获取LOD实体ID
                int lodEntityId = 0;
                if (rowIndex == 0)
                    lodEntityId = lodEntities.LodEntitiesIds.c0[colIndex];
                else
                    lodEntityId = lodEntities.LodEntitiesIds.c1[colIndex];

                // 从实体字典中查找LOD实体
                if (m_Entities.TryGetValue(lodEntityId, out Entity lodEntity))
                {
                    // 将相同的动画数据应用到LOD实体
                    entityManager.SetComponentData(lodEntity, clipData);
                }
            }
        }

        // 处理附件实体 - 如果实体有附件组件
        if (entityManager.HasComponent<GPUAnimationAttachments>(entity))
        {
            // 获取附件数据
            GPUAnimationAttachments attachments = entityManager.GetComponentData<GPUAnimationAttachments>(entity);

            // 遍历所有附件实体并更新它们的动画
            for (int i = 0; i < attachments.AttachedCount; i++)
            {
                // 计算附件实体在int4x2数组中的位置
                int rowIndex = i / 4;
                int colIndex = i % 4;

                // 获取附件实体ID
                int attachedEntityId = 0;
                if (rowIndex == 0)
                    attachedEntityId = attachments.AttachedEntitiesIds.c0[colIndex];
                else
                    attachedEntityId = attachments.AttachedEntitiesIds.c1[colIndex];

                // 从实体字典中查找附件实体
                if (m_Entities.TryGetValue(attachedEntityId, out Entity attachedEntity))
                {
                    // 将相同的动画数据应用到附件实体
                    entityManager.SetComponentData(attachedEntity, clipData);
                }
            }
        }

        // 如果实体有动画事件帧组件，重置它
        if (entityManager.HasComponent<GPUAnimationEventFramed>(entity))
        {
            entityManager.SetComponentData(entity, new GPUAnimationEventFramed { Value = 0 });
        }
    }




    public void PlayGPUAnimation(int entityId, int animClipIndex)
    {
        if (m_Entities.TryGetValue(entityId, out Entity entity))
        {
            Material entityMaterial = GetEntityMaterial(entity);

            if (entityMaterial == null)
            {
                throw new InvalidOperationException("Entity material not found");
            }
            float fadeDuration = 0.0f;
            if (entityMaterial.HasFloat(GPUANIMATION_FADE))
            {
                fadeDuration = entityMaterial.GetFloat(GPUANIMATION_FADE);
            }

            PlayGPUAnimation(entity, animClipIndex, fadeDuration);
        }
    }



    public void SetComponentData<T>(int entityId, T value) where T : unmanaged, IComponentData
    {
        if (m_Entities.TryGetValue(entityId, out Entity entity))
        {
          
            World defaultWorld = World.DefaultGameObjectInjectionWorld;
             
            EntityManager entityManager = defaultWorld.EntityManager;
             
            entityManager.SetComponentData(entity, value);
        }
    }



    public void SetComponentData<T>(Entity entity, T value) where T : unmanaged, IComponentData
    {
        World defaultWorld = World.DefaultGameObjectInjectionWorld;
        if (defaultWorld == null)
        {
            throw new InvalidOperationException("Default world not found");
        }
 
        EntityManager entityManager = defaultWorld.EntityManager;
 
        entityManager.SetComponentData(entity, value);
    }



    public T GetComponentData<T>(int entityId) where T : unmanaged, IComponentData
    {
        Entity entity = m_Entities[entityId];

        World defaultWorld = World.DefaultGameObjectInjectionWorld;
        if (defaultWorld == null)
        {
            throw new InvalidOperationException("Default world not found");
        }

        EntityManager entityManager = defaultWorld.EntityManager;


        return entityManager.GetComponentData<T>(entity);

    }



    public T GetComponentData<T>(Entity entity) where T : unmanaged,  IComponentData
    {
        // 获取默认世界
        World defaultWorld = World.DefaultGameObjectInjectionWorld;
        if (defaultWorld == null)
        {
            throw new InvalidOperationException("Default world not found");
        }

        // 获取实体管理器
        EntityManager entityManager = defaultWorld.EntityManager;

        // 获取并返回组件数据
        return entityManager.GetComponentData<T>(entity);
    }




    private Material GetEntityMaterial(Entity entity)
    {
        World defaultWorld = World.DefaultGameObjectInjectionWorld;
        if (defaultWorld == null)
        {
            throw new InvalidOperationException("Default world not found");
        }
        EntityManager entityManager = defaultWorld.EntityManager;
        MaterialMeshInfo materialMeshInfo = entityManager.GetComponentData<MaterialMeshInfo>(entity);
        RenderMeshArray renderMeshArray = entityManager.GetSharedComponentManaged<RenderMeshArray>(entity);
        return renderMeshArray.GetMaterial(materialMeshInfo);
    }




    public bool GPUAnimationEventsUpdate(out NativeQueue<GPUAnimationEventInfo> triggerResults)
    {
        triggerResults = new NativeQueue<GPUAnimationEventInfo>();
        if (m_Entities.IsCreated == false || m_Entities.Count == 0)
        {
            // 不直接访问m_Queue
            if (triggerResults.IsCreated)
            {
                triggerResults.Clear();
            }
            return false;
        }

        // 获取默认世界和实体管理器
        World defaultWorld = World.DefaultGameObjectInjectionWorld;
        if (defaultWorld == null)
        {
            Debug.LogError("Default world not found");
            return false;
        }

        EntityManager entityManager = defaultWorld.EntityManager;

        // 创建新的事件队列 - 不再直接赋值m_Queue
        var localEventQueue = new NativeQueue<GPUAnimationEventInfo>(Allocator.TempJob);

        // 如果需要的话，可以使用CopyFrom方法来转移队列内容
        // 但在这个例子中，似乎是要用localEventQueue替换triggerResults的内容
        if (triggerResults.IsCreated)
        {
            triggerResults.Dispose();
        }

        // 获取组件类型句柄
        var eventFrameHandle = entityManager.GetComponentTypeHandle<GPUAnimationEventFramed>(false);
        var clipIdHandle = entityManager.GetComponentTypeHandle<GPUAnimationClipId>(true);
        var renderDataHandle = entityManager.GetComponentTypeHandle<ECSRenderData>(true);

        // 创建并配置动画事件作业
        var eventJob = new GPUAnimationEventJob
        {
            CurrentTime = Time.time,
            EventHandle = eventFrameHandle,
            ClipIdHandle = clipIdHandle,
            RenderDataHandle = renderDataHandle,
            ClipInfos = m_AnimClipsInfos,
            TriggerFrames = m_AnimEventFrames,
            Results = localEventQueue.AsParallelWriter()
        };

        // 调度作业并等待完成
        JobHandle jobHandle = eventJob.Schedule(_renderDataQuery, default);
        jobHandle.Complete();

        // 检查是否有触发的事件
        bool hasEvents = localEventQueue.Count > 0;

        // 如果有事件，则将localEventQueue中的所有事件复制到triggerResults
        if (hasEvents)
        {
            // 创建新的triggerResults队列
            triggerResults = new NativeQueue<GPUAnimationEventInfo>(Allocator.Persistent);

            // 复制所有事件
            while (localEventQueue.TryDequeue(out var eventInfo))
            {
                triggerResults.Enqueue(eventInfo);
            }
        }

        // 释放本地临时队列
        localEventQueue.Dispose();

        return hasEvents;
    }




    public ECSGraphicsComponent()
    {
      
    }




    [SerializeField]
    private LightProbeUsage m_LightProbeUsage;




    [SerializeField]
    private bool m_AutoSyncRVO;




    [SerializeField]
    private ECSRender[] m_ECSRenders;




    private Entity m_EntityTemp;




    private NativeHashMap<int, Entity> m_Entities;




    private static readonly int GPUANIMATION_FADE = Shader.PropertyToID("_AnimTransDuration");




    private static readonly int GPUANIMATION_SPEED = Shader.PropertyToID("_AnimSpeed");


 

    private NativeHashMap<int2, GPUAnimationClipInfo> m_AnimClipsInfos;




    private NativeHashSet<int3> m_AnimEventFrames;




    private EntityQuery _renderDataQuery;
}
