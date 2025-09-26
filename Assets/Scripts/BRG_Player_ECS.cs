using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Nebukam.ORCA;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;



public class BRG_Player_ECS
{

    public int Id
    {


        get
        {
            return m_Id;
        }
    }




    public virtual int Hp
    {


        get
        {
            return mHp; // 返回当前生命值
        }
        protected set
        {
            if (value >= 0)
            {
                mHp = value; // 设置生命值
            }
        }
    }



    public CampInfo CampInfo { get; private set; }


    public bool IsDead => Hp == 0;


    public CombatUnitState UnitState
    {
        get => m_UnitState;
        set
        {
            if (m_UnitState != value && (value != CombatUnitState.Move || m_UnitCanMove))
            {
                m_UnitState = value; // 更新单位状态
                if (m_RVO != null)
                {
                    m_RVO.navigationEnabled = (value == CombatUnitState.Move);
                }
                OnUnitStateChanged(); // 状态改变通知
            }
        }
    }




    public CombatUnitState AnimState
    {


        get
        {
            return CombatUnitState.Idle;
        }


        set
        {
        }
    }




    public UnitAttackType AttackType { get; private set; }



    public float3 Position
    {


        get
        {
            return default(float3);
        }
    }




    public quaternion Rotation
    {


        get
        {
            return default(quaternion);
        }
    }




    public BRG_Player_ECS(int id, ORCALayer camp, Vector3 pos, Action<int> onPlayerDead)
    {
        m_Id = id;
        CampInfo = new CampInfo(camp,id);
        onPlayerDeadCallback = onPlayerDead;
        m_DamageValue = 1;
        m_DamageCount = 1;
        m_DamageRadius = 0.2f;
        m_LastFireTime = 0f;
        m_UnitState = CombatUnitState.None;
        m_FarAttack = true;
        AttackType = 0;
        mHp=3;

        AddRVO_BRG(pos);
        if (m_UnitState != CombatUnitState.None && m_UnitCanMove)
        {
            m_UnitState = CombatUnitState.Walk;
            if (m_RVO != null)
            {
                m_RVO.navigationEnabled = true;
                OnUnitStateChanged();
            }
        }
    }




    public virtual void UpdateLogic(float realElapseSeconds)
    {
    }




    public void Destroy()
    {
    }




    public void Attack(BRG_Player target)
    {
    }




    public bool CheckAttackCD()
    {
        return default(bool);
    }




    protected virtual void OnUnitStateChanged()
    {
    }




    private void AddRVO_BRG(Vector3 pos)
    {
    }




    private void RemoveRVO_BRG()
    {
    }




    public void UpdateNearestTarget(BRG_Player target, bool inAttackRadius)
    {
    }




    protected virtual void OnNearestTargetInAttackRadius()
    {
    }




    public void SetMovePoint(float3 pos)
    {
    }




    public void ApplyDamage(ORCALayer formCamp, int damageValue)
    {
    }




    private void OnDamage()
    {
    }




    protected virtual void OnDead(ORCALayer camp)
    {
    }




    internal void OnGPUAnimationEvent(int resIndex, int clipIndex, int triggerAtFrame)
    {
    }




    private void ParseCombatUnitAttackType()
    {
    }




    private int m_Id;




    private int mHp;




    private float m_LastFireTime;




    protected RVOAgent m_RVO;




    protected RendererNodeId m_RendererId;




    protected BRG_Player m_NearestTarget;




    protected bool m_NearestTargetInAttackRadius;




    private CombatUnitState m_UnitState;




    private CombatUnitState m_AnimState;




    private bool m_UseAnimAttackEvent;




    private Dictionary<int, BRG_Player> m_AttackTargets;




    private List<Vector3> m_FireTargetPoints;




    private bool m_FarAttack;




    private bool m_UnitCanMove;




    private int m_DamageValue;




    private int m_DamageCount;




    private float m_DamageRadius;




    private Action<int> onPlayerDeadCallback;
}
