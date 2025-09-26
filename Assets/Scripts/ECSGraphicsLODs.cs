using Unity.Mathematics;
using UnityEngine;

public class ECSGraphicsLODs : MonoBehaviour
{
    [SerializeField]
    private int m_SpawnCount;

    [SerializeField]
    private int m_PerRowCount;

    [SerializeField]
    private float m_PerPadding;

    private GUIStyle style;

    private void Start()
    {
        style = new GUIStyle();

        // �����ı������СΪ20
        style.fontSize = 20;

        // ����������ʽΪ���� (FontStyle.Bold = 1)
        style.fontStyle = FontStyle.Bold;

        // �����ı���ɫΪ��ɫ
        // �ӷ�������뿴��������һ��Ԥ�������ɫֵ���ܿ����ǰ�ɫ
        style.normal.textColor = Color.white;

        // ����Spawn������ʼ���ɶ���
        Spawn();
    }

    private void OnGUI()
    {
    }

    private void Spawn()
    {
        // ��ȡECSGraphicsComponent����ʵ��
        ECSGraphicsComponent ecsGraphics = ECSGraphicsComponent.Instance;

        if (ecsGraphics == null)
        {
            Debug.LogError("ECSGraphicsComponentʵ��������");
            return;
        }

        // ����Ƿ��п��õ�ECS��Ⱦ��
        if (ecsGraphics.ECSRendersCount < 1)
        {
            Debug.LogWarning("û�п��õ�ECS��Ⱦ��");
            return;
        }

        // ������ƫ�������������񲼾�
        float rowOffset = (m_PerRowCount * 0.5f) * m_PerPadding;

        // ����ָ��������ʵ��
        for (int i = 0; i < m_SpawnCount; i++)
        {
            // ��������λ��
            int column = i % m_PerRowCount;
            float xPos = (column * m_PerPadding) - rowOffset;
            float zPos = (i / m_PerRowCount) * m_PerPadding;

            // �ٴμ��ECSGraphicsComponentʵ���Ƿ���Ч
            if (ECSGraphicsComponent.Instance == null)
            {
                Debug.LogError("ECSGraphicsComponentʵ����ʧ");
                return;
            }

            // ��ȡ���õ���Ⱦ������
            int ecsRendersCount = ECSGraphicsComponent.Instance.ECSRendersCount;

            // ����λ������
            float3 position = new float3(xPos, 0, zPos);

            // ת��λ�ã�ʹ��TransformHelpers.Right��
            float4x4 transformMatrix = float4x4.identity;
            transformMatrix.c0.x = xPos;
            transformMatrix.c0.z = zPos;
            float3 transformedPosition = Unity.Transforms.TransformHelpers.Right(transformMatrix);

            // ��ȡĬ����ת
            quaternion rotation = quaternion.identity;

            // ���ʵ�嵽������ʹ�ò�ͬ����Ⱦ��ID��ѭ��ʹ�ÿ��õ���Ⱦ����
            ECSGraphicsComponent.Instance.Add(
                i % ecsRendersCount,  // ��Ⱦ��ID��ѭ��ʹ�ÿ�����Ⱦ��
                transformedPosition,  // λ��
                rotation,             // ��ת
                1.0f,                 // ����
                -1                    // ��ʵ��ID��-1��ʾû�и�ʵ�壩
            );
        }
    }
}
