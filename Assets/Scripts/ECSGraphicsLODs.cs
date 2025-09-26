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

        // 设置文本字体大小为20
        style.fontSize = 20;

        // 设置字体样式为粗体 (FontStyle.Bold = 1)
        style.fontStyle = FontStyle.Bold;

        // 设置文本颜色为白色
        // 从反编译代码看，加载了一个预定义的颜色值，很可能是白色
        style.normal.textColor = Color.white;

        // 调用Spawn方法开始生成对象
        Spawn();
    }

    private void OnGUI()
    {
    }

    private void Spawn()
    {
        // 获取ECSGraphicsComponent单例实例
        ECSGraphicsComponent ecsGraphics = ECSGraphicsComponent.Instance;

        if (ecsGraphics == null)
        {
            Debug.LogError("ECSGraphicsComponent实例不存在");
            return;
        }

        // 检查是否有可用的ECS渲染器
        if (ecsGraphics.ECSRendersCount < 1)
        {
            Debug.LogWarning("没有可用的ECS渲染器");
            return;
        }

        // 计算行偏移量，用于网格布局
        float rowOffset = (m_PerRowCount * 0.5f) * m_PerPadding;

        // 生成指定数量的实体
        for (int i = 0; i < m_SpawnCount; i++)
        {
            // 计算网格位置
            int column = i % m_PerRowCount;
            float xPos = (column * m_PerPadding) - rowOffset;
            float zPos = (i / m_PerRowCount) * m_PerPadding;

            // 再次检查ECSGraphicsComponent实例是否有效
            if (ECSGraphicsComponent.Instance == null)
            {
                Debug.LogError("ECSGraphicsComponent实例丢失");
                return;
            }

            // 获取可用的渲染器数量
            int ecsRendersCount = ECSGraphicsComponent.Instance.ECSRendersCount;

            // 创建位置向量
            float3 position = new float3(xPos, 0, zPos);

            // 转换位置（使用TransformHelpers.Right）
            float4x4 transformMatrix = float4x4.identity;
            transformMatrix.c0.x = xPos;
            transformMatrix.c0.z = zPos;
            float3 transformedPosition = Unity.Transforms.TransformHelpers.Right(transformMatrix);

            // 获取默认旋转
            quaternion rotation = quaternion.identity;

            // 添加实体到场景，使用不同的渲染器ID（循环使用可用的渲染器）
            ECSGraphicsComponent.Instance.Add(
                i % ecsRendersCount,  // 渲染器ID，循环使用可用渲染器
                transformedPosition,  // 位置
                rotation,             // 旋转
                1.0f,                 // 缩放
                -1                    // 父实体ID（-1表示没有父实体）
            );
        }
    }
}
