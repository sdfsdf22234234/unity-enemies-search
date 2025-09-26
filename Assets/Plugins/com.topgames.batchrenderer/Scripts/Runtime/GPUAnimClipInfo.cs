using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using UnityEngine;

[BurstCompile(CompileSynchronously = true)]
/// <summary>
/// ��ʾGPU������������Ϣ��
/// </summary>
public struct GPUAnimClipInfo
{
    /// <summary>
    /// ������������ʼ֡��
    /// </summary>
    public int StartFrame;

    /// <summary>
    /// ���������Ľ���֡��
    /// </summary>
    public int EndFrame;

    /// <summary>
    /// ������������ʱ��������Ϊ��λ����
    /// </summary>
    public float AnimLength;

    /// <summary>
    /// ���������Ĳ����ٶȡ�
    /// </summary>
    public float AnimSpeed;

    /// <summary>
    /// ָʾ���������Ƿ�ѭ�����š�
    /// </summary>
    public bool IsLoop;
}
