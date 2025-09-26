using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Nebukam.ORCA;
using Unity.Mathematics;
using UnityEngine;



public class BRG_Player_Old
{



    public int Id { get; private set; }



    public int Hp { get; private set; }




    public BRG_Player_Old(ORCALayer campId, int hp, float4 campColor, float3 pos, float3 tPos, float moveSpeed)
    {
        this.Hp = hp; //默认Hp
        m_Agent = RVOComponent.Instance.AddAgent(pos);
        Id = m_Agent.Id;
        m_RendererNodeId = BatchRendererComponent.Instance.AddRenderer(0, pos, quaternion.identity, new float3(1));
        m_Agent.rendererIndex = m_RendererNodeId.Index;
        m_Agent.targetPosition = tPos;
     
        m_Agent.maxSpeed = moveSpeed;
        m_Agent.camp = campId; //用于区分阵营
        m_Agent.searchRadius = 2f;// UnityEngine.Random.Range(2f, 3f); //索敌半径
        m_Agent.searchCount = 1;// UnityEngine.Random.Range(1, 3); //同时攻击几个单位
        m_Agent.radius = m_Agent.radiusObst = 0.5f;
        BatchRendererComponent.Instance.SetRendererColor(m_RendererNodeId, campColor);
        PlayAnimation(2);//跑
    }




    public void PlayAnimation(int animIndex)
    {
        if (animIndex == m_Agent.clipId.x) return;
        var extData = m_Agent.clipId;
        extData.x = animIndex;
        extData.y = Time.time;
           
        m_Damaging = animIndex == 3;
        if (m_Damaging)
        {
            extData.z = Time.time + 0.5f;
            m_Agent.SetAnimationBlink();
        }
        m_Attacking = animIndex == 4;
        m_Agent.navigationEnabled = !(m_Damaging || m_Attacking || animIndex == 0);//idle,攻击或受击中不能移动
        m_Agent.clipId = extData;
    }



    internal void GoForward()
    {
        if (m_Damaging || m_Attacking) return;
        //if (!m_Agent.targetPosition.Equals(this.SourcetargetPosition))
        //    m_Agent.targetPosition = this.SourcetargetPosition;
        PlayAnimation(2);
    }




    public void TryAttackTarget(BRG_Player_Old target)
    {
        // 计算与目标的距离

        Unity.Mathematics.float3 toTarget = target.m_Agent.pos - m_Agent.pos;

        //   // 计算距离的平方
        float distanceToTarget = Unity.Mathematics.math.lengthsq(toTarget);

        // 检查是否在攻击范围内
        if (distanceToTarget <= 1.0f) // 攻击范围设为1.0f
        {
            // 在攻击范围内，执行攻击
            if (!m_Damaging && Time.time - lastAttackTime >= attackInterval)
            {
                lastAttackTime = Time.time;
                Attack(target);
            }
            else if (m_Damaging)
            {
                PlayAnimation(0);
            }
        }
        else
        {
            // 不在攻击范围内，向目标移动
            Unity.Mathematics.float3 direction = Unity.Mathematics.math.normalizesafe(toTarget);

            // 设置首选速度
            if (m_Agent != null)
            {
                m_Agent.prefVelocity = direction * 0.5f;
            }
        }
    }



    public void Attack(BRG_Player_Old target)
    {
        if (target.Hp <= 0) return;
        PlayAnimation(4); //攻击动画
        Task.Delay(1000).ContinueWith(_ =>
        {
         
            PlayAnimation(0);
        });
        target.ApplyDamage(this, 1);
    }




    private void ApplyDamage(BRG_Player_Old attaker, int damageValue)
    {
        if (this.Hp <= 0) return;

        this.Hp -= damageValue;
        OnDamage(attaker, damageValue);
        if (this.Hp <= 0)
        {
            OnDead(attaker);
        }
    }



    public void ApplyDamage(int damageValue)
    {
        ApplyDamage(null, damageValue);
    }




    private void OnDamage(BRG_Player_Old attaker, int damageValue)
    {
        if (this.Hp <= 0) return;

        PlayAnimation(3);
        Task.Delay(500).ContinueWith(_ =>
        {
            PlayAnimation(m_Agent.navigationEnabled ? 2 : 0);
        });
    }




    private void OnDead(BRG_Player_Old attacker)
    {
        Destroy();
    }




    internal void Destroy()
    {
        BatchRendererComponent.Instance.RemoveRenderer(m_RendererNodeId);
        RVOComponent.Instance.RemoveAgent(m_Agent);
    }




    private RVOAgent m_Agent;




    private RendererNodeId m_RendererNodeId;




    private bool m_Damaging;




    private bool m_Attacking;




    private float attackInterval;

       
       

    private float lastAttackTime;
}
   