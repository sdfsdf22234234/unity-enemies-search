using System;
using System.Collections.Generic;
using UnityEngine;

namespace GPUAnimation.Runtime
{


	[CreateAssetMenu(fileName = "GPUAnimationEventData.asset", menuName = "GPUAnimationEventData")]
	public class GPUAnimationEventData : ScriptableObject
	{



		public List<GPUAnimEvents> ClipEvents
		{


			get
			{
				return m_ClipEvents;
			}
            set
            {
                 m_ClipEvents=value;
            }
        }




		public void Add(int clipIndex, int frame, string eventName)
		{
            // 检查 m_ClipEvents 是否已初始化
            if (m_ClipEvents == null)
            {
                m_ClipEvents = new List<GPUAnimEvents>();
            }

            // 确保 clipIndex 在有效范围内
            while (clipIndex >= m_ClipEvents.Count)
            {
                // 如果当前 clipIndex 超出了 m_ClipEvents 的大小，增加新的 GPUAnimEvents
                m_ClipEvents.Add(new GPUAnimEvents());
            }

            GPUAnimEvents clipEvent = m_ClipEvents[clipIndex];

            // 检查 clipEvent 是否为 null
            if (clipEvent == null)
            {
                clipEvent = new GPUAnimEvents();
                m_ClipEvents[clipIndex] = clipEvent;
            }

            // 将事件添加到 clipEvent 中
            if (clipEvent.ContainsKey(frame))
            {
                // 如果 frame 已存在，更新事件名称
                clipEvent[frame] = eventName;
            }
            else
            {
                // 如果 frame 不存在，添加新的事件
                clipEvent.Add(frame, eventName);
            }
        }




        public void ClearAll()
        {
            if (m_ClipEvents != null)
            {
               if(m_ClipEvents.Count>0)
                m_ClipEvents.Clear();
            }
        }




		public GPUAnimationEventData()
		{

         
        }




		[SerializeField]
		private List<GPUAnimEvents> m_ClipEvents;
	}
}
