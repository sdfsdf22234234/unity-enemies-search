#define ENABLE_MANTISLOD
#if ENABLE_MANTISLOD


using MantisLOD;
using MantisLODEditor;
// 其他编辑器相关的代码...


#endif
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;
using UnityEditor.PackageManager;

public class GPUAnimationLODCreator : EditorWindow
{
    private bool protectBoundary = true;
    private bool protectHardEdge = false;
    private bool moreDetails = false;
    private bool beautifulTriangles = true;
    private bool protectSymmetry = false;
    private bool recalculateNormal = false;

    private List<LODLevel> lodLevels = new List<LODLevel>();
    private ReorderableList lodLevelsList;
    private const int MAX_LOD_COUNT = 4;

    // LOD 级别数据结构
    [System.Serializable]
    private class LODLevel
    {
        public Mesh sourceMesh;
        public int vertexNum;
        public float quality = 100f;  // 三角形百分比
        public string meshName;
    }

    // 添加处理状态相关变量
    private int state = 0;
    private float startTime = 0.0f;
    private List<string> fileNameList = null;
    private int fileNameIndex = 0;
    private string currentFileName = null;
    private string messageHint = null;
    private Mesh[] sourceMeshes = null;

    private class LODMeshData
    {
        public Mesh mesh;
        public Vector3[] vertices;
        public int[] triangles;
        public Vector2[] uv;
        public Vector3[] normals;
    }

    [MenuItem("Game Framework/GPU Animation/GPU Animation LOD Creator")]
    static void ShowWindow()
    {
        var window = GetWindow<GPUAnimationLODCreator>("GPU Animation LOD Creator");
        window.Show();
    }

    void OnGUI()
    {
        // 顶部选项
        GUI.enabled = (state == 0);
        EditorGUILayout.BeginHorizontal();
        protectBoundary = EditorGUILayout.ToggleLeft("Protect Boundary[保护边界]", protectBoundary, GUILayout.Width(200));
        moreDetails = EditorGUILayout.ToggleLeft("More Details[更多细节]", moreDetails, GUILayout.Width(200));
        protectSymmetry = EditorGUILayout.ToggleLeft("Protect Symmetry[保护对称性]", protectSymmetry);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        protectHardEdge = EditorGUILayout.ToggleLeft("Protect Hard Edge[保护硬边]", protectHardEdge, GUILayout.Width(200));
        beautifulTriangles = EditorGUILayout.ToggleLeft("Beautiful Triangles[美观三角形]", beautifulTriangles, GUILayout.Width(200));
        recalculateNormal = EditorGUILayout.ToggleLeft("Recalculate Normal[重新计算法线]", recalculateNormal);
        EditorGUILayout.EndHorizontal();

        // LOD 配置标题和按钮
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("LOD Config:", EditorStyles.boldLabel);

        // 使用 ReorderableList 绘制 LOD 列表
        lodLevelsList.DoLayoutList();

        // 处理状态显示
        if (state != 0)
        {
            EditorGUILayout.LabelField("Processing: " + currentFileName);
            EditorGUILayout.LabelField("Time Elapsed: ", (Time.realtimeSinceStartup - startTime).ToString("F1") + "s");
        }

        // 错误消息显示
        if (!string.IsNullOrEmpty(messageHint))
        {
            EditorGUILayout.HelpBox(messageHint, MessageType.Info);
        }

        // 底部按钮
        EditorGUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = (state == 0);
        if (GUILayout.Button("Combine LOD Mesh", GUILayout.Height(30)))
        {
            CombineLODMeshes();
        }
        if (GUILayout.Button("Generate LOD Mesh", GUILayout.Height(30)))
        {
            if (ValidateAndPrepare())
            {
                StartGeneration();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    void OnEnable()
    {
        // 初始化 ReorderableList
        if (lodLevels.Count == 0)
        {
            // 默认添加3个LOD级别，质量依次为100%, 70%, 40%
            lodLevels.Add(new LODLevel { quality = 100f });
            lodLevels.Add(new LODLevel { quality = 70f });
            lodLevels.Add(new LODLevel { quality = 40f });
        }

        lodLevelsList = new ReorderableList(lodLevels, typeof(LODLevel), true, true, true, true)
        {
            drawHeaderCallback = DrawLODListHeader,
            drawElementCallback = DrawLODListElement,
            onAddCallback = OnAddLODLevel,
            onRemoveCallback = OnRemoveLODLevel,
            elementHeight = EditorGUIUtility.singleLineHeight + 4f // 单行高度加间距
        };
    }

    private void DrawLODListHeader(Rect rect)
    {
        EditorGUI.LabelField(rect, "LOD Levels");
    }

    private void DrawLODListElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        var lodLevel = lodLevels[index];
        float padding = 2f;
        rect.y += padding;
        rect.height = EditorGUIUtility.singleLineHeight;

        // 绘制背景
        if (isActive)
            EditorGUI.DrawRect(rect, new Color(0.6f, 0.6f, 0.6f, 0.1f));

        // LOD 标签
        float labelWidth = 45f;
        Rect labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
        EditorGUI.LabelField(labelRect, $"LOD {index}");

        // Mesh 选择区域 (减小宽度以适应Select按钮)
        float meshFieldWidth = (rect.width - labelWidth - 180f - 60f) * 0.7f; // 60f 是Select按钮的宽度
        Rect meshRect = new Rect(labelRect.xMax + padding, rect.y, meshFieldWidth, rect.height);

        EditorGUI.BeginChangeCheck();
        lodLevel.sourceMesh = (Mesh)EditorGUI.ObjectField(
            meshRect,
            lodLevel.sourceMesh,
            typeof(Mesh),
            false
        );
        if (EditorGUI.EndChangeCheck() && lodLevel.sourceMesh != null)
        {
            lodLevel.vertexNum = lodLevel.sourceMesh.vertexCount;
            lodLevel.meshName = lodLevel.sourceMesh.name;
        }

        // Select 按钮
        Rect selectButtonRect = new Rect(meshRect.xMax + padding, rect.y, 60f, rect.height);
        if (GUI.Button(selectButtonRect, "Select"))
        {
            string path = EditorUtility.OpenFilePanel("Select Mesh", "Assets", "fbx,obj,mesh,asset");
            if (!string.IsNullOrEmpty(path))
            {
                // 转换为相对路径
                if (path.StartsWith(Application.dataPath))
                {
                    path = "Assets" + path.Substring(Application.dataPath.Length);
                }

                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh != null)
                {
                    lodLevel.sourceMesh = mesh;
                    lodLevel.vertexNum = mesh.vertexCount;
                    lodLevel.meshName = mesh.name;
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "Selected file is not a valid mesh asset.", "OK");
                }
            }
        }

        // 质量标签
        float qualityLabelWidth = 50f;
        Rect qualityLabelRect = new Rect(selectButtonRect.xMax + 10, rect.y, qualityLabelWidth, rect.height);
        EditorGUI.LabelField(qualityLabelRect, "Quality:");

        // 质量滑动条
        float sliderWidth = 80f;
        Rect sliderRect = new Rect(qualityLabelRect.xMax + 2, rect.y, sliderWidth, rect.height);
        float newQualitySlider = GUI.HorizontalSlider(sliderRect, lodLevel.quality, 0f, 100f);
        newQualitySlider = Mathf.Round(newQualitySlider); // 四舍五入到整数

        // 质量数值输入框
        float fieldWidth = 40f;
        Rect qualityFieldRect = new Rect(sliderRect.xMax + 5, rect.y, fieldWidth, rect.height);
        int newQualityField = EditorGUI.IntField(qualityFieldRect, Mathf.RoundToInt(lodLevel.quality));

        // 百分号标签
        Rect percentRect = new Rect(qualityFieldRect.xMax + 2, rect.y, 15f, rect.height);
        EditorGUI.LabelField(percentRect, "%");

        // 处理质量值的更改
        float newQuality = lodLevel.quality;
        if (newQualitySlider != lodLevel.quality)
            newQuality = newQualitySlider;
        if (newQualityField != Mathf.RoundToInt(lodLevel.quality))
            newQuality = newQualityField;

        // 确保质量值在有效范围内并按顺序递减
        if (newQuality != lodLevel.quality)
        {
            newQuality = Mathf.RoundToInt(newQuality);
            newQuality = Mathf.Clamp(newQuality, 0f, 100f);

            if (index > 0 && newQuality >= lodLevels[index - 1].quality)
            {
                newQuality = Mathf.FloorToInt(lodLevels[index - 1].quality - 1);
            }
            if (index < lodLevels.Count - 1 && newQuality <= lodLevels[index + 1].quality)
            {
                newQuality = Mathf.CeilToInt(lodLevels[index + 1].quality + 1);
            }

            lodLevel.quality = newQuality;
        }

        // 顶点信息（如果有空间）
        if (lodLevel.sourceMesh != null)
        {
            string vertexInfo = $"({lodLevel.vertexNum} vertices)";
            float infoWidth = 100f;
            Rect infoRect = new Rect(percentRect.xMax + 5, rect.y, infoWidth, rect.height);
            EditorGUI.LabelField(infoRect, vertexInfo);
        }
    }

    private void OnAddLODLevel(ReorderableList list)
    {
        if (lodLevels.Count >= 4)
        {
            EditorUtility.DisplayDialog("Warning", "Maximum LOD count (4) reached.", "OK");
            return;
        }

        float defaultQuality;
        switch (lodLevels.Count)
        {
            case 0: defaultQuality = 100f; break;
            case 1: defaultQuality = 70f; break;
            default: defaultQuality = 40f; break;
        }
        lodLevels.Add(new LODLevel { quality = defaultQuality });
    }

    private void OnRemoveLODLevel(ReorderableList list)
    {
        if (lodLevels.Count > 1)
        {
            lodLevels.RemoveAt(list.index);
        }
        else
        {
            EditorUtility.DisplayDialog("Warning", "Cannot remove the last LOD level.", "OK");
        }
    }

    private void SelectMesh(int index)
    {
        string path = EditorUtility.OpenFilePanel("Select Mesh", "Assets", "fbx,obj,mesh");
        if (!string.IsNullOrEmpty(path))
        {
            string relativePath = "Assets" + path.Substring(Application.dataPath.Length);
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(relativePath);
            if (mesh != null)
            {
                lodLevels[index].vertexNum = mesh.vertexCount;
                lodLevels[index].meshName = mesh.name;
                lodLevels[index].sourceMesh = mesh;
            }
        }
    }

    private bool ValidateAndPrepare()
    {
        if (lodLevels.Count == 0 || lodLevels[0].sourceMesh == null)
        {
            messageHint = "Please select at least one source mesh.";
            return false;
        }

        for (int i = 0; i < lodLevels.Count; i++)
        {
            if (lodLevels[i].quality < 0 || lodLevels[i].quality > 100)
            {
                messageHint = $"LOD {i} quality must be between 0 and 100.";
                return false;
            }
        }

        for (int i = 1; i < lodLevels.Count; i++)
        {
            if (lodLevels[i].quality >= lodLevels[i - 1].quality)
            {
                messageHint = $"LOD {i} quality must be lower than LOD {i - 1}.";
                return false;
            }
        }

        return true;
    }

    private void StartGeneration()
    {
        state = 1;
        startTime = Time.realtimeSinceStartup;
        EditorApplication.update += Update;
    }

    private void Update()
    {
        switch (state)
        {
            case 1:
                GenerateLODMeshes();
                break;
            case 2:
                FinishGeneration();
                break;
        }
        Repaint();
    }

    private void GenerateLODMeshes()
    {
        try
        {
            // 这里实现LOD生成逻辑
            // 可以参考MantisLODEditor的实现
            // 处理每个选中的mesh
            //foreach (var mesh in sourceMeshes)
            //{
            //    if (mesh != null)
            //    {
            //        ProcessMesh(mesh);
            //    }
            //}
            if (lodLevels[0].sourceMesh != null)
                ProcessMesh(lodLevels[0].sourceMesh);


            state = 2;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error generating LOD meshes: {e.Message}");
            state = 0;
        }
    }

#if ENABLE_MANTISLOD
    private void ProcessMesh(Mesh sourceMesh)
    {
        try
        {
            currentFileName = sourceMesh.name;
            Mantis_Mesh[] mantisMeshes = new Mantis_Mesh[1];

            // 4. 为每个 LOD 级别生成网格
            for (int i = 1; i < lodLevels.Count; i++) // 跳过 LOD0，因为它是原始网格
            {
                Mesh meshCopy = Instantiate(sourceMesh);
                // 1. 创建 Mantis_Mesh

                mantisMeshes[0] = new Mantis_Mesh { mesh = meshCopy };
                int originalVertCount = meshCopy.vertexCount;
                int originalTriCount = meshCopy.triangles.Length / 3;

                // 2. 准备简化
                int originalFaceCount = MantisLODEditorUtility.PrepareSimplify(mantisMeshes, false);

                // 3. 执行网格简化
                MantisLODEditorUtility.Simplify(
                    mantisMeshes,
                    protectBoundary,
                    moreDetails,
                    protectSymmetry,
                    protectHardEdge,
                    beautifulTriangles,
                    false,
                    10
                );

                float quality = lodLevels[i].quality;
                int currentTriCount = MantisLODEditorUtility.SetQuality(mantisMeshes, quality);

                Mesh lodMesh = mantisMeshes[0].mesh;

                // 重要：保留原始网格的顶点属性，特别是用于GPU动画的数据
                // 复制顶点颜色和UV数据，这些通常包含骨骼权重和索引
                if (sourceMesh.colors.Length > 0)
                    lodMesh.colors = sourceMesh.colors;

                // 确保复制所有UV通道，特别是存储动画数据的通道
                if (sourceMesh.uv.Length > 0)
                    lodMesh.uv = sourceMesh.uv;
                if (sourceMesh.uv2.Length > 0)
                    lodMesh.uv2 = sourceMesh.uv2;
                if (sourceMesh.uv3.Length > 0)
                    lodMesh.uv3 = sourceMesh.uv3;
                if (sourceMesh.uv4.Length > 0)
                    lodMesh.uv4 = sourceMesh.uv4;
                if (sourceMesh.uv5.Length > 0)
                    lodMesh.uv5 = sourceMesh.uv5;
                if (sourceMesh.uv6.Length > 0)
                    lodMesh.uv6 = sourceMesh.uv6;
                if (sourceMesh.uv7.Length > 0)
                    lodMesh.uv7 = sourceMesh.uv7;
                if (sourceMesh.uv8.Length > 0)
                    lodMesh.uv8 = sourceMesh.uv8;

                // 确保复制顶点属性，这些可能包含动画数据
                if (sourceMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord1))
                {
                    List<Vector4> uvs1 = new List<Vector4>();
                    sourceMesh.GetUVs(1, uvs1);
                    lodMesh.SetUVs(1, uvs1);
                }
                if (sourceMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord2))
                {
                    List<Vector4> uvs2 = new List<Vector4>();
                    sourceMesh.GetUVs(2, uvs2);
                    lodMesh.SetUVs(2, uvs2);
                }
                if (recalculateNormal)
                {
                    lodMesh.RecalculateNormals();
                }

                SaveLODMesh(currentFileName, lodMesh, i);

                // 计算并显示三角形和顶点的减少情况
                float triReduction = (1f - (float)currentTriCount / originalTriCount) * 100f;
                float vertReduction = (1f - (float)lodMesh.vertexCount / originalVertCount) * 100f;
                lodLevels[i].sourceMesh = lodMesh;
                lodLevels[i].vertexNum = lodMesh.vertexCount;
                lodLevels[i].meshName = lodMesh.name;
                Debug.Log($"Created LOD{i} with:\n" +
                         $"Triangles: {currentTriCount}/{originalTriCount} ({triReduction:F1}% reduction)\n" +
                         $"Vertices: {lodMesh.vertexCount}/{originalVertCount} ({vertReduction:F1}% reduction)");
            }

            // 5. 清理资源
            MantisLODEditorUtility.FinishSimplify(mantisMeshes, false, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error processing mesh {sourceMesh.name}: {e.Message}\n{e.StackTrace}");
            throw;
        }
    }
#endif




    private void SaveLODMesh(string currentFileName, Mesh mesh, int lodLevel)
    {
        // 创建保存路径
        string path = $"Assets/LODs/{currentFileName}{lodLevel}.asset";
        string directory = Path.GetDirectoryName(path);

        // 确保目录存在
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // 保存网格资产
        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
    }


    private void FinishGeneration()
    {
        state = 0;
        EditorApplication.update -= Update;
        messageHint = "LOD generation completed!";
        AssetDatabase.Refresh();
    }

    private void CombineLODMeshes()
    {
        if (!ValidateAndPrepare()) return;

        try
        {
            Mesh combinedMesh = new Mesh();
            combinedMesh.name = "Combined_LOD_Mesh";
            
            // 设置32位索引格式以支持更多顶点
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            // 准备数据列表
            List<Vector3> allVertices = new List<Vector3>();
            List<Vector3> allNormals = new List<Vector3>();
            List<Vector2> allUVs = new List<Vector2>();
            List<Vector4> allTangents = new List<Vector4>();
            List<Color> allColors = new List<Color>();
            
            // 添加GPU动画所需的额外UV通道
            List<Vector4> allUV1 = new List<Vector4>();
            List<Vector4> allUV2 = new List<Vector4>();
            List<Vector4> allUV3 = new List<Vector4>();
            List<Vector4> allUV4 = new List<Vector4>();
            List<Vector4> allUV5 = new List<Vector4>();
            List<Vector4> allUV6 = new List<Vector4>();
            List<Vector4> allUV7 = new List<Vector4>();
            
            // 计算总子网格数量 - 修改这里，考虑每个LOD网格可能有多个子网格
            int totalSubmeshCount = 0;
            Dictionary<int, List<int>> lodToSubmeshMap = new Dictionary<int, List<int>>();
            
            // 首先计算总子网格数量并创建映射
            for (int i = 0; i < lodLevels.Count; i++)
            {
                var lodMesh = lodLevels[i].sourceMesh;
                if (lodMesh == null) continue;
                
                int submeshCount = lodMesh.subMeshCount;
                totalSubmeshCount += submeshCount;
                
                List<int> submeshIndices = new List<int>();
                for (int j = 0; j < submeshCount; j++)
                {
                    submeshIndices.Add(j);
                }
                lodToSubmeshMap[i] = submeshIndices;
            }
            
            // 初始化子网格三角形列表 - 使用总子网格数量
            List<int>[] subMeshTriangles = new List<int>[totalSubmeshCount];
            for (int i = 0; i < totalSubmeshCount; i++)
            {
                subMeshTriangles[i] = new List<int>();
            }

            // 处理每个LOD级别
            int vertexOffset = 0;
            int submeshOffset = 0;
            
            for (int i = 0; i < lodLevels.Count; i++)
            {
                var lodMesh = lodLevels[i].sourceMesh;
                if (lodMesh == null) continue;

                try
                {
                    // 确保网格数据是最新的
                    lodMesh.RecalculateBounds();

                    // 添加顶点数据
                    Vector3[] vertices = lodMesh.vertices;
                    Vector3[] normals = lodMesh.normals;
                    Vector2[] uvs = lodMesh.uv;
                    
                    allVertices.AddRange(vertices);
                    allNormals.AddRange(normals);
                    allUVs.AddRange(uvs);
                    
                    // 添加切线数据（如果有）
                    if (lodMesh.tangents != null && lodMesh.tangents.Length > 0)
                    {
                        allTangents.AddRange(lodMesh.tangents);
                    }
                    
                    // 添加顶点颜色（如果有）
                    if (lodMesh.colors != null && lodMesh.colors.Length > 0)
                    {
                        allColors.AddRange(lodMesh.colors);
                    }
                    
                    // 添加GPU动画所需的UV通道数据
                    for (int uvChannel = 1; uvChannel <= 7; uvChannel++)
                    {
                        if (lodMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0 + uvChannel))
                        {
                            List<Vector4> tempUvData = new List<Vector4>();
                            lodMesh.GetUVs(uvChannel, tempUvData);
                            
                            switch (uvChannel)
                            {
                                case 1: allUV1.AddRange(tempUvData); break;
                                case 2: allUV2.AddRange(tempUvData); break;
                                case 3: allUV3.AddRange(tempUvData); break;
                                case 4: allUV4.AddRange(tempUvData); break;
                                case 5: allUV5.AddRange(tempUvData); break;
                                case 6: allUV6.AddRange(tempUvData); break;
                                case 7: allUV7.AddRange(tempUvData); break;
                            }
                            
                            tempUvData.Clear();
                        }
                    }

                    // 处理每个子网格的三角形索引
                    for (int subMeshIndex = 0; subMeshIndex < lodMesh.subMeshCount; subMeshIndex++)
                    {
                        int[] triangles = lodMesh.GetTriangles(subMeshIndex);
                        int combinedSubMeshIndex = submeshOffset + subMeshIndex;
                        
                        // 添加三角形索引，需要考虑顶点偏移
                        for (int j = 0; j < triangles.Length; j++)
                        {
                            subMeshTriangles[combinedSubMeshIndex].Add(triangles[j] + vertexOffset);
                        }
                        
                        Debug.Log($"LOD {i}, SubMesh {subMeshIndex}: Triangles: {triangles.Length/3}");
                    }

                    // 更新顶点偏移和子网格偏移
                    vertexOffset += vertices.Length;
                    submeshOffset += lodMesh.subMeshCount;

                    // 输出调试信息
                    Debug.Log($"LOD {i}: Vertices: {vertices.Length}, SubMeshes: {lodMesh.subMeshCount}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error processing LOD {i}: {e.Message}");
                }
            }

            // 设置顶点数据
            combinedMesh.vertices = allVertices.ToArray();
            combinedMesh.normals = allNormals.ToArray();
            combinedMesh.uv = allUVs.ToArray();
            
            // 设置切线数据（如果有）
            if (allTangents.Count > 0 && allTangents.Count == allVertices.Count)
            {
                combinedMesh.tangents = allTangents.ToArray();
            }
            
            // 设置顶点颜色（如果有）
            if (allColors.Count > 0 && allColors.Count == allVertices.Count)
            {
                combinedMesh.colors = allColors.ToArray();
            }
            
            // 设置GPU动画所需的UV通道数据
            if (allUV1.Count > 0) combinedMesh.SetUVs(1, allUV1);
            if (allUV2.Count > 0) combinedMesh.SetUVs(2, allUV2);
            if (allUV3.Count > 0) combinedMesh.SetUVs(3, allUV3);
            if (allUV4.Count > 0) combinedMesh.SetUVs(4, allUV4);
            if (allUV5.Count > 0) combinedMesh.SetUVs(5, allUV5);
            if (allUV6.Count > 0) combinedMesh.SetUVs(6, allUV6);
            if (allUV7.Count > 0) combinedMesh.SetUVs(7, allUV7);

            // 清理临时列表以减少内存压力
            allUV1.Clear(); allUV2.Clear(); allUV3.Clear(); allUV4.Clear();
            allUV5.Clear(); allUV6.Clear(); allUV7.Clear();
            allVertices.Clear(); allNormals.Clear(); allUVs.Clear();
            allTangents.Clear(); allColors.Clear();

            // 设置子网格
            combinedMesh.subMeshCount = totalSubmeshCount;
            for (int i = 0; i < totalSubmeshCount; i++)
            {
                if (subMeshTriangles[i].Count > 0)
                {
                    int[] triangles = subMeshTriangles[i].ToArray();
                    combinedMesh.SetTriangles(triangles, i);
                    Debug.Log($"Setting submesh {i} with {triangles.Length/3} triangles");
                    
                    // 清理临时数组
                    subMeshTriangles[i].Clear();
                }
                else
                {
                    Debug.LogWarning($"SubMesh {i} 没有有效的三角形");
                }
            }

            // 重新计算边界
            combinedMesh.RecalculateBounds();

            // 保存合并后的网格
            string directory = "Assets/LODs/";
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string meshPath = $"{directory}Combined_LOD_Mesh.asset";
            AssetDatabase.CreateAsset(combinedMesh, meshPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 强制垃圾回收
            System.GC.Collect();

            // 验证合并后的mesh
            Debug.Log($"Combined mesh: Vertices: {combinedMesh.vertexCount}, Submeshes: {combinedMesh.subMeshCount}");
            for (int i = 0; i < combinedMesh.subMeshCount; i++)
            {
                Debug.Log($"Submesh {i} triangle count: {combinedMesh.GetTriangles(i).Length/3}");
            }

            messageHint = "LOD mesh combined successfully!";
            
            // 选中新创建的资产
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error combining meshes: {e.Message}\n{e.StackTrace}");
            messageHint = "Error combining meshes. Check console for details.";
        }
        finally
        {
            // 确保在方法结束时强制垃圾回收
            System.GC.Collect();
        }
    }
}
