using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Cysharp.Threading.Tasks;
using Nebukam.ORCA;
using Unity.Mathematics;
using UnityEngine;
using static UnityEngine.AI.NavMesh;

/// <summary>
/// 表示游戏中的玩家角色，负责管理角色的状态、行为和交互。
/// </summary>
public class BRG_Player
{
    // 私有字段
    private int m_Id; // 玩家ID
    private int mHp; // 当前生命值
    private float m_LastFireTime; // 上次攻击时间
    protected RVOAgent m_RVO; // RVO代理
    protected RendererNodeId m_RendererId; // 渲染节点ID
    protected BRG_Player m_NearestTarget; // 最近目标
    protected bool m_NearestTargetInAttackRadius; // 最近目标是否在攻击范围内
    private CombatUnitState m_UnitState; // 当前单位状态
    private CombatUnitState m_AnimState; // 当前动画状态
    private bool m_UseAnimAttackEvent; // 是否使用动画攻击事件
    private Dictionary<int, BRG_Player> m_AttackTargets; // 攻击目标列表
    private List<Vector3> m_FireTargetPoints; // 攻击目标点列表
    private bool m_FarAttack; // 是否远程攻击
    private bool m_UnitCanMove = true; // 单位是否可以移动
    private int m_DamageValue; // 伤害值
    private int m_DamageCount; // 伤害计数
    private float m_DamageRadius; // 伤害半径
    private Action<int> onPlayerDeadCallback; // 玩家死亡回调

    private RendererNodeId m_HpBarRendererId;  // 生命条渲染ID
    private float m_HpBarHeight = 4f;          // 生命条高度偏移
    private bool m_ShowHpBar = false;           // 是否显示生命条
    private float m_LastDamageTime;

    public RendererNodeId RendererId => m_RendererId;


    // 添加属性访问器
    public float LastDamageTime => m_LastDamageTime;
    /// <summary>
    /// 获取玩家ID
    /// </summary>
    public int Id
    {
        get
        {
            return m_Id; // 返回玩家ID
        }
    }

    /// <summary>
    /// 获取和设置玩家当前生命值
    /// </summary>
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

    /// <summary>
    /// 获取玩家的阵营信息
    /// </summary>
    public CampInfo CampInfo { get; private set; }

    /// <summary>
    /// 获取玩家是否死亡
    /// </summary>
    public bool IsDead => Hp == 0;

    /// <summary>
    /// 获取和设置当前单位状态
    /// </summary>
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

    /// <summary>
    /// 获取和设置当前动画状态
    /// </summary>
    public CombatUnitState AnimState
    {
        get => this.m_AnimState;
        set
        {
            m_AnimState = value;

            var batchRenderer = BatchRendererComponent.Instance;
            if (batchRenderer == null)
            {
                throw new NullReferenceException("BatchRendererComponent instance is null");
            }

            RendererNodeId nodeId = new RendererNodeId(
                m_RendererId.BatchKey,
                m_RendererId.BatchKey.Id,
                m_RendererId.ResourceIndex
            );

            batchRenderer.SetRendererClipId(nodeId, (int)m_AnimState); // 更新动画片段ID
        }
    }

    /// <summary>
    /// 获取当前的攻击类型
    /// </summary>
    public UnitAttackType AttackType { get; private set; }

    /// <summary>
    /// 获取当前的位置
    /// </summary>
    public float3 Position
    {
        get
        {
            if (m_RVO == null)
            {
                throw new NullReferenceException("RVO Agent is null");
            }

            return m_RVO.pos; // 返回当前位置
        }
    }

    /// <summary>
    /// 获取当前的旋转
    /// </summary>
    public quaternion Rotation
    {
        get
        {
            if (m_RVO == null)
            {
                throw new NullReferenceException("RVO Agent is null");
            }

            return m_RVO.rotation; // 返回当前旋转
        }
    }

    /// <summary>
    /// 构造函数，初始化玩家
    /// </summary>
    public BRG_Player(int id, ORCALayer camp, Vector3 pos, Action<int> onPlayerDead)
    {
        m_DamageValue = 1;
        m_DamageCount = 1;
        m_DamageRadius = 0.2f;

        // 初始化其他字段
        onPlayerDeadCallback = onPlayerDead;
        m_LastFireTime = 0f;
        m_Id = id;
        m_FarAttack = false;
        AttackType = UnitAttackType.Single;
        CampInfo = new CampInfo(camp, id);
        mHp = 100; // 设置初始生命值
        AddRVO_BRG(pos); // 添加RVO代理

        // 默认状态为移动状态
        if (m_UnitState != CombatUnitState.Move && m_UnitCanMove)
        {
            if (m_RVO == null)
            {
                throw new NullReferenceException("RVO Agent is null");
            }

            m_UnitState = CombatUnitState.Move;
            m_RVO.navigationEnabled = true;
            OnUnitStateChanged(); // 状态改变通知
        }
     if(m_ShowHpBar)
   InitializeHpBar(pos); // 初始化生命条
    }

    // 初始化生命条渲染
    private void InitializeHpBar(Vector3 pos)
    {
        var batchRenderer = BatchRendererComponent.Instance;
        if (batchRenderer != null)
        {
            // 使用资源索引1添加生命条
            int hpBarResourceIndex = 1;
            Vector3 hpBarPosition = pos + new Vector3(0, m_HpBarHeight, 0);
            Quaternion hpBarRotation = Quaternion.Euler(0, -90, 0); // 设置生命条为水平
            m_HpBarRendererId = batchRenderer.AddRenderer(
                hpBarResourceIndex,
                hpBarPosition,
                hpBarRotation,
                new Vector3(1, 1, 1)
            );

            // 设置生命条的初始颜色
            var campColor = Color.white; // 这里可以根据阵营设置不同颜色
            float4 float4Color = new Vector4(campColor.r, campColor.g, campColor.b, campColor.a);

            batchRenderer.SetRendererColor(m_HpBarRendererId, float4Color);

            // 存储当前生命信息到用户数据
            float hpRatio = (float)mHp / 100; // 假设最大生命值为3
            m_RVO.SetAnimationBlink(hpRatio);
            
        }
    }

    // 更新生命条位置
    public void UpdateHpBarPosition()
    {
        if (!m_ShowHpBar || IsDead) return;

        var batchRenderer = BatchRendererComponent.Instance;
        if (batchRenderer != null)
        {
            // 设置生命条位置
            var hpBarPosition = Position + new float3(0, m_HpBarHeight, 0);
            Quaternion hpBarRotation = Quaternion.Euler(0, -90, 0);
            batchRenderer.SetRendererData(m_HpBarRendererId, hpBarPosition, hpBarRotation);
        }
    }
    
    /// <summary>
    /// 更新逻辑处理
    /// </summary>
    public virtual void UpdateLogic(float realElapseSeconds)
    {
        const float SPEED_THRESHOLD = 0.0099999998f;

        // 如果单位已经死亡，直接返回
        if (m_UnitState == CombatUnitState.Dead)
            return;

        // 确保RVO代理有效
        if (m_RVO == null)
        {
            throw new System.NullReferenceException("RVO Agent is null");
        }
        if(m_ShowHpBar)
       UpdateHpBarPosition(); // 更新生命条位置

        // 检测是否静止
        bool isStationary = m_RVO.MoveSpeed <= SPEED_THRESHOLD;
       // Debug.LogWarning($"MoveSpeed"+ m_RVO.MoveSpeed);

        //if (m_UnitState == CombatUnitState.Attack && isStationary)
        //{
        //    if (m_UnitState != CombatUnitState.Idle)
        //    {
        //        m_UnitState = CombatUnitState.Idle;
        //        m_RVO.navigationEnabled = false;
        //        OnUnitStateChanged();
        //    }


        //}

 
    }


    /// <summary>
    /// 销毁该单位的实例
    /// </summary>
    public void Destroy()
    {
        BatchRendererComponent.Instance.RemoveRenderer(m_RendererId);
        RVOComponent.Instance.RemoveAgent(m_RVO);
    }

    /// <summary>
    /// 执行攻击操作
    /// </summary>
    /// <param name="target">攻击目标</param>
    public void Attack(BRG_Player target)
    {
        // 如果当前状态不是攻击状态，则更新状态
        if (m_UnitState != CombatUnitState.Attack)
        {
            if (m_RVO == null)
                throw new System.NullReferenceException("RVO Agent is null");

            // 更新状态为攻击
            m_UnitState = CombatUnitState.Attack;
            m_RVO.navigationEnabled = false; // 禁用导航
            OnUnitStateChanged();
        }

        // 检查目标是否为null
        if (target == null)
            throw new System.ArgumentNullException(nameof(target));

        // 根据使用动画攻击事件决定如何处理攻击
        if (m_UseAnimAttackEvent)
        {
            // 添加攻击目标到目标列表
            if (m_AttackTargets != null)
                m_AttackTargets.TryAdd(target.Id, target);
        }
        else
        {
            // 直接对目标造成伤害
            target.ApplyDamage(CampInfo.Camp, m_DamageValue);
        }

        // 记录最后攻击时间
        m_LastFireTime = Time.time;
    }

    /// <summary>
    /// 检查攻击冷却时间
    /// </summary>
    /// <returns>如果冷却时间已过则返回true</returns>
    public bool CheckAttackCD()
    {
        float currentTime = Time.time;

        // 检查当前时间是否超过上次攻击时间加冷却时间（1秒）
        return currentTime > (m_LastFireTime + 1.0f);
    }

    /// <summary>
    /// 状态改变时的回调
    /// </summary>
    protected virtual void OnUnitStateChanged()
    {
        // 更新动画状态
        m_AnimState = m_UnitState;
        if (m_RVO != null)
            m_RVO.PlayGPUAnimation((int)m_AnimState);

        var batchRenderer = BatchRendererComponent.Instance;
        if (batchRenderer == null)
            throw new System.NullReferenceException("Batch Renderer instance is null");

        if (m_UnitState == CombatUnitState.Attack || m_UnitState == CombatUnitState.Damage)
        {
            float animationDuration = batchRenderer.GetAnimationClipInfo(m_RendererId, (int)m_UnitState).z;

            // 等待动画播放完成
            UniTask.Delay((int)(animationDuration * 1000)).ContinueWith(() => {
                // 如果单位已经死亡，直接返回
                if (m_UnitState == CombatUnitState.Dead)
                    return;

                // 只有当前状态还是原状态时才切换到Idle
                CombatUnitState currentState = m_UnitState;
                if (m_UnitState == currentState)
                {
                    m_UnitState = CombatUnitState.Idle;
                    if (m_RVO != null)
                    {
                        m_RVO.navigationEnabled = false;
                    }
                    OnUnitStateChanged();
                }
            });
        }
    }

    /// <summary>
    /// 添加RVO代理
    /// </summary>
    /// <param name="pos">目标位置</param>
    private void AddRVO_BRG(Vector3 pos)
    {
        // 添加RVO代理
        m_RVO = RVOComponent.Instance.AddAgent(pos);
        if (m_RVO == null) return;

        // 设置初始旋转
        m_RVO.rotation = Quaternion.identity;

        // 获取批渲染组件实例
        var batchRenderer = BatchRendererComponent.Instance;
        if (batchRenderer == null) return;

        // 添加渲染器
        var scale = new Vector3(1, 1, 1);
        m_RendererId = batchRenderer.AddRenderer(0, pos, m_RVO.rotation, scale);

        // 检查是否有动画攻击事件
        bool hasAnimAttackEvent = batchRenderer.HasAnimationEvent(m_RendererId, (int)CombatUnitState.Attack, "OnAttack");
        m_UseAnimAttackEvent = hasAnimAttackEvent;


     // batchRenderer.SetRendererUserdataVec4(m_RendererId, new float4(Time.time, 0,0,0)); ;

  

        if (hasAnimAttackEvent)
        {
            m_AttackTargets = new Dictionary<int, BRG_Player>(m_DamageCount);
        }

        // 设置RVO代理属性
        if (m_RVO != null)
        {
            m_RVO.rendererIndex = m_RendererId.Index;
            m_RVO.camp = CampInfo.Camp;

            // 设置渲染器颜色
            var campColor = CampInfo.Camp == ORCALayer.L0 ? Color.blue : Color.red;
            float4 float4Color = new Vector4(campColor.r, campColor.g, campColor.b, campColor.a);
            batchRenderer.SetRendererColor(m_RendererId, float4Color);

            // 设置RVO代理的运动属性
            m_RVO.maxSpeed = 10.0f;
            m_RVO.radius = 0.5f;
            m_RVO.radiusObst = 0.5f;
            m_RVO.searchRadius = m_DamageRadius;
            m_RVO.searchCount = m_DamageCount;
            m_RVO.collisionEnabled = true;
            m_RVO.navigationEnabled = m_UnitCanMove;
        }
    }

    /// <summary>
    /// 移除RVO代理
    /// </summary>
    private void RemoveRVO_BRG()
    {
        if (m_RVO == null)
        {
            return;
        }

        var rvoComponent = RVOComponent.Instance;
        if (rvoComponent != null)
        {
            rvoComponent.RemoveAgent(m_RVO);
            m_RVO = null;
        }

        var batchRenderer = BatchRendererComponent.Instance;
        if (batchRenderer != null)
        {
            batchRenderer.RemoveRenderer(m_RendererId);
            m_RendererId = default;
        }
    }
  

    /// <summary>
    /// 更新最近的目标
    /// </summary>
    /// <param name="target">最近的目标</param>
    /// <param name="inAttackRadius">是否在攻击范围内</param>
    public void UpdateNearestTarget(BRG_Player target, bool inAttackRadius)
    {

        // 只更新目标信息
        m_NearestTarget = target;
        m_NearestTargetInAttackRadius = inAttackRadius;

       // if (m_UnitState == CombatUnitState.Attack)
         //   return;


        // 更新移动目标位置
        if (!inAttackRadius && m_UnitCanMove && m_NearestTarget != null &&
            m_NearestTarget.m_RVO != null && m_RVO != null)
        {
           // m_UnitState = CombatUnitState.Move;
            m_RVO.navigationEnabled = true;
            m_RVO.targetPosition = m_NearestTarget.m_RVO.pos;
            m_RVO.stopMoveSelf = false;
        }
        else
        {
            OnNearestTargetInAttackRadius();
        }









        //// 设置最近目标和攻击范围状态
        //m_NearestTarget = target;
        //m_NearestTargetInAttackRadius = inAttackRadius;

        //// 如果单位处于攻击状态，直接返回
        //if (m_UnitState == CombatUnitState.Attack)
        //    return;

        //if (!inAttackRadius)
        //{
        //    // 如果目标不在攻击范围内，进行移动状态处理
        //    if (m_UnitState != CombatUnitState.Move && m_UnitCanMove)
        //    {
        //        if (m_RVO == null)
        //            throw new System.NullReferenceException("RVO Agent is null");

        //        // 设置移动状态
        //        m_UnitState = CombatUnitState.Move;
        //        m_RVO.navigationEnabled = true;
        //        OnUnitStateChanged();
        //    }

        //    // 更新RVO目标的位置
        //    if (m_NearestTarget != null && m_NearestTarget.m_RVO != null)
        //    {
        //        if (m_RVO == null)
        //            throw new System.NullReferenceException("RVO Agent is null");

        //        m_RVO.targetPosition = m_NearestTarget.m_RVO.pos;
        //    }

        //}
        //else
        //{
        //   // Debug.Log(target.Id + " 已经进入攻击范围");

        //    // 如果单位状态不是闲置状态，切换为闲置状态
        //    if (m_UnitState != CombatUnitState.Idle)
        //    {
        //        if (m_RVO == null)
        //            throw new System.NullReferenceException("RVO Agent is null");

        //        // 设置闲置状态
        //        m_UnitState = CombatUnitState.Idle;
        //        m_RVO.navigationEnabled = false;
        //        OnUnitStateChanged();
        //    }
        //}
    }

    /// <summary>
    /// 当目标在攻击范围内的处理逻辑
    /// </summary>
    protected virtual void OnNearestTargetInAttackRadius()
    {
        // 检查最近目标和RVO状态
        if (m_NearestTarget == null || m_NearestTarget.m_RVO == null || m_RVO == null)
        {
            return;
        }

        //try
        //{
        float3 targetPosition;
        // 根据不同状态决定面向目标
        if (m_NearestTarget != null && m_NearestTarget.m_RVO != null)
        {
            // 如果有目标，面向目标
            targetPosition = m_NearestTarget.Position;
        }
        else if (m_UnitState == CombatUnitState.Move)
        {
            // 如果在移动状态，面向移动方向
            targetPosition = m_RVO.targetPosition;
        }
        else
        {
            return;
        }

        // 计算方向
        float3 direction = targetPosition - Position;

        // 如果距离太近，不更新朝向
        if (math.lengthsq(direction) < 0.01f) return;

        // 忽略Y轴差异
        direction.y = 0;
        direction = math.normalize(direction);

        // 计算目标旋转
        quaternion targetRotation = quaternion.LookRotation(direction, new float3(0, 1, 0));

        // 平滑旋转
        float rotationSpeed = 5f;
        m_RVO.rotation = math.slerp(m_RVO.rotation, targetRotation, Time.deltaTime  * rotationSpeed);

        //// 计算方向向量
        //float3 direction = targetPos - currentPos;

        //// 计算距离和归一化方向
        //float distance = math.length(direction);
        //float3 normalizedDir = math.normalize(direction);

        // 计算目标旋转
        //quaternion targetRotation = quaternion.LookRotation(
        //    new float3(normalizedDir.x, 0, normalizedDir.z), // 前方方向
        //    new float3(0, 1, 0)  // 上方方向
        //);

        //// 当前旋转
        //quaternion currentRotation = m_RVO.rotation;

        //// 用Slerp进行平滑旋转
        //m_RVO.rotation = math.slerp(
        //    currentRotation,
        //    targetRotation,
        //    Time.deltaTime * 100
        //);
        //}
        //catch (System.Exception e)
        //{
        //    Debug.LogError($"Error in OnNearestTargetInAttackRadius: {e.Message}");
        //}
    }

    /// <summary>
    /// 设置移动目标点
    /// </summary>
    /// <param name="pos">目标位置</param>
    public void SetMovePoint(float3 pos)
    {
        // 获取RVOAgent
        RVOAgent rvoAgent = this.m_RVO;

        // 检查RVOAgent是否被初始化
        if (rvoAgent == null)
        {
            Debug.LogError("RVOAgent is not initialized.");
            return;
        }

        // 设置目标位置
        rvoAgent.targetPosition = new Vector3(pos.x, rvoAgent.targetPosition.y, pos.z);
    }

    /// <summary>
    /// 应用伤害
    /// </summary>
    /// <param name="formCamp">伤害来源阵营</param>
    /// <param name="damageValue">伤害值</param>
    public void ApplyDamage(ORCALayer formCamp, int damageValue)
    {
        if (IsDead) return;  // 只在死亡时返回

        // 检查是否在受伤冷却时间内
        float currentTime = Time.time;
        if (currentTime - m_LastDamageTime < 1.0f) return;

        // 更新HP值
        mHp = Math.Max(0, mHp - damageValue);

        var batchRenderer = BatchRendererComponent.Instance;
        if (batchRenderer == null)
            throw new System.NullReferenceException("BatchRenderer is null");

        // 应用伤害效果
     
        m_RVO.SetAnimationBlink();
        //if (m_ShowHpBar)
        //{
        //    float hpRatio = (float)mHp / 100;
        //    m_RVO.SetAnimationBlink(hpRatio);
              
        //}
           


        // 处理状态变化
        if (IsDead && m_UnitState != CombatUnitState.Dead)
        {
            m_UnitState = CombatUnitState.Dead;
            if (m_RVO != null)
            {
                m_RVO.navigationEnabled = false;
                OnUnitStateChanged();
            }
            OnDead(formCamp);
        }
        // 如果单位没有死亡，切换到受伤状态
        else if (!IsDead && m_UnitState == CombatUnitState.Idle)
        {
            m_LastDamageTime = currentTime;
            m_UnitState = CombatUnitState.Damage;
            if (m_RVO != null)
            {
                m_RVO.navigationEnabled = false;
                OnUnitStateChanged();
            }
        }
    }

    /// <summary>
    /// 处理受伤逻辑
    /// </summary>
    private void OnDamage()
    {
        var batchRenderer = BatchRendererComponent.Instance;
        if (batchRenderer == null)
        {
            throw new NullReferenceException("BatchRendererComponent instance is null");
        }
        // 创建伤害效果
        float4 damageEffect = new float4(
            Time.time,  // 当前时间
            0,          // y
            0,          // z
            0           // w
        );
        // 应用伤害效果
      //  batchRenderer.SetRendererUserdataVec4(m_RendererId, damageEffect);
    }

    /// <summary>
    /// 处理单位死亡逻辑
    /// </summary>
    /// <param name="camp">死亡时的阵营</param>
    protected virtual void OnDead(ORCALayer camp)
    {
        // 通知玩家死亡事件
        onPlayerDeadCallback?.Invoke(m_Id);

        // 移除RVO代理
        var rvoComponent = RVOComponent.Instance;
        if (rvoComponent != null)
        {
            rvoComponent.RemoveAgent(m_RVO);
        }

        // 移除渲染效果
        var batchRenderer = BatchRendererComponent.Instance;
        if (batchRenderer != null)
        {
            batchRenderer.RemoveRenderer(m_RendererId);
            batchRenderer.RemoveRenderer(m_HpBarRendererId);
            

        }

        // 清空最近目标
        m_NearestTargetInAttackRadius = false;
        m_NearestTarget = null;

        // 清理攻击相关的目标
        if (m_UseAnimAttackEvent)
        {
            // 清理攻击目标点
            if (m_FireTargetPoints != null)
            {
                m_FireTargetPoints.Clear();
            }
            else if (m_AttackTargets != null)
            {
                // 清理攻击目标
                m_AttackTargets.Clear();
            }
        }
    }

    /// <summary>
    /// 处理GPU动画事件
    /// </summary>
    /// <param name="resIndex">资源索引</param>
    /// <param name="clipIndex">动画片段索引</param>
    /// <param name="triggerAtFrame">触发帧</param>
    internal void OnGPUAnimationEvent(int resIndex, int clipIndex, int triggerAtFrame)
    {
        // 检查是否使用动画攻击事件，单位状态是否为死亡，并且当前片段索引是否为2
        if (m_UseAnimAttackEvent && m_UnitState != CombatUnitState.Dead && clipIndex == (int)CombatUnitState.Attack)
        {
            // 确保攻击目标字典不为空
            if (m_AttackTargets == null)
            {
                throw new System.NullReferenceException("Attack targets dictionary is null");
            }

            try
            {
                // 遍历攻击目标列表
                foreach (var kvp in m_AttackTargets)
                {
                    BRG_Player targetPlayer = kvp.Value; // 获取目标玩家对象
                    if (targetPlayer != null)
                    {
                        // 检查目标玩家的ID是否与当前攻击目标的ID匹配
                        if (kvp.Key == targetPlayer.Id)
                        {
                            // 应用伤害给匹配的目标玩家
                            targetPlayer.ApplyDamage(CampInfo.Camp, m_DamageValue);
                        }
                    }
                }

                // 清空攻击目标列表，以便后续使用
                m_AttackTargets.Clear();
            }
            catch (System.Exception e)
            {
                // 捕获并记录异常信息
                Debug.LogError($"Error in OnGPUAnimationEvent: {e.Message}");
            }
        }
    }

    /// <summary>
    /// 解析战斗单位的攻击类型
    /// </summary>
    private void ParseCombatUnitAttackType()
    {
        this.AttackType = 0; // 初始化攻击类型为0
        this.m_FarAttack = false; // 初始化远程攻击标志为false
    }


}
