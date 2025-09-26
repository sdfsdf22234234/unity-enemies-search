using System;
using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

 
[MaterialProperty("_MainColor", -1)]
[Serializable]
public struct MaterialMainColor : IComponentData, IQueryTypeParameter
{
 
    public float4 Value;
}
