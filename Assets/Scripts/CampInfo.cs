using System;
using System.Runtime.CompilerServices;
using Nebukam.ORCA;

/// <summary>
/// 表示一个营的信息，包括营的类型、忽略搜索标签、实体ID、攻击类型和营索引。
/// </summary>
public struct CampInfo : IEquatable<CampInfo>
{
    /// <summary>
    /// 获取该营的类型。
    /// </summary>
    public ORCALayer Camp { get; private set; }

    /// <summary>
    /// 获取忽略搜索时使用的标签。
    /// </summary>
    public ORCALayer IgnoreSearchTag { get; private set; }

    /// <summary>
    /// 获取实体的唯一标识符。
    /// </summary>
    public int EntityId { get; private set; }

    /// <summary>
    /// 获取攻击类型。
    /// </summary>
    public UnitAttackType AttackType { get; private set; }

    /// <summary>
    /// 获取营的索引。
    /// </summary>
    public int CampIndex { get; private set; }


    public CampInfo(ORCALayer camp, int eId)
    {
        Camp = camp;
        IgnoreSearchTag = ORCALayer.NONE;
        EntityId = eId;
        AttackType = UnitAttackType.Single;
        CampIndex = CampToIndex(camp);
    }


    /// <summary>
    /// 初始化一个新的 <see cref="CampInfo"/> 实例。
    /// </summary>
    /// <param name="camp">营的类型。</param>
    /// <param name="ignoreTag">忽略搜索的标签。</param>
    /// <param name="eId">实体的唯一标识符。</param>
    /// <param name="attackType">攻击类型。</param>
    public CampInfo(ORCALayer camp, ORCALayer ignoreTag, int eId, UnitAttackType attackType)
    {
        Camp = camp;
        IgnoreSearchTag = ignoreTag;
        EntityId = eId;
        AttackType = attackType;
        CampIndex = CampToIndex(camp);
    }

    /// <summary>
    /// 将营的类型转换为索引。
    /// </summary>
    /// <param name="camp">营的类型。</param>
    /// <returns>对应的索引。</returns>
    public static int CampToIndex(ORCALayer camp)
    {
        return (int)Math.Log((float)camp, 2);
    }

    /// <summary>
    /// 将索引转换为营的类型。
    /// </summary>
    /// <param name="campIndex">营的索引。</param>
    /// <returns>对应的营类型。</returns>
    public static ORCALayer IndexToCamp(int campIndex)
    {
        return (ORCALayer)Math.Pow(2, campIndex);
    }

    /// <summary>
    /// 判断指定的对象是否等于当前对象。
    /// </summary>
    public override bool Equals(object obj)
    {
        return obj is CampInfo info && Equals(info);
    }

    /// <summary>
    /// 判断指定的营信息是否等于当前营信息。
    /// </summary>
    public bool Equals(CampInfo other)
    {
        return Camp == other.Camp &&
               IgnoreSearchTag == other.IgnoreSearchTag &&
               EntityId == other.EntityId &&
               AttackType == other.AttackType &&
               CampIndex == other.CampIndex;
    }

    /// <summary>
    /// 获取当前对象的哈希码。
    /// </summary>
    public override int GetHashCode()
    {
        HashCode hash = new HashCode();
        hash.Add((int)Camp);
        hash.Add((int)IgnoreSearchTag);
        hash.Add(EntityId);
        hash.Add((int)AttackType);
        hash.Add(CampIndex);
        return hash.ToHashCode();

        //return _Camp;
    }

    /// <summary>
    /// 判断两个营信息是否相等。
    /// </summary>
    public static bool operator ==(CampInfo a, CampInfo b)
    {
        return a.Equals(b);
    }

    /// <summary>
    /// 判断两个营信息是否不相等。
    /// </summary>
    public static bool operator !=(CampInfo a, CampInfo b)
    {
        return !a.Equals(b);
    }

    /// <summary>
    /// 返回表示当前对象的字符串。
    /// </summary>
    public override string ToString()
    {
        return $"Camp: {Camp}, Index: {CampIndex}, EntityId: {EntityId}, AttackType: {AttackType}";
    }

    /// <summary>
    /// 检查是否是敌对阵营
    /// </summary>
    public bool IsHostileTo(CampInfo other)
    {
        return Camp != other.Camp && Camp != ORCALayer.NONE && other.Camp != ORCALayer.NONE;
    }

    /// <summary>
    /// 检查是否是友方阵营
    /// </summary>
    public bool IsFriendlyTo(CampInfo other)
    {
        return Camp == other.Camp || Camp == ORCALayer.NONE || other.Camp == ORCALayer.NONE;
    }
}
