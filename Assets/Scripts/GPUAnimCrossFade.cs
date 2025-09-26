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

        // 遍历动画列表
        for (int i = 0; i < m_Anims.Length; i++)
        {
            var animation = m_Anims[i];

            // 检查动画是否为空
            if (animation == null)
            { 
                continue;
            }

            // 播放动画
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

        // 获取滑动条的当前值
        float sliderValue = slider.value;

        // 获取滑动条下的文本组件
        Text sliderText = slider.GetComponentInChildren<Text>();
        if (sliderText != null)
        {
            sliderText.text = string.Format("平滑过渡时间:{0}", sliderValue);
        }

        // 遍历 m_Anims 数组中的每个动画
        if (m_Anims != null)
        {
            for (int i = 0; i < m_Anims.Length; i++)
            {
                GPUAnimation.Runtime.GPUAnimation anim = m_Anims[i];
                if (anim != null)
                {
                    // 获取动画的 MeshRenderer 组件
                    MeshRenderer meshRenderer = anim.GetComponent<MeshRenderer>();
                    if (meshRenderer != null)
                    {
                        // 获取材质
                        Material material = meshRenderer.material;
                        if (material != null)
                        {
                            // 设置材质中的浮动参数
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

        //// 创建一个新的 ScriptableObject 实例
        //var asset = ScriptableObject.CreateInstance<GPUAnimationEventData>();
        //asset.ClipEvents = m_ClipEvents;

        //// 保存资产
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
