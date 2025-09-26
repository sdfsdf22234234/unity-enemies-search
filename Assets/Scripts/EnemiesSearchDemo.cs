using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Nebukam.ORCA;
using UnityEngine.UI;
using Unity.Collections;
using Unity.Entities.UniversalDelegates;

public class EnemiesSearchDemo : MonoBehaviour
{
    /// <summary>
    /// 地图半径
    /// </summary>
    [SerializeField] float m_MapRadius = 50;  // 地图半径，控制游戏地图的大小范围

    [SerializeField] float m_SpawnEnemiesInterval = 0.5f;  // 刷怪间隔，每隔多久刷一个敌人

    [SerializeField] float m_RvoMoveSpeed = 10f;  // RVO 代理的移动速度

    [SerializeField] float m_RvoPanding = 1.5f;  // RVO 代理的膨胀半径，用于避让障碍物

    [SerializeField] float m_SpawnCount = 20;  // 每次刷怪的数量
   
    [SerializeField] int m_MaxPlayerCount = 10000;  // 最大玩家数量限制

    [SerializeField] Text m_AgentsCountText;  // 用于显示代理数量的 Text 对象

    [SerializeField] EdgeObstacle m_Obs;  // 边缘障碍物对象

    Vector3[] m_SpawnPoints;  // 刷怪点数组，用于存储可以刷怪的位置坐标
   



    RVOComponent rvo;


    private float searchTargetTimer;
    float m_Timer = 0;
    float4[] m_Colors = new float4[] { new float4(1, 0, 0, 1), new float4(0, 0, 1, 1) };

    Dictionary<int, Player> m_Players;

    [System.Obsolete]
    void Start()
    {
       
        Application.targetFrameRate = -1;
        rvo = FindObjectOfType<RVOComponent>();
        m_Players = new Dictionary<int, Player>(m_MaxPlayerCount);
        int campCount = 2;
        m_SpawnPoints = new Vector3[campCount];
        float angleDelta = 360f / campCount;
        for (int i = 0; i < campCount; i++)
        {
            m_SpawnPoints[i] = Quaternion.Euler(0, i * angleDelta, 0) * Vector3.right * m_MapRadius;
        }

        m_Obs?.AddObstacles();

       
    }

    // Update is called once per frame
    void Update()
    {
        m_AgentsCountText.text = m_Players.Count.ToString();

        SpawnPlayerUpdate();
      SearchAndAttackTargetUpdate();
    }
    void SearchAndAttackTargetUpdate()
    {
        if (!rvo.TryGetAgentData(out var queryAgents)) return;
        //统一指定每个Agent搜索目标个数
        int agentSearchCount = 1;
        var nearestIndexes = rvo.SearchAgentsWithin(queryAgents, agentSearchCount);
        if (nearestIndexes.IsCreated)
        {
            for (int i = 0; i < nearestIndexes.Length; i += agentSearchCount)
            {
                for (int j = 0; j < agentSearchCount; j++)
                {
                    var agentDt = queryAgents[i];
                    if (!m_Players.TryGetValue(agentDt.id, out Player attacker)) continue;

                    int attTargetId = nearestIndexes[i * agentSearchCount + j];
                    if (attTargetId <= 0 || !m_Players.TryGetValue(attTargetId, out Player damagePlayer))
                    {
                        // attacker.ClearState();
                        attacker.GoForward();//如果没有攻击目标,继续前进
                        continue;
                    }

                    attacker.TryAttackTarget(damagePlayer);
                    if (damagePlayer.Hp <= 0)
                    {
                        m_Players.Remove(attTargetId);
                    }
                }
            }
            nearestIndexes.Dispose();
        }

        /*以下是以每个Agent各自的SearchCount个数锁敌*/

        //根据Agent各自的搜索目标个数; searchCount为1,单体伤害; >1即群体伤害
        //var queryAgents = provider.outputAgents;
        //var nearestIndexes = rvo.SearchAgentsWithin(queryAgents);

        //int nextAgentBeginIdx = 0; //根据每个Agent群伤个数偏移得到下一个Agent群伤目标起始索引
        //for (int i = 0; i < queryAgents.Length; i++)
        //{
        //    var agentDt = queryAgents[i];
        //    for (int attIdx = 0; attIdx < agentDt.searchCount; attIdx++)
        //    {
        //        if (!m_Players.ContainsKey(agentDt.id)) continue;
        //        int attTargetId = nearestIndexes[nextAgentBeginIdx + attIdx];

        //        if (attTargetId < 0 || !m_Players.ContainsKey(attTargetId))
        //        {
        //            m_Players[agentDt.id].GoForward();//如果没有攻击目标,继续前进
        //            break;
        //        }

        //        var targetPlayer = m_Players[attTargetId];
        //        //Debug.DrawLine(attackerAgent.pos, targetAgent.pos);
        //        m_Players[agentDt.id].TryAttackTarget(targetPlayer);
        //        if (targetPlayer.Hp <= 0)
        //        {
        //            m_Players.Remove(attTargetId);
        //        }
        //    }
        //    nextAgentBeginIdx += agentDt.searchCount;
        //}
    }






    public void SearchAndAttackUpdate(float deltaTime)
    {

        searchTargetTimer += deltaTime;

        if (searchTargetTimer >= 0.2f)
        {
            searchTargetTimer = 0.0f; // 重置计时器

            if (!rvo.TryGetAgentData(out var agentData)) return;


            var nearbyAgents = rvo.SearchAgentsNearest(agentData);




        }
         
        }
    public void AOETest()
    {
        float damageRadius = UnityEngine.Random.Range(10, 20);
        var randomXZ = UnityEngine.Random.insideUnitCircle;
        var randomPos = new Vector3(randomXZ.x, 0, randomXZ.y) * UnityEngine.Random.Range(0, 50);
        NativeArray<float3> tempQueryPoints = new NativeArray<float3>(1, Allocator.TempJob);
        tempQueryPoints[0] = randomPos;

        int maxDamageCount = Mathf.CeilToInt(Mathf.PI * Mathf.Pow(damageRadius, 2)) * 2; //根据伤害半径动态计算群伤单位最大数
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
                    m_Players[agentId].ApplyDamage(1);
                }
            }
        }
        ids.Dispose();
        tempQueryPoints.Dispose();
    }
    private void SpawnPlayerUpdate()
    {
        if (m_Players.Count + m_SpawnCount <= m_MaxPlayerCount && (m_Timer += Time.deltaTime) > m_SpawnEnemiesInterval)
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
                    SpawnPlayer((ORCALayer)i + 1, pos, targetPos);
                }
            }
        }
    }

    private void SpawnPlayer(ORCALayer campId, float3 pos, float3 targetPos)
    {
        var player = new Player(campId, UnityEngine.Random.Range(2, 10), m_Colors[(int)campId - 1], pos, targetPos, m_RvoMoveSpeed);

        m_Players.Add(player.Id, player);
    }

    /// <summary>
    /// 获取到所有攻击冷却完的Agent Id
    /// </summary>
    //[BurstCompile]
    //private struct GetCanAttackAgentsJob : IJobParallelFor
    //{
    //    [ReadOnly]
    //    public NativeArray<AgentData> agents;
    //    [ReadOnly]
    //    public float time;
    //    [WriteOnly]
    //    public NativeParallelHashSet<AgentData>.ParallelWriter attackerIds;
    //    public void Execute(int index)
    //    {
    //        var agent = agents[index];
    //        if (time - agent.attackTimer >= agent.attackInterval)
    //        {
    //            attackerIds.Add(agent);
    //        }
    //    }
    //}
}