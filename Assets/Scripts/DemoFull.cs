using Nebukam.Common;
using Nebukam.ORCA;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.EventSystems.EventTrigger;

public class DemoFull : MonoBehaviour
{
    [SerializeField] RVOComponent m_Rvo;
    [SerializeField]
    private bool m_UseECS;
   // [SerializeField] BatchRendererComponent m_Brg;

    [SerializeField] Texture2D m_Texture;
    [SerializeField] float m_Radius = 200;
    [SerializeField] float m_RvoMoveSpeed = 10f;

    [SerializeField] Text m_Text;
    private async void Start()
    {
        await Task.Delay(500);
        StartDemo();
    }
    private void Update()
    {

    }
    /// <summary>
    /// 启动演示方法，在场景中根据纹理创建 RVOAgent 和 RendererNode
    /// </summary>
    private void StartDemo()
    {
        // 如果 m_Texture 为 null，则直接返回，不执行后续操作
        if (m_Texture == null)
        {
            return;
        }

        // 获取纹理的像素颜色数组
        var colors = m_Texture.GetPixels32();
        uint seed = (uint)Time.frameCount;
        RVOAgent agent;  // RVOAgent 变量，用于存储添加的代理
        var random = new Unity.Mathematics.Random((uint)Time.frameCount);  // 使用帧数作为种子创建随机数生成器
        var halfWidth = m_Texture.width * 0.5f;  // 纹理宽度的一半
        var halfHeight = m_Texture.height * 0.5f;  // 纹理高度的一半
        if (!m_UseECS)
        {
            BatchRendererComponent batchRenderer = BatchRendererComponent.Instance;
            // 遍历纹理的所有像素颜色
            for (int i = 0; i < colors.Length; i++)
            {
                var col = colors[i];  // 获取当前像素的颜色值

                // 如果像素的透明度小于 10，跳过当前循环（透明度较低的像素不处理）
                if (col.a < 10) continue;

                // 随机生成一个位置，使用随机角度和半径在半径范围内
                float3 pos = math.forward(quaternion.Euler(0, random.NextFloat() * 360, 0)) * random.NextFloat(m_Radius * 0.5f, m_Radius);

                // 向 RVO 系统中添加一个代理，并获取返回的代理对象
                agent = m_Rvo.AddAgent(pos);

                // 将代理的目标位置设置为当前像素在纹理中的位置偏移
                agent.targetPosition = new float3(i % m_Texture.width - halfWidth, 0, i / m_Texture.width - halfHeight);

                // 在 BatchRendererGroup 中添加一个渲染节点，并获取返回的渲染节点 ID
                var renderId = batchRenderer.AddRenderer(0, pos, quaternion.identity, new float3(1));

                // 如果 renderId 为 RendererNodeId.Null，表示添加渲染节点失败
                if (renderId == RendererNodeId.Null)
                {
                    Debug.LogError($"添加RendererNode失败, 已超过BatchRendererComponent设置容量(Capacity)上限, 请设置增加容量大小");
                    return;
                }

                // 将代理的渲染索引设置为渲染节点的索引
                agent.rendererIndex = renderId.Index;

                // 设置代理的剪辑 ID（这里设置为跑步动画索引，具体动画索引需根据项目具体情况调整）
                agent.clipId = 2;

                // 设置代理的半径和避障半径
                agent.radius = agent.radiusObst = 0.25f;

                // 设置代理的最大速度
                agent.maxSpeed = m_RvoMoveSpeed;

                // 设置渲染节点的颜色，将像素颜色转换为范围在 [0, 1] 的浮点数
                batchRenderer.SetRendererColor(renderId, new float4(col.r, col.g, col.b, col.a) / 255);
            }

            // 如果 m_Text 不为 null，则更新文本显示为当前可见渲染节点的总数
            if (m_Text != null)
            {
                m_Text.text = $"Count: {batchRenderer.TotalVisibleCount}";
            }

        }
        else
        {
            // 使用ECS渲染模式
            ECSGraphicsComponent ecsGraphics = ECSGraphicsComponent.Instance;

            // 遍历所有像素
            int index = 0;
            while (index < colors.Length)
            {
                // 只处理Alpha值大于等于10的像素
                var col = colors[index];
                if (col.a >= 10)
                {
                    // 生成随机旋转

                    // 生成随机半径
                    float randomAngle = ((seed >> 9) / (float)0x3F800000 - 1.0f) * 360.0f;
                    quaternion rotation = quaternion.Euler(0, randomAngle, 0);
                    float3 pos = math.forward(quaternion.Euler(0, random.NextFloat() * 360, 0)) * random.NextFloat(m_Radius * 0.5f, m_Radius);
                 

                    // 添加导航代理
                     agent = m_Rvo.AddAgent(pos);

                    // 设置目标位置
                    agent.targetPosition = new float3(index % m_Texture.width - halfWidth, 0, index / m_Texture.width - halfHeight);

                    // 添加ECS实体
                    int entityId = ecsGraphics.Add(0, pos, rotation, 1.0f, -1);

                    // 设置导航代理参数
                    agent.rendererIndex = entityId;
                    agent.radiusObst = 0.25f;
                    agent.radius = 0.25f;
                    agent.maxSpeed = m_RvoMoveSpeed;

                    // 设置实体颜色
                 
                    ecsGraphics.SetMainColor(entityId, new float4(col.r, col.g, col.b, col.a) / 255);

                    // 播放GPU动画
                    ecsGraphics.PlayGPUAnimation(entityId, 1);
                }

                index++;
            }

            // 更新UI显示
            if (m_Text != null)
            {
                m_Text.text = string.Format("Instances: {0}", ecsGraphics.Entities.Count);
            }
        }


    }
}
