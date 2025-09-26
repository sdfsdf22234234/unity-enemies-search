using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JobsRVO.Demo
{
    public class Demo : MonoBehaviour
    {
        [SerializeField] RVOComponent m_rvoComponent;
        [SerializeField] int m_CreateRvoAgentCount = 100;
        [SerializeField] GameObject m_Prefab;

        [SerializeField] GameObject m_Obstacles;

        [SerializeField] Transform m_RvoFollowTarget;

        private List<RVOAgent> m_Agents;
        void Start()
        {
            m_Obstacles.SetActive(true);
            m_Agents = new List<RVOAgent>();
            for (int i = 0; i < m_CreateRvoAgentCount; i++)
            {
                var go = Instantiate(m_Prefab, transform);
                var randomVec2 = UnityEngine.Random.insideUnitCircle * 2;
                go.transform.position = new Vector3(randomVec2.x, 0, randomVec2.y);

                var agent = m_rvoComponent.AddAgent(go.transform);
                m_Agents.Add(agent);
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (m_RvoFollowTarget == null) return;

            foreach (var agent in m_Agents)
            {
                agent.targetPosition = m_RvoFollowTarget.position;
            }
        }
    }
}

