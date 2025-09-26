#if UNITY_2022_1_OR_NEWER
using System;
using System.Runtime.InteropServices;
using UnityEngine.Rendering;
[StructLayout(LayoutKind.Sequential)]
public struct DrawBatchKey : IEquatable<DrawBatchKey>, IComparable<DrawBatchKey>
{
    public static readonly DrawBatchKey Null = new DrawBatchKey { MaterialID = BatchMaterialID.Null, MeshID = BatchMeshID.Null, SubmeshIndex = 0 };
    public BatchMeshID MeshID;
    public uint SubmeshIndex;
    public BatchMaterialID MaterialID;
    public BatchDrawCommandFlags drawCommandFlags;
    public bool isTransparent;
    public int Id { get; private set; }
    public override int GetHashCode()
    {
        // return HashCode.Combine( MeshID.GetHashCode(), MaterialID.GetHashCode(), SubmeshIndex.GetHashCode());
       // int hashCode = MeshID.GetHashCode();
       // return 23 * (hashCode + 391) + MaterialID.GetHashCode();
        unchecked
        {
            // 使用位运算组合哈希值
            return (MeshID.GetHashCode() << 16) ^ (MaterialID.GetHashCode() << 8) ^ SubmeshIndex.GetHashCode();
        }
    }
    public DrawBatchKey(int id, BatchMeshID meshId, BatchMaterialID matId, bool isTrans)
    {
        this.SubmeshIndex = 0;
        this.isTransparent = isTrans;
        this.Id = id;
        this.MeshID = meshId;
        this.MaterialID = matId;
        this.drawCommandFlags = (BatchDrawCommandFlags)(isTrans ? 8 : 0); 
    }
    public int CompareTo(DrawBatchKey other)
    {
        int cmpMaterial = MaterialID.CompareTo(other.MaterialID);
        int cmpMesh = MeshID.CompareTo(other.MeshID);
        int cmpSubmesh = SubmeshIndex.CompareTo(other.SubmeshIndex);

        if (cmpMaterial != 0)
            return cmpMaterial;
        if (cmpMesh != 0)
            return cmpMesh;

        return cmpSubmesh;
    }
    public  bool IsValid()
    {
        // 检查MeshID和MaterialID是否都有效
        bool isMeshValid = MeshID.value != BatchMeshID.Null.value;
        bool isMaterialValid = MaterialID.value != BatchMaterialID.Null.value;

        return isMeshValid && isMaterialValid;
    }
    public bool Equals(DrawBatchKey other) => CompareTo(other) == 0;
    public override string ToString()
    {
        return $"DrawBatchKey Hash:{GetHashCode()}";
    }
}
#endif