using System;
using System.Collections.Generic;
using GPUAnimation.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;


public class GPUAnimCrossFade : MonoBehaviour
{

	public void SetAnim(int clipId)
	{
        if (m_Anims == null || m_Anims.Length == 0)
        {
           
            return;
        }

        // ���������б�
        for (int i = 0; i < m_Anims.Length; i++)
        {
            var animation = m_Anims[i];

            // ��鶯���Ƿ�Ϊ��
            if (animation == null)
            { 
                continue;
            }

            // ���Ŷ���
            animation.PlayAnimation(clipId);
        }
    }


	public void SetAnimSlider(Slider slider)
	{
        if (slider == null)
        {
            Debug.LogError("Slider is null.");
            return;
        }

        // ��ȡ�������ĵ�ǰֵ
        float sliderValue = slider.value;

        // ��ȡ�������µ��ı����
        Text sliderText = slider.GetComponentInChildren<Text>();
        if (sliderText != null)
        {
            sliderText.text = string.Format("ƽ������ʱ��:{0}", sliderValue);
        }

        // ���� m_Anims �����е�ÿ������
        if (m_Anims != null)
        {
            for (int i = 0; i < m_Anims.Length; i++)
            {
                GPUAnimation.Runtime.GPUAnimation anim = m_Anims[i];
                if (anim != null)
                {
                    // ��ȡ������ MeshRenderer ���
                    MeshRenderer meshRenderer = anim.GetComponent<MeshRenderer>();
                    if (meshRenderer != null)
                    {
                        // ��ȡ����
                        Material material = meshRenderer.material;
                        if (material != null)
                        {
                            // ���ò����еĸ�������
                            material.SetFloat("_AnimTransDuration", sliderValue);  
                        }
                    }
                }
            }
        }
    
 
	}
    private void Start()
    {
        //var m_ClipEvents = new List<GPUAnimEvents>();
        //var animevent = new GPUAnimEvents();
        //animevent.Add(1, "Event A");
        //m_ClipEvents.Add(animevent);

        //// ����һ���µ� ScriptableObject ʵ��
        //var asset = ScriptableObject.CreateInstance<GPUAnimationEventData>();
        //asset.ClipEvents = m_ClipEvents;

        //// �����ʲ�
        //string path = "Assets/test.asset";
        //AssetDatabase.CreateAsset(asset, path);
        //AssetDatabase.SaveAssets();
        //AssetDatabase.Refresh();
    }

    public GPUAnimCrossFade()
	{
      
    }


	[SerializeField]
	private GPUAnimation.Runtime.GPUAnimation[] m_Anims;
}
