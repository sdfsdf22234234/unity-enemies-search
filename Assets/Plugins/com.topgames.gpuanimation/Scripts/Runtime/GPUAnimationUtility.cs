using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.UIElements;

namespace GPUAnimation.Runtime
{
    public class GPUAnimationUtility
    {
        static Dictionary<int, AnimTexData> m_CachedTextures;
        private static Dictionary<Texture2D, Dictionary<int, string>> s_BoneNameCache = new Dictionary<Texture2D, Dictionary<int, string>>();
        private static Dictionary<string, Dictionary<int, string>> s_BoneNameJsonCache = new Dictionary<string, Dictionary<int, string>>();

        public static GPUBoneData GetAttachBoneTransform(Texture2D animTex, float clipIndex, float animStartTime, float animSpeed, int attachBoneId, float scale = 1f)
        {




            var animData = InitCaches(animTex);
            var boneIndex = GetBoneIndex(animData, attachBoneId);
            return GetBoneTransform(animData, (int)clipIndex, animStartTime, animSpeed, boneIndex, scale);
        }
        public static GPUBoneData GetAttachBoneTransform(Material mat, int attachBoneId, float scale = 1f)
        {
            var animTex = mat.GetTexture("_AnimTex") as Texture2D;
            var animSpeed = mat.GetFloat("_AnimSpeed");
            var animClipId = mat.GetVector("_ClipId");
            return GetAttachBoneTransform(animTex, (int)animClipId.x, animClipId.y, animSpeed, attachBoneId);
        }

        public static GPUBoneData GetAttachBoneTransform(Material mat, float animIndex, float animStartTime, int attachBoneId, float scale = 1f)
        {

            var animTex = mat.GetTexture("_AnimTex") as Texture2D;
            var animSpeed = mat.GetFloat("_AnimSpeed");

            return GetAttachBoneTransform(animTex, animIndex, animStartTime, animSpeed, attachBoneId, scale);
        }



        public static GPUBoneData GetAttachBoneTransform(Texture2D animTex, int clipIndex, float animStartTime, float animSpeed, int boneIndex, float scale)
        {
            int textureWidth = animTex.width;
            Vector4 clipInfo = GetAnimClipData(animTex, clipIndex);
            // 获取动画当前帧
            int currentFrame = GetAnimationCurrentFrame(clipInfo, animSpeed, clipIndex);
            // 获取骨骼变换
            Color boneTransformColor = animTex.GetPixel(boneIndex, currentFrame);
            return GetBoneTransform(animTex, (int)clipIndex, animStartTime, animSpeed, boneIndex);

        }
        /// <summary>
        /// 获取动画片段信息, xy:帧数范围; z:动画时长;w:loop
        /// </summary>
        /// <param name="mat"></param>
        /// <param name="animClipIndex"></param>
        /// <returns></returns>
        public static Vector4 GetAnimationClipInfo(Material mat, int animClipIndex)
        {
            if (!mat.HasTexture("_AnimTex")) return Vector4.one;

            var animTex = mat.GetTexture("_AnimTex") as Texture2D;
            var animData = InitCaches(animTex);
            var result = GetAnimClipData(animData, animClipIndex);
            result.z /= mat.GetFloat("_AnimSpeed");
            return GetAnimClipData(animData, animClipIndex);
        }

        //public static void SetTransDuration(Material mat, float Duration)
        //{
        //    if (mat.HasProperty("_AnimTransDuration"))
        //    {
        //       var tranDuration= mat.GetFloat("_AnimTransDuration");
        //        mat.SetFloat("_AnimTransDuration", Duration);
        //    }

        //}


        public static int GetAnimationCurrentFrame(int startFrame, int endFrame, float animLength, bool loop, float startPlayTime, float animSpeed)
        {
            float time = Time.time; // 获取当前时间
            float elapsed = (time - startPlayTime) * animSpeed; // 计算经过的时间

            // 如果是循环动画，使用模运算来计算当前时间
            if (loop)
            {
                elapsed = Mathf.Repeat(elapsed, animLength);
            }

            // 计算动画进度
            float progress = elapsed / animLength;
            progress = Mathf.Clamp01(progress); // 确保进度在0到1之间

            // 计算当前帧
            int currentFrame = (int)Mathf.Ceil((endFrame - startFrame) * progress + startFrame);
            return currentFrame;
        }




     
        public static int GetAnimationCurrentFrame(Vector4 clipInfo, float animSpeed, float startPlayTime)
        {
            int startFrame = (int)clipInfo.x;
            int endFrame = (int)clipInfo.y;
            float duration = clipInfo.z;

            float timeElapsed = Time.time - startPlayTime;
            float animationTime;

            // 处理循环动画
            if (duration > 0.001f)
            {
                animationTime = Mathf.Repeat(timeElapsed * animSpeed, duration);
            }
            else
            {
                animationTime = timeElapsed * animSpeed;
            }

            // 计算归一化时间并限制在 [0,1] 范围内
            float normalizedTime = Mathf.Clamp01(animationTime / duration);

            // 使用 lerp 计算当前帧
            float lerpedFrame = Mathf.Lerp(startFrame, endFrame, normalizedTime);
            int currentFrame = Mathf.CeilToInt(lerpedFrame);

            // 返回相对于起始帧的偏移值 + 1
            return currentFrame - startFrame + 1;
        }

        //public static GPUBoneData GetBoneTransform(AnimTexData animTex, int clipIndex, float animStartTime, float animSpeed, int boneIndex, float scale)
        //{

        //    var clipDt = GetAnimClipData(animTex, clipIndex);

        //    float animPlayTime;
        //    if (clipDt.w > 0.1f) //Loop类型
        //    {
        //        animPlayTime = ((Time.time - animStartTime) * animSpeed) % clipDt.z;
        //    }
        //    else
        //    {
        //        animPlayTime = (Time.time - animStartTime) * animSpeed;
        //    }

        //    int curAnimFrame = Mathf.RoundToInt(Mathf.Lerp(clipDt.x, clipDt.y, Mathf.Clamp01(animPlayTime / clipDt.z)));
        //    int bonesCount = (animTex.TexSize.x - 1) / 3;
        //    Matrix4x4 result = new Matrix4x4();
        //    result.SetRow(0, animTex.GetPixel(boneIndex, curAnimFrame));
        //    result.SetRow(1, animTex.GetPixel(boneIndex + bonesCount, curAnimFrame));
        //    result.SetRow(2, animTex.GetPixel(boneIndex + bonesCount * 2, curAnimFrame));
        //    return new GPUBoneData(result.GetPosition(), Quaternion.identity, Vector3.one, curAnimFrame);
        //}




        public static GPUBoneData GetBoneTransform(Texture2D animTex, int clipIndex, float animStartTime, float animSpeed, int boneIndex)
        {
            var clipDt = GetAnimClipData(animTex, clipIndex);

            float animPlayTime;
            if (clipDt.w > 0.1f) //Loop类型
            {
                animPlayTime = ((Time.time - animStartTime) * animSpeed) % clipDt.z;
            }
            else
            {
                animPlayTime = (Time.time - animStartTime) * animSpeed;
            }

            int curAnimFrame = Mathf.RoundToInt(Mathf.Lerp(clipDt.x, clipDt.y, Mathf.Clamp01(animPlayTime / clipDt.z)));
            int bonesCount = (animTex.width - 1) / 3;
            Matrix4x4 result = new Matrix4x4();
            result.SetRow(0, animTex.GetPixel(boneIndex, curAnimFrame));
            result.SetRow(1, animTex.GetPixel(boneIndex + bonesCount, curAnimFrame));
            result.SetRow(2, animTex.GetPixel(boneIndex + bonesCount * 2, curAnimFrame));

            return new GPUBoneData(result.GetPosition(), result.rotation, result.lossyScale, curAnimFrame);
        }
        private static GPUBoneData GetBoneTransform(AnimTexData animTex, int clipIndex, float animStartTime, float animSpeed, int boneIndex, float scale)
        {
            var clipDt = GetAnimClipData(animTex, clipIndex);

            float animPlayTime;
            if (clipDt.w > 0.1f) //Loop类型
            {
                animPlayTime = ((Time.time - animStartTime) * animSpeed) % clipDt.z;
            }
            else
            {
                animPlayTime = (Time.time - animStartTime) * animSpeed;
            }

            int curAnimFrame = Mathf.RoundToInt(Mathf.Lerp(clipDt.x, clipDt.y, Mathf.Clamp01(animPlayTime / clipDt.z)));
            int bonesCount = (animTex.TexSize.x - 1) / 3;
            Matrix4x4 result = new Matrix4x4();
            result.SetRow(0, animTex.GetPixel(boneIndex, curAnimFrame));
            result.SetRow(1, animTex.GetPixel(boneIndex + bonesCount, curAnimFrame));
            result.SetRow(2, animTex.GetPixel(boneIndex + bonesCount * 2, curAnimFrame));
            return new GPUBoneData(result.GetPosition() * scale, Quaternion.identity, Vector3.one * scale, curAnimFrame);
        }
        /// <summary>
        /// 返回动画Clip信息: x:起始像素y坐标; y:结束像素y坐标; z:动画长度s; w:动画Loop
        /// </summary>
        /// <param name="animTex"></param>
        /// <param name="clipIndex"></param>
        /// <returns></returns>
        public static Vector4 GetAnimClipData(Texture2D animTex, int clipIndex)
        {
            Vector4 col = animTex.GetPixel(animTex.width - 1, clipIndex);
            return col;
        }
        private static Vector4 GetAnimClipData(AnimTexData animTex, int clipIndex)
        {
            Vector4 col = animTex.GetPixel(animTex.TexSize.x - 1, clipIndex);
            return col;
        }
        private static int GetBoneIndex(Texture2D animTex, int attachBoneId)
        {
            int texHeight = animTex.height;
            attachBoneId = UnityEngine.Mathf.Clamp(attachBoneId, 0, texHeight - 1);
            var col = animTex.GetPixel(animTex.width - 1, texHeight - 1 - attachBoneId);
            if (col.g == 0) return attachBoneId;
            return Mathf.RoundToInt(col.r);
        }
        private static int GetBoneIndex(AnimTexData animTexData, int attachBoneId)
        {
            int texHeight = animTexData.TexSize.y;
            attachBoneId = UnityEngine.Mathf.Clamp(attachBoneId, 0, texHeight - 1);
            var col = animTexData.GetPixel(animTexData.TexSize.x - 1, texHeight - 1 - attachBoneId);
            if (col.g == 0) return attachBoneId;
            return Mathf.RoundToInt(col.r);
        }
        public static int PixelCoord2Index(Vector2Int texSize, int x, int y)
        {
            return texSize.x * y + x;
        }
        private static AnimTexData InitCaches(Texture2D animTex)
        {
            if (m_CachedTextures == null)
            {
                m_CachedTextures = new Dictionary<int, AnimTexData>();
            }
            AnimTexData result;
            int instanceId = animTex.GetInstanceID();
            if (!m_CachedTextures.TryGetValue(instanceId, out result))
            {
                result = new AnimTexData(animTex);
                m_CachedTextures.Add(instanceId, result);
            }
            return result;
        }

        /// <summary>
        /// 获取骨骼名称
        /// </summary>
        /// <param name="animTex">动画纹理</param>
        /// <param name="boneIndex">骨骼索引</param>
        /// <returns>骨骼名称</returns>
        public static string GetBoneName(Texture2D animTex, int boneIndex)
        {
            if (animTex == null)
            {
                Debug.LogWarning("GetBoneName: animTex为空");
                return $"Bone_{boneIndex}";
            }

            // 尝试从JSON文件获取骨骼名称
#if UNITY_EDITOR
            string texturePath = UnityEditor.AssetDatabase.GetAssetPath(animTex);
#else
// 在构建版本中使用的替代逻辑，可能是：
string texturePath = ""; // 或者其他方式获取路径
#endif
            if (string.IsNullOrEmpty(texturePath))
            {
                Debug.LogWarning("GetBoneName: 无法获取纹理路径");
                return $"Bone_{boneIndex}";
            }

            Debug.Log($"纹理路径: {texturePath}");

            // 构造JSON文件路径
            string directory = System.IO.Path.GetDirectoryName(texturePath);
            string fileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(texturePath);

            Debug.Log($"目录: {directory}, 文件名: {fileNameWithoutExt}");

            // 移除"_bones_anim"后缀，如果有的话
            if (fileNameWithoutExt.EndsWith("_bones_anim"))
                fileNameWithoutExt = fileNameWithoutExt.Substring(0, fileNameWithoutExt.Length - 11);

            string jsonPath = System.IO.Path.Combine(directory, $"{fileNameWithoutExt}_bone_names.json");

            Debug.Log($"查找骨骼名称映射文件: {jsonPath}");

            // 检查文件是否存在
            if (!System.IO.File.Exists(jsonPath))
            {
                Debug.LogWarning($"找不到骨骼名称映射文件: {jsonPath}");

                // 尝试列出目录中的所有JSON文件
                try
                {
                    string[] jsonFiles = System.IO.Directory.GetFiles(directory, "*.json");
                    Debug.Log($"目录中的JSON文件数量: {jsonFiles.Length}");
                    foreach (string file in jsonFiles)
                    {
                        Debug.Log($"  发现JSON文件: {file}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"列出目录文件时出错: {e.Message}");
                }

                return $"Bone_{boneIndex}";
            }

            // 从缓存中获取或加载骨骼名称映射
            Dictionary<int, string> boneNames;
            if (!s_BoneNameJsonCache.TryGetValue(jsonPath, out boneNames))
            {
                try
                {
                    string json = System.IO.File.ReadAllText(jsonPath);
                    Debug.Log($"加载的JSON内容: {json.Substring(0, Math.Min(100, json.Length))}...");

                    // 手动解析JSON
                    boneNames = ParseBoneNamesJson(json);
                    s_BoneNameJsonCache[jsonPath] = boneNames;

                    Debug.Log($"解析出的骨骼名称数量: {boneNames.Count}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"加载骨骼名称映射文件时出错: {e.Message}");
                    return $"Bone_{boneIndex}";
                }
            }

            // 从映射中获取骨骼名称
            string boneName;
            if (boneNames.TryGetValue(boneIndex, out boneName))
            {
                Debug.Log($"找到骨骼名称: 索引={boneIndex}, 名称={boneName}");
                return boneName;
            }

            Debug.LogWarning($"在映射中找不到骨骼索引: {boneIndex}");
            return $"Bone_{boneIndex}";
        }

        /// <summary>
        /// 添加一个调试方法，用于DrawAttachBone脚本
        /// </summary>
        public static void DebugBoneNameEncoding(Texture2D animTex)
        {
            if (animTex == null || !animTex.isReadable)
            {
                Debug.LogError("纹理为空或不可读");
                return;
            }

            Debug.Log($"纹理尺寸: {animTex.width}x{animTex.height}");

            // 检查特殊标记
            Color marker = animTex.GetPixel(animTex.width - 1, 0);
            Debug.Log($"特殊标记像素: R={marker.r:F3}, G={marker.g:F3}, B={marker.b:F3}, A={marker.a:F3}");
            Debug.Log($"期望的标记alpha值: {123f / 255f:F3}，差值: {Mathf.Abs(marker.a - (123f / 255f)):F3}");

            // 检查最后几行，查找骨骼索引映射
            Debug.Log("检查骨骼索引映射...");
            for (int row = animTex.height - 1; row >= animTex.height - 20; row--)
            {
                if (row < 0) continue;

                Color pixel = animTex.GetPixel(animTex.width - 1, row);
                Debug.Log($"行 {row} 的映射像素: R={pixel.r:F3}, G={pixel.g:F3}, B={pixel.b:F3}, A={pixel.a:F3}");
            }

            // 检查纹理中部，查找骨骼名称数据
            Debug.Log("检查骨骼名称数据...");
            int startSearchRow = animTex.height / 2;
            for (int row = startSearchRow; row >= startSearchRow - 50; row--)
            {
                if (row < 0) continue;

                Color infoPixel = animTex.GetPixel(0, row);
                if (infoPixel.r > 0 || infoPixel.g > 0)
                {
                    int boneIndex = Mathf.RoundToInt(infoPixel.r * 255);
                    int nameLength = Mathf.RoundToInt(infoPixel.g * 255);
                    Debug.Log($"行 {row} 可能包含骨骼数据: 骨骼索引={boneIndex}, 名称长度={nameLength}");

                    // 显示该行的后续像素
                    for (int i = 1; i <= 10; i++)
                    {
                        if (i >= animTex.width) break;

                        Color namePixel = animTex.GetPixel(i, row);
                        Debug.Log($"  名称像素 {i}: R={namePixel.r:F3}, G={namePixel.g:F3}, B={namePixel.b:F3}, A={namePixel.a:F3}");

                        // 尝试解码这些字节
                        byte[] bytes = new byte[4];
                        bytes[0] = (byte)(namePixel.r * 255);
                        bytes[1] = (byte)(namePixel.g * 255);
                        bytes[2] = (byte)(namePixel.b * 255);
                        bytes[3] = (byte)(namePixel.a * 255);

                        string decodedChars = "";
                        for (int b = 0; b < 4; b++)
                        {
                            if (bytes[b] >= 32 && bytes[b] <= 126) // 可打印ASCII字符
                                decodedChars += (char)bytes[b];
                            else
                                decodedChars += $"[{bytes[b]}]";
                        }
                        Debug.Log($"  解码为: {decodedChars}");
                    }
                }
            }
        }

        /// <summary>
        /// 获取附加骨骼的名称
        /// </summary>
        /// <param name="animTex">动画纹理</param>
        /// <param name="attachBoneId">附加骨骼ID</param>
        /// <returns>骨骼名称</returns>
        public static string GetAttachBoneName(Texture2D animTex, int attachBoneId)
        {
            if (animTex == null)
                return $"Bone_{attachBoneId}";

            var animData = InitCaches(animTex);
            var boneIndex = GetBoneIndex(animData, attachBoneId);
            return GetBoneName(animTex, boneIndex);
        }

        /// <summary>
        /// 获取附加骨骼的名称
        /// </summary>
        /// <param name="mat">材质</param>
        /// <param name="attachBoneId">附加骨骼ID</param>
        /// <returns>骨骼名称</returns>
        public static string GetAttachBoneName(Material mat, int attachBoneId)
        {
            if (mat == null || !mat.HasProperty("_AnimTex"))
                return $"Bone_{attachBoneId}";

            var animTex = mat.GetTexture("_AnimTex") as Texture2D;
            return GetAttachBoneName(animTex, attachBoneId);
        }

        /// <summary>
        /// 解析骨骼名称JSON文件
        /// </summary>
        /// <param name="json">JSON字符串</param>
        /// <returns>骨骼索引到名称的映射</returns>
        public static Dictionary<int, string> ParseBoneNamesJson(string json)
        {
            Dictionary<int, string> result = new Dictionary<int, string>();

            try
            {
                // 查找"boneNames"对象
                int boneNamesStart = json.IndexOf("\"boneNames\"");
                if (boneNamesStart == -1) return result;

                // 查找对象开始
                int objectStart = json.IndexOf('{', boneNamesStart);
                if (objectStart == -1) return result;

                // 查找对象结束
                int objectEnd = json.IndexOf('}', objectStart);
                if (objectEnd == -1) return result;

                // 提取对象内容
                string boneNamesObject = json.Substring(objectStart + 1, objectEnd - objectStart - 1);

                // 分割成键值对
                string[] pairs = boneNamesObject.Split(',');
                foreach (string pair in pairs)
                {
                    // 跳过空白行
                    if (string.IsNullOrWhiteSpace(pair)) continue;

                    // 分割键和值
                    int colonIndex = pair.IndexOf(':');
                    if (colonIndex == -1) continue;

                    string keyStr = pair.Substring(0, colonIndex).Trim();
                    string valueStr = pair.Substring(colonIndex + 1).Trim();

                    // 移除引号和空格
                    keyStr = keyStr.Trim('"', ' ', '\n', '\r', '\t');
                    valueStr = valueStr.Trim('"', ' ', '\n', '\r', '\t');

                    // 转换键为整数
                    if (int.TryParse(keyStr, out int key))
                    {
                        result[key] = valueStr;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"解析骨骼名称JSON时出错: {e.Message}");
            }

            return result;
        }
    }


}

