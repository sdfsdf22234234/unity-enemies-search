using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗单位的状态枚举，用于表示一个战斗单位在游戏中的不同状态。
/// </summary>
public enum CombatUnitState
{

   
    /// <summary>
    /// 无效状态，通常用于初始化或未设置状态时的默认值。
    /// </summary>
    None = -1,
   
    /// <summary>
    /// 空闲状态，表示战斗单位没有进行任何动作，处于待命状态。
    /// </summary>
    Idle=0,

    /// <summary>
    /// 走动状态
    /// </summary>
    Walk=1,
    /// <summary>
    /// 移动状态，表示战斗单位正在移动到目标位置。
    /// </summary>
    Move=2,

    /// <summary>
    /// 受伤状态，表示战斗单位正在受到伤害或已被击中。
    /// </summary>
    Damage = 3,

    /// <summary>
    /// 攻击状态，示战斗单位正在执行攻击动作。
    /// </summary>
    Attack = 4,

    /// <summary>
    /// 死亡状态，表示战斗单位已经死亡，无法继续参与战斗。
    /// </summary>
    Dead,

    /// <summary>
    /// 另一个死亡状态，用于表示特殊的死亡状态，可能涉及动画或其他处理。
    /// </summary>
    Dead2
}
