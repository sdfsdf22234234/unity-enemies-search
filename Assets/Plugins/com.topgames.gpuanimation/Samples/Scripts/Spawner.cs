using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GPUAnimation.Runtime.Samples
{

    public class Spawner : MonoBehaviour
    {
        [SerializeField] int m_SpawnCount = 10000;
        [SerializeField] int m_PerRowCount = 100;
        [SerializeField] float m_PerPadding = 2f;
        [SerializeField] GameObject m_Prefab;

        void Start()
        {
            Spawn();
        }

        // Update is called once per frame
        void Update()
        {

        }

        private void Spawn()
        {
            if (m_Prefab == null) return;

            float offsetX = m_PerRowCount * 0.5f * m_PerPadding;
            for (int i = 0; i < m_SpawnCount; ++i)
            {
                int row = i % m_PerRowCount;
                var go = Instantiate(m_Prefab, new Vector3(row * m_PerPadding - offsetX, 0, i / m_PerRowCount * m_PerPadding), Quaternion.identity, transform);
                //_ClipId为Shader预留float4字段,即x,y,z,w，其中x用于动画index
                //随机设置一个动画
                int animIndex = UnityEngine.Random.Range(0, 5);
                float animStartTime = Time.time;
                var allMeshRd = go.GetComponentsInChildren<MeshRenderer>();
                for (int j = 0; j < allMeshRd.Length; ++j)
                {
                    foreach (var mat in allMeshRd[j].materials)
                    {
                        mat.SetVector("_ClipId", new Vector4(animIndex, animStartTime, 0, 0));
                    }
                }
            }
        }
    }

}