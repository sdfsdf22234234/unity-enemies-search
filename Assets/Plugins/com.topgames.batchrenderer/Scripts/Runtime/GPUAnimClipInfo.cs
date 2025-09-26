using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using UnityEngine;

[BurstCompile(CompileSynchronously = true)]
/// <summary>
/// 表示GPU动画剪辑的信息。
/// </summary>
public struct GPUAnimClipInfo
{
    /// <summary>
    /// 动画剪辑的起始帧。
    /// </summary>
    public int StartFrame;

    /// <summary>
    /// 动画剪辑的结束帧。
    /// </summary>
    public int EndFrame;

    /// <summary>
    /// 动画剪辑的总时长（以秒为单位）。
    /// </summary>
    public float AnimLength;

    /// <summary>
    /// 动画剪辑的播放速度。
    /// </summary>
    public float AnimSpeed;

    /// <summary>
    /// 指示动画剪辑是否循环播放。
    /// </summary>
    public bool IsLoop;
}
