#if UNITY_2022_1_OR_NEWER
using GPUAnimation.Runtime;
using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
public class RendererResource
{
    [Header("此物体最大渲染个数:")]
    public int capacity = 10000;
    public Mesh mesh;
    public Material material;
   // public DrawBatchKey Key { get;  set; } = DrawBatchKey.Null;
    public DrawBatchKey Key { get; set; } = DrawBatchKey.Null;
    [Header("GPU动画事件: 由[GPU Animation Converter]工具生成")]
    public GPUAnimationEventData gpuAnimEventData;
    [Header("LOD距离(米), 最大支持4级: 由[GPU Animation LOD]工具生成LOD Mesh")]
    public Vector4 lodDistances;
    public int lodCount
    { 
        get
        {
            if (mesh == null)
            {
                return 0;  
            }

            return mesh.subMeshCount; 
        }
    }
    public RendererResource()
    {
    }


    public void RegisterResource(int resIndex, BatchMeshID meshId, BatchMaterialID matId, bool isTransparent)
    {
        Key = new DrawBatchKey
        {
             
            MeshID = meshId,
            MaterialID = matId,
            SubmeshIndex =(uint) resIndex,
            isTransparent = isTransparent,
            drawCommandFlags = isTransparent ? BatchDrawCommandFlags.HasSortingPosition : BatchDrawCommandFlags.None
        };
    }
    public ValueTuple<float4, int> GetLods(float distanceScale)
    {
        if (mesh != null)
        {
            int subMeshCount = mesh.subMeshCount;

            // Handle LOD distances for 2 or more submeshes
            if (subMeshCount >= 2)
            {
                // LOD 0
                float x = lodDistances.x <= 0 ? 25.0f : lodDistances.x;
                lodDistances.x = Mathf.Pow(x * distanceScale, 2);

                // LOD 1
                float y = lodDistances.y <= 0 ? 50.0f : lodDistances.y;
                lodDistances.y = Mathf.Pow(y * distanceScale, 2);
            }

            // Handle LOD 2
            if (subMeshCount >= 3)
            {
                float z = lodDistances.z <= 0 ? 100.0f : lodDistances.z;
                lodDistances.z = Mathf.Pow(z * distanceScale, 2);
            }

            // Handle LOD 3
            if (subMeshCount >= 4)
            {
                float w = lodDistances.w <= 0 ? 200.0f : lodDistances.w;
                lodDistances.w = Mathf.Pow(w * distanceScale, 2);
            }

            return (lodDistances, subMeshCount);
        }

        return (new float4(), 0);
    }

   


    public void RegisterResource(BatchMeshID meshId, BatchMaterialID matId)
    {
        Key = new DrawBatchKey
        {
            MeshID = meshId,
            MaterialID = matId,
            SubmeshIndex = 0
        };
    }
}
#endif