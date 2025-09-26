using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.IO;
using SoxwareInteractive.AnimationConversion;
using UnityEngine.Rendering;
using System;
using System.Linq;
using GPUAnimation.Runtime;
namespace GPUAnimation.Editor
{


    public class GPUAnimationConverterMerge : EditorWindow
    {
        enum GPUAnimMode
        {
            /// <summary>
            /// gpu顶点动画
            /// 性能更好,但贴图较大
            /// </summary>
            Vertex,
            /// <summary>
            /// gpu骨骼动画
            /// 性能比顶点动画略低,但贴图小很多
            /// </summary>
            Bone
        }
        readonly string[] GPU_ANIM_SHADERS = { "GPUAnimation/GPUVertexAnimLit", "GPUAnimation/GPUBonesAnimLit" };
        readonly string[] GPU_ANIM_MODE_DOC = { "GPU顶点动画性能更高, 但贴图较大", "GPU骨骼动画性能比GPU顶点动画略低,但贴图较小" };
        AnimationConverter.Configuration m_Config;
        ReorderableList m_ClipsList;
        List<AnimationClip> m_Clips;

        Vector2 m_ScrollPos = Vector2.zero;
        Vector2 m_ScrollPosBindBones = Vector2.zero;

        ReorderableList m_BindBonesRList;
        List<GameObject> m_BindBones;
        List<int> m_SkinUseBones;
        GPUAnimMode m_AnimMode = GPUAnimMode.Bone;
        Shader m_Shader;
        [MenuItem("Game Framework/GPU Animation/GPU Animation Merge Mesh Converter ")]
        public static GPUAnimationConverterMerge ShowWindow()
        {
            var win = GetWindow<GPUAnimationConverterMerge>();
            win.Show(true);
            return win;
        }
        private void OnEnable()
        {
            if (m_Config == null)
                m_Config = new AnimationConverter.Configuration
                {
                    ConstrainRootMotionPosition = new AnimationConverter.ConstrainMask(true, false, true),
                    ConstrainRootMotionRotation = new AnimationConverter.ConstrainMask(false, false, false),
                    DestinationAnimationType = AnimationConverter.AnimationType.Legacy,
                    GenerateGenericRootMotion = true,
                    HumanoidKeepExtraGenericBones = false,
                    KeyReduction = AnimationConverter.KeyReductionMode.Lossless,
                    KeyReductionPositionError = 0.5f,
                    KeyReductionRotationError = 0.5f,
                    KeyReductionScaleError = 0.5f,
                    SampleHumanoidFootIK = true,
                    SampleHumanoidHandIK = true,
                    OutputDirectory = "Assets/GPUAnimation/",
                    Prefabs = new AnimationConverter.PrefabPair[1],
                };
            m_Clips = new List<AnimationClip>();
            m_ClipsList = new ReorderableList(m_Clips, typeof(AnimationClip), true, true, true, true);
            m_ClipsList.drawHeaderCallback = DrawClipsListHeader;
            m_ClipsList.drawElementCallback = DrawClipsListElement;

            m_BindBones = new List<GameObject>();
            m_BindBonesRList = new ReorderableList(m_BindBones, typeof(GameObject), true, true, true, true);
            m_BindBonesRList.drawHeaderCallback = DrawBindBonesRListHeader;
            m_BindBonesRList.drawElementCallback = DrawBindBonesRListElement;
            m_BindBonesRList.onAddCallback = BindBonesRListAddCallback;
            m_AnimMode = (GPUAnimMode)EditorPrefs.GetInt("GPUAnim.AnimMode", (int)GPUAnimMode.Bone);
            m_Shader = Shader.Find(GPU_ANIM_SHADERS[(int)m_AnimMode]);
            m_SkinUseBones = new List<int>();
        }


        private void OnDisable()
        {
            // 保存动画模式设置
            EditorPrefs.SetInt("GPUAnim.AnimMode", (int)m_AnimMode);

            // 清理资源
            CleanupResources();
        }

        private void OnDestroy()
        {
            CleanupResources();
        }

        private void CleanupResources()
        {
            // 清理列表
            if (m_Clips != null)
            {
                m_Clips.Clear();
            }

            if (m_BindBones != null)
            {
                m_BindBones.Clear();
            }

            if (m_SkinUseBones != null)
            {
                m_SkinUseBones.Clear();
            }

            // 清理配置
            m_Config = null;
            m_ClipsList = null;
            m_BindBonesRList = null;
            m_Shader = null;

            // 强制GC回收
            System.GC.Collect();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.BeginHorizontal(GUILayout.Height(30));
                {
                    EditorGUI.BeginChangeCheck();
                    {
                        m_Config.Prefabs[0].SourcePrefab = EditorGUILayout.ObjectField("Input:", m_Config.Prefabs[0].SourcePrefab, typeof(GameObject), true) as GameObject;
                        if (EditorGUI.EndChangeCheck())
                        {
                            if (EditorUtility.DisplayDialog("Input changed", "Refresh animation clips with animator ?", "OKay", "No"))
                            {
                                RefreshAnimationClips(m_Config.Prefabs[0].SourcePrefab, ref m_Clips);
                            }
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.BeginChangeCheck();
                {
                    m_AnimMode = (GPUAnimMode)EditorGUILayout.EnumPopup("GPU Animation Mode:", m_AnimMode);
                    if (EditorGUI.EndChangeCheck())
                    {
                        m_Shader = Shader.Find(GPU_ANIM_SHADERS[(int)m_AnimMode]);
                    }
                    EditorGUILayout.HelpBox(GPU_ANIM_MODE_DOC[(int)m_AnimMode], MessageType.Info);
                }
                m_Config.KeyReduction = (AnimationConverter.KeyReductionMode)EditorGUILayout.EnumPopup("Key Reduction:", m_Config.KeyReduction);
                if (m_Config.KeyReduction == AnimationConverter.KeyReductionMode.Lossy)
                {
                    m_Config.KeyReductionPositionError = EditorGUILayout.FloatField("Position Error", m_Config.KeyReductionPositionError);
                    m_Config.KeyReductionRotationError = EditorGUILayout.FloatField("Rotation Error", m_Config.KeyReductionRotationError);
                    m_Config.KeyReductionScaleError = EditorGUILayout.FloatField("Scale Error", m_Config.KeyReductionScaleError);
                }
                m_Config.SampleHumanoidHandIK = EditorGUILayout.Toggle("Apply Humanoid Hand IK", m_Config.SampleHumanoidHandIK, GUILayout.Width(150));
                m_Config.SampleHumanoidFootIK = EditorGUILayout.Toggle("Apply Humanoid Foot IK", m_Config.SampleHumanoidFootIK, GUILayout.Width(150));

                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("Freeze Root Position", GUILayout.Width(150));
                    m_Config.ConstrainRootMotionPosition.ConstrainX = EditorGUILayout.ToggleLeft("X", m_Config.ConstrainRootMotionPosition.ConstrainX, GUILayout.Width(40));
                    m_Config.ConstrainRootMotionPosition.ConstrainY = EditorGUILayout.ToggleLeft("Y", m_Config.ConstrainRootMotionPosition.ConstrainY, GUILayout.Width(40));
                    m_Config.ConstrainRootMotionPosition.ConstrainZ = EditorGUILayout.ToggleLeft("Z", m_Config.ConstrainRootMotionPosition.ConstrainZ, GUILayout.Width(40));
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("Freeze Root Rotation", GUILayout.Width(150));
                    m_Config.ConstrainRootMotionRotation.ConstrainX = EditorGUILayout.ToggleLeft("X", m_Config.ConstrainRootMotionRotation.ConstrainX, GUILayout.Width(40));
                    m_Config.ConstrainRootMotionRotation.ConstrainY = EditorGUILayout.ToggleLeft("Y", m_Config.ConstrainRootMotionRotation.ConstrainY, GUILayout.Width(40));
                    m_Config.ConstrainRootMotionRotation.ConstrainZ = EditorGUILayout.ToggleLeft("Z", m_Config.ConstrainRootMotionRotation.ConstrainZ, GUILayout.Width(40));
                    EditorGUILayout.EndHorizontal();
                }

                m_Shader = EditorGUILayout.ObjectField("GPU Animation Shader", m_Shader, typeof(Shader), false) as Shader;
                m_ScrollPos = EditorGUILayout.BeginScrollView(m_ScrollPos);
                m_ClipsList.DoLayoutList();
                EditorGUILayout.EndScrollView();

                if (m_AnimMode == GPUAnimMode.Bone)
                {
                    m_ScrollPosBindBones = EditorGUILayout.BeginScrollView(m_ScrollPosBindBones);
                    m_BindBonesRList.DoLayoutList();
                    EditorGUILayout.EndScrollView();
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.BeginHorizontal("box");
                {
                    if (GUILayout.Button("Convert", GUILayout.Height(30)))
                    {
                        var msg = ConvertToAnimation(m_Config, m_Clips.ToArray());
                        Debug.Log("Convert Animation Result:" + msg);
                        GUIUtility.ExitGUI();
                    }

                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
        }

        private void BindBonesRListAddCallback(ReorderableList list)
        {
            m_BindBones.Add(null);
        }
        private void DrawBindBonesRListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            m_BindBones[index] = EditorGUI.ObjectField(rect, $"{index}", m_BindBones[index], typeof(GameObject), true) as GameObject;
        }

        private void DrawBindBonesRListHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "Record Bones:");
        }

        private void DrawClipsListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            m_Clips[index] = EditorGUI.ObjectField(rect, $"{index}", m_Clips[index], typeof(AnimationClip), false) as AnimationClip;
        }
        private void DrawClipsListHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "Animation Clips:");
        }

        private bool ValidateAnimator(Animator animator, GameObject prefab)
        {
            if (animator == null) return false;

            if (animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning($"Animator on {prefab.name} has no RuntimeAnimatorController");
                return false;
            }

            var clips = animator.runtimeAnimatorController.animationClips;
            if (clips == null || clips.Length == 0)
            {
                Debug.LogWarning($"No animation clips found in controller on {prefab.name}");
                return false;
            }

            return true;
        }


        private void RefreshAnimationClips(GameObject prefab, ref List<AnimationClip> clips)
        {
            if (prefab == null) return;
            var animtor = prefab.GetComponentInChildren<Animator>();

            if (!ValidateAnimator(animtor, prefab)) return;

            if (animtor != null && animtor.runtimeAnimatorController.animationClips.Length > 0)
            {
                clips.Clear();
                clips.AddRange(animtor.runtimeAnimatorController.animationClips);
                return;
            }
            var animation = prefab.GetComponentInChildren<Animation>();
            if (animation != null)
            {
                clips.Clear();
                var animClips = AnimationUtility.GetAnimationClips(animation.gameObject);
                if (animClips != null && animClips.Length > 0)
                {
                    clips.AddRange(animClips);
                }
            }
        }
        public string ConvertToAnimation(AnimationConverter.Configuration config, AnimationClip[] animationClips)
        {
            if (animationClips == null || animationClips.Length < 1)
            {
                return $"GPU动画转换失败: 请添加AnimationClips";
            }

            if (!Directory.Exists(config.OutputDirectory))
            {
                Directory.CreateDirectory(config.OutputDirectory);
                AssetDatabase.Refresh();
            }

            if (config.Prefabs == null || config.Prefabs.Length < 1 || config.Prefabs[0].SourcePrefab == null)
            {
                return $"GPU动画转换失败: 请设置要转换的Prefab";
            }
            if (config.Prefabs[0].DestinationPrefab == null)
            {
                var isPrefabInstance = PrefabUtility.IsAnyPrefabInstanceRoot(config.Prefabs[0].SourcePrefab);
                string inputAssetPath;
                if (isPrefabInstance)
                    inputAssetPath = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource<GameObject>(config.Prefabs[0].SourcePrefab));
                else
                    inputAssetPath = AssetDatabase.GetAssetPath(config.Prefabs[0].SourcePrefab);

                Debug.Log($">>>>>>>>>>>>>>>{inputAssetPath}");
                var inputDir = Path.GetDirectoryName(inputAssetPath);
                var outputPath = Path.Combine(inputDir, $"{config.Prefabs[0].SourcePrefab.name}_GPU_Anim", $"{config.Prefabs[0].SourcePrefab.name}.prefab");
                var outputDir = Path.GetDirectoryName(outputPath);
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                    AssetDatabase.Refresh();
                }
                config.Prefabs[0].DestinationPrefab = GenerateOutputPrefab(outputPath, config.Prefabs[0].SourcePrefab);
                config.OutputDirectory = outputDir;
            }

            // 检查是否有动画事件，只有在有事件时才创建事件数据资产
            bool hasAnimationEvents = false;
            for (int clipIndex = 0; clipIndex < animationClips.Length; clipIndex++)
            {
                var clip = animationClips[clipIndex];
                var events = AnimationUtility.GetAnimationEvents(clip);
                if (events != null && events.Length > 0)
                {
                    hasAnimationEvents = true;
                    break;
                }
            }

            // 创建事件数据资产 - 即使没有事件也创建，以确保系统兼容性
            string eventDataPath = Path.Combine(config.OutputDirectory, $"{config.Prefabs[0].SourcePrefab.name}_events.asset");
            var eventData = ScriptableObject.CreateInstance<GPUAnimationEventData>();

            // 遍历所有动画片段，提取事件数据
            for (int clipIndex = 0; clipIndex < animationClips.Length; clipIndex++)
            {
                var clip = animationClips[clipIndex];
                var events = AnimationUtility.GetAnimationEvents(clip);

                if (events != null && events.Length > 0)
                {
                    foreach (var evt in events)
                    {
                        int frame = Mathf.FloorToInt(evt.time * clip.frameRate);
                        if (!string.IsNullOrEmpty(evt.functionName))
                            eventData.Add(clipIndex, frame, evt.functionName);
                    }
                }
                else
                {
                    // 如果没有事件，添加一个frame为-1的空事件
                    // 这对于系统兼容性很重要
                    eventData.Add(clipIndex, -1, "");
                }
            }

            // 如果文件已存在则删除
            if (File.Exists(eventDataPath))
            {
                AssetDatabase.DeleteAsset(eventDataPath);
            }

            // 创建新的资产
            AssetDatabase.CreateAsset(eventData, eventDataPath);
            AssetDatabase.SaveAssets();

            var animtor = config.Prefabs[0].SourcePrefab.GetComponentInChildren<Animator>();
            bool convert2Animation = false;
            if (animtor != null)
            {
                convert2Animation = true;
            }
            else
            {
                var animation = config.Prefabs[0].SourcePrefab.GetComponentInChildren<Animation>();
                if (animation == null && AnimationUtility.GetAnimationClips(animation.gameObject).Length < 1)
                {
                    return $"GPU动画转换失败: Prefab({config.Prefabs[0].SourcePrefab.name})未找到Animator或Animation组件";
                }
            }
            string logMessage = string.Empty;
            if (convert2Animation)
            {
                AnimationConverter.Convert(animationClips, config, out logMessage);
            }
            TryAssignAnimationClips(config.OutputDirectory, config.Prefabs[0].DestinationPrefab, m_Clips);

            switch (m_AnimMode)
            {
                case GPUAnimMode.Vertex:
                    {
                        BakeVertexAnim(config.Prefabs[0].DestinationPrefab, config.OutputDirectory);
                    }
                    break;
                case GPUAnimMode.Bone:
                    {
                        BakeBonesAnim(config.Prefabs[0].DestinationPrefab, config.OutputDirectory);
                    }
                    break;
            }

            //删除临时文件
            if (convert2Animation)
            {
                var tempClips = AnimationUtility.GetAnimationClips(config.Prefabs[0].DestinationPrefab);
                foreach (var curClip in tempClips)
                {
                    var clipAsset = AssetDatabase.GetAssetPath(curClip);
                    AssetDatabase.DeleteAsset(clipAsset);
                }

                var tempPrefab = AssetDatabase.GetAssetPath(config.Prefabs[0].DestinationPrefab);
                AssetDatabase.DeleteAsset(tempPrefab);
            }

            // 转换完成后，强制刷新资源
            AssetDatabase.Refresh();

            // 添加延迟处理，确保资源完全加载
            EditorApplication.delayCall += () => {
                FinalizeConversion(config.OutputDirectory, config.Prefabs[0].DestinationPrefab);
            };

            return $"GPU动画转换成功:";
        }
        private void BakeBonesAnim(GameObject animGameObject, string outputDir)
        {
            try
            {
                // 获取动画组件
                var animation = animGameObject.GetComponentInChildren<Animation>();
                var clips = AnimationUtility.GetAnimationClips(animation.gameObject); // 获取所有动画剪辑
                m_SkinUseBones.Clear(); // 清空皮肤使用的骨骼列表
                var bones = animGameObject.GetComponentsInChildren<Transform>(true); // 获取所有骨骼（Transform组件）
                int bonesCount = bones.Length; // 获取骨骼数量
                var bonesL2WMatrices = new Matrix4x4[bonesCount]; // 本地到世界的矩阵数组
                var bonesW2LMatrices = new Matrix4x4[bonesCount]; // 世界到本地的矩阵数组
                Dictionary<string, int> bonesIndexes = new Dictionary<string, int>(); // 存储骨骼路径和索引的字典
                HashSet<string> positionBones = new HashSet<string>(); // 存储位置骨骼的集合
                HashSet<string> rotationBones = new HashSet<string>(); // 存储旋转骨骼的集合
                HashSet<string> scaleBones = new HashSet<string>(); // 存储缩放骨骼的集合

                // 遍历所有骨骼，初始化骨骼矩阵和索引
                for (int i = 0; i < bonesCount; i++)
                {
                    var bone = bones[i];
                    var bonePath = GetNodePath(animGameObject.transform, bone); // 获取骨骼的节点路径
                    bonesIndexes.Add(bonePath, i); // 将骨骼路径和索引添加到字典中
                    bonesL2WMatrices[i] = bone.localToWorldMatrix; // 获取骨骼的本地到世界矩阵
                    bonesW2LMatrices[i] = bone.worldToLocalMatrix; // 获取骨骼的世界到本地矩阵
                }

                int pixelCount = 0; // 像素计数
                int totalFrameCount = 0; // 总帧数计数

                // 遍历所有动画剪辑，计算总帧数和像素总数
                for (int i = 0; i < clips.Length; i++)
                {
                    var clip = clips[i];
                    var clipFrameCount = Mathf.CeilToInt(clip.frameRate * clip.length); // 计算当前剪辑的帧数
                    totalFrameCount += clipFrameCount; // 累加总帧数
                    pixelCount += bonesCount * 3 * clipFrameCount; // 每个骨骼的3个分量（位置、旋转、缩放）乘以帧数
                }
                pixelCount += totalFrameCount; // 增加一列像素用于储存剪辑信息

                // 计算纹理的宽度和高度
                int texWidth = bonesCount * 3 + 1; // 纹理宽度（骨骼数量 * 3 + 1列用于存储信息）
                int texHeight = Mathf.CeilToInt(pixelCount / (float)texWidth); // 纹理高度

                // 创建一个新的纹理用于存储骨骼动画信息
                Texture2D animBoneTex = new Texture2D(texWidth, texHeight, TextureFormat.RGBAHalf, false, true);
                animBoneTex.filterMode = FilterMode.Point; // 设置纹理过滤模式为点过滤
                string texturePath = string.Format("{0}/{1}_bones_anim.asset", outputDir, animGameObject.name);

                // 如果文件已存在则删除
                if (System.IO.File.Exists(texturePath))
                {
                    UnityEditor.AssetDatabase.DeleteAsset(texturePath);
                    // 确保资产数据库已刷新
                    UnityEditor.AssetDatabase.Refresh();
                }

                // 创建纹理资产
                UnityEditor.AssetDatabase.CreateAsset(animBoneTex, texturePath);

                // 创建GPU骨骼动画资产
                CreateGPUBonesAnimAssets(animGameObject.transform, animBoneTex, bonesIndexes, bonesL2WMatrices, bonesW2LMatrices, outputDir);
                RecordBones(animBoneTex, bonesIndexes); // 记录骨骼信息

                // 保存骨骼名称到JSON文件
                Debug.Log($"开始保存骨骼名称映射，骨骼数量: {bonesIndexes.Count}");
                SaveBoneNamesToJson(outputDir, animGameObject.name, bonesIndexes);

                int frameStartIndex = 0; // 当前帧的起始索引

                // 遍历每个动画剪辑，记录每帧骨骼的变换信息
                for (int i = 0; i < clips.Length; i++)
                {
                    var clip = clips[i];
                    var curveBindings = AnimationUtility.GetCurveBindings(clip); // 获取当前剪辑的曲线绑定
                    positionBones.Clear(); // 清空位置骨骼集合
                    rotationBones.Clear(); // 清空旋转骨骼集合
                    scaleBones.Clear(); // 清空缩放骨骼集合

                    // 遍历曲线绑定，确定每个骨骼的变换类型
                    foreach (var curveBinding in curveBindings)
                    {
                        if (!bonesIndexes.ContainsKey(curveBinding.path)) // 如果骨骼索引中没有这个路径
                        {
                            continue; // 跳过
                        }

                        // 根据属性名称确定骨骼变换类型
                        if (!positionBones.Contains(curveBinding.path) && curveBinding.propertyName.StartsWith("m_LocalPosition"))
                            positionBones.Add(curveBinding.path);
                        if (!rotationBones.Contains(curveBinding.path) && curveBinding.propertyName.StartsWith("m_LocalRotation"))
                            rotationBones.Add(curveBinding.path);
                        if (!scaleBones.Contains(curveBinding.path) && curveBinding.propertyName.StartsWith("m_LocalScale"))
                            scaleBones.Add(curveBinding.path);
                    }

                    var clipFrameCount = Mathf.CeilToInt(clip.frameRate * clip.length); // 当前剪辑的帧数
                    var clipFrameRange = new Vector2Int(frameStartIndex, frameStartIndex + clipFrameCount - 1); // 当前剪辑的帧范围
                    animBoneTex.SetPixel(texWidth - 1, i, new Vector4(clipFrameRange.x, clipFrameRange.y, clip.length, clip.isLooping ? 1 : 0)); // 设置剪辑信息

                    // 每一根骨骼 每一帧的矩阵
                    for (int frameIdx = 0; frameIdx < clipFrameCount; frameIdx++)
                    {
                        float frameTime = frameIdx / clip.frameRate; // 计算当前帧的时间

                        // 保存动画每帧的骨骼位置、旋转和缩放
                        foreach (var bonePath in positionBones)
                        {
                            int boneIndex = bonesIndexes[bonePath]; // 获取骨骼索引
                            var bonePosition = GetBonePositionAtTime(bonePath, clip, frameTime); // 获取骨骼在当前时间的位置信息
                            bones[boneIndex].localPosition = bonePosition; // 设置骨骼的本地位置
                        }
                        foreach (var bonePath in rotationBones)
                        {
                            int boneIndex = bonesIndexes[bonePath];
                            var boneRotation = GetBoneRotationAtTime(bonePath, clip, frameTime); // 获取骨骼在当前时间的旋转信息
                            bones[boneIndex].localRotation = boneRotation; // 设置骨骼的本地旋转
                        }
                        foreach (var bonePath in scaleBones)
                        {
                            int boneIndex = bonesIndexes[bonePath];
                            var boneScale = GetBoneScaleAtTime(bonePath, clip, frameTime); // 获取骨骼在当前时间的缩放信息
                            bones[boneIndex].localScale = boneScale; // 设置骨骼的本地缩放
                        }

                        int curFrameIndex = frameStartIndex + frameIdx; // 当前帧的索引
                        for (int boneIdx = 0; boneIdx < bones.Length; boneIdx++)
                        {
                            var bone = bones[boneIdx];
                            bool isSkineBone = m_SkinUseBones.Contains(boneIdx); // 判断是否为皮肤骨骼
                            var boneMatrix = bone.localToWorldMatrix; // 获取骨骼的本地到世界矩阵
                            if (isSkineBone)
                            {
                                // 如果是皮肤骨骼，则将世界到本地矩阵应用于骨骼矩阵
                                boneMatrix *= bonesW2LMatrices[boneIdx];
                            }

                            // 将骨骼矩阵的三个分量设置到纹理中
                            animBoneTex.SetPixel(boneIdx, curFrameIndex, boneMatrix.GetRow(0)); // 设置X分量
                            animBoneTex.SetPixel(bonesCount + boneIdx, curFrameIndex, boneMatrix.GetRow(1)); // 设置Y分量
                            animBoneTex.SetPixel(bonesCount * 2 + boneIdx, curFrameIndex, boneMatrix.GetRow(2)); // 设置Z分量
                        }
                    }
                    frameStartIndex += clipFrameCount; // 更新帧起始索引
                }
                animBoneTex.Apply(true, false); // 强制应用纹理更改，不生成mipmap
                UnityEditor.EditorUtility.SetDirty(animBoneTex); // 标记纹理为脏，确保保存
                UnityEditor.AssetDatabase.SaveAssetIfDirty(animBoneTex);

                // 强制保存所有资产
                UnityEditor.AssetDatabase.SaveAssets();

                // 设置纹理导入器属性
                string assetPath = UnityEditor.AssetDatabase.GetAssetPath(animBoneTex);
                UnityEditor.TextureImporter importer = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.TextureImporter;
                if (importer != null)
                {
                    importer.isReadable = true;
                    importer.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
                    importer.filterMode = FilterMode.Point;
                    importer.mipmapEnabled = false;
                    importer.SaveAndReimport();
                }

                // 再次刷新资源数据库，确保所有资产都被正确识别
                UnityEditor.AssetDatabase.Refresh();
            }
            finally
            {
                // 确保清理所有临时对象和资源
                if (animGameObject != null)
                {
                    var tempComponents = animGameObject.GetComponentsInChildren<Component>();
                    foreach (var component in tempComponents)
                    {
                        if (component != null && component is Animator)
                        {
                            DestroyImmediate(component);
                        }
                    }
                }
            }
        }

        // 修改SaveBoneNamesToJson方法，添加更多调试信息
        private void SaveBoneNamesToJson(string outputDir, string prefabName, Dictionary<string, int> bonesIndexes)
        {
            try
            {
                // 创建一个反向映射，从索引到名称
                Dictionary<int, string> indexToName = new Dictionary<int, string>();
                foreach (var pair in bonesIndexes)
                {
                    string[] pathParts = pair.Key.Split('/');
                    string boneName = pathParts[pathParts.Length - 1];
                    indexToName[pair.Value] = boneName;

                    // 添加调试日志
                    Debug.Log($"骨骼映射: 索引={pair.Value}, 路径={pair.Key}, 名称={boneName}");
                }

                // 添加皮肤使用的骨骼
                if (m_SkinUseBones != null && m_SkinUseBones.Count > 0)
                {
                    Debug.Log($"添加皮肤骨骼，数量: {m_SkinUseBones.Count}");
                    foreach (var boneIndex in m_SkinUseBones)
                    {
                        if (!indexToName.ContainsKey(boneIndex))
                        {
                            // 尝试找到骨骼名称
                            string boneName = "SkinBone_" + boneIndex;

                            // 遍历所有骨骼路径，找到对应索引的骨骼
                            foreach (var pair in bonesIndexes)
                            {
                                if (pair.Value == boneIndex)
                                {
                                    string[] pathParts = pair.Key.Split('/');
                                    boneName = pathParts[pathParts.Length - 1];
                                    break;
                                }
                            }

                            indexToName[boneIndex] = boneName;
                            Debug.Log($"添加皮肤骨骼: 索引={boneIndex}, 名称={boneName}");
                        }
                    }
                }

                // 将映射转换为JSON格式的字符串
                string json = "{\n";
                json += "  \"boneNames\": {\n";
                int count = 0;
                foreach (var pair in indexToName)
                {
                    json += $"    \"{pair.Key}\": \"{pair.Value}\"";
                    if (count < indexToName.Count - 1)
                        json += ",\n";
                    else
                        json += "\n";
                    count++;
                }
                json += "  }\n";
                json += "}";

                // 确保输出目录存在
                if (!System.IO.Directory.Exists(outputDir))
                {
                    System.IO.Directory.CreateDirectory(outputDir);
                    Debug.Log($"创建输出目录: {outputDir}");
                }

                // 保存到文件
                string filePath = System.IO.Path.Combine(outputDir, $"{prefabName}_bone_names.json");
                System.IO.File.WriteAllText(filePath, json);

                // 刷新资源数据库，确保Unity能够识别新文件
                UnityEditor.AssetDatabase.Refresh();

                Debug.Log($"骨骼名称映射已保存到: {filePath}");

                // 验证文件是否存在
                if (System.IO.File.Exists(filePath))
                {
                    Debug.Log($"确认文件已创建: {filePath}");
                    // 读取文件内容进行验证
                    string fileContent = System.IO.File.ReadAllText(filePath);
                    Debug.Log($"文件内容长度: {fileContent.Length} 字节");
                    Debug.Log($"文件内容预览: {fileContent.Substring(0, Math.Min(100, fileContent.Length))}...");
                }
                else
                {
                    Debug.LogError($"文件创建失败: {filePath}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"保存骨骼名称映射时出错: {e.Message}\n{e.StackTrace}");
            }
        }

        // 修改RecordBones方法，移除骨骼名称编码部分
        private void RecordBones(Texture2D animTex, Dictionary<string, int> bonesIndexes)
        {
            if (m_BindBones != null && m_BindBones.Count > 0)
            {
                int texHeight = animTex.height;
                for (int i = 0; i < m_BindBones.Count; i++)
                {
                    if (m_BindBones[i] == null)
                        continue;

                    var bonePath = GetNodePath(m_Config.Prefabs[0].SourcePrefab.transform, m_BindBones[i].transform);
                    if (!bonesIndexes.TryGetValue(bonePath, out int boneIndex))
                        continue;

                    // 设置骨骼映射信息
                    animTex.SetPixel(animTex.width - 1, texHeight - 1 - i, new Color(boneIndex, 1, 0, 0));
                }
            }

            // 记录皮肤使用的骨骼
            if (m_SkinUseBones != null && m_SkinUseBones.Count > 0)
            {
                int texHeight = animTex.height;
                int bindBonesCount = m_BindBones != null ? m_BindBones.Count : 0;

                for (int i = 0; i < m_SkinUseBones.Count; i++)
                {
                    int boneIndex = m_SkinUseBones[i];
                    int recordIndex = bindBonesCount + i;

                    // 设置骨骼映射信息
                    animTex.SetPixel(animTex.width - 1, texHeight - 1 - recordIndex, new Color(boneIndex, 1, 0, 0));
                }
            }

            // 应用纹理更改
            animTex.Apply();

            // 确保纹理设置为可读写
            string texturePath = AssetDatabase.GetAssetPath(animTex);
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer != null)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
        }

        private void CreateGPUBonesAnimAssets(Transform prefabRoot, Texture2D animTex, Dictionary<string, int> bonesIndies, Matrix4x4[] bonesL2WMatrices, Matrix4x4[] bonesW2LMatrices, string outputDir)
        {
            string gpuAnimPrefabName = string.Format("{0}/{1}_bones_anim.prefab", outputDir, prefabRoot.name);
            MeshRenderer gpuAnimMr;
            MeshFilter gpuAnimMf;

            GameObject gpuAnimPrefab;
            if (!File.Exists(gpuAnimPrefabName))
            {
                gpuAnimPrefab = new GameObject(Path.GetFileNameWithoutExtension(gpuAnimPrefabName), new System.Type[] { typeof(MeshFilter), typeof(MeshRenderer) });
                PrefabUtility.SaveAsPrefabAsset(gpuAnimPrefab, gpuAnimPrefabName);
                DestroyImmediate(gpuAnimPrefab);
            }
            gpuAnimPrefab = PrefabUtility.LoadPrefabContents(gpuAnimPrefabName);

            // Create a combined mesh for ALL meshes (both skinned and regular)
            string combinedMeshName = string.Format("{0}/{1}_combined_mesh.asset", outputDir, prefabRoot.name);

            // Lists to store combined mesh data
            List<Vector3> combinedVertices = new List<Vector3>();
            List<Vector3> combinedNormals = new List<Vector3>();
            List<Vector2> combinedUVs = new List<Vector2>();
            List<Vector4> combinedBoneIndices = new List<Vector4>();
            List<Vector4> combinedBoneWeights = new List<Vector4>();
            List<int> combinedTriangles = new List<int>();

            // For texture atlas creation
            List<Texture2D> texturesToCombine = new List<Texture2D>();
            List<Material> originalMaterials = new List<Material>();
            Dictionary<Texture2D, Rect> textureRects = new Dictionary<Texture2D, Rect>();
            Dictionary<Material, int> materialIndices = new Dictionary<Material, int>();

            int vertexOffset = 0;

            // First pass: collect all textures from materials
            // Process SkinnedMeshRenderers
            var skinMeshArr = prefabRoot.GetComponentsInChildren<SkinnedMeshRenderer>();
            for (int meshIdx = 0; meshIdx < skinMeshArr.Length; meshIdx++)
            {
                var skinMesh = skinMeshArr[meshIdx];
                if (skinMesh.sharedMesh == null) continue;

                foreach (var mat in skinMesh.sharedMaterials)
                {
                    if (mat == null) continue;

                    Texture2D mainTex = null;

                    // 尝试获取主纹理，考虑不同着色器的属性名称
                    if (mat.HasProperty("_MainTex"))
                        mainTex = mat.GetTexture("_MainTex") as Texture2D;
                    else if (mat.HasProperty("_BaseMap"))
                        mainTex = mat.GetTexture("_BaseMap") as Texture2D;
                    else if (mat.HasProperty("_BaseColorMap"))
                        mainTex = mat.GetTexture("_BaseColorMap") as Texture2D;
                    else if (mat.HasProperty("_DiffuseMap"))
                        mainTex = mat.GetTexture("_DiffuseMap") as Texture2D;
                    else if (mat.HasProperty("_AlbedoMap"))
                        mainTex = mat.GetTexture("_AlbedoMap") as Texture2D;

                    if (mainTex != null && !texturesToCombine.Contains(mainTex))
                    {
                        // Make sure the texture is readable
                        string texPath = AssetDatabase.GetAssetPath(mainTex);
                        if (!string.IsNullOrEmpty(texPath))
                        {
                            TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
                            if (importer != null && !importer.isReadable)
                            {
                                importer.isReadable = true;
                                importer.SaveAndReimport();
                                Debug.Log($"Made texture readable: {texPath}");
                            }
                        }

                        texturesToCombine.Add(mainTex);
                        originalMaterials.Add(mat);
                        materialIndices[mat] = texturesToCombine.Count - 1;
                    }
                }
            }

            // Process MeshRenderers
            var meshRendererArr = prefabRoot.GetComponentsInChildren<MeshRenderer>();
            for (int meshIdx = 0; meshIdx < meshRendererArr.Length; meshIdx++)
            {
                var meshRenderer = meshRendererArr[meshIdx];

                foreach (var mat in meshRenderer.sharedMaterials)
                {
                    if (mat == null) continue;

                    // 如果材质尚未处理
                    if (!originalMaterials.Contains(mat))
                    {
                        Texture2D mainTex = null;

                        // 尝试获取主纹理，考虑不同着色器的属性名称
                        if (mat.HasProperty("_MainTex"))
                            mainTex = mat.GetTexture("_MainTex") as Texture2D;
                        else if (mat.HasProperty("_BaseMap"))
                            mainTex = mat.GetTexture("_BaseMap") as Texture2D;
                        else if (mat.HasProperty("_BaseColorMap"))
                            mainTex = mat.GetTexture("_BaseColorMap") as Texture2D;
                        else if (mat.HasProperty("_DiffuseMap"))
                            mainTex = mat.GetTexture("_DiffuseMap") as Texture2D;
                        else if (mat.HasProperty("_AlbedoMap"))
                            mainTex = mat.GetTexture("_AlbedoMap") as Texture2D;

                        if (mainTex != null)
                        {
                            // Make sure the texture is readable
                            string texPath = AssetDatabase.GetAssetPath(mainTex);
                            if (!string.IsNullOrEmpty(texPath))
                            {
                                TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
                                if (importer != null && !importer.isReadable)
                                {
                                    importer.isReadable = true;
                                    importer.SaveAndReimport();
                                    Debug.Log($"Made texture readable: {texPath}");
                                }
                            }

                            texturesToCombine.Add(mainTex);
                        }

                        originalMaterials.Add(mat);
                        materialIndices[mat] = originalMaterials.Count - 1;
                    }
                }
            }

            // Create texture atlas
            Texture2D atlasTexture = null;
            if (texturesToCombine.Count > 0)
            {
                string atlasPath = string.Format("{0}/{1}_texture_atlas.png", outputDir, prefabRoot.name);

                // 创建纹理图集时使用更优的设置
                atlasTexture = new Texture2D(2048, 2048, TextureFormat.RGBA32, true);
                atlasTexture.filterMode = FilterMode.Bilinear;

                try
                {
                    // 使用更高效的打包方法
                    Rect[] rects = atlasTexture.PackTextures(texturesToCombine.ToArray(), 2, 2048, false);

                    // 存储UV重映射信息
                    for (int i = 0; i < texturesToCombine.Count; i++)
                    {
                        textureRects[texturesToCombine[i]] = rects[i];
                    }

                    // 保存和配置图集
                    byte[] bytes = atlasTexture.EncodeToPNG();
                    File.WriteAllBytes(atlasPath, bytes);
                    AssetDatabase.ImportAsset(atlasPath);

                    ConfigureTextureImporter(atlasPath);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error creating texture atlas: {e.Message}");
                    atlasTexture = null;
                }
            }

            // Create a single material with the atlas
            Material combinedMaterial = null;
            if (atlasTexture != null)
            {
                string materialPath = string.Format("{0}/{1}_combined_material.mat", outputDir, prefabRoot.name);

                // Check if material already exists
                if (File.Exists(materialPath))
                {
                    combinedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    // 确保即使是已存在的材质也设置了动画纹理
                    combinedMaterial.SetTexture("_AnimTex", animTex);
                    EditorUtility.SetDirty(combinedMaterial);
                }
                else
                {
                    // Create a new material
                    combinedMaterial = new Material(m_Shader);
                    combinedMaterial.SetTexture("_MainTex", atlasTexture);
                    combinedMaterial.SetTexture("_AnimTex", animTex);

                    AssetDatabase.CreateAsset(combinedMaterial, materialPath);
                    AssetDatabase.SaveAssets();
                }
            }

            // Second pass: process meshes and remap UVs
            // Process SkinnedMeshRenderers
            for (int meshIdx = 0; meshIdx < skinMeshArr.Length; meshIdx++)
            {
                var skinMesh = skinMeshArr[meshIdx];
                if (skinMesh.sharedMesh == null) continue;

                // 创建一个临时网格来获取当前姿势的顶点
                Mesh sharedMesh = new Mesh();
                skinMesh.BakeMesh(sharedMesh, true);

                var vertexArr = sharedMesh.vertices;
                var normalArr = sharedMesh.normals;
                var uvArr = sharedMesh.uv;
                var triangles = sharedMesh.triangles;

                // 处理骨骼权重和索引
                Vector4[] vertexBoneIndies = new Vector4[vertexArr.Length];
                Vector4[] vertexBoneWeights = new Vector4[vertexArr.Length];

                if (skinMesh.sharedMesh.bindposeCount > 0)
                {
                    var meshBones = skinMesh.bones;
                    var bonePath = GetNodePath(prefabRoot, meshBones[0]);
                    if (!bonesIndies.ContainsKey(bonePath)) continue;

                    // 修复：使用正确的变换矩阵
                    // 我们需要将顶点转换到世界空间，同时保持它们的位置
                    var meshMatrix = skinMesh.transform.localToWorldMatrix;
                    var meshWeights = skinMesh.sharedMesh.boneWeights;

                    for (int i = 0; i < vertexArr.Length; i++)
                    {
                        var weight = meshWeights[i];
                        int boneIndex0 = bonesIndies[GetNodePath(prefabRoot, meshBones[weight.boneIndex0])];
                        int boneIndex1 = bonesIndies[GetNodePath(prefabRoot, meshBones[weight.boneIndex1])];
                        int boneIndex2 = bonesIndies[GetNodePath(prefabRoot, meshBones[weight.boneIndex2])];
                        int boneIndex3 = bonesIndies[GetNodePath(prefabRoot, meshBones[weight.boneIndex3])];

                        AddSkinUseBone(boneIndex0);
                        AddSkinUseBone(boneIndex1);
                        AddSkinUseBone(boneIndex2);
                        AddSkinUseBone(boneIndex3);

                        vertexBoneIndies[i] = new Vector4(boneIndex0, boneIndex1, boneIndex2, boneIndex3);
                        vertexBoneWeights[i] = new Vector4(weight.weight0, weight.weight1, weight.weight2, weight.weight3);

                        // 将顶点转换到世界空间以保持它们的位置
                        vertexArr[i] = meshMatrix.MultiplyPoint3x4(vertexArr[i]);
                        normalArr[i] = meshMatrix.MultiplyVector(normalArr[i]);
                    }
                }
                else
                {
                    var bonePath = GetNodePath(prefabRoot, skinMesh.transform);
                    if (!bonesIndies.ContainsKey(bonePath)) continue;

                    int boneIndex0 = bonesIndies[bonePath];

                    // 修复：使用正确的变换矩阵
                    var meshMatrix = skinMesh.transform.localToWorldMatrix;

                    for (int i = 0; i < vertexArr.Length; i++)
                    {
                        vertexBoneIndies[i] = new Vector4(boneIndex0, boneIndex0, boneIndex0, boneIndex0);
                        vertexBoneWeights[i] = new Vector4(0.25f, 0.25f, 0.25f, 0.25f);

                        // 将顶点转换到世界空间以保持它们的位置
                        vertexArr[i] = meshMatrix.MultiplyPoint3x4(vertexArr[i]);
                        normalArr[i] = meshMatrix.MultiplyVector(normalArr[i]);
                    }
                }

                // Remap UVs to atlas if we have a material with texture
                if (atlasTexture != null && skinMesh.sharedMaterials.Length > 0 && skinMesh.sharedMaterials[0] != null)
                {
                    Material mat = skinMesh.sharedMaterials[0];
                    Texture2D mainTex = null;

                    // 尝试获取主纹理
                    if (mat.HasProperty("_MainTex"))
                        mainTex = mat.GetTexture("_MainTex") as Texture2D;
                    else if (mat.HasProperty("_BaseMap"))
                        mainTex = mat.GetTexture("_BaseMap") as Texture2D;
                    else if (mat.HasProperty("_BaseColorMap"))
                        mainTex = mat.GetTexture("_BaseColorMap") as Texture2D;
                    else if (mat.HasProperty("_DiffuseMap"))
                        mainTex = mat.GetTexture("_DiffuseMap") as Texture2D;
                    else if (mat.HasProperty("_AlbedoMap"))
                        mainTex = mat.GetTexture("_AlbedoMap") as Texture2D;

                    if (mainTex != null && textureRects.ContainsKey(mainTex))
                    {
                        Rect rect = textureRects[mainTex];

                        // 获取材质的缩放和偏移
                        Vector2 scale = Vector2.one;
                        Vector2 offset = Vector2.zero;

                        if (mat.HasProperty("_MainTex_ST"))
                            scale = new Vector2(mat.GetVector("_MainTex_ST").x, mat.GetVector("_MainTex_ST").y);
                        else if (mat.HasProperty("_BaseMap_ST"))
                            scale = new Vector2(mat.GetVector("_BaseMap_ST").x, mat.GetVector("_BaseMap_ST").y);

                        // 重映射UV到图集坐标
                        for (int i = 0; i < uvArr.Length; i++)
                        {
                            float originalU = uvArr[i].x;
                            float originalV = uvArr[i].y;

                            // 应用材质的缩放和偏移
                            originalU = (originalU * scale.x) + offset.x;
                            originalV = (originalV * scale.y) + offset.y;

                            // 包装UV用于平铺纹理
                            originalU = originalU - Mathf.Floor(originalU);
                            originalV = originalV - Mathf.Floor(originalV);

                            // 映射到图集
                            uvArr[i].x = rect.x + originalU * rect.width;
                            uvArr[i].y = rect.y + originalV * rect.height;
                        }
                    }
                }

                // Add triangles with vertex offset
                for (int i = 0; i < triangles.Length; i++)
                {
                    combinedTriangles.Add(triangles[i] + vertexOffset);
                }

                // Add vertices, normals, UVs, and bone data
                combinedVertices.AddRange(vertexArr);
                combinedNormals.AddRange(normalArr);
                combinedUVs.AddRange(uvArr);
                combinedBoneIndices.AddRange(vertexBoneIndies);
                combinedBoneWeights.AddRange(vertexBoneWeights);

                // Update vertex offset for next mesh
                vertexOffset += vertexArr.Length;
            }

            // Process MeshRenderers
            for (int meshIdx = 0; meshIdx < meshRendererArr.Length; meshIdx++)
            {
                var meshRenderer = meshRendererArr[meshIdx];
                var meshFilter = meshRenderer.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null) continue;

                Mesh sharedMesh = meshFilter.sharedMesh;
                var vertexArr = sharedMesh.vertices;
                var normalArr = sharedMesh.normals;
                var uvArr = sharedMesh.uv;
                var triangles = sharedMesh.triangles;

                // Get bone information
                var bonePath = GetNodePath(prefabRoot, meshRenderer.transform);
                if (!bonesIndies.ContainsKey(bonePath)) continue;

                int boneIndex0 = bonesIndies[bonePath];

                // 修复：使用正确的变换矩阵
                var meshMatrix = meshRenderer.transform.localToWorldMatrix;

                // Create bone indices and weights for this mesh
                Vector4[] vertexBoneIndies = new Vector4[vertexArr.Length];
                Vector4[] vertexBoneWeights = new Vector4[vertexArr.Length];

                // Transform vertices and set bone data
                for (int i = 0; i < vertexArr.Length; i++)
                {
                    vertexBoneIndies[i] = new Vector4(boneIndex0, boneIndex0, boneIndex0, boneIndex0);
                    vertexBoneWeights[i] = new Vector4(0.25f, 0.25f, 0.25f, 0.25f);

                    // 将顶点转换到世界空间以保持它们的位置
                    vertexArr[i] = meshMatrix.MultiplyPoint3x4(vertexArr[i]);
                    normalArr[i] = meshMatrix.MultiplyVector(normalArr[i]);
                }

                // Remap UVs to atlas if we have a material with texture
                if (atlasTexture != null && meshRenderer.sharedMaterials.Length > 0 && meshRenderer.sharedMaterials[0] != null)
                {
                    Material mat = meshRenderer.sharedMaterials[0];
                    Texture2D mainTex = null;

                    // 尝试获取主纹理
                    if (mat.HasProperty("_MainTex"))
                        mainTex = mat.GetTexture("_MainTex") as Texture2D;
                    else if (mat.HasProperty("_BaseMap"))
                        mainTex = mat.GetTexture("_BaseMap") as Texture2D;
                    else if (mat.HasProperty("_BaseColorMap"))
                        mainTex = mat.GetTexture("_BaseColorMap") as Texture2D;
                    else if (mat.HasProperty("_DiffuseMap"))
                        mainTex = mat.GetTexture("_DiffuseMap") as Texture2D;
                    else if (mat.HasProperty("_AlbedoMap"))
                        mainTex = mat.GetTexture("_AlbedoMap") as Texture2D;

                    if (mainTex != null && textureRects.ContainsKey(mainTex))
                    {
                        Rect rect = textureRects[mainTex];

                        // 获取材质的缩放和偏移
                        Vector2 scale = Vector2.one;
                        Vector2 offset = Vector2.zero;

                        if (mat.HasProperty("_MainTex_ST"))
                            scale = new Vector2(mat.GetVector("_MainTex_ST").x, mat.GetVector("_MainTex_ST").y);
                        else if (mat.HasProperty("_BaseMap_ST"))
                            scale = new Vector2(mat.GetVector("_BaseMap_ST").x, mat.GetVector("_BaseMap_ST").y);

                        // 重映射UV到图集坐标
                        for (int i = 0; i < uvArr.Length; i++)
                        {
                            float originalU = uvArr[i].x;
                            float originalV = uvArr[i].y;

                            // 应用材质的缩放和偏移
                            originalU = (originalU * scale.x) + offset.x;
                            originalV = (originalV * scale.y) + offset.y;

                            // 包装UV用于平铺纹理
                            originalU = originalU - Mathf.Floor(originalU);
                            originalV = originalV - Mathf.Floor(originalV);

                            // 映射到图集
                            uvArr[i].x = rect.x + originalU * rect.width;
                            uvArr[i].y = rect.y + originalV * rect.height;
                        }
                    }
                }

                // Add triangles with vertex offset
                for (int i = 0; i < triangles.Length; i++)
                {
                    combinedTriangles.Add(triangles[i] + vertexOffset);
                }

                // Add vertices, normals, UVs, and bone data
                combinedVertices.AddRange(vertexArr);
                combinedNormals.AddRange(normalArr);
                combinedUVs.AddRange(uvArr);
                combinedBoneIndices.AddRange(vertexBoneIndies);
                combinedBoneWeights.AddRange(vertexBoneWeights);

                // Update vertex offset for next mesh
                vertexOffset += vertexArr.Length;
            }

            // If we have any meshes to combine
            if (combinedVertices.Count > 0)
            {
                // Create or load the combined mesh
                Mesh combinedMesh;
                if (File.Exists(combinedMeshName))
                {
                    combinedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(combinedMeshName);
                }
                else
                {
                    combinedMesh = new Mesh();
                    AssetDatabase.CreateAsset(combinedMesh, combinedMeshName);
                }

                // Set mesh data
                combinedMesh.Clear();
                combinedMesh.vertices = combinedVertices.ToArray();
                combinedMesh.normals = combinedNormals.ToArray();
                combinedMesh.uv = combinedUVs.ToArray();
                combinedMesh.triangles = combinedTriangles.ToArray();
                combinedMesh.SetUVs(1, combinedBoneIndices);
                combinedMesh.SetUVs(2, combinedBoneWeights);

                combinedMesh.RecalculateBounds();

                EditorUtility.SetDirty(combinedMesh);
                AssetDatabase.SaveAssetIfDirty(combinedMesh);

                // Set up the main GameObject with the combined mesh
                gpuAnimMf = gpuAnimPrefab.GetComponent<MeshFilter>();
                if (gpuAnimMf == null) gpuAnimMf = gpuAnimPrefab.AddComponent<MeshFilter>();
                gpuAnimMr = gpuAnimPrefab.GetComponent<MeshRenderer>();
                if (gpuAnimMr == null) gpuAnimMr = gpuAnimPrefab.AddComponent<MeshRenderer>();

                gpuAnimMf.sharedMesh = combinedMesh;

                // Use the combined material
                if (combinedMaterial != null)
                {
                    gpuAnimMr.sharedMaterial = combinedMaterial;
                }
                else
                {
                    // 回退到原始的材质创建方式
                    List<Material> gpuMeshMats = new List<Material>();
                    foreach (var mat in originalMaterials)
                    {
                        var gpuAnimMat = GetSameAnimMaterial(outputDir, animTex, mat.mainTexture);
                        if (gpuAnimMat == null)
                        {
                            gpuAnimMat = new Material(m_Shader);
                            string gpuAnimMatFileName = string.Format("{0}/{1}_mat_{2}.mat", outputDir, prefabRoot.name, gpuMeshMats.Count);
                            AssetDatabase.CreateAsset(gpuAnimMat, gpuAnimMatFileName);

                            gpuAnimMat.SetTexture("_AnimTex", animTex);
                            gpuAnimMat.mainTexture = mat.mainTexture;
                            // 复制其他必要的材质属性

                            AssetDatabase.SaveAssetIfDirty(gpuAnimMat);
                        }
                        else
                        {
                            // 确保即使是已存在的材质也设置了动画纹理
                            gpuAnimMat.SetTexture("_AnimTex", animTex);
                            EditorUtility.SetDirty(gpuAnimMat);
                        }
                        gpuMeshMats.Add(gpuAnimMat);
                    }
                    gpuAnimMr.sharedMaterials = gpuMeshMats.ToArray();
                }
            }

            PrefabUtility.SaveAsPrefabAssetAndConnect(gpuAnimPrefab, gpuAnimPrefabName, InteractionMode.AutomatedAction);
        }

        private void AddSkinUseBone(int boneIndex0)
        {
            if (!m_SkinUseBones.Contains(boneIndex0)) m_SkinUseBones.Add(boneIndex0);
        }

        private Material GetSameAnimMaterial(string dir, Texture2D animTex, Texture mainTex)
        {
            var matGuids = AssetDatabase.FindAssets("t:material", new string[] { dir });
            foreach (var matGuid in matGuids)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(matGuid));
                if (mat == null) continue;

                if (mat.mainTexture == mainTex && mat.shader.name == m_Shader.name && mat.GetTexture("_AnimTex") == animTex)
                {
                    return mat;
                }
            }
            return null;
        }
        private Vector3 GetBonePositionAtTime(string bonePath, AnimationClip clip, float animTime)
        {
            var localPosXCurve = EditorCurveBinding.FloatCurve(bonePath, typeof(Transform), "m_LocalPosition.x");
            var localPosYCurve = EditorCurveBinding.FloatCurve(bonePath, typeof(Transform), "m_LocalPosition.y");
            var localPosZCurve = EditorCurveBinding.FloatCurve(bonePath, typeof(Transform), "m_LocalPosition.z");

            Vector3 pos = Vector3.zero;
            pos.x = AnimationUtility.GetEditorCurve(clip, localPosXCurve).Evaluate(animTime);
            pos.y = AnimationUtility.GetEditorCurve(clip, localPosYCurve).Evaluate(animTime);
            pos.z = AnimationUtility.GetEditorCurve(clip, localPosZCurve).Evaluate(animTime);
            return pos;
        }
        /// <summary>
        /// 获取动画时刻的骨骼的旋转
        /// </summary>
        /// <param name="bonePath"></param>
        /// <param name="clip"></param>
        /// <param name="animTime"></param>
        /// <returns></returns>
        private Quaternion GetBoneRotationAtTime(string bonePath, AnimationClip clip, float animTime)
        {
            var localRotationXCurve = EditorCurveBinding.FloatCurve(bonePath, typeof(Transform), "m_LocalRotation.x");
            var localRotationYCurve = EditorCurveBinding.FloatCurve(bonePath, typeof(Transform), "m_LocalRotation.y");
            var localRotationZCurve = EditorCurveBinding.FloatCurve(bonePath, typeof(Transform), "m_LocalRotation.z");
            var localRotationWCurve = EditorCurveBinding.FloatCurve(bonePath, typeof(Transform), "m_LocalRotation.w");

            Quaternion rotation = Quaternion.identity;
            rotation.x = AnimationUtility.GetEditorCurve(clip, localRotationXCurve).Evaluate(animTime);
            rotation.y = AnimationUtility.GetEditorCurve(clip, localRotationYCurve).Evaluate(animTime);
            rotation.z = AnimationUtility.GetEditorCurve(clip, localRotationZCurve).Evaluate(animTime);
            rotation.w = AnimationUtility.GetEditorCurve(clip, localRotationWCurve).Evaluate(animTime);
            var r = rotation.x * rotation.x;
            r += rotation.y * rotation.y;
            r += rotation.z * rotation.z;
            r += rotation.w * rotation.w;
            if (r > 0.1f)
            {
                r = 1.0f / Mathf.Sqrt(r);
                rotation.x *= r;
                rotation.y *= r;
                rotation.z *= r;
                rotation.w *= r;
            }
            return rotation;
        }
        private Vector3 GetBoneScaleAtTime(string bonePath, AnimationClip clip, float animTime)
        {
            var localScaleXCurve = EditorCurveBinding.FloatCurve(bonePath, typeof(Transform), "m_LocalScale.x");
            var localScaleYCurve = EditorCurveBinding.FloatCurve(bonePath, typeof(Transform), "m_LocalScale.y");
            var localScaleZCurve = EditorCurveBinding.FloatCurve(bonePath, typeof(Transform), "m_LocalScale.z");
            Vector3 scale = Vector3.one;
            scale.x = AnimationUtility.GetEditorCurve(clip, localScaleXCurve).Evaluate(animTime);
            scale.y = AnimationUtility.GetEditorCurve(clip, localScaleYCurve).Evaluate(animTime);
            scale.z = AnimationUtility.GetEditorCurve(clip, localScaleZCurve).Evaluate(animTime);
            return scale;
        }
        private string GetNodePath(Transform rootBone, Transform childBone)
        {
            string result = childBone.name;
            if (rootBone == childBone)
            {
                return "";
            }
            Transform parent = childBone.parent;
            while (parent != null && parent != rootBone)
            {
                result = string.Format("{0}/{1}", parent.name, result);
                parent = parent.parent;
            }
            return result;
        }
        private void TryAssignAnimationClips(string clipsDir, GameObject animationPrefab, IList<AnimationClip> clips)
        {
            var animation = animationPrefab.GetComponent<Animation>();
            AnimationClip[] newClips = new AnimationClip[clips.Count];
            for (int i = 0; i < clips.Count; i++)
            {
                var clipAsset = Path.Combine(clipsDir, $"{clips[i].name}.anim");
                newClips[i] = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipAsset);
            }
            animation.clip = newClips[0];
            AnimationUtility.SetAnimationClips(animation, newClips);
            EditorUtility.SetDirty(animationPrefab);
        }

        public void BakeVertexAnim(GameObject animationPrefab, string outputDir)
        {
            try
            {
                var baker = new GPUAnimationBaker();
                baker.SetAnimData(animationPrefab);
                var bakedDataArr = baker.Bake();
                if (bakedDataArr == null || bakedDataArr.Count < 1)
                {
                    return;
                }
                var tex2d = bakedDataArr[0].RawAnimTex;
                Texture2DArray tex2DArray = new Texture2DArray(tex2d.width, tex2d.height, bakedDataArr.Count, tex2d.format, false);
                Texture2DArray normalTex2DArray = new Texture2DArray(tex2d.width, tex2d.height, bakedDataArr.Count, tex2d.format, false);
                tex2DArray.filterMode = normalTex2DArray.filterMode = tex2d.filterMode;
                tex2DArray.wrapMode = normalTex2DArray.wrapMode = tex2d.wrapMode;
                for (int i = 0; i < bakedDataArr.Count; i++)
                {
                    var bakeDt = bakedDataArr[i];
                    tex2DArray.SetPixelData(bakeDt.RawAnimTex.GetRawTextureData(), 0, i);
                    normalTex2DArray.SetPixelData(bakeDt.RawAnimNormalTex.GetRawTextureData(), 0, i);
                }
                tex2DArray.Apply();
                normalTex2DArray.Apply();
                string fileName = Path.Combine(outputDir, $"{animationPrefab.name}_vertex_anim.asset");
                AssetDatabase.CreateAsset(tex2DArray, fileName);
                string normalFileName = Path.Combine(outputDir, $"{animationPrefab.name}_vertex_anim_normal.asset");
                AssetDatabase.CreateAsset(normalTex2DArray, normalFileName);

                string meshFileName = Path.Combine(outputDir, $"{animationPrefab.name}_vertex_anim_mesh.asset");
                Mesh newMesh;
                if (!File.Exists(meshFileName))
                {
                    newMesh = new Mesh();// Instantiate(baker.AnimBakeData.SkinMesh.sharedMesh);
                    AssetDatabase.CreateAsset(newMesh, meshFileName);
                }
                else
                {
                    newMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshFileName);
                }
                var sharedMesh = baker.AnimBakeData.SkinMesh.sharedMesh;
                var points = new List<Vector3>(sharedMesh.vertices).ToArray();
                var meshTransform = baker.AnimBakeData.SkinMesh.transform;
                Vector3 vertexOffset = meshTransform.position;
                Vector3 vertexScale = meshTransform.lossyScale;
                Quaternion vertexRotation = meshTransform.rotation;

                for (int i = 0; i < points.Length; i++)
                {
                    var point = points[i] + vertexOffset;
                    point.Scale(vertexScale);
                    points[i] = vertexRotation * point;
                }
                newMesh.vertices = points;
                newMesh.triangles = sharedMesh.triangles;
                newMesh.uv = sharedMesh.uv;

                //newMesh.RecalculateBounds();
                //newMesh.RecalculateNormals();
                //newMesh.RecalculateTangents();
                EditorUtility.SetDirty(newMesh);
                AssetDatabase.SaveAssetIfDirty(newMesh);
                var newPrefabName = $"{m_Config.Prefabs[0].SourcePrefab.name}_vertex_anim";
                var newPrefab = new GameObject(newPrefabName, typeof(MeshFilter), typeof(MeshRenderer));
                newPrefab.GetComponent<MeshFilter>().mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshFileName);
                var matFileName = Path.Combine(outputDir, $"{newPrefab.name}.mat");
                Material newMat;
                if (!File.Exists(matFileName))
                {
                    newMat = new Material(m_Shader);
                    AssetDatabase.CreateAsset(newMat, matFileName);
                }
                newMat = AssetDatabase.LoadAssetAtPath<Material>(matFileName);
                newMat.shader = m_Shader;
                newMat.SetTexture("_AnimTexArr", AssetDatabase.LoadAssetAtPath<Texture2DArray>(fileName));
                newMat.SetTexture("_AnimNormalTexArr", AssetDatabase.LoadAssetAtPath<Texture2DArray>(normalFileName));
                newMat.SetFloat("_AnimMaxLen", baker.MaxAnimLength);

                // 确保材质变更被保存
                EditorUtility.SetDirty(newMat);
                AssetDatabase.SaveAssetIfDirty(newMat);

                newPrefab.GetComponent<MeshRenderer>().material = newMat;
                PrefabUtility.SaveAsPrefabAssetAndConnect(newPrefab, Path.Combine(outputDir, $"{newPrefabName}.prefab"), InteractionMode.AutomatedAction);
                DestroyImmediate(newPrefab);
            }
            finally
            {
                // 确保清理临时对象
                if (animationPrefab != null)
                {
                    DestroyImmediate(animationPrefab);
                }
            }
        }

        /// <summary>
        /// 生成静态Mesh的Prefab
        /// </summary>
        /// <param name="outputPath"></param>
        /// <param name="mesh"></param>
        /// <returns></returns>
        private GameObject GenerateOutputPrefab(string outputPath, GameObject sourcePrefab)
        {
            var go = Instantiate(sourcePrefab, Vector3.zero, Quaternion.identity);
            go.name = sourcePrefab.name;
            var animator = go.GetComponentInChildren<Animator>();
            GameObject result = null;
            if (animator != null)
            {
                animator.gameObject.AddComponent<Animation>();
                DestroyImmediate(animator);
                result = PrefabUtility.SaveAsPrefabAsset(go, outputPath, out bool success);
                if (success)
                {
                    DestroyImmediate(go);
                }
            }
            return result;
        }

        // 新增方法：配置纹理导入设置
        private void ConfigureTextureImporter(string texturePath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.mipmapEnabled = true;
                importer.isReadable = true;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
        }

        // 添加一个新方法，用于在转换完成后进行最终处理
        private void FinalizeConversion(string outputDir, GameObject destinationPrefab)
        {
            try
            {
                Debug.Log("开始最终处理...");

                // 重新加载所有纹理资源
                string[] textureAssets = Directory.GetFiles(outputDir, "*_anim.asset");
                foreach (string texturePath in textureAssets)
                {
                    string assetPath = texturePath.Replace(Application.dataPath, "Assets");
                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    if (tex != null)
                    {
                        Debug.Log($"重新加载纹理: {assetPath}, 尺寸: {tex.width}x{tex.height}");

                        // 确保纹理设置正确
                        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                        if (importer != null)
                        {
                            importer.isReadable = true;
                            importer.textureCompression = TextureImporterCompression.Uncompressed;
                            importer.filterMode = FilterMode.Point;
                            importer.mipmapEnabled = false;
                            importer.SaveAndReimport();
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"无法加载纹理: {assetPath}");
                    }
                }

                // 重新保存预制体
                if (destinationPrefab != null)
                {
                    string prefabPath = AssetDatabase.GetAssetPath(destinationPrefab);
                    if (!string.IsNullOrEmpty(prefabPath))
                    {
                        Debug.Log($"重新保存预制体: {prefabPath}");
                        EditorUtility.SetDirty(destinationPrefab);
                        AssetDatabase.SaveAssets();
                    }
                }

                // 最终刷新   
                AssetDatabase.Refresh();

                Debug.Log("最终处理完成。如果GPU动画仍然不工作，请尝试重新打开场景或重启Unity。");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"最终处理时出错: {e.Message}\n{e.StackTrace}");
            }
        }   
    }

}
