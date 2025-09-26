using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BRG_Demo : MonoBehaviour
{
    [SerializeField] int m_SpawnCount = 10000;
    [SerializeField] int m_PerRowCount = 100;
    [SerializeField] float m_PerPadding = 2f;
    BatchRendererComponent m_BRG;
    void Start()
    {
        m_BRG = GameObject.FindObjectOfType<BatchRendererComponent>();
        Spawn();
    }

    private void Spawn()
    {
        if (m_BRG.GetResourceCount() < 1)
        {
            Debug.LogWarning($"BatchRendererComponent: please add renderer resource!");
            return;
        }
        float offsetX = m_PerRowCount * 0.5f * m_PerPadding;
        for (int i = 0; i < m_SpawnCount; ++i)
        {
            int row = i % m_PerRowCount;
            var id = m_BRG.AddRenderer(UnityEngine.Random.Range(0, m_BRG.GetResourceCount()), new Vector3(row * m_PerPadding - offsetX, 0, i / m_PerRowCount * m_PerPadding), Quaternion.identity, Vector3.one);
        }
    }
}
