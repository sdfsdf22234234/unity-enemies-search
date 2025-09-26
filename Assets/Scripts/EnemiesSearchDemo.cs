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
    /// ��ͼ�뾶
    /// </summary>
    [SerializeField] float m_MapRadius = 50;  // ��ͼ�뾶��������Ϸ��ͼ�Ĵ�С��Χ

    [SerializeField] float m_SpawnEnemiesInterval = 0.5f;  // ˢ�ּ����ÿ�����ˢһ������

    [SerializeField] float m_RvoMoveSpeed = 10f;  // RVO ������ƶ��ٶ�

    [SerializeField] float m_RvoPanding = 1.5f;  // RVO ��������Ͱ뾶�����ڱ����ϰ���

    [SerializeField] float m_SpawnCount = 20;  // ÿ��ˢ�ֵ�����
   
    [SerializeField] int m_MaxPlayerCount = 10000;  // ��������������

    [SerializeField] Text m_AgentsCountText;  // ������ʾ���������� Text ����

    [SerializeField] EdgeObstacle m_Obs;  // ��Ե�ϰ������

    Vector3[] m_SpawnPoints;  // ˢ�ֵ����飬���ڴ洢����ˢ�ֵ�λ������
   



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
        //ͳһָ��ÿ��Agent����Ŀ�����
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
                        attacker.GoForward();//���û�й���Ŀ��,����ǰ��
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

        /*��������ÿ��Agent���Ե�SearchCount��������*/

        //����Agent���Ե�����Ŀ�����; searchCountΪ1,�����˺�; >1��Ⱥ���˺�
        //var queryAgents = provider.outputAgents;
        //var nearestIndexes = rvo.SearchAgentsWithin(queryAgents);

        //int nextAgentBeginIdx = 0; //����ÿ��AgentȺ�˸���ƫ�Ƶõ���һ��AgentȺ��Ŀ����ʼ����
        //for (int i = 0; i < queryAgents.Length; i++)
        //{
        //    var agentDt = queryAgents[i];
        //    for (int attIdx = 0; attIdx < agentDt.searchCount; attIdx++)
        //    {
        //        if (!m_Players.ContainsKey(agentDt.id)) continue;
        //        int attTargetId = nearestIndexes[nextAgentBeginIdx + attIdx];

        //        if (attTargetId < 0 || !m_Players.ContainsKey(attTargetId))
        //        {
        //            m_Players[agentDt.id].GoForward();//���û�й���Ŀ��,����ǰ��
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
            searchTargetTimer = 0.0f; // ���ü�ʱ��

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

        int maxDamageCount = Mathf.CeilToInt(Mathf.PI * Mathf.Pow(damageRadius, 2)) * 2; //�����˺��뾶��̬����Ⱥ�˵�λ�����
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
    /// ��ȡ�����й�����ȴ���Agent Id
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