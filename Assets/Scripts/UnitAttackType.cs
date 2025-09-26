using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单位攻击类型枚举，用于描述不同类型的攻击方式。
/// </summary>
public enum UnitAttackType
{
    /// <summary>
    /// 单体攻击：攻击一个目标。
    /// </summary>
    Single,

    /// <summary>
    /// 群体攻击：攻击多个目标。
    /// </summary>
    Multi,

    /// <summary>
    /// 远程单体攻击：从远距离攻击单个目标。
    /// </summary>
    FarSingle,

    /// <summary>
    /// 远程群体攻击：从远距离攻击多个目标。
    /// </summary>
    FarMulti
}
