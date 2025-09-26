#define ENABLE_SPINE //导入Spine插件后取消此行注释以开启功能

using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.IO;
using System.Collections.Generic;
#if ENABLE_SPINE
using Spine.Unity;
using Spine.Unity.Editor;
using Spine;
using Icons = Spine.Unity.Editor.SpineEditorUtilities.Icons;
#endif
public class Spine2Animator : EditorWindow
{
    [MenuItem("Game Framework/GPU Animation/Spine(Skeleton) to Animator", false, 0)]
    public static void ShowBakingWindow()
    {
        Spine2Animator window = EditorWindow.GetWindow<Spine2Animator>();
        window.minSize = new Vector2(330f, 530f);
        window.maxSize = new Vector2(600f, 1000f);
        window.titleContent = new GUIContent("Spine2Animator");
        window.Show();
#if !ENABLE_SPINE
        Debug.LogError("请安装Spine插件后, 将此工具代码首行取消注释以开启Spine转Animator功能");
#endif
    }
#if ENABLE_SPINE
    public SkeletonDataAsset skeletonDataAsset;
    [SpineSkin(dataField: "skeletonDataAsset")]
    public string skinToBake = "default";

    // Settings
    bool bakeAnimations = true;
    bool bakeIK = true;
    SendMessageOptions bakeEventOptions = SendMessageOptions.DontRequireReceiver;

    SerializedObject so;
    Skin bakeSkin;
    string outputDir;
    void DataAssetChanged()
    {
        bakeSkin = null;
    }
    private void OnEnable()
    {
        outputDir = EditorPrefs.GetString("Spine2Animator.OutputDir", "Assets");
    }
    private void OnDisable()
    {
        EditorPrefs.SetString("Spine2Animator.OutputDir", outputDir);
    }
    void OnGUI()
    {
        so = so ?? new SerializedObject(this);

        EditorGUIUtility.wideMode = true;
        EditorGUILayout.LabelField("Spine Skeleton Prefab Baking", EditorStyles.boldLabel);

        const string BakingWarningMessage = "\nSkeleton baking is not the primary use case for Spine skeletons." +
            "\nUse baking if you have specialized uses, such as simplified skeletons with movement driven by physics." +

            "\n\nBaked Skeletons do not support the following:" +
            "\n\tDisabled rotation or scale inheritance" +
            "\n\tLocal Shear" +
            "\n\tAll Constraint types" +
            "\n\tWeighted mesh verts with more than 4 bound bones" +

            "\n\nBaked Animations do not support the following:" +
            "\n\tMesh Deform Keys" +
            "\n\tColor Keys" +
            "\n\tDraw Order Keys" +

            "\n\nAnimation Curves are sampled at 60fps and are not realtime." +
            "\nConstraint animations are also baked into animation curves." +
            "\nSee SkeletonBaker.cs comments for full details.\n";

        EditorGUILayout.HelpBox(BakingWarningMessage, MessageType.Info, true);
        EditorGUILayout.BeginHorizontal();
        {
            EditorGUILayout.LabelField("Output:", GUILayout.Width(50));
            EditorGUILayout.LabelField(outputDir, EditorStyles.selectionRect);
            if (GUILayout.Button("Select Folder"))
            {
                string selectPath = EditorUtility.SaveFolderPanel("Select Folder", outputDir, "");
                if (!string.IsNullOrEmpty(selectPath))
                {
                    outputDir = Path.GetRelativePath(Directory.GetParent(Application.dataPath).FullName, selectPath);
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUI.BeginChangeCheck();
        SerializedProperty skeletonDataAssetProperty = so.FindProperty("skeletonDataAsset");
        EditorGUILayout.PropertyField(skeletonDataAssetProperty, EditorGUIUtility.TrTextContentWithIcon("SkeletonDataAsset", Icons.spine));
        if (EditorGUI.EndChangeCheck())
        {
            so.ApplyModifiedProperties();
            DataAssetChanged();
        }
        EditorGUILayout.Space();

        if (skeletonDataAsset == null) return;
        SkeletonData skeletonData = skeletonDataAsset.GetSkeletonData(false);
        if (skeletonData == null) return;
        bool hasExtraSkins = skeletonData.Skins.Count > 1;

        using (new SpineInspectorUtility.BoxScope(false))
        {
            EditorGUILayout.LabelField(skeletonDataAsset.name, EditorStyles.boldLabel);
            using (new SpineInspectorUtility.IndentScope())
            {
                EditorGUILayout.LabelField(EditorGUIUtility.TrTextContentWithIcon("Bones: " + skeletonData.Bones.Count, Icons.bone));
                EditorGUILayout.LabelField(EditorGUIUtility.TrTextContentWithIcon("Slots: " + skeletonData.Slots.Count, Icons.slotRoot));

                if (hasExtraSkins)
                {
                    EditorGUILayout.LabelField(EditorGUIUtility.TrTextContentWithIcon("Skins: " + skeletonData.Skins.Count, Icons.skinsRoot));
                    EditorGUILayout.LabelField(EditorGUIUtility.TrTextContentWithIcon("Current skin attachments: " + (bakeSkin == null ? 0 : bakeSkin.Attachments.Count), Icons.skinPlaceholder));
                }
                else if (skeletonData.Skins.Count == 1)
                {
                    EditorGUILayout.LabelField(EditorGUIUtility.TrTextContentWithIcon("Skins: 1 (only default Skin)", Icons.skinsRoot));
                }

                int totalAttachments = 0;
                foreach (Skin s in skeletonData.Skins)
                    totalAttachments += s.Attachments.Count;
                EditorGUILayout.LabelField(EditorGUIUtility.TrTextContentWithIcon("Total Attachments: " + totalAttachments, Icons.genericAttachment));
            }
        }
        using (new SpineInspectorUtility.BoxScope(false))
        {
            EditorGUILayout.LabelField("Animations", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(EditorGUIUtility.TrTextContentWithIcon("Animations: " + skeletonData.Animations.Count, Icons.animation));

            using (new SpineInspectorUtility.IndentScope())
            {
                bakeAnimations = EditorGUILayout.Toggle(EditorGUIUtility.TrTextContentWithIcon("Bake Animations", Icons.animationRoot), bakeAnimations);
                using (new EditorGUI.DisabledScope(!bakeAnimations))
                {
                    using (new SpineInspectorUtility.IndentScope())
                    {
                        bakeIK = EditorGUILayout.Toggle(EditorGUIUtility.TrTextContentWithIcon("Bake IK", Icons.constraintIK), bakeIK);
                        bakeEventOptions = (SendMessageOptions)EditorGUILayout.EnumPopup(EditorGUIUtility.TrTextContentWithIcon("Event Options", Icons.userEvent), bakeEventOptions);
                    }
                }
            }
        }
        EditorGUILayout.Space();

        if (!string.IsNullOrEmpty(skinToBake) && UnityEngine.Event.current.type == EventType.Repaint)
            bakeSkin = skeletonData.FindSkin(skinToBake) ?? skeletonData.DefaultSkin;

        Texture2D prefabIcon = EditorGUIUtility.FindTexture("PrefabModel Icon");

        if (hasExtraSkins)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(so.FindProperty("skinToBake"));
            if (EditorGUI.EndChangeCheck())
            {
                so.ApplyModifiedProperties();
                Repaint();
            }

            if (SpineInspectorUtility.LargeCenteredButton(EditorGUIUtility.TrTextContentWithIcon(string.Format("Bake Skeleton with Skin ({0})", (bakeSkin == null ? "default" : bakeSkin.Name)), prefabIcon)))
            {
                SkeletonBaker.BakeToPrefab(skeletonDataAsset, new ExposedList<Skin>(new[] { bakeSkin }), GetOuputDir(), bakeAnimations, bakeIK, bakeEventOptions);
                UpdateBakedMesh();
            }

            if (SpineInspectorUtility.LargeCenteredButton(EditorGUIUtility.TrTextContentWithIcon(string.Format("Bake All ({0} skins)", skeletonData.Skins.Count), prefabIcon)))
            {
                SkeletonBaker.BakeToPrefab(skeletonDataAsset, skeletonData.Skins, GetOuputDir(), bakeAnimations, bakeIK, bakeEventOptions);
                UpdateBakedMesh();
            }
        }
        else
        {
            if (SpineInspectorUtility.LargeCenteredButton(EditorGUIUtility.TrTextContentWithIcon("Bake Skeleton", prefabIcon)))
            {
                SkeletonBaker.BakeToPrefab(skeletonDataAsset, new ExposedList<Skin>(new[] { bakeSkin }), GetOuputDir(), bakeAnimations, bakeIK, bakeEventOptions);
                UpdateBakedMesh();
            }

        }
    }
    private void UpdateBakedMesh()
    {
        string prefabPath = GetOuputDir();
        var allSkinPrefabs = AssetDatabase.FindAssets("t:prefab", new string[] { prefabPath });
        foreach (var guid in allSkinPrefabs)
        {
            var assetsPath = AssetDatabase.GUIDToAssetPath(guid);
            var subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetsPath);
            if (subAssets.Length < 1) continue;
            Dictionary<string, int> meshOrder = new Dictionary<string, int>();
            var prefab = PrefabUtility.LoadPrefabContents(assetsPath);
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            
            foreach (var renderer in renderers)
            {
                if (renderer is SkinnedMeshRenderer rMeshRd)
                {
                    if (!meshOrder.ContainsKey(rMeshRd.sharedMesh.name)) meshOrder.Add(rMeshRd.sharedMesh.name, rMeshRd.sortingOrder);
                }
                else if (renderer is MeshRenderer meshRd && meshRd.GetComponent<MeshFilter>() != null)
                {
                    var meshName = meshRd.GetComponent<MeshFilter>().sharedMesh.name;
                    if (!meshOrder.ContainsKey(meshName)) meshOrder.Add(meshName, meshRd.sortingOrder);
                }
            }
            foreach (var item in subAssets)
            {
                if (item is Mesh mesh && meshOrder.ContainsKey(mesh.name))
                {
                    var meshPoints = mesh.vertices;
                    float posZ = -meshOrder[mesh.name] * 0.1f;
                    for (int i = 0; i < meshPoints.Length; i++)
                    {
                        var point = meshPoints[i];
                        point.z = posZ;
                        meshPoints[i] = point;
                    }
                    mesh.SetVertices(meshPoints);
                    AssetDatabase.SaveAssetIfDirty(mesh);
                }
            }
            for (int i = renderers.Length - 1; i >= 0; i--)
            {
                var go = renderers[i].gameObject;
                if (!go.activeInHierarchy)
                {
                    DestroyImmediate(go);
                    ArrayUtility.RemoveAt(ref renderers, i);
                }
            }
            PrefabUtility.SaveAsPrefabAssetAndConnect(prefab, assetsPath, InteractionMode.AutomatedAction);
        }
    }
    private string GetOuputDir()
    {
        string dir = Path.Combine(outputDir, skeletonDataAsset.skeletonJSON.name);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }
        return dir;
    }
#else
    private void OnGUI()
    {
        EditorGUILayout.LabelField("请安装Spine插件后, 将此工具代码首行取消注释以开启Spine转Animator功能", EditorStyles.whiteLargeLabel);
    }
#endif
}