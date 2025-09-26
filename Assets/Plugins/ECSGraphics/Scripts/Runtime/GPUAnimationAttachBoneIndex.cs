using System;
using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Rendering;

[MaterialProperty("_AttachBoneIndex", -1)]
[Serializable]
public struct GPUAnimationAttachBoneIndex : IComponentData, IQueryTypeParameter
{

    public int Value;
}
