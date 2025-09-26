using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.IO;
using SoxwareInteractive.AnimationConversion;
using UnityEngine.Rendering;
using System;
using System.Linq;
namespace GPUAnimation.Editor
{


    public class GPUAnimationConverter : EditorWindow
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
        [MenuItem("Game Framework/GPU Animation/GPU Animation Converter")]
        public static GPUAnimationConverter ShowWindow()
        {
            var win = GetWindow<GPUAnimationConverter>();
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
            EditorPrefs.SetInt("GPUAnim.AnimMode", (int)m_AnimMode);
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

        private void RefreshAnimationClips(GameObject prefab, ref List<AnimationClip> clips)
        {
            if (prefab == null) return;
            var animtor = prefab.GetComponentInChildren<Animator>();
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
            if (config.Prefabs == null || config.Prefabs.Length < 1 || config.Prefabs[0].SourcePrefab == null)
            {
                return $"GPU动画转换失败: 请设置要转换的Prefab";
            }
            if (config.Prefabs[0].DestinationPrefab == null)
            {
                //var skinMeshArr = config.Prefabs[0].SourcePrefab.GetComponentsInChildren<SkinnedMeshRenderer>();
                //if (skinMeshArr == null || skinMeshArr.Length <= 0)
                //{
                //    return $"GPU动画转换失败: Prefab({config.Prefabs[0].SourcePrefab.name})中不包含SkinnedMeshRenderer";
                //}
                //else if (skinMeshArr.Length != 1 && m_AnimMode != GPUAnimMode.Bone)
                //{
                //    return $"GPU动画转换失败: Prefab({config.Prefabs[0].SourcePrefab.name})包含多个SkinnedMeshRenderer, 请先合并Mesh(可使用Mesh Baker一键合并Skinned Mesh/Material)";
                //}
                //if (skinMeshArr[0].sharedMesh == null)
                //{
                //    return $"GPU动画转换失败: Prefab({config.Prefabs[0].SourcePrefab.name})SkinnedMeshRenderer的mesh丢失.";
                //}
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
            return logMessage;
        }
        private void BakeBonesAnim(GameObject animGameObject, string outputDir)
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
            AssetDatabase.CreateAsset(animBoneTex, string.Format("{0}/{1}_bones_anim.asset", outputDir, animGameObject.name)); // 创建纹理资产

            // 创建GPU骨骼动画资产
            CreateGPUBonesAnimAssets(animGameObject.transform, animBoneTex, bonesIndexes, bonesL2WMatrices, bonesW2LMatrices, outputDir);
            RecordBones(animBoneTex, bonesIndexes); // 记录骨骼信息

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
            animBoneTex.Apply(); // 应用纹理更改
            AssetDatabase.SaveAssetIfDirty(animBoneTex); // 如果纹理被修改，保存它
        }

        /// <summary>
        /// 把需要绑定武器的骨骼索引记录到动画贴图
        /// </summary>
        /// <param name="tex"></param>
        /// <param name="bonesIndexes"></param>
        private void RecordBones(Texture2D tex, Dictionary<string, int> bonesIndexes)
        {
            var root = m_Config.Prefabs[0].SourcePrefab;
            if (root == null) return;
            for (int i = 0; i < m_BindBones.Count; i++)
            {
                var bone = m_BindBones[i];
                if (bone == null) continue;

                var bonePath = GetNodePath(root.transform, bone.transform);
                if (bonesIndexes.TryGetValue(bonePath, out int boneIndex))
                {
                    tex.SetPixel(tex.width - 1, tex.height - 1 - i, new Color(boneIndex, boneIndex, boneIndex, boneIndex));
                }
            }
        }
        private void CreateGPUBonesAnimAssets(Transform prefabRoot, Texture2D animTex, Dictionary<string, int> bonesIndies, Matrix4x4[] bonesL2WMatrices, Matrix4x4[] bonesW2LMatrices, string outputDir)
        {
            string gpuAnimPrefabName = string.Format("{0}/{1}_bones_anim.prefab", outputDir, prefabRoot.name);
            MeshRenderer gpuAnimMr;
            MeshFilter gpuAnimMf;

            GameObject gpuAnimPrefab;
            GameObject curMeshNode;
            if (!File.Exists(gpuAnimPrefabName))
            {
                gpuAnimPrefab = new GameObject(Path.GetFileNameWithoutExtension(gpuAnimPrefabName), new System.Type[] { typeof(MeshFilter), typeof(MeshRenderer) });
                PrefabUtility.SaveAsPrefabAsset(gpuAnimPrefab, gpuAnimPrefabName);
                DestroyImmediate(gpuAnimPrefab);
            }
            gpuAnimPrefab = PrefabUtility.LoadPrefabContents(gpuAnimPrefabName);
            curMeshNode = gpuAnimPrefab;

            var skinMeshArr = prefabRoot.GetComponentsInChildren<SkinnedMeshRenderer>();
            for (int meshIdx = 0; meshIdx < skinMeshArr.Length; meshIdx++)
            {
                var skinMesh = skinMeshArr[meshIdx];
                string outputMeshName = string.Format("{0}/{1}_{2}_mesh_{3}.asset", outputDir, prefabRoot.name, skinMesh.name, meshIdx);

                Mesh sharedMesh = new Mesh();
                skinMesh.BakeMesh(sharedMesh, true);
                //sharedMesh.bindposes = skinMesh.sharedMesh.bindposes;
                //sharedMesh.boneWeights = skinMesh.sharedMesh.boneWeights;
                var meshBones = skinMesh.bones;
                var vertexArr = sharedMesh.vertices;
                var normalArr = sharedMesh.normals;
                var vertexBoneIndies = new Vector4[vertexArr.Length]; //顶点关联的骨骼index
                var vertexBoneWeights = new Vector4[vertexArr.Length]; //顶点关联的骨骼权重

                if (skinMesh.sharedMesh.bindposeCount > 0)
                {
                    var bonePath = GetNodePath(prefabRoot, meshBones[0]);
                    if (!bonesIndies.ContainsKey(bonePath)) continue;
                    var meshMatrix = bonesL2WMatrices[bonesIndies[bonePath]] * bonesW2LMatrices[bonesIndies[bonePath]] * skinMesh.transform.localToWorldMatrix;
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
                        vertexArr[i] = meshMatrix * vertexArr[i];
                        normalArr[i] = meshMatrix * normalArr[i];
                    }
                }
                else
                {
                    var bonePath = GetNodePath(prefabRoot, skinMesh.transform);
                    if (!bonesIndies.ContainsKey(bonePath)) continue;
                    int boneIndex0 = bonesIndies[bonePath];
                    var meshMatrix = bonesL2WMatrices[bonesIndies[bonePath]] * bonesW2LMatrices[bonesIndies[bonePath]];
                    for (int i = 0; i < vertexArr.Length; i++)
                    {
                        vertexBoneIndies[i] = new Vector4(boneIndex0, boneIndex0, boneIndex0, boneIndex0);
                        vertexBoneWeights[i] = new Vector4(0.25f, 0.25f, 0.25f, 0.25f);
                        vertexArr[i] = meshMatrix * vertexArr[i];
                        normalArr[i] = meshMatrix * normalArr[i];
                    }
                }

                Mesh gpuMesh;
                if (File.Exists(outputMeshName))
                {
                    gpuMesh = AssetDatabase.LoadAssetAtPath<Mesh>(outputMeshName);
                }
                else
                {
                    gpuMesh = new Mesh();
                    AssetDatabase.CreateAsset(gpuMesh, outputMeshName);
                }
                gpuMesh.vertices = vertexArr;
                gpuMesh.normals = normalArr;
                gpuMesh.triangles = sharedMesh.triangles;
                gpuMesh.uv = sharedMesh.uv;

                if (sharedMesh.subMeshCount > 0)
                {
                    SubMeshDescriptor[] subMeshes = new SubMeshDescriptor[sharedMesh.subMeshCount];
                    for (int i = 0; i < sharedMesh.subMeshCount; i++)
                    {
                        subMeshes[i] = sharedMesh.GetSubMesh(i);
                    }
                    gpuMesh.SetSubMeshes(subMeshes);
                }

                gpuMesh.SetUVs(1, vertexBoneIndies);
                gpuMesh.SetUVs(2, vertexBoneWeights);
                EditorUtility.SetDirty(gpuMesh);
                AssetDatabase.SaveAssetIfDirty(gpuMesh);

                if (meshIdx > 0)
                {
                    var curMeshNodeName = Path.GetFileNameWithoutExtension(outputMeshName);
                    var node = gpuAnimPrefab.transform.Find(curMeshNodeName);
                    curMeshNode = node == null ? new GameObject(curMeshNodeName) : node.gameObject;
                    curMeshNode.transform.SetParent(gpuAnimPrefab.transform);

                    curMeshNode.transform.localPosition = Vector3.zero;
                    curMeshNode.transform.localRotation = Quaternion.identity;
                    curMeshNode.transform.localScale = Vector3.one;
                }

                gpuAnimMf = curMeshNode.GetComponent<MeshFilter>();
                if (gpuAnimMf == null) gpuAnimMf = curMeshNode.AddComponent<MeshFilter>();
                gpuAnimMr = curMeshNode.GetComponent<MeshRenderer>();
                if (gpuAnimMr == null) gpuAnimMr = curMeshNode.AddComponent<MeshRenderer>();

                gpuAnimMr.sortingOrder = skinMesh.sortingOrder;
                List<Material> gpuMeshMats = new List<Material>();
                for (int matIdx = 0; matIdx < skinMesh.sharedMaterials.Length; matIdx++)
                {
                    var sharedMat = skinMesh.sharedMaterials[matIdx];
                    var gpuAnimMat = GetSameAnimMaterial(outputDir, animTex, sharedMat.mainTexture);
                    if (gpuAnimMat == null)
                    {
                        gpuAnimMat = new Material(m_Shader);
                        string gpuAnimMatFileName = string.Format("{0}/{1}_{2}_{3}_{4}.mat", outputDir, prefabRoot.name, skinMesh.name, meshIdx, matIdx);
                        AssetDatabase.CreateAsset(gpuAnimMat, gpuAnimMatFileName);
                    }
                    gpuAnimMat.SetTexture("_AnimTex", animTex);
                    try
                    {
                        if (sharedMat.HasProperty("_MainTex"))
                        {
                            gpuAnimMat.mainTexture = sharedMat.mainTexture;
                        }

                        gpuAnimMat.mainTexture = sharedMat.mainTexture;
                        if (sharedMat.HasProperty("_Color"))
                        {
                            gpuAnimMat.color = sharedMat.color;
                        }

                        if (sharedMat.HasProperty("_MainTex"))
                        {
                            gpuAnimMat.mainTextureOffset = sharedMat.mainTextureOffset;
                            gpuAnimMat.mainTextureScale = sharedMat.mainTextureScale;
                        }
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                    
                    AssetDatabase.SaveAssetIfDirty(gpuAnimMat);
                    gpuMeshMats.Add(gpuAnimMat);
                }

                gpuAnimMf.sharedMesh = gpuMesh;
                gpuAnimMr.SetMaterials(gpuMeshMats);
            }

            var meshRendererArr = prefabRoot.GetComponentsInChildren<MeshRenderer>();
            for (int meshIdx = 0; meshIdx < meshRendererArr.Length; meshIdx++)
            {
                var meshRenderer = meshRendererArr[meshIdx];

                string outputMeshName = string.Format("{0}/{1}_{2}_bindmesh_{3}.asset", outputDir, prefabRoot.name, meshRenderer.name, meshIdx);
                Mesh sharedMesh = meshRenderer.GetComponent<MeshFilter>().sharedMesh;
                if (!sharedMesh) continue;
                var vertexArr = sharedMesh.vertices;
                var normalArr = sharedMesh.normals;
                var vertexBoneIndies = new Vector4[vertexArr.Length]; //顶点关联的骨骼index
                var vertexBoneWeights = new Vector4[vertexArr.Length]; //顶点关联的骨骼权重

                var bonePath = GetNodePath(prefabRoot, meshRenderer.transform);
                if (!bonesIndies.ContainsKey(bonePath)) continue;
                int boneIndex0 = bonesIndies[bonePath];
                var meshMatrix = bonesL2WMatrices[bonesIndies[bonePath]] * bonesW2LMatrices[bonesIndies[bonePath]];
                for (int i = 0; i < vertexArr.Length; i++)
                {
                    vertexBoneIndies[i] = new Vector4(boneIndex0, boneIndex0, boneIndex0, boneIndex0);
                    vertexBoneWeights[i] = new Vector4(0.25f, 0.25f, 0.25f, 0.25f);
                    vertexArr[i] = meshMatrix * vertexArr[i];
                    normalArr[i] = meshMatrix * normalArr[i];
                }
                Mesh gpuMesh;
                if (File.Exists(outputMeshName))
                {
                    gpuMesh = AssetDatabase.LoadAssetAtPath<Mesh>(outputMeshName);
                }
                else
                {
                    gpuMesh = new Mesh();
                    AssetDatabase.CreateAsset(gpuMesh, outputMeshName);
                }
                gpuMesh.vertices = vertexArr;
                gpuMesh.normals = normalArr;
                gpuMesh.triangles = sharedMesh.triangles;
                gpuMesh.uv = sharedMesh.uv;

                if (sharedMesh.subMeshCount > 0)
                {
                    SubMeshDescriptor[] subMeshes = new SubMeshDescriptor[sharedMesh.subMeshCount];
                    for (int i = 0; i < sharedMesh.subMeshCount; i++)
                    {
                        subMeshes[i] = sharedMesh.GetSubMesh(i);
                    }
                    gpuMesh.SetSubMeshes(subMeshes);
                }

                gpuMesh.SetUVs(1, vertexBoneIndies);
                gpuMesh.SetUVs(2, vertexBoneWeights);
                EditorUtility.SetDirty(gpuMesh);
                AssetDatabase.SaveAssetIfDirty(gpuMesh);

                var curMeshNodeName = Path.GetFileNameWithoutExtension(outputMeshName);
                var node = gpuAnimPrefab.transform.Find(curMeshNodeName);
                curMeshNode = node == null ? new GameObject(curMeshNodeName) : node.gameObject;
                curMeshNode.transform.SetParent(gpuAnimPrefab.transform);
                curMeshNode.transform.localPosition = Vector3.zero;
                curMeshNode.transform.localRotation = Quaternion.identity;
                curMeshNode.transform.localScale = Vector3.one;

                gpuAnimMf = curMeshNode.GetComponent<MeshFilter>();
                if (gpuAnimMf == null) gpuAnimMf = curMeshNode.AddComponent<MeshFilter>();
                gpuAnimMr = curMeshNode.GetComponent<MeshRenderer>();
                if (gpuAnimMr == null) gpuAnimMr = curMeshNode.AddComponent<MeshRenderer>();

                gpuAnimMr.sortingOrder = meshRenderer.sortingOrder;
                List<Material> gpuMeshMats = new List<Material>();
                for (int matIdx = 0; matIdx < meshRenderer.sharedMaterials.Length; matIdx++)
                {
                    var sharedMat = meshRenderer.sharedMaterials[matIdx];
                    var gpuAnimMat = GetSameAnimMaterial(outputDir, animTex, sharedMat.mainTexture);
                    if (gpuAnimMat == null)
                    {
                        gpuAnimMat = new Material(m_Shader);
                        string gpuAnimMatFileName = string.Format("{0}/{1}_{2}_{3}_{4}.mat", outputDir, prefabRoot.name, meshRenderer.name, meshIdx, matIdx); ;
                        AssetDatabase.CreateAsset(gpuAnimMat, gpuAnimMatFileName);
                    }
                    try
                    {
                        gpuAnimMat.SetTexture("_AnimTex", animTex);
                        gpuAnimMat.mainTexture = sharedMat.mainTexture;
                        gpuAnimMat.color = sharedMat.color;
                        gpuAnimMat.mainTextureOffset = sharedMat.mainTextureOffset;
                        gpuAnimMat.mainTextureScale = sharedMat.mainTextureScale;
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                    AssetDatabase.SaveAssetIfDirty(gpuAnimMat);
                    gpuMeshMats.Add(gpuAnimMat);
                }

                gpuAnimMf.sharedMesh = gpuMesh;
                gpuAnimMr.SetMaterials(gpuMeshMats);
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
            EditorUtility.SetDirty(newMat);
            AssetDatabase.SaveAssetIfDirty(newMat);
            newPrefab.GetComponent<MeshRenderer>().material = newMat;
            PrefabUtility.SaveAsPrefabAssetAndConnect(newPrefab, Path.Combine(outputDir, $"{newPrefabName}.prefab"), InteractionMode.AutomatedAction);
            DestroyImmediate(newPrefab);
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
    }

}