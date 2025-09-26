/*--------------------------------------------------------
   ProgressiveMeshRuntime.cs

   Created by MINGFEN WANG on 13-12-26.
   Copyright (c) 2013 MINGFEN WANG. All rights reserved.
   http://www.mesh-online.net/
   --------------------------------------------------------*/
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections.Generic;

namespace MantisLODEditor
{
    public class ProgressiveMeshRuntime : MonoBehaviour
    {
        // Drag a reference or assign it with code
        public ProgressiveMesh progressiveMesh = null;

        // Optional fields
        public Text fpsHint = null;
        public Text lodHint = null;
        public Text triangleHint = null;

        [HideInInspector]
        public bool optimize_on_the_fly = true;
        // Clamp lod to [minLod, maxLod]
        [HideInInspector]
        public int[] mesh_lod_range = null;
        [HideInInspector]
        public bool never_cull = true;
        [HideInInspector]
        public int lod_strategy = 1;
        [HideInInspector]
        public float cull_ratio = 0.1f;
        [HideInInspector]
        public float disappear_distance = 250.0f;
        [HideInInspector]
        public float updateInterval = 0.25f;

        private int current_lod = -1;

        private Component[] allBasicRenderers = null;

        // How often to check lod changes, default four times per second.
        // You may increase the value to balance the load if you have hundreds of 3d models in the scene.
        private float currentTimeToInterval = 0.0f;
        private bool culled = false;
        private bool working = false;

#if UNITY_EDITOR
        [MenuItem("Window/Mantis LOD Editor/Component/Runtime/Progressive Mesh Runtime")]
        public static void AddComponent()
        {
            GameObject SelectedObject = Selection.activeGameObject;
            if (SelectedObject)
            {
                // Register root object for undo.
                Undo.RegisterCreatedObjectUndo(SelectedObject.AddComponent(typeof(ProgressiveMeshRuntime)), "Add Progressive Mesh Runtime");
            }
        }
        [MenuItem("Window/Mantis LOD Editor/Component/Runtime/Progressive Mesh Runtime", true)]
        static bool ValidateAddComponent()
        {
            // Return false if no gameobject is selected.
            return Selection.activeGameObject != null;
        }
#endif
        void Awake()
        {
            get_all_meshes();
        }
        // Use this for initialization
        void Start()
        {
        }
        private float ratio_of_screen()
        {
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (Component child in allBasicRenderers)
            {
                Renderer rend = (Renderer)child;
                Vector3 center = rend.bounds.center;
                float radius = rend.bounds.extents.magnitude;
                Vector3[] six_points = new Vector3[6];
                six_points[0] = Camera.main.WorldToScreenPoint(new Vector3(center.x - radius, center.y, center.z));
                six_points[1] = Camera.main.WorldToScreenPoint(new Vector3(center.x + radius, center.y, center.z));
                six_points[2] = Camera.main.WorldToScreenPoint(new Vector3(center.x, center.y - radius, center.z));
                six_points[3] = Camera.main.WorldToScreenPoint(new Vector3(center.x, center.y + radius, center.z));
                six_points[4] = Camera.main.WorldToScreenPoint(new Vector3(center.x, center.y, center.z - radius));
                six_points[5] = Camera.main.WorldToScreenPoint(new Vector3(center.x, center.y, center.z + radius));
                foreach (Vector3 v in six_points)
                {
                    if (v.x < min.x) min.x = v.x;
                    if (v.y < min.y) min.y = v.y;
                    if (v.x > max.x) max.x = v.x;
                    if (v.y > max.y) max.y = v.y;
                }
            }
            float ratio_width = (max.x - min.x) / Camera.main.pixelWidth;
            float ratio_height = (max.y - min.y) / Camera.main.pixelHeight;
            float ratio = (ratio_width > ratio_height) ? ratio_width : ratio_height;
            if (ratio > 1.0f) ratio = 1.0f;

            return ratio;
        }
        private float ratio_of_distance(float distance0)
        {
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (Component child in allBasicRenderers)
            {
                Renderer rend = (Renderer)child;
                Vector3 center = rend.bounds.center;
                float radius = rend.bounds.extents.magnitude;
                Vector3[] six_points = new Vector3[6];
                six_points[0] = new Vector3(center.x - radius, center.y, center.z);
                six_points[1] = new Vector3(center.x + radius, center.y, center.z);
                six_points[2] = new Vector3(center.x, center.y - radius, center.z);
                six_points[3] = new Vector3(center.x, center.y + radius, center.z);
                six_points[4] = new Vector3(center.x, center.y, center.z - radius);
                six_points[5] = new Vector3(center.x, center.y, center.z + radius);
                foreach (Vector3 v in six_points)
                {
                    if (v.x < min.x) min.x = v.x;
                    if (v.y < min.y) min.y = v.y;
                    if (v.z < min.z) min.z = v.z;
                    if (v.x > max.x) max.x = v.x;
                    if (v.y > max.y) max.y = v.y;
                    if (v.z > max.z) max.z = v.z;
                }
            }
            Vector3 average_position = (min + max) * 0.5f;
            float distance = Vector3.Distance(Camera.main.transform.position, average_position);
            float ratio = 1.0f - distance / distance0;
            if (ratio < 0.0f) ratio = 0.0f;

            return ratio;
        }
        // ÿ֡����һ�� Update
        void Update()
        {
            if (progressiveMesh)
            {
                currentTimeToInterval -= Time.deltaTime;
                // ��ʱ���
                if (currentTimeToInterval <= 0.0f)
                {
                    // �����Ϸ�����Ƿ�ɼ�
                    bool visable = false;
                    if (!culled)
                    {
                        allBasicRenderers = (Component[])(gameObject.GetComponentsInChildren(typeof(Renderer)));
                        foreach (Component child in allBasicRenderers)
                        {
                            if (((Renderer)child).isVisible) visable = true;
                            break;
                        }
                    }
                    // ֻ�е���Ϸ���������޳���ɼ�ʱ�Ÿ��Ĳ㼶
                    if (culled || visable)
                    {
                        float ratio = 0.0f;
                        // ֻ�е�������ڲ㼶�д��ڼ���״̬ʱ�ż�����Ļ����
                        if (Camera.main != null && Camera.main.gameObject != null && Camera.main.gameObject.activeInHierarchy)
                        {
                            allBasicRenderers = (Component[])(gameObject.GetComponentsInChildren(typeof(Renderer)));
                            if (lod_strategy == 0) ratio = ratio_of_screen();
                            if (lod_strategy == 1) ratio = ratio_of_distance(disappear_distance);
                        }
                        // ������Ը����޳�����
                        if (never_cull == false && ratio < cull_ratio)
                        {
                            // �޳���Ϸ����
                            if (!culled)
                            {
                                // �����޳�
                                allBasicRenderers = (Component[])(gameObject.GetComponentsInChildren(typeof(Renderer)));
                                foreach (Component child in allBasicRenderers)
                                {
                                    ((Renderer)child).enabled = false;
                                }
                                culled = true;
                            }
                        }
                        else
                        {
                            // ��ʾ��Ϸ����
                            if (culled)
                            {
                                // ������ʾ
                                allBasicRenderers = (Component[])(gameObject.GetComponentsInChildren(typeof(Renderer)));
                                foreach (Component child in allBasicRenderers)
                                {
                                    ((Renderer)child).enabled = true;
                                }
                                culled = false;
                            }
                            // ��ȡ LOD ����
                            int max_lod_count = progressiveMesh.triangles[0];
                            // ���ݵ�ǰ LOD �����������б�
                            int lod = (int)((1.0f - ratio) * max_lod_count);
                            // ����ֵ
                            if (lod > max_lod_count - 1) lod = max_lod_count - 1;
                            // LOD �ı�
                            if (current_lod != lod)
                            {
                                Component[] MeshFilters = (Component[])(gameObject.GetComponentsInChildren(typeof(MeshFilter)));
                                Component[] SkinnedMeshRenderers = (Component[])(gameObject.GetComponentsInChildren(typeof(SkinnedMeshRenderer)));
                                Component[] Components = new Component[MeshFilters.Length + SkinnedMeshRenderers.Length];
                                Array.Copy(MeshFilters, 0, Components, 0, MeshFilters.Length);
                                Array.Copy(SkinnedMeshRenderers, 0, Components, MeshFilters.Length, SkinnedMeshRenderers.Length);
                                int total_triangles_count = MantisLODEditorUtility.SwitchRuntimeLOD(progressiveMesh, mesh_lod_range, lod, Components);
                                // ����ֻ��״̬
                                if (lodHint) lodHint.text = "ϸ�ڲ㼶: " + lod.ToString();
                                if (triangleHint) triangleHint.text = "����������: " + (total_triangles_count / 3).ToString();
                                current_lod = lod;
                            }
                        }
                    }
                    if (fpsHint)
                    {
                        int fps = Mathf.RoundToInt(1.0f / Time.smoothDeltaTime);
                        fpsHint.text = "֡��: " + fps.ToString();
                    }
                    // ���ü�ʱ��
                    currentTimeToInterval = updateInterval + (UnityEngine.Random.value + 0.5f) * currentTimeToInterval;
                }
            }
        }
        private void create_default_mesh_lod_range()
        {
            int max_lod_count = progressiveMesh.triangles[0];
            int mesh_count = progressiveMesh.triangles[1];
            mesh_lod_range = new int[mesh_count * 2];
            for (int i = 0; i < mesh_count; i++)
            {
                mesh_lod_range[i * 2] = 0;
                mesh_lod_range[i * 2 + 1] = max_lod_count - 1;
            }
        }
        private void get_all_meshes()
        {
            // ��鵱ǰ�Ƿ����ڹ�����������ǣ����������
            if (!working)
            {
                // ��ȡ���LOD��ϸ�ڲ�Σ����������� progressiveMesh.triangles[0] �������LOD����Ϣ
                int max_lod_count = progressiveMesh.triangles[0];

                // ��� mesh_lod_range Ϊ�ջ򳤶�Ϊ0���򴴽�Ĭ�ϵ�mesh LOD��Χ
                if (mesh_lod_range == null || mesh_lod_range.Length == 0)
                {
                    create_default_mesh_lod_range();
                }

                // ��ȡ��ǰ��Ϸ�������Ӷ����е�����MeshFilter���
                Component[] MeshFilters = (Component[])(gameObject.GetComponentsInChildren(typeof(MeshFilter)));

                // ��ȡ��ǰ��Ϸ�������Ӷ����е�����SkinnedMeshRenderer���
                Component[] SkinnedMeshRenderers = (Component[])(gameObject.GetComponentsInChildren(typeof(SkinnedMeshRenderer)));

                // ����һ���µ�������飬����ΪMeshFilters��SkinnedMeshRenderers���ܺ�
                Component[] Components = new Component[MeshFilters.Length + SkinnedMeshRenderers.Length];

                // ��MeshFilters��������Ƶ���������
                Array.Copy(MeshFilters, 0, Components, 0, MeshFilters.Length);

                // ��SkinnedMeshRenderers��������Ƶ���������
                Array.Copy(SkinnedMeshRenderers, 0, Components, MeshFilters.Length, SkinnedMeshRenderers.Length);

                // ����MantisLODEditorUtility.GenerateRuntimeLOD������������ʱ��LOD
                MantisLODEditorUtility.GenerateRuntimeLOD(progressiveMesh, Components, optimize_on_the_fly);

                // ��ȡ������Ⱦ�����������MeshRender��SkinnedMeshRenderer
                allBasicRenderers = (Component[])(gameObject.GetComponentsInChildren(typeof(Renderer)));

                // ʹ�����ֵ����ɢ���µ�ʱ�̣���Χ��[0, updateInterval]֮��
                currentTimeToInterval = UnityEngine.Random.value * updateInterval;

                // ��ʼ����ǰLODΪ-1����ʾ��δ����
                current_lod = -1;

                // ���ù���״̬Ϊtrue����ʾ��ʼ����
                working = true;
            }
        }
        public void reset_all_parameters()
        {
            optimize_on_the_fly = true;
            mesh_lod_range = null;
            never_cull = true;
            lod_strategy = 1;
            cull_ratio = 0.1f;
            disappear_distance = 250.0f;
            updateInterval = 0.25f;
        }
        private void clean_all()
        {
            if (working)
            {
                MantisLODEditorUtility.FinishRuntimeLOD(progressiveMesh);
                allBasicRenderers = null;

                working = false;
            }
        }
        void OnEnable()
        {
            Awake();
            Start();
        }
        void OnDisable()
        {
            clean_all();
        }
        void OnDestroy()
        {
            clean_all();
        }
    }
}
