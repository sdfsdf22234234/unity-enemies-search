using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace GPUAnimation.Editor
{
    public class GPUBoneViewer : EditorWindow
    {
        [MenuItem("Game Framework/GPU Animation/Bone Viewer")]
        public static void ShowWindow()
        {
            GetWindow<GPUBoneViewer>("GPU Bone Viewer");
        }

        private GameObject targetModel;
        private Transform selectedBone;
        private Vector2 scrollPosition;
        private bool showAllBones = true;
        private float sphereSize = 0.05f;
        private Color boneColor = Color.yellow;
        private Color selectedBoneColor = Color.red;
        private bool showLabels = true;
        private bool showBoneIndex = true;
        private Dictionary<Transform, int> boneIndices = new Dictionary<Transform, int>();
        private List<Transform> allBones = new List<Transform>();
        private string searchText = "";
        private bool autoSelectInScene = true;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("GPU Bone Viewer", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 模型选择
            EditorGUI.BeginChangeCheck();
            targetModel = (GameObject)EditorGUILayout.ObjectField("Target Model", targetModel, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                if (targetModel != null)
                {
                    RefreshBoneList();
                }
                else
                {
                    allBones.Clear();
                    boneIndices.Clear();
                    selectedBone = null;
                }
            }

            if (targetModel == null)
            {
                EditorGUILayout.HelpBox("Please select a model to view its bones.", MessageType.Info);
                return;
            }

            // 显示设置
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Display Settings", EditorStyles.boldLabel);

            showAllBones = EditorGUILayout.Toggle("Show All Bones", showAllBones);
            showLabels = EditorGUILayout.Toggle("Show Labels", showLabels);
            showBoneIndex = EditorGUILayout.Toggle("Show Bone Index", showBoneIndex);
            sphereSize = EditorGUILayout.Slider("Sphere Size", sphereSize, 0.01f, 0.2f);
            boneColor = EditorGUILayout.ColorField("Bone Color", boneColor);
            selectedBoneColor = EditorGUILayout.ColorField("Selected Bone Color", selectedBoneColor);
            autoSelectInScene = EditorGUILayout.Toggle("Auto Select In Scene", autoSelectInScene);

            // 搜索框
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            searchText = EditorGUILayout.TextField("Search", searchText);
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                searchText = "";
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();
                    
            // 骨骼列表
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Bones ({allBones.Count})", EditorStyles.boldLabel);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            for (int i = 0; i < allBones.Count; i++)
            {
                Transform bone = allBones[i];
                if (bone == null) continue;

                // 如果有搜索文本，过滤不匹配的骨骼
                if (!string.IsNullOrEmpty(searchText) &&
                    !bone.name.ToLower().Contains(searchText.ToLower()))
                {
                    continue;
                }

                EditorGUILayout.BeginHorizontal();

                // 缩进以显示层次结构
                int depth = GetBoneDepth(bone);
                GUILayout.Space(depth * 15);

                // 选择按钮
                bool isSelected = selectedBone == bone;
                bool newSelected = EditorGUILayout.ToggleLeft(
                    $"{bone.name} (Index: {boneIndices[bone]})",
                    isSelected,
                    isSelected ? EditorStyles.boldLabel : EditorStyles.label
                );

                if (newSelected != isSelected)
                {
                    selectedBone = newSelected ? bone : null;
                    if (newSelected && autoSelectInScene)
                    {
                        Selection.activeGameObject = bone.gameObject;
                    }
                    SceneView.RepaintAll();
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            // 选中骨骼的详细信息
            if (selectedBone != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Selected Bone Details", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;

                EditorGUILayout.LabelField("Name", selectedBone.name);
                EditorGUILayout.LabelField("Index", boneIndices[selectedBone].ToString());
                EditorGUILayout.LabelField("Path", GetBonePath(selectedBone));
                EditorGUILayout.Vector3Field("Position", selectedBone.position);

                EditorGUI.indentLevel--;

                EditorGUILayout.Space();
                if (GUILayout.Button("Copy Bone Index to Clipboard"))
                {
                    EditorGUIUtility.systemCopyBuffer = boneIndices[selectedBone].ToString();
                    Debug.Log($"Copied bone index {boneIndices[selectedBone]} to clipboard");
                }

                if (GUILayout.Button("Copy Bone Path to Clipboard"))
                {
                    string path = GetBonePath(selectedBone);
                    EditorGUIUtility.systemCopyBuffer = path;
                    Debug.Log($"Copied bone path to clipboard: {path}");
                }
            }

            // 刷新按钮
            EditorGUILayout.Space();
            if (GUILayout.Button("Refresh Bone List"))
            {
                RefreshBoneList();
                SceneView.RepaintAll();
            }

            // 自动刷新场景视图
            if (GUI.changed)
            {
                SceneView.RepaintAll();
            }
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (targetModel == null || allBones.Count == 0)
                return;

            // 绘制所有骨骼点
            if (showAllBones)
            {
                foreach (var bone in allBones)
                {
                    if (bone == null) continue;

                    bool isSelected = bone == selectedBone;
                    Color color = isSelected ? selectedBoneColor : boneColor;

                    Handles.color = color;
                    Handles.SphereHandleCap(0, bone.position, Quaternion.identity, sphereSize, EventType.Repaint);

                    // 绘制标签
                    if (showLabels)
                    {
                        string label = showBoneIndex ?
                            $"{bone.name} ({boneIndices[bone]})" :
                            bone.name;

                        Handles.Label(bone.position + Vector3.up * sphereSize * 1.5f, label);
                    }

                    // 绘制父子关系连线
                    if (bone.parent != null && allBones.Contains(bone.parent))
                    {
                        Handles.DrawLine(bone.position, bone.parent.position);
                    }
                }
            }
            // 只绘制选中的骨骼
            else if (selectedBone != null)
            {
                Handles.color = selectedBoneColor;
                Handles.SphereHandleCap(0, selectedBone.position, Quaternion.identity, sphereSize, EventType.Repaint);

                if (showLabels)
                {
                    string label = showBoneIndex ?
                        $"{selectedBone.name} ({boneIndices[selectedBone]})" :
                        selectedBone.name;

                    Handles.Label(selectedBone.position + Vector3.up * sphereSize * 1.5f, label);
                }
            }
        }

        private void RefreshBoneList()
        {
            if (targetModel == null) return;

            allBones.Clear();
            boneIndices.Clear();

            // 获取所有骨骼
            Transform[] transforms = targetModel.GetComponentsInChildren<Transform>(true);

            // 按层次结构排序
            allBones = transforms.OrderBy(t => GetBonePath(t)).ToList();

            // 分配索引
            for (int i = 0; i < allBones.Count; i++)
            {
                boneIndices[allBones[i]] = i;
            }

            // 如果之前有选中的骨骼，尝试重新找到它
            if (selectedBone != null)
            {
                string previousPath = GetBonePath(selectedBone);
                selectedBone = null;

                foreach (var bone in allBones)
                {
                    if (GetBonePath(bone) == previousPath)
                    {
                        selectedBone = bone;
                        break;
                    }
                }
            }
        }

        private int GetBoneDepth(Transform bone)
        {
            int depth = 0;
            Transform current = bone;

            while (current != null && current != targetModel.transform)
            {
                depth++;
                current = current.parent;
            }

            return depth;
        }

        private string GetBonePath(Transform bone)
        {
            if (bone == null || targetModel == null) return "";

            string path = bone.name;
            Transform current = bone.parent;

            while (current != null && current != targetModel.transform)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }
}