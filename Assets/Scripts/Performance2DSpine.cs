using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public class Performance2DSpine : MonoBehaviour
{
    private enum AnimMode
    {
        Spine = 0,
        GPUAnimation = 1,
        GPUAnimationBRG = 2
    }

    [SerializeField]
    private int m_SpawnCount;

    [SerializeField]
    private int m_PerRowCount;

    [SerializeField]
    private float m_PerPadding;

    [SerializeField]
    private GameObject[] m_Prefabs;

    [SerializeField]
    private Dropdown m_Dropdown;

    [SerializeField]
    private bool m_UseECSGraphics;

    private BatchRendererComponent m_BRG;

    private ECSGraphicsComponent m_ECS;

    [SerializeField]
    private AnimMode m_AnimMode;

    private GameObject m_InstanceRoot;

    private void Start()
    {
        Application.targetFrameRate = -1;
        m_BRG = FindObjectOfType<BatchRendererComponent>();
        m_ECS = ECSGraphicsComponent.Instance;
        if (!m_UseECSGraphics)
        {
            if (m_Dropdown != null)
            {
                List<Dropdown.OptionData> options = m_Dropdown.options;
                if (options != null && options.Count > 0)
                {
                    Dropdown.OptionData lastOption = options[options.Count - 1];
                    if (lastOption != null)
                    {
                        lastOption.text = "GPU Animation BRG"; 

                        if (m_BRG != null && m_BRG.GetResourceCount() < 1)
                        {
                            Debug.LogWarning("BatchRendererComponent: please add renderer resource!");
                            return;
                        }

                        Spawn(m_AnimMode);
                        return;
                    }
                }
            }
            else
            {
                if (m_Dropdown != null)
                {
                    List<Dropdown.OptionData> options = m_Dropdown.options;
                    if (options != null && options.Count > 0)
                    {
                        Dropdown.OptionData lastOption = options[options.Count - 1];
                        if (lastOption != null)
                        {
                            lastOption.text = "ECS Graphics"; // Replace with actual string literal

                            if (m_ECS != null && m_ECS.ECSRendersCount >= 1)
                            {
                                Spawn(m_AnimMode);
                                return;
                            }

                            Debug.LogWarning("ECSGraphicsComponent: please add renderer resource!");
                        }
                    }
                }
 
            }

        }




    }

    private void Spawn(AnimMode mode)
    {
        if (!m_UseECSGraphics)
        {
            if (m_BRG != null)
            {
                m_BRG.RemoveAllRenderers();
            }
        }
        else
        {
            if (m_ECS != null)
            {
                m_ECS.RemoveAll();
            }
        }

        if (m_InstanceRoot != null)
        {
            Transform rootTransform = m_InstanceRoot.transform;
            for (int i = rootTransform.childCount - 1; i >= 0; i--)
            {
                Destroy(rootTransform.GetChild(i).gameObject);
            }
        }

        float halfRowCount = m_PerRowCount * 0.5f * m_PerPadding;
        for (int i = 0; i < m_SpawnCount; i++)
        {
            float xOffset = (i % m_PerRowCount) * m_PerPadding - halfRowCount;
            float zOffset = (i / m_PerRowCount) * m_PerPadding;

            if (mode == 0)
            {
                if (m_Prefabs != null && m_Prefabs.Length > 0)
                {
                    Instantiate(m_Prefabs[0], new Vector3(xOffset, 0, zOffset), quaternion.identity, m_InstanceRoot.transform);
                }
            }
            else if (mode == AnimMode.GPUAnimation)
            {
                if (m_Prefabs != null && m_Prefabs.Length > 1)
                {
                    GameObject instance = Instantiate(m_Prefabs[1], new Vector3(xOffset, 0, zOffset), quaternion.identity, m_InstanceRoot.transform);
                    //GPUAnimation animation = instance.GetComponent<GPUAnimation>();
                    //if (animation != null)
                    //{
                    //    Destroy(animation.gameObject);
                    //}
                }
            }
            else if (mode == AnimMode.GPUAnimationBRG)
            {
                if (m_UseECSGraphics)
                {
                    float3 position = new float3(xOffset, 0, zOffset);
                    quaternion rotation = quaternion.identity;
                    m_ECS.Add(0,position, rotation, 1.0f, -1);
                }
                else
                {
                    float3 position = new float3(xOffset, 0, zOffset);
                    quaternion rotation = quaternion.identity;
                    m_BRG.AddRenderer(0,position, rotation, new float3(1, 1, 1));
                }
            }
        }
    }

    public void OnAnimModeChanged(Dropdown drop)
    {
        if (drop != null)
        {
            m_AnimMode =(AnimMode)drop.value;
            Spawn(m_AnimMode);
        }

       
    }
}
