using System;
using System.Collections.Generic;
using System.Linq;
using Nebukam.ORCA;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;



public class EnemiesSearchDemo_Old : MonoBehaviour
{



    private void Start()
    {



        Application.targetFrameRate = -1;
        rvo = FindObjectOfType<RVOComponent>();
        m_Players = new Dictionary<int, BRG_Player_Old>(m_MaxPlayerCount);
        int campCount = 2;
        m_SpawnPoints = new Vector3[campCount];
        float angleDelta = 360f / campCount;
        for (int i = 0; i < campCount; i++)
        {
            m_SpawnPoints[i] = Quaternion.Euler(0, i * angleDelta, 0) * Vector3.right * m_MapRadius;
        }

        m_Obs?.AddObstacles();



    }




    private void FixedUpdate()
    {
        m_AgentsCountText.text = m_Players.Count.ToString();

        SpawnPlayerUpdate();
        SearchAndAttackTargetUpdate();
    }




    private void SearchAndAttackTargetUpdate()
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
                    if (!m_Players.TryGetValue(agentDt.id, out BRG_Player_Old attacker)) continue;

                    int attTargetId = nearestIndexes[i * agentSearchCount + j];
                    if (attTargetId <= 0 || !m_Players.TryGetValue(attTargetId, out BRG_Player_Old damagePlayer))
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
    }




    public void AOETest()
    {
        float damageRadius = UnityEngine.Random.Range(10, 20);
        var randomXZ = UnityEngine.Random.insideUnitCircle;
        var randomPos = new Vector3(randomXZ.x, 0, randomXZ.y) * UnityEngine.Random.Range(0, 50);
        NativeArray<float3> tempQueryPoints = new NativeArray<float3>(1, Allocator.TempJob);
        tempQueryPoints[0] = randomPos;

        int maxDamageCount = Mathf.CeilToInt(Mathf.PI * Mathf.Pow(damageRadius, 2)) * 2; //�����˺��뾶��̬����Ⱥ�˵�λ�����
        var ids = rvo.SearchAgentsWithin(tempQueryPoints, damageRadius, maxDamageCount);
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
                    SpawnPlayer((ORCALayer)i+1, pos, targetPos);
                }
            }
        }
    }




    private void SpawnPlayer(ORCALayer campId, float3 pos, float3 targetPos)
    {
        int health = Random.Range(2, 10);

        // �����Ӫ��ɫ�Ƿ����
        if (m_Colors == null ||  ((int)campId) > m_Colors.Count())
        {
            Debug.LogError("Invalid camp ID or colors dictionary not initialized");
            return;
        }

        // ��ȡ��Ӫ��Ӧ����ɫ
        float4 color = m_Colors[(int)campId-1];

        // �����µ���Ҷ���
        BRG_Player_Old player = new BRG_Player_Old(
            campId,          // ��ӪID
            health,          // ����ֵ
            color,           // ��ɫ
            pos,             // ��ʼλ��
            targetPos,       // Ŀ��λ��
            m_RvoMoveSpeed   // �ƶ��ٶ�
        );

        // �����Һ�����ֵ��Ƿ���Ч
        if (player == null || m_Players == null)
        {
            Debug.LogError("Failed to create player or players dictionary not initialized");
            return;
        }

        // �������ӵ��ֵ���
        m_Players.Add(player.Id, player);
    }




    public EnemiesSearchDemo_Old()
    {
        m_Colors = new float4[2];

        // ����������Ӫ����ɫ
        // �ӷ���������п�������ʹ����Ԥ�������ɫֵ
        // ����ʹ�ú�ɫ����ɫ��Ϊʾ��
        m_Colors[0] = new float4(1, 0, 0, 1);  // ��ɫ (RGBA)
        m_Colors[1] = new float4(0, 0, 1, 1);  // ��ɫ (RGBA)
    }




    [SerializeField]
    private float m_MapRadius= 50.0f;               // ��ͼ�뾶




    [SerializeField]
    private float m_SpawnEnemiesInterval = 0.5f;     // ���ɵ��˵�ʱ����




    [SerializeField]
    private float m_RvoMoveSpeed = 10.0f;            // RVO�ƶ��ٶ�




    [SerializeField]
    private float m_RvoPanding = 1.5f;               // RVO�߾�




    [SerializeField]
    private float m_SpawnCount = 20.0f;              // ��������




    [SerializeField]
    private int m_MaxPlayerCount = 10000;            // ����������




    [SerializeField]
    private Text m_AgentsCountText;




    [SerializeField]
    private EdgeObstacle m_Obs;




    [SerializeField]
    private Button m_AoeBtn;




    private Vector3[] m_SpawnPoints;




    private RVOComponent rvo;




    private float m_Timer;




    private float4[] m_Colors;




    private Dictionary<int, BRG_Player_Old> m_Players;




    private float m_SearchInterval = 0.02f;          // �������




    private float m_SearchTimer = 0f;               // ������ʱ��
}
