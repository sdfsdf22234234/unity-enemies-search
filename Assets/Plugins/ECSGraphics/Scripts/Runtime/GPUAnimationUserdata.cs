using System;
using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

 
[MaterialProperty("_Userdata", -1)]
[Serializable]
public struct GPUAnimationUserdata : IComponentData, IQueryTypeParameter
{
  
    public float4 Value;
}
