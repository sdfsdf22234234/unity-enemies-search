using System;
using System.Runtime.InteropServices;

using Unity.Entities;
using Unity.Mathematics;

 
public struct GPUAnimationAttachments : IComponentData, IQueryTypeParameter
{
  
    public int4x2 AttachedEntitiesIds;

  
    public int AttachedCount;
}
