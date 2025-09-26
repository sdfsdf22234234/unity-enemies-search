using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ս����λ��״̬ö�٣����ڱ�ʾһ��ս����λ����Ϸ�еĲ�ͬ״̬��
/// </summary>
public enum CombatUnitState
{

   
    /// <summary>
    /// ��Ч״̬��ͨ�����ڳ�ʼ����δ����״̬ʱ��Ĭ��ֵ��
    /// </summary>
    None = -1,
   
    /// <summary>
    /// ����״̬����ʾս����λû�н����κζ��������ڴ���״̬��
    /// </summary>
    Idle=0,

    /// <summary>
    /// �߶�״̬
    /// </summary>
    Walk=1,
    /// <summary>
    /// �ƶ�״̬����ʾս����λ�����ƶ���Ŀ��λ�á�
    /// </summary>
    Move=2,

    /// <summary>
    /// ����״̬����ʾս����λ�����ܵ��˺����ѱ����С�
    /// </summary>
    Damage = 3,

    /// <summary>
    /// ����״̬��ʾս����λ����ִ�й���������
    /// </summary>
    Attack = 4,

    /// <summary>
    /// ����״̬����ʾս����λ�Ѿ��������޷���������ս����
    /// </summary>
    Dead,

    /// <summary>
    /// ��һ������״̬�����ڱ�ʾ���������״̬�������漰��������������
    /// </summary>
    Dead2
}
