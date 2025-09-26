using System;
using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

 
[MaterialProperty("_BaseColor", -1)]
[Serializable]
public struct MaterialBaseColor : IComponentData, IQueryTypeParameter
{
  
    public float4 Value;
}
