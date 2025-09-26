using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using GPUAnimation.Runtime;
using Unity.Burst.Intrinsics;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;



[Serializable]
public class ECSRender
{




    public bool HasGPUAnimationEvent;





    public int RenderMeshArrayIndex;



    public Mesh Mesh
    {
        get
        {
            if (LODRenders == null || LODRenders.Length == 0)
                return null;

            var firstLODRender = LODRenders[0];
            if (firstLODRender == null)
                throw new NullReferenceException("First LOD render is null");

            return firstLODRender.Mesh;
        }
    }

    public Material Material
    {
        get
        {
            if (LODRenders == null || LODRenders.Length == 0)
                return null;

            var firstLODRender = LODRenders[0];
            if (firstLODRender == null)
                throw new NullReferenceException("First LOD render is null");

            return firstLODRender.Material;
        }
    }





    public AABB AABB
    {
        get
        {
            Unity.Mathematics.AABB result = new Unity.Mathematics.AABB
            {
                Center = new float3(0, 0, 0),
                Extents = new float3(0, 0, 0)
            };

            if (LODRenders != null && LODRenders.Length > 0 && LODRenders[0] != null && LODRenders[0].Mesh != null)
            {
                UnityEngine.Bounds bounds = LODRenders[0].Mesh.bounds;

                float4x4 transformMatrix = float4x4.identity;

                // Set center
                transformMatrix.c0 = new float4(bounds.center.x, bounds.center.y, bounds.center.z, 0);
                result.Center = Unity.Transforms.TransformHelpers.Right(transformMatrix);

                // Set extents
                transformMatrix.c0 = new float4(bounds.extents.x, bounds.extents.y, bounds.extents.z, 0);
                result.Extents = Unity.Transforms.TransformHelpers.Right(transformMatrix);
            }

            return result;
        }
    }




    public RenderFilterSettings RenderFilterSettings
    {
        get
        {
            var defaultSettings = Unity.Entities.Graphics.RenderFilterSettings.Default;

            var result = new Unity.Entities.Graphics.RenderFilterSettings
            {
                Layer = defaultSettings.Layer,
                ShadowCastingMode = ShadowMode,
                ReceiveShadows = ReceiveShadows
            };

            return result;
        }
    }




    public Mesh[] LODMeshes
    {


        get
        {
            InitLODs();
            return this.m_LODMeshes;
        }
    }




    public Material[] LODMaterials
    {


        get
        {
            InitLODs();
            return this.m_LODMaterials;
        }
    }





    public float4 LODDistances0;




    public float4 LODDistances1;



    private void InitLODs()
    {
        if (LODRenders != null && LODRenders.Length > 0)
        {
        
            if (m_LODMeshes == null || m_LODMaterials == null)
            {
                int lodCount = LODRenders.Length;

              
                m_LODMeshes = new UnityEngine.Mesh[lodCount];
                m_LODMaterials = new UnityEngine.Material[lodCount];

         
                for (int i = 0; i < lodCount; i++)
                {
                    if (LODRenders == null || LODRenders[i] == null)
                        throw new NullReferenceException("LOD render is null");

                    m_LODMeshes[i] = LODRenders[i].Mesh;
                    m_LODMaterials[i] = LODRenders[i].Material;

                   
                    if (m_LODMaterials[i] != null && !(m_LODMaterials[i] is UnityEngine.Material))
                    {
                        throw new InvalidCastException("Object is not a Material");
                    }
                }

       
                Unity.Mathematics.float4 lodDistances0 = new Unity.Mathematics.float4(float.MaxValue);

          
                int lodIndex = 0;
                for (int i = 0; i < Mathf.Min(4, LODRenders.Length); i++)
                {
                    if (LODRenders == null || LODRenders[i] == null)
                        throw new NullReferenceException("LOD render is null");

                    lodDistances0[i] = LODRenders[i].Distance;
                    lodIndex++;
                }

                this.LODDistances0 = lodDistances0;

                Unity.Mathematics.float4 lodDistances1 = new Unity.Mathematics.float4(float.MaxValue);
                int distanceIndex = 4;

                while (distanceIndex < Mathf.Min(8, LODRenders.Length))
                {
                    if (LODRenders == null)
                        throw new NullReferenceException("LOD renders array is null");

                    lodDistances1[distanceIndex - 4] = LODRenders[distanceIndex].Distance;
                    distanceIndex++;
                }

         
                HasGPUAnimationEvent = false;

                if (GPUAnimationEvents != null)
                {
                    for (int i = 0; ; i++)
                    {
                        if (GPUAnimationEvents == null || GPUAnimationEvents.ClipEvents == null)
                            break;

                        if (i >= GPUAnimationEvents.ClipEvents.Count)
                            return;

                        var clipEvent = GPUAnimationEvents.ClipEvents[i];
                        if (clipEvent == null)
                            break;

                        var enumerator = ((GPUAnimation.Runtime.GPUAnimEvents)clipEvent).GetEnumerator();

                        try
                        {
                            while (enumerator.MoveNext())
                            {
                                if (enumerator.Current is KeyValuePair<int, string>)
                                {
                                    HasGPUAnimationEvent = true;
                                    break;
                                }
                            }
                        }
                        finally
                        {
                            if (enumerator is IDisposable disposable)
                                disposable.Dispose();
                        }

                        if (HasGPUAnimationEvent)
                            return;
                    }
                }
                else
                {
                    HasGPUAnimationEvent = false;
                }
            }
        }
    }




    public MaterialMeshInfo FromRenderMeshArrayIndices(int lodLv = 0)
    {
        int meshIndex = lodLv + RenderMeshArrayIndex;
        int materialIndex = lodLv + RenderMeshArrayIndex;
        return Unity.Rendering.MaterialMeshInfo.FromRenderMeshArrayIndices(
            meshIndex,      
            materialIndex,  
            0              
        );
    }




    public ECSRender()
    {
    }

 
    public GPUAnimationEventData GPUAnimationEvents;
 
    public ECSLODRender[] LODRenders;


 
    public ShadowCastingMode ShadowMode= ShadowCastingMode.On;

 
    public bool ReceiveShadows=true;



 
    private Mesh[] m_LODMeshes;


 
    private Material[] m_LODMaterials;



    [Serializable]
    public class ECSLODRender
    {



        public AABB AABB
        {


            get
            {
                var result = new Unity.Mathematics.AABB
                {
                    Center = new Unity.Mathematics.float3(0, 0, 0),
                    Extents = new Unity.Mathematics.float3(0, 0, 0)
                };

                if (Mesh != null)
                {
                    UnityEngine.Bounds bounds = Mesh.bounds;

                    var matrix = new Unity.Mathematics.float4x4();

                    matrix.c2.z = bounds.center.x;
                    matrix.c2.w = bounds.center.y;
                    matrix.c3.x = bounds.center.z;

                    matrix.c3.y = bounds.extents.x;
                    matrix.c3.z = bounds.extents.y;
                    matrix.c3.w = bounds.extents.z;

                    matrix.c0.x = bounds.center.x;
                    matrix.c0.y = bounds.center.y;
                    matrix.c0.z = bounds.center.z;

                    result.Center = Unity.Transforms.TransformHelpers.Right(matrix);

                    matrix.c0.x = bounds.extents.x;
                    matrix.c0.y = bounds.extents.y;
                    matrix.c0.z = bounds.extents.z;
 
                    result.Extents = Unity.Transforms.TransformHelpers.Right(matrix);
                }

                return result;
            }
        }




        public ECSLODRender()
        {
        }



 
        public Mesh Mesh;



  
        public Material Material;


 
        public float Distance;
    }
}
