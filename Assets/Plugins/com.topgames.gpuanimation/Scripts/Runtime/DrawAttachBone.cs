using UnityEngine;
using System.Text;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace GPUAnimation.Runtime.Samples
{
    public class DrawAttachBone : MonoBehaviour
    {
        [SerializeField] int m_BoneIdx = 0;
        [SerializeField] float m_DrawRadius = 0.2f;
        [SerializeField] bool m_ShowBoneName = true;

        // ???????????
        private string m_BoneName;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            var rd = GetComponent<Renderer>();
            var mat = EditorApplication.isPlaying ? rd.material : rd.sharedMaterial;

            // ????????任????
            var boneDt = GPUAnimationUtility.GetAttachBoneTransform(mat, m_BoneIdx);

            // ??????????λ??
            Vector3 worldPos = transform.localToWorldMatrix.MultiplyPoint(boneDt.Position);

            // ???????????
            Gizmos.DrawWireSphere(worldPos, m_DrawRadius);

            // ???????????
            if (m_ShowBoneName)
            {
                // ???????????
                if (string.IsNullOrEmpty(m_BoneName))
                {
                    // ????????????????????????????
                    m_BoneName = GPUAnimationUtility.GetAttachBoneName(mat, m_BoneIdx);
                }

                // ?????????????????????
                Handles.color = Color.yellow;
                Handles.Label(worldPos + Vector3.up * m_DrawRadius,
                    $"{m_BoneName} (ID: {m_BoneIdx})");
            }
        }

        // ??????????????????????
        private void OnValidate()
        {
            m_BoneName = null;
        }

        // ??????????
        [ContextMenu("Debug Bone Name Encoding")]
        private void DebugBoneNameEncodingMenu()
        {
            DebugBoneNameEncoding();
        }

        private void DebugBoneNameEncoding()
        {
            var rd = GetComponent<Renderer>();
            var mat = EditorApplication.isPlaying ? rd.material : rd.sharedMaterial;

            if (mat == null || !mat.HasProperty("_AnimTex"))
            {
                Debug.LogError("Material does not have _AnimTex property");
                return;
            }

            Texture2D animTex = mat.GetTexture("_AnimTex") as Texture2D;
            if (animTex == null)
            {
                Debug.LogError("_AnimTex is null");
                return;
            }

            // ????????????
            if (!animTex.isReadable)
            {
                Debug.LogError("Texture is not readable! Enable Read/Write in import settings.");
                return;
            }

            // ??????????????
            Debug.Log($"Texture info: {animTex.name}, Size: {animTex.width}x{animTex.height}, Format: {animTex.format}");

            // ?????????
            Color marker = animTex.GetPixel(animTex.width - 1, 0);
            Debug.Log($"Special marker: R={marker.r}, G={marker.g}, B={marker.b}, A={marker.a}");

            // ??鵱??????????????
            Debug.Log($"Current bone index: {m_BoneIdx}");
            string boneName = GPUAnimationUtility.GetAttachBoneName(mat, m_BoneIdx);
            Debug.Log($"GetAttachBoneName result: {boneName}");

            // ????????????Щ????????
            Debug.Log("Attempting to manually decode bone names...");
            for (int row = 1; row < Mathf.Min(50, animTex.height); row++)
            {
                try
                {
                    Color infoPixel = animTex.GetPixel(0, row);
                    int boneIndex = Mathf.RoundToInt(infoPixel.r * 255);
                    int nameLength = Mathf.RoundToInt(infoPixel.g * 255);

                    Debug.Log($"Row {row}: Pixel(0,{row}) = R:{infoPixel.r}, G:{infoPixel.g}, B:{infoPixel.b}, A:{infoPixel.a}");
                    Debug.Log($"  Interpreted as: BoneIndex={boneIndex}, NameLength={nameLength}");

                    if (nameLength > 0 && nameLength <= 32)
                    {
                        Debug.Log($"Found potential bone name at row {row}: Index={boneIndex}, Length={nameLength}");

                        // ???????????
                        byte[] nameBytes = new byte[nameLength];
                        int pixelsNeeded = Mathf.CeilToInt(nameLength / 4f);

                        StringBuilder pixelInfo = new StringBuilder();
                        for (int i = 0; i < pixelsNeeded; i++)
                        {
                            Color pixel = animTex.GetPixel(i + 1, row);
                            pixelInfo.AppendLine($"  Pixel({i + 1},{row}): R={pixel.r:F3}, G={pixel.g:F3}, B={pixel.b:F3}, A={pixel.a:F3}");

                            if (i * 4 < nameLength) nameBytes[i * 4] = (byte)(pixel.r * 255);
                            if (i * 4 + 1 < nameLength) nameBytes[i * 4 + 1] = (byte)(pixel.g * 255);
                            if (i * 4 + 2 < nameLength) nameBytes[i * 4 + 2] = (byte)(pixel.b * 255);
                            if (i * 4 + 3 < nameLength) nameBytes[i * 4 + 3] = (byte)(pixel.a * 255);
                        }
                        Debug.Log(pixelInfo.ToString());

                        string decodedName = System.Text.Encoding.UTF8.GetString(nameBytes);
                        Debug.Log($"  Decoded name: '{decodedName}'");

                        // ????????????????????????????????????
                        if (boneIndex == m_BoneIdx)
                        {
                            Debug.Log($"<color=green>*** This is our target bone index {m_BoneIdx}! ***</color>");
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error processing row {row}: {e.Message}");
                }
            }

            // ?????????????У???????????????
            Debug.Log("Checking last few rows of texture...");
            for (int row = animTex.height - 10; row < animTex.height; row++)
            {
                if (row < 0) continue;

                try
                {
                    Color pixel = animTex.GetPixel(animTex.width - 1, row);
                    Debug.Log($"Last column, Row {row}: R={pixel.r:F3}, G={pixel.g:F3}, B={pixel.b:F3}, A={pixel.a:F3}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error checking row {row}: {e.Message}");
                }
            }
        }
#endif

        private void OnEnable()
        {
            // 获取材质       
            var rd = GetComponent<Renderer>();
            var mat = rd.sharedMaterial;

            // 获取动画纹理         
            var animTex = mat.GetTexture("_AnimTex") as Texture2D;

            // 调试骨骼名称编码            
            if (animTex != null)
            {
                Debug.Log($"开始调试骨骼名称编码，骨骼索引: {m_BoneIdx}");
                GPUAnimationUtility.DebugBoneNameEncoding(animTex);

                // 尝试获取骨骼名称
                string boneName = GPUAnimationUtility.GetBoneName(animTex, m_BoneIdx);
                Debug.Log($"获取到的骨骼名称: '{boneName}'");
            }
            else
            {
                Debug.LogError("无法获取动画纹理");
            }
        }
    }
}