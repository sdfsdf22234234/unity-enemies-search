using Nebukam.ORCA;
using UnityEngine;

public class RVOAgent : Agent
{
    public Transform transform { get; private set; }

    /// <summary>
    /// 强制停止移动
    /// </summary>
    public bool StopMoving
    {
        get => !navigationEnabled;
        set
        {
            navigationEnabled = !value;
        }
    }
    /// <summary>
    /// 获取当前RVO Agent移动速度
    /// </summary>
    public float MoveSpeed
    {
        get
        {
            return Unity.Mathematics.math.length(this.velocity);
        }
    }
    /// <summary>
    /// 获取当前归一化的移动速度(相较于最大移速)
    /// </summary>
    public float MoveSpeedNormalized
    {
        get
        {
            return MoveSpeed / this.maxSpeed;
        }
    }
    /// <summary>
    /// 判断当前是否正在移动
    /// </summary>
    public bool IsMoving
    {
        get
        {
            return Unity.Mathematics.math.lengthsq(this.velocity) > 0.01f;
        }
    }
    /// <summary>
    /// Dont call this function
    /// </summary>
    /// <param name="id"></param>
    public void BindTransform(int id, Transform entity)
    {
        this.Id = id;
        this.transform = entity;
    }
    public void SetAnimationBlink()
    {
        userdataVec4.x = Time.time;
    }
    public void SetAnimationBlink(float value)
    {
        userdataVec4.x = value;
    }

    public void PlayGPUAnimation(int clipIndx)
    {
        clipId.z = clipId.x;  
        clipId.w = clipId.y;  
        clipId.x = clipIndx; 
        clipId.y = Time.time + 0.2f; 
    }
}
