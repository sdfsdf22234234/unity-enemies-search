using System;
using System.Runtime.InteropServices;
using Unity.Entities;

 
public struct GPUAnimationEventFramed : IComponentData, IQueryTypeParameter
{
 
    public int Value;
}
