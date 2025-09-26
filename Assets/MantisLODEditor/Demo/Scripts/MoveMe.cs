using UnityEngine;
using System.Collections;

public class MoveMe : MonoBehaviour
{
    private bool forwarding = false;
    private float speed = 10f; // 每秒移动的单位数

    void Update()
    {
        if (forwarding)
        {
            if (gameObject.transform.position.z > 0.0f)
            {
                forwarding = false;
            }
        }
        else
        {
            if (gameObject.transform.position.z < -30.0f)
            {
                forwarding = true;
            }
        }

        // 使用 Time.deltaTime 使移动速度与帧率无关
        float movement = speed * Time.deltaTime;
        gameObject.transform.Translate(0, 0, forwarding ? movement : (-movement));
    }
}
