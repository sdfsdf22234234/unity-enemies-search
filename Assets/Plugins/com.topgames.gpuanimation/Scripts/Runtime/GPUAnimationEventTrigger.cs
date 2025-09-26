using System;

using UnityEngine;
using UnityEngine.Events;

namespace GPUAnimation.Runtime
{
	
	
	public class GPUAnimationEventTrigger : MonoBehaviour
	{
		
		
		
		
		public UnityEvent<int, int, string> Events
		{
			
			
			get
			{
				return this.m_Events;
			}
			
			
			set
			{
				this.m_Events = value;
			}
		}

		
		
		
		public int AnimationClipsCount
		{


			get
			{
				if (m_EventData == null || m_EventData.ClipEvents == null)
				{
					return 0;
				}


				return m_EventData.ClipEvents.Count;
			}
		}

		
		
		
		public Material GPUAnimMaterial
		{


			get
			{
				if (!m_GPUAnimRenderer)
					return m_GPUAnimRenderer.material;
				return null;
			}
		}

		
		
		
		private void Start()
		{

			if (m_EventData == null || m_EventData.ClipEvents == null)
			{
				 
				return;
			}
			m_ClipsInfo = new Vector4[m_EventData.ClipEvents.Count];

			for (int i = 0; i < m_EventData.ClipEvents.Count; i++)
			{
				var gpuAnimRenderer = m_GPUAnimRenderer;
				if (gpuAnimRenderer == null) continue;

				Material material = gpuAnimRenderer.material;
				Vector4 animationClipInfo = GPUAnimationUtility.GetAnimationClipInfo(material, i);

				// 这里假设 m_ClipsInfo 是 Vector4 数组
				if (i >= m_ClipsInfo.Length)
				{
					 
					return;
				}

				m_ClipsInfo[i] = animationClipInfo;
			}

			m_ClipIdPropertyID = Shader.PropertyToID("_ClipId");
			m_AnimSpeedPropertyID = Shader.PropertyToID("_AnimSpeed");
			m_PreFrame = 0;

		}

		
		
		
		private void Update()
		{
			if (m_GPUAnimRenderer == null)
			{
			
				return;
			}

			// 获取材质
			Material material = m_GPUAnimRenderer.material;
			if (material == null)
			{
			
				return;
			}
		

			// 获取向量和动画速度
			Vector4 vector = material.GetVector(m_ClipIdPropertyID);
			float animSpeed = material.GetFloat(m_AnimSpeedPropertyID);

			if (m_ClipsInfo == null)
			{

				return;
			}

			int clipIndex = Mathf.Clamp((int)vector.x, 0, m_ClipsInfo.Length - 1);
			if (clipIndex >= m_ClipsInfo.Length)
			{
				
				return;
			}
			Vector4 clipInfo = m_ClipsInfo[clipIndex];
			int currentFrame = GPUAnimationUtility.GetAnimationCurrentFrame(clipInfo, animSpeed, vector.y);

			if (m_PreFrame != currentFrame)
			{
				if (currentFrame == m_PreFrame + 1)
				{
					TryTriggerEvent(clipIndex, currentFrame);
				}
				else if (currentFrame > m_PreFrame)
				{
					for (int frame = m_PreFrame + 1; frame <= currentFrame; frame++)
					{
						TryTriggerEvent(clipIndex, frame);
					}
				}

				m_PreFrame = currentFrame;
			}



		}




		private void TryTriggerEvent(int clipIndex, int currentFrame)
		{
			if (m_EventData == null)
			{
			 
				return;
			}

			// 获取当前剪辑事件列表
			var clipEvents = m_EventData.ClipEvents;

			if (clipEvents == null || clipIndex < 0 || clipIndex >= clipEvents.Count)
			{
				 
				return;
			}

			var eventDictionary = clipEvents[clipIndex];

			if (eventDictionary == null)
			{
				
				return;
			}

			// 检查当前帧是否有事件
			if (eventDictionary.ContainsKey(currentFrame))
			{
				object eventData = eventDictionary[currentFrame];

				// 检查事件是否存在
				if (m_Events == null)
				{
					
					return;
				}

				// 触发事件
				m_Events.Invoke(clipIndex, currentFrame, eventData.ToString());

				// 调试信息格式化
				string logMessage = string.Format("动画事件:[{0}]触发在第{1}帧.", clipIndex, currentFrame, eventData);
				Debug.Log(logMessage);
			}
		}

		
		
		
		public GPUAnimationEventTrigger()
		{
			this.m_Events = new UnityEvent<int, int, string>();
		}

		
		
		
		[SerializeField]
		private GPUAnimationEventData m_EventData;

		
		
		
		[SerializeField]
		private Renderer m_GPUAnimRenderer;

		
		
		
		private UnityEvent<int, int, string> m_Events;

		
		
		
		private int m_PreFrame;

		
		
		
		private Vector4[] m_ClipsInfo;

		
		
		
		private int m_ClipIdPropertyID ;

		
		
		
		private int m_AnimSpeedPropertyID;
	}
}
