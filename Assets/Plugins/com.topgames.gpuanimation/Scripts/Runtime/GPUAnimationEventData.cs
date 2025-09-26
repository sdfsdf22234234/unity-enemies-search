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
            // ��� m_ClipEvents �Ƿ��ѳ�ʼ��
            if (m_ClipEvents == null)
            {
                m_ClipEvents = new List<GPUAnimEvents>();
            }

            // ȷ�� clipIndex ����Ч��Χ��
            while (clipIndex >= m_ClipEvents.Count)
            {
                // �����ǰ clipIndex ������ m_ClipEvents �Ĵ�С�������µ� GPUAnimEvents
                m_ClipEvents.Add(new GPUAnimEvents());
            }

            GPUAnimEvents clipEvent = m_ClipEvents[clipIndex];

            // ��� clipEvent �Ƿ�Ϊ null
            if (clipEvent == null)
            {
                clipEvent = new GPUAnimEvents();
                m_ClipEvents[clipIndex] = clipEvent;
            }

            // ���¼���ӵ� clipEvent ��
            if (clipEvent.ContainsKey(frame))
            {
                // ��� frame �Ѵ��ڣ������¼�����
                clipEvent[frame] = eventName;
            }
            else
            {
                // ��� frame �����ڣ�����µ��¼�
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
