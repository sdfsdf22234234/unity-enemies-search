using System;
 
using Unity.Burst;
using Unity.Mathematics;

 
[BurstCompile(CompileSynchronously = true)]
public struct GPUAnimTriggerInfo
{
    /// <summary>
    /// �����Ψһ��ʶ����
    /// ����ʶ����˶���������Ĵ���
    /// </summary>
    public int AgentId;

    public int3 TriggerKey;
    ///// <summary>
    ///// ��Դ������
    ///// ָ����˶�����ص���Դ��������
    ///// </summary>
    //public int ResIndex;

    ///// <summary>
    ///// ����������
    ///// ��ʾ��ǰ���������ڶ����б��е�����λ�á�
    ///// </summary>
    //public int ClipIndex;

    ///// <summary>
    ///// ����֡��
    ///// ָ���ڶ������ĸ�֡�������¼���
    ///// </summary>
    //public int TriggerAtFrame;
}
