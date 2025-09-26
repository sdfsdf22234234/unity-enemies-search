using System;
using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

 
[MaterialProperty("_ClipId", -1)]
[Serializable]
public struct GPUAnimationClipId : IComponentData, IQueryTypeParameter
{
  
    public float4 Value;
}
