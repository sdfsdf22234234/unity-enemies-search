using System;
using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;
 
public struct LODsEntities : IComponentData, IQueryTypeParameter
{
  
    public int4x2 LodEntitiesIds;
 
    public int LodCount;
}
