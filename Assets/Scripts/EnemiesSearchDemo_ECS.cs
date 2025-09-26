using System;
using System.Collections.Generic;
using Nebukam.ORCA;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;


public class EnemiesSearchDemo_ECS : MonoBehaviour
{
    [Obsolete]
    private void Start()
    {
        Application.targetFrameRate = -1;
        rvo = FindObjectOfType<RVOComponent>();
        m_Players = new Dictionary<int, BRG_Player>(m_MaxPlayerCount);
        m_SpawnPoints = new Vector3[2];

        for (int i = 0; i < 2; i++)
        {
       
            float angle = i * 180f * Mathf.Deg2Rad;

          
            Quaternion rotation = Quaternion.Euler(0, angle * Mathf.Rad2Deg, 0);

       
            Vector3 direction = rotation * Vector3.right;
            Vector3 position = direction * m_MapRadius;

            m_SpawnPoints[i] = position;
        }

        if (m_Obs != null)
        {
            m_Obs.AddObstacles();
        }

        if (m_AoeBtn != null)
        {
           
            m_AoeBtn.onClick.RemoveAllListeners();

      
            m_AoeBtn.onClick.AddListener(AOETest);
        }


    }



    private void Update()
    {
        if (m_Players == null || m_AgentsCountText == null)
            return;

        m_AgentsCountText.text = m_Players.Count.ToString();

        float deltaTime = Time.deltaTime;
        foreach (var player in m_Players.Values)
        {
            if (player != null)
            {
                player.UpdateLogic(deltaTime);
            }
        }

        SpawnPlayerUpdate();

   
        SearchAndAttackTargetUpdate();

        GPUAnimTriggerUpdate();

    }




    private void GPUAnimTriggerUpdate()
    {
        var batchRenderer = BatchRendererComponent.Instance;
        if (batchRenderer == null)
            return;
        var triggerResults = new NativeQueue<GPUAnimTriggerInfo>(Allocator.Temp);

        if (batchRenderer.TryTriggerGPUAnimationEvents(out triggerResults))
        {
            GPUAnimTriggerInfo triggerInfo;


            while (triggerResults.TryDequeue(out triggerInfo))
            {
                if (m_Players == null)
                    break;


                if (m_Players.TryGetValue(triggerInfo.AgentId, out BRG_Player player))
                {
                    if (player == null)
                        break;


                    player.OnGPUAnimationEvent(
                        triggerInfo.TriggerKey.x,
                        triggerInfo.TriggerKey.y,
                        triggerInfo.TriggerKey.z
                    );
                }
            }

        }
        if (triggerResults.IsCreated)
            triggerResults.Dispose();

    }



    private void SearchAndAttackTargetUpdate()
    {
        NativeArray<AgentData> agentData = default;
        NativeList<AgentData> agentList = new NativeList<AgentData>(Allocator.Temp);
        NativeArray<int2> nearestAgents = default;

 
        if (rvo == null) return;

       
        if (!rvo.TryGetAgentData(out agentData)) return;

       
        nearestAgents = rvo.SearchAgentsNearestAndCheck(agentData);

        if (nearestAgents.IsCreated && nearestAgents.Length > 0)
        {
            for (int i = 0; i < nearestAgents.Length; i++)
            {
                int2 agentInfo = nearestAgents[i];
                int targetId = agentInfo.x;
                bool isInRange = agentInfo.y != 0;

                if (targetId == -1) continue;

                AgentData currentAgent = agentData[i];

        
                    if (!m_Players.TryGetValue(currentAgent.id, out BRG_Player currentPlayer)) continue;
                    if (!m_Players.TryGetValue(targetId, out BRG_Player targetPlayer)) continue;

                   
                    currentPlayer.UpdateNearestTarget(targetPlayer, isInRange);

                    if (currentPlayer.UnitState == CombatUnitState.Dead ||
                   currentPlayer.UnitState == CombatUnitState.Damage ||
                   currentPlayer.UnitState == CombatUnitState.Attack)
                        continue;

                  
                    if (isInRange)
                    {
                        if (currentPlayer.CheckAttackCD() &&
                            (currentPlayer.UnitState == CombatUnitState.Idle ||
                             currentPlayer.UnitState == CombatUnitState.Move))
                        {
                            currentPlayer.Attack(targetPlayer);
                        }
                    }
                    else if (currentPlayer.UnitState == CombatUnitState.Idle)
                    {
                        currentPlayer.UnitState = CombatUnitState.Move;
                    }






                
            }
        }

        if (nearestAgents.IsCreated)
            nearestAgents.Dispose();
 






    }




 


    public void AOETest()
    {
        float damageRadius = UnityEngine.Random.Range(10, 20);
        var randomXZ = UnityEngine.Random.insideUnitCircle;
        var randomPos = new Vector3(randomXZ.x, 0, randomXZ.y) * UnityEngine.Random.Range(0, 50);
        NativeArray<float3> tempQueryPoints = new NativeArray<float3>(1, Allocator.TempJob);
        tempQueryPoints[0] = randomPos;

        int maxDamageCount = Mathf.CeilToInt(Mathf.PI * Mathf.Pow(damageRadius, 2)) * 2;  
        var ids = rvo.SearchAgentsWithin(tempQueryPoints, damageRadius,  maxDamageCount);

        for (int i = 0; i < ids.Length; i += maxDamageCount)
        {
            int indexOffset = i * maxDamageCount;
            for (int j = 0; j < maxDamageCount; j++)
            {
                int agentId = ids[indexOffset + j];
                if (agentId <= 0) break;

                if (m_Players.ContainsKey(agentId))
                {
                    m_Players[agentId].ApplyDamage(ORCALayer.ANY,  
                damageValue: 100);
                }
            }
        }

        ids.Dispose();
        tempQueryPoints.Dispose();

    }



    private void SpawnPlayerUpdate()
    {
        if (m_Players == null) return;
        float totalPlayers = m_Players.Count + m_SpawnCount;
        if (m_MaxPlayerCount < totalPlayers) return;
        m_Timer += Time.deltaTime;
        if (m_Timer <= m_SpawnEnemiesInterval) return;
        m_Timer = 0f;



        if (m_Players.Count + m_SpawnCount <= m_MaxPlayerCount)
        {
            m_Timer = 0;
            for (int i = 0; i < m_SpawnPoints.Length; i++)
            {
                var spawnPoint = m_SpawnPoints[i];
                spawnPoint.z += m_SpawnCount * 0.5f * m_RvoPanding;
                for (int j = 0; j < m_SpawnCount; j++)
                {
                    var pos = spawnPoint - Vector3.forward * j * m_RvoPanding;
                    var targetPos = pos;
                    targetPos.x = m_SpawnPoints[(i + 1) % 2].x;
                    int teamId = (i == 0) ? 1 : 2;
                    SpawnPlayer((ORCALayer)teamId, pos, targetPos, Quaternion.identity);

                }
            }
        }

 
    }







    private void SpawnPlayer(ORCALayer camp, float3 pos, float3 targetPos, quaternion rot)
    {
        int newEntityId = ++m_EntityId;

    
        Action<int> playerDeadCallback = OnPlayerDead;

      
        BRG_Player player = new BRG_Player(newEntityId, camp, pos, OnPlayerDead);

        if (player.Id > 0)
            m_Players.Add(player.Id, player);
    }



    private void OnPlayerDead(int playerId)
    {
        if (m_Players != null)
        {
            m_Players.Remove(playerId);
        }
    }



    public EnemiesSearchDemo_ECS()
    {
        this.m_MapRadius = 50.0f;
        this.m_SpawnEnemiesInterval = 0.5f;
        this.m_RvoMoveSpeed = 10.0f;
        this.m_RvoPanding = 1.5f;
        this.m_SpawnCount = 20.0f;
        this.m_MaxPlayerCount = 10000;
        if (m_Colors == null || m_Colors.Length == 0)
        {
            m_Colors = new float4[]
            {
                new float4(1, 0, 0, 1),   
                new float4(0, 0, 1, 1)   
            };
        }


    }



    /// <summary>
    /// 实体ID
    /// </summary>
    private static int m_EntityId;

    [Header("地图半径")]
    [SerializeField]
    private float m_MapRadius;

    [Header("生成敌人的时间间隔")]
    [SerializeField]
    private float m_SpawnEnemiesInterval;

    [Header("RVO移动速度")]
    [SerializeField]
    private float m_RvoMoveSpeed;

    [Header("RVO偏移量")]
    [SerializeField]
    private float m_RvoPanding;

    [Header("生成敌人的数量")]
    [SerializeField]
    private float m_SpawnCount;

    [Header("最大玩家数量")]
    [SerializeField]
    private int m_MaxPlayerCount;

    [Header("当前玩家数量文本")]
    [SerializeField]
    private Text m_AgentsCountText;

    [Header("障碍物")]
    [SerializeField]
    private EdgeObstacle m_Obs;

    [Header("AOE按钮")]
    [SerializeField]
    private Button m_AoeBtn;

    // 存储生成点位置
    private Vector3[] m_SpawnPoints;

    // RVO主类实例
    private RVOComponent rvo;

    // 计时器
    private float m_Timer;

    // 存储角色颜色
    private float4[] m_Colors;

    // 存储玩家的字典
    private Dictionary<int, BRG_Player> m_Players;
}
