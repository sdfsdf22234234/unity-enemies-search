using System;
using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;

public struct ECSRenderData : IComponentData, IQueryTypeParameter, IEquatable<ECSRenderData>
{
    public int RenderIndex;
    public int EntityIndex;

    public bool Equals(ECSRenderData other)
    {
        return RenderIndex == other.RenderIndex && EntityIndex == other.EntityIndex;
    }

    public override bool Equals(object obj)
    {
        return obj is ECSRenderData other && Equals(other);
    }

    public override int GetHashCode()
    {
         return (int)math.hash(new int2(RenderIndex, EntityIndex));
    }
}
