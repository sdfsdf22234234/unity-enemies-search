using System;
using UnityEngine;




public class BRG_LOD_Demo : MonoBehaviour
{
    [Obsolete]
    private void Start()
	{
		m_BRG = FindObjectOfType<BatchRendererComponent>();
		style = new GUIStyle
		{
			fontSize = 20,
			fontStyle = FontStyle.Bold // 假设我们将字体样式设置为粗体
		};

		// 设置文本颜色
		style.normal.textColor = new Color(1f, 0.5f, 0f); // 这里可以根据需要调整颜色
		Spawn();
	}




	private void OnGUI()
	{
	}




	private void Spawn()
	{
		int resourceCount = m_BRG.GetResourceCount();
		if (resourceCount < 1)
		{
		 
			return;
		}

		float offset = (m_PerRowCount * 0.5f) * m_PerPadding;

		for (int i = 0; i < m_SpawnCount; i++)
		{
			// 计算在行和列中的位置
			int row = i / m_PerRowCount;
			int col = i % m_PerRowCount;

			float xPos = (col * m_PerPadding) - offset;
			float zPos = row * m_PerPadding;
			   
			// 生成位置和旋转
			Vector3 position = new Vector3(xPos, 0, zPos);
			Quaternion rotation = Quaternion.identity; // 默认旋转

            // 添加渲染对象到BatchRendererComponent
            var renderId = m_BRG.AddRenderer(i % resourceCount, position, rotation,1);

            m_BRG.SetRendererClipId(renderId, 5);

        }
	}




	public BRG_LOD_Demo()
	{
	}




	[SerializeField]
	private int m_SpawnCount;




	[SerializeField]
	private int m_PerRowCount;




	[SerializeField]
	private float m_PerPadding;




	private BatchRendererComponent m_BRG;




	private GUIStyle style;
}
