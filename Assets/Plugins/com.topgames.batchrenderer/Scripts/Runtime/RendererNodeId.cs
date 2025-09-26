#if UNITY_2022_1_OR_NEWER
using System;
using System.Runtime.InteropServices;


/// <summary>
/// 类型和名称的组合值。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct RendererNodeId : IEquatable<RendererNodeId>
{
    public static readonly RendererNodeId Null = new RendererNodeId(DrawBatchKey.Null, 0,0);
    private readonly DrawBatchKey m_BatchKey;
    private readonly int m_Index;
    private readonly int m_ResourceIndex;
    /// <summary>
    /// 初始化类型和名称的组合值的新实例。
    /// </summary>
    /// <param name="type">类型。</param>
    /// <param name="name">名称。</param>
    public RendererNodeId(DrawBatchKey type, int id, int resIndex)
    {
        m_ResourceIndex = resIndex;
        m_BatchKey = type;
        m_Index = id;
    }

    /// <summary>
    /// 获取类型。
    /// </summary>
    public DrawBatchKey BatchKey
    {
        get
        {
            return m_BatchKey;
        }
    }
    public int ResourceIndex
    {
        
        get
        {
            return m_ResourceIndex;
        }
    }
    /// <summary>
    /// 获取名称。
    /// </summary>
    public int Index
    {
        get
        {
            return m_Index;
        }
    }

    /// <summary>
    /// 获取类型和名称的组合值字符串。
    /// </summary>
    /// <returns>类型和名称的组合值字符串。</returns>
    public override string ToString()
    {
        return string.Format("{0}.{1}", m_BatchKey, m_Index);
    }

    /// <summary>
    /// 获取对象的哈希值。
    /// </summary>
    /// <returns>对象的哈希值。</returns>
    public override int GetHashCode()
    {
        return m_BatchKey.GetHashCode() ^ m_Index.GetHashCode();
    }

    /// <summary>
    /// 比较对象是否与自身相等。
    /// </summary>
    /// <param name="obj">要比较的对象。</param>
    /// <returns>被比较的对象是否与自身相等。</returns>
    public override bool Equals(object obj)
    {
        return obj is RendererNodeId && Equals((RendererNodeId)obj);
    }

    /// <summary>
    /// 比较对象是否与自身相等。
    /// </summary>
    /// <param name="value">要比较的对象。</param>
    /// <returns>被比较的对象是否与自身相等。</returns>
    public bool Equals(RendererNodeId value)
    {
        return m_BatchKey.Equals(value.m_BatchKey) && m_Index == value.m_Index;
    }

    /// <summary>
    /// 判断两个对象是否相等。
    /// </summary>
    /// <param name="a">值 a。</param>
    /// <param name="b">值 b。</param>
    /// <returns>两个对象是否相等。</returns>
    public static bool operator ==(RendererNodeId a, RendererNodeId b)
    {
        return a.Equals(b);
    }

    /// <summary>
    /// 判断两个对象是否不相等。
    /// </summary>
    /// <param name="a">值 a。</param>
    /// <param name="b">值 b。</param>
    /// <returns>两个对象是否不相等。</returns>
    public static bool operator !=(RendererNodeId a, RendererNodeId b)
    {
        return !(a == b);
    }
}
#endif