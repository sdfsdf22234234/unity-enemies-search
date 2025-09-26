using System;
using System.Runtime.CompilerServices;
using Nebukam.ORCA;

/// <summary>
/// ��ʾһ��Ӫ����Ϣ������Ӫ�����͡�����������ǩ��ʵ��ID���������ͺ�Ӫ������
/// </summary>
public struct CampInfo : IEquatable<CampInfo>
{
    /// <summary>
    /// ��ȡ��Ӫ�����͡�
    /// </summary>
    public ORCALayer Camp { get; private set; }

    /// <summary>
    /// ��ȡ��������ʱʹ�õı�ǩ��
    /// </summary>
    public ORCALayer IgnoreSearchTag { get; private set; }

    /// <summary>
    /// ��ȡʵ���Ψһ��ʶ����
    /// </summary>
    public int EntityId { get; private set; }

    /// <summary>
    /// ��ȡ�������͡�
    /// </summary>
    public UnitAttackType AttackType { get; private set; }

    /// <summary>
    /// ��ȡӪ��������
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
    /// ��ʼ��һ���µ� <see cref="CampInfo"/> ʵ����
    /// </summary>
    /// <param name="camp">Ӫ�����͡�</param>
    /// <param name="ignoreTag">���������ı�ǩ��</param>
    /// <param name="eId">ʵ���Ψһ��ʶ����</param>
    /// <param name="attackType">�������͡�</param>
    public CampInfo(ORCALayer camp, ORCALayer ignoreTag, int eId, UnitAttackType attackType)
    {
        Camp = camp;
        IgnoreSearchTag = ignoreTag;
        EntityId = eId;
        AttackType = attackType;
        CampIndex = CampToIndex(camp);
    }

    /// <summary>
    /// ��Ӫ������ת��Ϊ������
    /// </summary>
    /// <param name="camp">Ӫ�����͡�</param>
    /// <returns>��Ӧ��������</returns>
    public static int CampToIndex(ORCALayer camp)
    {
        return (int)Math.Log((float)camp, 2);
    }

    /// <summary>
    /// ������ת��ΪӪ�����͡�
    /// </summary>
    /// <param name="campIndex">Ӫ��������</param>
    /// <returns>��Ӧ��Ӫ���͡�</returns>
    public static ORCALayer IndexToCamp(int campIndex)
    {
        return (ORCALayer)Math.Pow(2, campIndex);
    }

    /// <summary>
    /// �ж�ָ���Ķ����Ƿ���ڵ�ǰ����
    /// </summary>
    public override bool Equals(object obj)
    {
        return obj is CampInfo info && Equals(info);
    }

    /// <summary>
    /// �ж�ָ����Ӫ��Ϣ�Ƿ���ڵ�ǰӪ��Ϣ��
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
    /// ��ȡ��ǰ����Ĺ�ϣ�롣
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
    /// �ж�����Ӫ��Ϣ�Ƿ���ȡ�
    /// </summary>
    public static bool operator ==(CampInfo a, CampInfo b)
    {
        return a.Equals(b);
    }

    /// <summary>
    /// �ж�����Ӫ��Ϣ�Ƿ���ȡ�
    /// </summary>
    public static bool operator !=(CampInfo a, CampInfo b)
    {
        return !a.Equals(b);
    }

    /// <summary>
    /// ���ر�ʾ��ǰ������ַ�����
    /// </summary>
    public override string ToString()
    {
        return $"Camp: {Camp}, Index: {CampIndex}, EntityId: {EntityId}, AttackType: {AttackType}";
    }

    /// <summary>
    /// ����Ƿ��ǵж���Ӫ
    /// </summary>
    public bool IsHostileTo(CampInfo other)
    {
        return Camp != other.Camp && Camp != ORCALayer.NONE && other.Camp != ORCALayer.NONE;
    }

    /// <summary>
    /// ����Ƿ����ѷ���Ӫ
    /// </summary>
    public bool IsFriendlyTo(CampInfo other)
    {
        return Camp == other.Camp || Camp == ORCALayer.NONE || other.Camp == ORCALayer.NONE;
    }
}
