using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace GPUAnimation.Editor
{

    /// <summary>
    /// 烘焙器
    /// </summary>
    public class GPUAnimationBaker
    {

        #region FIELDS

        private AnimData? _animData = null;
        private Mesh _bakedMesh;
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<BakedData> _bakedDataList = new List<BakedData>();
        private Vector2Int m_Tex2DArraySize;
        public Vector2Int Tex2DArraySize => m_Tex2DArraySize;
        public float MaxAnimLength { get; private set; }
        public AnimData AnimBakeData => _animData.Value;
        #endregion

        #region METHODS

        public void SetAnimData(GameObject go)
        {
            if (go == null)
            {
                Debug.LogError("go is null!!");
                return;
            }
            var anim = go.GetComponent<Animation>();
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();

            if (anim == null || smr == null)
            {
                Debug.LogError("anim or smr is null!!");
                return;
            }
            _bakedMesh = new Mesh();
            _animData = new AnimData(anim, smr, go.name);
            m_Tex2DArraySize = Vector2Int.zero;
        }

        public List<BakedData> Bake()
        {
            m_Tex2DArraySize = Vector2Int.zero;
            if (_animData == null)
            {
                Debug.LogError("bake data is null!!");
                return _bakedDataList;
            }
            foreach (var t in _animData.Value.AnimationClips)
            {
                if (!t.clip.legacy)
                {
                    Debug.LogError(string.Format($"{t.clip.name} is not legacy!!"));
                    continue;
                }
                if (MaxAnimLength < t.clip.length)
                {
                    MaxAnimLength = t.clip.length;
                }
                var curClipFrame = Mathf.CeilToInt(t.clip.frameRate * t.length);
                if (_animData.Value.MapWidth > m_Tex2DArraySize.x)
                {
                    m_Tex2DArraySize.x = _animData.Value.MapWidth;
                }
                if (curClipFrame > m_Tex2DArraySize.y)
                {
                    m_Tex2DArraySize.y = curClipFrame;
                }
            }

            //每一个动作都生成一个动作图
            foreach (var t in _animData.Value.AnimationClips)
            {
                if (!t.clip.legacy)
                {
                    Debug.LogError(string.Format($"{t.clip.name} is not legacy!!"));
                    continue;
                }
                BakePerAnimClip(t);
            }

            return _bakedDataList;
        }

        private void BakePerAnimClip(AnimationState curAnim)
        {
            var curClipFrame = 0;
            float sampleTime = 0;
            float perFrameTime = 0;
            curClipFrame = Mathf.CeilToInt(curAnim.clip.frameRate * curAnim.length);
            perFrameTime = curAnim.length / curClipFrame;
            var animMap = new Texture2D(m_Tex2DArraySize.x, m_Tex2DArraySize.y, TextureFormat.RGBAHalf, false);
            var animNormalMap = new Texture2D(m_Tex2DArraySize.x, m_Tex2DArraySize.y, TextureFormat.RGBAHalf, false);
            animMap.name = $"{_animData.Value.Name}_{curAnim.name}.animMap";
            animNormalMap.name = $"{_animData.Value.Name}_{curAnim.name}.animNormalMap";
            _animData.Value.AnimationPlay(curAnim.name);

            var meshTransform = _animData.Value.SkinMesh.transform;
            var vertexOffset = meshTransform.position;
            var vertexScale = meshTransform.lossyScale;
            var quaternion = meshTransform.rotation;
            for (var i = 0; i < curClipFrame; i++)
            {
                curAnim.time = sampleTime;
                _animData.Value.SampleAnimAndBakeMesh(ref _bakedMesh, false);
                for (var j = 0; j < _bakedMesh.vertexCount; j++)
                {
                    var vertex = _bakedMesh.vertices[j];
                    var normal = _bakedMesh.normals[j];
                    vertex += vertexOffset;
                    vertex.Scale(vertexScale);
                    vertex = quaternion * vertex;
                    normal = quaternion * normal;
                    animMap.SetPixel(j, i, new Color(vertex.x, vertex.y, vertex.z, 1));
                    animNormalMap.SetPixel(j, i, new Color(normal.x, normal.y, normal.z, 1));
                }

                sampleTime += perFrameTime;
            }
            for (int y = curClipFrame; y < m_Tex2DArraySize.y; y++)
            {
                for (int x = 0; x < _bakedMesh.vertexCount; x++)
                {
                    var repeatColor = animMap.GetPixel(x, y % curClipFrame);
                    var repeatNormalColor = animNormalMap.GetPixel(x, y % curClipFrame);
                    animMap.SetPixel(x, y, repeatColor);
                    animNormalMap.SetPixel(x, y, repeatNormalColor);
                }
            }

            Color col;
            col = animMap.GetPixel(0, 0);
            col.a = curClipFrame;
            animMap.SetPixel(0, 0, col); //clip帧数
            col = animMap.GetPixel(0, 1);
            col.a = curAnim.clip.length;
            animMap.SetPixel(0, 1, col);// clip长度
            col = animMap.GetPixel(0, 2);
            col.a = curAnim.clip.isLooping ? 1 : 0;
            animMap.SetPixel(0, 2, col);// 循环
                                        //int gridWidth = m_Tex2DArraySize.x / 3;
                                        //for (int i = 0; i < gridWidth; i++)
                                        //{
                                        //    for (int j = 0; j < m_Tex2DArraySize.y; j++)
                                        //    {
                                        //        col = animMap.GetPixel(i, j);
                                        //        col.a = curClipFrame / (float)m_Tex2DArraySize.y;
                                        //        animMap.SetPixel(i, j, col);
                                        //    }
                                        //}
                                        //for (int i = 0; i < gridWidth; i++)
                                        //{
                                        //    for (int j = 0; j < m_Tex2DArraySize.y; j++)
                                        //    {
                                        //        col = animMap.GetPixel(m_Tex2DArraySize.x - 1 - i, j);
                                        //        col.a = curAnim.clip.length / MaxAnimLength;

            //        animMap.SetPixel(m_Tex2DArraySize.x - 1 - i, j, col);
            //    }
            //}
            animMap.Apply();
            animNormalMap.Apply();
            _bakedDataList.Add(new BakedData(animMap.name, curAnim.clip.length, animMap, animNormalMap, new Vector2(_animData.Value.MapWidth, curClipFrame)));
        }

        #endregion

    }

    /// <summary>
    /// 保存需要烘焙的动画的相关数据
    /// </summary>
    public struct AnimData
    {
        #region FIELDS

        private int _vertexCount;
        private int _mapWidth;
        private readonly List<AnimationState> _animClips;
        private string _name;

        private Animation _animation;
        private SkinnedMeshRenderer _skin;
        public SkinnedMeshRenderer SkinMesh => _skin;
        public List<AnimationState> AnimationClips => _animClips;
        public int MapWidth => _mapWidth;
        public string Name => _name;

        #endregion

        public AnimData(Animation anim, SkinnedMeshRenderer smr, string goName)
        {
            _vertexCount = smr.sharedMesh.vertexCount;
            _mapWidth = _vertexCount;
            _animClips = new List<AnimationState>(anim.Cast<AnimationState>());
            _animation = anim;
            _skin = smr;
            _name = goName;
        }

        #region METHODS

        public void AnimationPlay(string animName)
        {
            _animation.Play(animName);
        }

        public void SampleAnimAndBakeMesh(ref Mesh m, bool useScale)
        {
            SampleAnim();
            BakeMesh(ref m, useScale);
        }

        private void SampleAnim()
        {
            if (_animation == null)
            {
                Debug.LogError("animation is null!!");
                return;
            }

            _animation.Sample();
        }

        private void BakeMesh(ref Mesh m, bool useScale)
        {
            if (_skin == null)
            {
                Debug.LogError("skin is null!!");
                return;
            }
            _skin.BakeMesh(m, useScale);
        }


        #endregion

    }

    /// <summary>
    /// 烘焙后的数据
    /// </summary>
    public struct BakedData
    {
        #region FIELDS

        private readonly string _name;
        private readonly float _animLen;
        private readonly Texture2D _rawAnimTex;
        private readonly Vector2 _rawAnimTexSize;
        private readonly Texture2D _rawAnimNormalTex;
        private readonly int _animMapWidth;
        private readonly int _animMapHeight;

        #endregion

        public BakedData(string name, float animLen, Texture2D animMap, Texture2D animNormalMap, Vector2 realAnimMapSize)
        {
            _name = name;
            _animLen = animLen;
            _animMapHeight = animMap.height;
            _animMapWidth = animMap.width;
            _rawAnimTex = animMap;
            _rawAnimTexSize = realAnimMapSize;
            _rawAnimNormalTex = animNormalMap;
        }

        public int AnimMapWidth => _animMapWidth;

        public string Name => _name;

        public float AnimLen => _animLen;

        public Texture2D RawAnimTex => _rawAnimTex;
        public Texture2D RawAnimNormalTex => _rawAnimNormalTex;
        public int AnimMapHeight => _animMapHeight;
    }
}