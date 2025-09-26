using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;



public class ECSGPerformance : MonoBehaviour
{



    private void Start()
    {
        Application.targetFrameRate = -1;
        m_BRG = ECSGraphicsComponent.Instance;
        if (m_BRG == null)
        {
          
            return;
        }
        if (m_BRG.ECSRendersCount >= 1)
     
        {
            
            m_InstanceRoot = new GameObject("InstanceRoot"); 
           Spawn(m_AnimMode);
        }
    }




    private void Spawn(ECSGPerformance.AnimMode mode)
    {
        float4x4 transformMatrix = new float4x4();
        transformMatrix.c3.x = 0.0f;
        transformMatrix.c3.z = 0.0f;
        if (m_BRG == null)
            return;
        m_BRG.RemoveAll();
        if (m_InstanceRoot == null)
            return;

        Transform rootTransform = m_InstanceRoot.transform;
        if (rootTransform == null)
            return;
        int childCount = rootTransform.childCount - 1;
        for (int i = childCount; i >= 0; i--)
        {
            if (m_InstanceRoot == null)
                break;

            Transform transform = m_InstanceRoot.transform;
            if (transform == null)
                break;

            Transform child = transform.GetChild(i);
            if (child == null)
                break;

            GameObject childObject = child.gameObject;
            Destroy(childObject);
        }

        float rowOffset = (m_PerRowCount * 0.5f) * m_PerPadding;

        for (int i = 0; i < m_SpawnCount; i++)
        {
            //int column = i % m_PerRowCount;
            //float xPos = (column * m_PerPadding) - rowOffset;
            //float zPos = (i / m_PerRowCount) * m_PerPadding;

            int row = i % m_PerRowCount;
            var pos = new Vector3(row * m_PerPadding - rowOffset, 0, i / m_PerRowCount * m_PerPadding);
            var rotation = Quaternion.identity;

 

            switch (mode)
            {
                case 0: // Regular animator mode
                    if (m_Prefabs == null || m_Prefabs.Length == 0)
                        return;

                 
                    if (m_InstanceRoot == null)
                        return;

                    Transform parentTransform = m_InstanceRoot.transform;
                  

                    GameObject instance = Instantiate(m_Prefabs[0], pos, rotation, parentTransform);
                    if (instance == null)
                        return;

               
                    Animator animator = instance.GetComponent<Animator>();
                    if (animator == null)
                        return;

                    RuntimeAnimatorController animController = animator.runtimeAnimatorController;
                    if (animController == null)
                        return;

                    AnimationClip[] clips = animController.animationClips;
                    if (clips == null)
                        return;

                    int clipIndex = row % clips.Length;
                    if (clipIndex >= clips.Length)
                        return;

                    string clipName = clips[clipIndex].name;
                    animator.Play(clipName);
                    break;

                case AnimMode.GPUAnimation: 
                    if (m_Prefabs == null || m_Prefabs.Length <= 1)
                        return;

                    if (m_InstanceRoot == null)
                        return;

                    parentTransform = m_InstanceRoot.transform;
                  
                    // Create instance from second prefab
                    instance = Instantiate(m_Prefabs[1], pos, rotation, parentTransform);
                    if (instance == null)
                        return;

                    // Remove GPUAnimation component if it exists
                    var gpuAnimation = instance.GetComponent<GPUAnimator>();
                    if (gpuAnimation != null)
                    {
                        Destroy(gpuAnimation);
                    }

                    // Set material property for variant display
                    var renderer = instance.GetComponent<MeshRenderer>();
                    if (renderer == null)
                        return;

                    Material material = renderer.material;
                    if (material == null)
                        return;

                    int group = row / 5;
                    int variant = row % 5;

                    // Set vector property for variant selection
                    material.SetVector("_Variant", new Vector4(variant, 0, 0, 0));
                    break;

                case AnimMode.ECSGraphics: 
                  
                  //  float3 transformedPos = Unity.Transforms.TransformHelpers.Right(transformMatrix, position3D);

                 
                    if (m_BRG == null)
                        return;

                    int entityId = m_BRG.Add(0, pos, rotation, 1.0f, -1);

                
                    if (m_BRG == null)
                        return;

                    m_BRG.PlayGPUAnimation(entityId, row % 5);
                    break;
            }
        }
    }




    public void OnAnimModeChanged(Dropdown drop)
    {
        if (drop == null)
            return;

        m_AnimMode = (AnimMode)drop.value;
        Spawn(m_AnimMode);
    }




    public ECSGPerformance()
    {
    }




    [SerializeField]
    private int m_SpawnCount;




    [SerializeField]
    private int m_PerRowCount;




    [SerializeField]
    private float m_PerPadding;




    [SerializeField]
    private GameObject[] m_Prefabs;




    private ECSGraphicsComponent m_BRG;




    [SerializeField]
    private ECSGPerformance.AnimMode m_AnimMode;




    private GameObject m_InstanceRoot;



    private enum AnimMode
    {


        Animator,


        GPUAnimation,


        ECSGraphics
    }
}
