using UnityEngine;
using System.Collections;

public class MoveMe : MonoBehaviour
{
    private bool forwarding = false;
    private float speed = 10f; // ÿ���ƶ��ĵ�λ��

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

        // ʹ�� Time.deltaTime ʹ�ƶ��ٶ���֡���޹�
        float movement = speed * Time.deltaTime;
        gameObject.transform.Translate(0, 0, forwarding ? movement : (-movement));
    }
}
