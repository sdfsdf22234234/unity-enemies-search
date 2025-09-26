using System;
using UnityEngine;
using UnityEngine.Events;

namespace GPUAnimation.Runtime
{
    /// <summary>
    /// GPUAnimation 类用于管理 GPU 动画的播放和事件处理。
    /// </summary>
    public class GPUAnimation : MonoBehaviour
    {
        /// <summary>
        /// 动画索引，表示当前播放的动画的索引。
        /// </summary>
        public int AnimationIndex
        {
            get
            {
                return (int)this.m_ClipId.x;
            }
            
        }

        /// <summary>
        /// 事件，用于在动画播放过程中触发的自定义事件。
        /// </summary>
        public UnityEvent<int, int, string> Events
        {
            get
            {
                return m_Events; // 返回事件 
            }
            
        }

        /// <summary>
        /// 动画片段的数量。
        /// </summary>
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

        /// <summary>
        /// 用于 GPU 动画的材质。
        /// </summary>
        public Material GPUAnimMaterial
        {
            get
            {
                if (m_GpuRenderer)
                    return m_GpuRenderer.material;
                return null;
            }
        }

        /// <summary>
        /// 动画片段信息的数组。
        /// </summary>
        public Vector4[] ClipInfos
        {
            get
            {
                if (m_EventData != null)
                {
                    return m_ClipsInfo; // 返回剪辑信息
                }
                else
                {
                    return null; // 返回 null
                }
            }
        }

        private void Awake()
        {
            // 初始化事件和组件
            if (OnAnimChanged == null)
            {
                OnAnimChanged = new UnityEvent<Vector4>();
            }
            m_GpuRenderer = GetComponent<Renderer>();
            SetClipId(m_ClipId);
            if (m_EventData == null || m_EventData.ClipEvents == null || m_EventData.ClipEvents.Count == 0)
            { 
                return;
            }
            m_ClipsInfo = new Vector4[m_EventData.ClipEvents.Count];
            for (int i = 0; i < m_EventData.ClipEvents.Count; i++)
            {
                // 获取每个ClipEvent并处理
                GPUAnimEvents clipEvent = m_EventData.ClipEvents[i];
                if (clipEvent != null)
                {
                    Material material = m_GpuRenderer.material;
              //  xy: 帧数范围; z: 动画时长; w: loop
                    Vector4 animationClipInfo = GPUAnimationUtility.GetAnimationClipInfo(material, i);
                    // 存储剪辑信息
                    m_ClipsInfo[i] = animationClipInfo;
                }
            }
            m_ClipIdPropertyID = Shader.PropertyToID(ANIM_CLIP_ID);
            m_AnimSpeedPropertyID = Shader.PropertyToID(ANIM_SPEED);
            this.m_PreFrame = 0;
        }

        private void OnEnable()
        {
            if (m_ParentGPUAnimation != null)
            {
                // 创建新的监听器
                UnityAction<Vector4> listener = OnParentGPUAnimationChanged;

                // 检查是否已经有监听器并添加
                if (OnAnimChanged == null)
                {
                    OnAnimChanged = new UnityEvent<Vector4>();
                }

                OnAnimChanged.AddListener(listener);
            }
        }

        private void OnDisable()
        {
            if (m_ParentGPUAnimation != null)
            {
                // 创建一个新的事件监听器
                UnityAction<Vector4> action = OnParentGPUAnimationChanged;

                // 如果 OnAnimChanged 是 null，就不需要移除监听器
                if (OnAnimChanged != null)
                {
                    // 移除监听器
                    OnAnimChanged.RemoveListener(action);
                }
            }
        }

        private void Start()
        {
            if (m_DefaultAnimationIndex != (int)m_ClipId.x)
            {
                
                m_ClipId.z = m_ClipId.x;
                m_ClipId.w = m_ClipId.y;
                m_ClipId.x = (float)m_DefaultAnimationIndex;
 
                if (m_GpuRenderer == null || m_GpuRenderer.material == null)
                {
                    return;
                }

                Material material = m_GpuRenderer.material;
                float animationOffset = material.HasProperty(ANIM_TRANS) ? material.GetFloat(ANIM_TRANS) : 0.0f;

                
                float currentTime = Time.time;

                m_ClipId.y = currentTime + animationOffset;
                SetClipId(m_ClipId);
            }
        }

        private void Update()
        {
            EventTriggerUpdate();
        }

        /// <summary>
        /// 更新事件触发器，检查是否需要触发事件。
        /// </summary>
        private void EventTriggerUpdate()
        {
            if (m_EventData != null)
            {
                if (m_GpuRenderer == null)
                {
                    return;
                }

                Material material = m_GpuRenderer.material;

                if (material == null)
                {
                    return;
                }

                Vector4 vectorValue = material.GetVector(m_ClipIdPropertyID);
                float animSpeed = material.GetFloat(m_AnimSpeedPropertyID);
               // Debug.Log(vectorValue);
                if (m_ClipsInfo == null || m_ClipsInfo.Length == 0)
                {
                    return;
                }

                int clipIndex = Mathf.Clamp((int)vectorValue.x, 0, m_ClipsInfo.Length - 1);
              

                Vector4 currentClip = m_ClipsInfo[clipIndex];

              

                int animationCurrentFrame = GPUAnimationUtility.GetAnimationCurrentFrame(
                    currentClip,
                    animSpeed,
                    vectorValue.y // 假设y分量存储了动画的相关信息
                );
              //  Debug.Log(animationCurrentFrame);
                if (m_PreFrame != animationCurrentFrame)
                {
                    TryTriggerEvent(clipIndex, animationCurrentFrame);
                    //if (animationCurrentFrame != m_PreFrame + 1)
                    //{
                      
                    //}
                   // else if (animationCurrentFrame > m_PreFrame)
                    {
                      
                        // for (int frame = m_PreFrame + 1; frame <= animationCurrentFrame; frame++)

                        //while (m_PreFrame <= animationCurrentFrame)
                        //{
                        // TryTriggerEvent(clipIndex, m_PreFrame++);
                        //};

                    }
                    m_PreFrame = animationCurrentFrame;
                }
            }
        }

        /// <summary>
        /// 尝试触发动画事件。
        /// </summary>
        /// <param name="clipIndex">动画片段索引。</param>
        /// <param name="currentFrame">当前帧。</param>
        private void TryTriggerEvent(int clipIndex, int currentFrame)
        {
            if (m_EventData == null || m_EventData.ClipEvents == null)
            {
               
                return;
            }
        
            // 获取与clipIndex对应的事件列表
            var eventsList = m_EventData.ClipEvents[clipIndex];
            if (eventsList == null)
            {
               
                return;
            }
            if (eventsList.ContainsKey(currentFrame))
            {
                var eventData = eventsList[currentFrame];

                // 确保事件存在并触发事件
                if (m_Events != null)
                {
                    m_Events.Invoke(clipIndex, currentFrame, eventData);
                 
                }
                
            }
        }

        /// <summary>
        /// 当父级 GPU 动画发生变化时调用的函数。
        /// </summary>
        /// <param name="arg0">变化的参数。</param>
        private void OnParentGPUAnimationChanged(Vector4 arg0)
        {
            SetClipId(arg0); // 更新片段 ID
        }

        /// <summary>
        /// 播放指定索引的动画。
        /// </summary>
        /// <param name="animIndex">动画索引。</param>
        public void PlayAnimation(int animIndex)
        {
            if (animIndex != (int)m_ClipId.x)
            {
                // 保存之前的动画索引 m_ClipId z:上一个索引,w，上一个开始时间,x 当前索引,y,当前播放时间
                m_ClipId.z = m_ClipId.x;  
                m_ClipId.w = m_ClipId.y;
                m_ClipId.x = (float)animIndex;
              if (m_GpuRenderer == null || m_GpuRenderer.material == null) return;
              Material material = m_GpuRenderer.material;

                float animationOffset = material.HasProperty(ANIM_TRANS) ? material.GetFloat(ANIM_TRANS) : 0.0f;
                float currentTime = Time.time;
                 m_ClipId.y = currentTime+ animationOffset;
               
                SetClipId(m_ClipId);
            }
        }

        /// <summary>
        /// 设置动画事件数据。
        /// </summary>
        /// <param name="dt">动画事件数据。</param>
        public void SetEventData(GPUAnimationEventData dt)
        {
            this.m_EventData = dt;
        }

        /// <summary>
        /// 设置动画片段 ID。
        /// </summary>
        /// <param name="clipId">片段 ID。</param>
        private void SetClipId(Vector4 clipId)
        {
            m_ClipId = clipId;

            // 检查是否存在 GPU 渲染器
            if (m_GpuRenderer != null)
            {
                // 获取材质数组
                Material[] materials = m_GpuRenderer.materials;
                if (materials != null)
                {
                    // 遍历每个材质并设置 ClipId
                    foreach (var material in materials)
                    {
                        if (material != null)
                        {
                            material.SetVector(ANIM_CLIP_ID, m_ClipId);
                        }
                    }
                }
            }

            // 触发动画更改事件
            if (OnAnimChanged != null)
            {
                OnAnimChanged.Invoke(m_ClipId);
            }
        }

        /// <summary>
        /// GPUAnimation 的构造函数。
        /// </summary>
        public GPUAnimation()
        {
            this.m_ClipId = Vector4.zero;
            this.m_Events = new UnityEvent<int, int, string>();
        }

        // 常量定义
        private const string ANIM_TRANS = "_AnimTransDuration"; // 动画过渡持续时间属性
        private const string ANIM_CLIP_ID = "_ClipId"; // 动画片段 ID 属性
        private const string ANIM_SPEED = "_AnimSpeed"; // 动画速度
        // 序列化字段
        [SerializeField]
        private int m_DefaultAnimationIndex; // 默认动画索引

        [SerializeField]
        private GPUAnimationEventData m_EventData; // 动画事件数据

        [SerializeField]
        private GPUAnimation m_ParentGPUAnimation; // 父级 GPU 动画

        /// <summary>
        /// 动画改变时触发的事件。
        /// </summary>
        public UnityEvent<Vector4> OnAnimChanged;

        private Vector4 m_ClipId; // 当前片段 ID

        private Renderer m_GpuRenderer; // GPU 渲染器

        private UnityEvent<int, int, string> m_Events; // 内部事件

        private int m_PreFrame; // 前一帧

        private Vector4[] m_ClipsInfo; // 动画片段信息数组

        private int m_ClipIdPropertyID; // 片段 ID 属性的 ID

        private int m_AnimSpeedPropertyID; // 动画速度属性的 ID
    }
}
