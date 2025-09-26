using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public class PerformanceDemo1 : MonoBehaviour
{
    [SerializeField] int m_SpawnCount = 10000;
    [SerializeField] int m_PerRowCount = 100;
    [SerializeField] float m_PerPadding = 2f;
    [SerializeField] GameObject[] m_Prefabs;
    BatchRendererComponent m_BRG;

    enum AnimMode
    {
        Animator = 0,
        GPUAnimation,
        GPUAnimationBRG
    }

    [SerializeField] AnimMode m_AnimMode = AnimMode.Animator;

    GameObject m_InstanceRoot;
    private Dictionary<int, RendererNodeId> m_Players;
    [System.Obsolete]
    void Start()
    {
        m_BRG = GameObject.FindObjectOfType<BatchRendererComponent>();
        m_Players = new Dictionary<int, RendererNodeId>();
        if (m_BRG.GetResourceCount() < 1)
        {
            Debug.LogWarning($"BatchRendererComponent: please add renderer resource!");
            return;
        }
        m_InstanceRoot = new GameObject("Root");
        Spawn(m_AnimMode);
    }

    private void Spawn(AnimMode mode)
    {
        m_BRG.RemoveAllRenderers();
        m_Players.Clear();
        for (int i = m_InstanceRoot.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(m_InstanceRoot.transform.GetChild(i).gameObject);
        }   
        float offsetX = m_PerRowCount * 0.5f * m_PerPadding;
        for (int i = 0; i < m_SpawnCount; ++i)
        {   
            int row = i % m_PerRowCount;
            var pos = new Vector3(row * m_PerPadding - offsetX, 0, i / m_PerRowCount * m_PerPadding);
            var rotation = Quaternion.identity;
            switch (mode)
            {
                case AnimMode.Animator:
                    {
                        var go = Instantiate(m_Prefabs[(int)mode], pos, rotation, m_InstanceRoot.transform);
                        var animator = go.GetComponent<Animator>();
                        var clips = animator.runtimeAnimatorController.animationClips;
                        animator.Play(clips[row % clips.Length].name);
                    }
                    break;
                case AnimMode.GPUAnimation:
                    {
                        var go = Instantiate(m_Prefabs[(int)mode], pos, rotation, m_InstanceRoot.transform);
                        go.GetComponent<MeshRenderer>().material.SetVector("_ClipId", new Vector4(row % 5, 0, 0, 0));
                    }
                    break;
                case AnimMode.GPUAnimationBRG:
                    var renderId = m_BRG.AddRenderer(0, pos, rotation, Vector3.one);
                    m_BRG.SetRendererClipId(renderId, row % 5);
                  
                    m_Players.Add(i,renderId);
                    break;
            }
        }
    }

 

    void Update()
    {
       
            //Debug.Log("F键被按下"); // 添加这行来测试按键是否被检
           
        
    }
    public void OnAnimModeChanged(Dropdown drop)
    {
        m_AnimMode = (AnimMode)drop.value;
        Spawn(m_AnimMode);
    }
}
