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
    /// ������ʾ�������ڳ����и��������� RVOAgent �� RendererNode
    /// </summary>
    private void StartDemo()
    {
        // ��� m_Texture Ϊ null����ֱ�ӷ��أ���ִ�к�������
        if (m_Texture == null)
        {
            return;
        }

        // ��ȡ�����������ɫ����
        var colors = m_Texture.GetPixels32();
        uint seed = (uint)Time.frameCount;
        RVOAgent agent;  // RVOAgent ���������ڴ洢��ӵĴ���
        var random = new Unity.Mathematics.Random((uint)Time.frameCount);  // ʹ��֡����Ϊ���Ӵ��������������
        var halfWidth = m_Texture.width * 0.5f;  // �����ȵ�һ��
        var halfHeight = m_Texture.height * 0.5f;  // ����߶ȵ�һ��
        if (!m_UseECS)
        {
            BatchRendererComponent batchRenderer = BatchRendererComponent.Instance;
            // �������������������ɫ
            for (int i = 0; i < colors.Length; i++)
            {
                var col = colors[i];  // ��ȡ��ǰ���ص���ɫֵ

                // ������ص�͸����С�� 10��������ǰѭ����͸���Ƚϵ͵����ز�����
                if (col.a < 10) continue;

                // �������һ��λ�ã�ʹ������ǶȺͰ뾶�ڰ뾶��Χ��
                float3 pos = math.forward(quaternion.Euler(0, random.NextFloat() * 360, 0)) * random.NextFloat(m_Radius * 0.5f, m_Radius);

                // �� RVO ϵͳ�����һ����������ȡ���صĴ������
                agent = m_Rvo.AddAgent(pos);

                // �������Ŀ��λ������Ϊ��ǰ�����������е�λ��ƫ��
                agent.targetPosition = new float3(i % m_Texture.width - halfWidth, 0, i / m_Texture.width - halfHeight);

                // �� BatchRendererGroup �����һ����Ⱦ�ڵ㣬����ȡ���ص���Ⱦ�ڵ� ID
                var renderId = batchRenderer.AddRenderer(0, pos, quaternion.identity, new float3(1));

                // ��� renderId Ϊ RendererNodeId.Null����ʾ�����Ⱦ�ڵ�ʧ��
                if (renderId == RendererNodeId.Null)
                {
                    Debug.LogError($"���RendererNodeʧ��, �ѳ���BatchRendererComponent��������(Capacity)����, ����������������С");
                    return;
                }

                // ���������Ⱦ��������Ϊ��Ⱦ�ڵ������
                agent.rendererIndex = renderId.Index;

                // ���ô���ļ��� ID����������Ϊ�ܲ��������������嶯�������������Ŀ�������������
                agent.clipId = 2;

                // ���ô���İ뾶�ͱ��ϰ뾶
                agent.radius = agent.radiusObst = 0.25f;

                // ���ô��������ٶ�
                agent.maxSpeed = m_RvoMoveSpeed;

                // ������Ⱦ�ڵ����ɫ����������ɫת��Ϊ��Χ�� [0, 1] �ĸ�����
                batchRenderer.SetRendererColor(renderId, new float4(col.r, col.g, col.b, col.a) / 255);
            }

            // ��� m_Text ��Ϊ null��������ı���ʾΪ��ǰ�ɼ���Ⱦ�ڵ������
            if (m_Text != null)
            {
                m_Text.text = $"Count: {batchRenderer.TotalVisibleCount}";
            }

        }
        else
        {
            // ʹ��ECS��Ⱦģʽ
            ECSGraphicsComponent ecsGraphics = ECSGraphicsComponent.Instance;

            // ������������
            int index = 0;
            while (index < colors.Length)
            {
                // ֻ����Alphaֵ���ڵ���10������
                var col = colors[index];
                if (col.a >= 10)
                {
                    // ���������ת

                    // ��������뾶
                    float randomAngle = ((seed >> 9) / (float)0x3F800000 - 1.0f) * 360.0f;
                    quaternion rotation = quaternion.Euler(0, randomAngle, 0);
                    float3 pos = math.forward(quaternion.Euler(0, random.NextFloat() * 360, 0)) * random.NextFloat(m_Radius * 0.5f, m_Radius);
                 

                    // ��ӵ�������
                     agent = m_Rvo.AddAgent(pos);

                    // ����Ŀ��λ��
                    agent.targetPosition = new float3(index % m_Texture.width - halfWidth, 0, index / m_Texture.width - halfHeight);

                    // ���ECSʵ��
                    int entityId = ecsGraphics.Add(0, pos, rotation, 1.0f, -1);

                    // ���õ����������
                    agent.rendererIndex = entityId;
                    agent.radiusObst = 0.25f;
                    agent.radius = 0.25f;
                    agent.maxSpeed = m_RvoMoveSpeed;

                    // ����ʵ����ɫ
                 
                    ecsGraphics.SetMainColor(entityId, new float4(col.r, col.g, col.b, col.a) / 255);

                    // ����GPU����
                    ecsGraphics.PlayGPUAnimation(entityId, 1);
                }

                index++;
            }

            // ����UI��ʾ
            if (m_Text != null)
            {
                m_Text.text = string.Format("Instances: {0}", ecsGraphics.Entities.Count);
            }
        }


    }
}
