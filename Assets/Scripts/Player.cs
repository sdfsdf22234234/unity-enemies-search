using Nebukam.ORCA;
using System;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

public class Player// : MonoBehaviour
{
    public int Id { get; private set; }
    public int Hp { get; protected set; }
    private RVOAgent m_Agent;
    private RendererNodeId m_RendererNodeId;

    /// <summary>
    /// 受击中
    /// </summary>
    private bool m_Damaging;//受击中
    private bool m_Attacking;//攻击中
    private float m_AttackRange = 1.0f; // 攻击范围
    private float attackInterval;

    private float3 SourcetargetPosition;
    private float lastAttackTime;
    public Player(ORCALayer campId, int hp, float4 campColor, float3 pos, float3 tPos, float moveSpeed)
    {
        this.Hp = hp; //默认Hp
        m_Agent = RVOComponent.Instance.AddAgent(pos);
        Id = m_Agent.Id;
        m_RendererNodeId = BatchRendererComponent.Instance.AddRenderer(0, pos, quaternion.identity, new float3(1));
        m_Agent.rendererIndex = m_RendererNodeId.Index;
        m_Agent.targetPosition = tPos;
        this.SourcetargetPosition = tPos;
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
    // 获取当前位置
    public Vector3 CurrentPosition
    {
        get { return m_Agent.pos; }
    }

    internal void GoForward()
    {
        if (m_Damaging || m_Attacking) return;
        if(!m_Agent.targetPosition.Equals(this.SourcetargetPosition))
            m_Agent.targetPosition = this.SourcetargetPosition;
        PlayAnimation(2);
    }




    public void TryAttackTarget(Player target)
    {
        if (m_Attacking) return;

        if (!m_Damaging)
        {
            Vector3 currentPosition = CurrentPosition;
            Vector3 targetPosition = target.CurrentPosition;

            float3 towards = targetPosition - currentPosition;
            float distancesq = math.distancesq(currentPosition, targetPosition);

            float range = this.m_Agent.radius + target.m_Agent.radius + this.m_AttackRange;

            if (distancesq <= range * range)
            {
                Attack(target);
            }
            else
            {

                float3 offset = towards / math.sqrt(distancesq) * (this.m_Agent.radius + this.m_Agent.radius);

                float3 float3Value = new float3(targetPosition.x, targetPosition.y, targetPosition.z);
                m_Agent.targetPosition = float3Value - offset;

                PlayAnimation(2);//跑
               // 更新移动目标位置
                                 // m_Agent.targetPosition = targetPosition;
                                 // GoForward();
            }
            //    if (Time.time - lastAttackTime >= attackInterval)
            //{
            //    lastAttackTime = Time.time;
            //    Attack(target);
            //}
        }
        else
        {
            PlayAnimation(0);
        }
    }

    public void ClearState()
    {
        m_Damaging = false;
        m_Attacking = false;
    }



        public void Attack(Player target)
    {
        if (target.Hp <= 0|| RVOComponent.Instance.GetAgent(target.Id) == null)
        {
            ClearState();
            return;
        }
   
        if (Time.time - lastAttackTime >= attackInterval)
        {
            lastAttackTime = Time.time;
            PlayAnimation(4); //攻击动画
            Task.Delay(1000).ContinueWith(_ =>
            {
                PlayAnimation(0);
            });
            target.ApplyDamage(this, 1);
        }
        //else
        //{
        //    m_Agent.navigationEnabled = false;
        //}
    }

    private void ApplyDamage(Player attaker, int damageValue)
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

    private void OnDamage(Player attaker, int damageValue)
    {
        if (this.Hp <= 0) return;

        PlayAnimation(3);
   
        Task.Delay(500).ContinueWith(_ =>
        {
            PlayAnimation(m_Agent.navigationEnabled ? 2 : 0);
        });
    }

    private void OnDead(Player attacker)
    {
        Destroy();
    }

    internal void Destroy()
    {
        BatchRendererComponent.Instance.RemoveRenderer(m_RendererNodeId);
        RVOComponent.Instance.RemoveAgent(m_Agent);
    }

}
