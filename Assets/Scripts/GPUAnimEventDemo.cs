using Cysharp.Threading.Tasks;
using GPUAnimation.Runtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GPUAnimEventDemo : MonoBehaviour
{
    
    void Start()
    {
        if (m_GPUAnim != null )
        {
            if (m_GPUAnim.Events != null)
                m_GPUAnim.Events.AddListener(OnGPUAnimEventCallback);
            m_GPUAnim.PlayAnimation(4);
           
        }



    }

    private void OnDestroy()
    {
        if (m_GPUAnim != null && m_GPUAnim.Events != null)
        {

            m_GPUAnim.Events.RemoveListener(OnGPUAnimEventCallback);
        }

        // 创建一个新的 UnityAction
       // UnityEngine.Events.UnityAction<int, int, string> action = OnGPUAnimEventCallback;

        // 从事件中移除监听器
      
    }
    private void OnGPUAnimEventCallback(int arg0, int arg1, string arg2)
    {

        if (arg2!=null) 
        {
           
            if (m_GPUAnim == null) return;
         

            //var gpuAnimMaterial = m_GPUAnim.GPUAnimMaterial;
            //var attachBoneTransform = GPUAnimationUtility.GetAttachBoneTransform(gpuAnimMaterial, 1);
            //GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            //var transform = m_GPUAnim.transform;
            //if (transform == null) return;
            //Transform primitiveTransform = primitive.transform;
            //primitiveTransform.position = transform.TransformPoint(attachBoneTransform.Position);
            //primitiveTransform.localScale = Vector3.one * 0.2f;
            //UniTask.Delay(1000).ContinueWith(() =>
            //{
            //    Destroy(primitive); 
            //}).Forget();



            var boneData = GPUAnimationUtility.GetAttachBoneTransform(m_GPUAnim.GPUAnimMaterial, 0);//获取右手当前动画帧的位置
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.transform.localScale = Vector3.one * 0.2f;
            ball.transform.position = m_GPUAnim.transform.position + boneData.Position;

            _ = UniTask.Delay(1000).ContinueWith(() =>
            {
                Destroy(ball);

            });









        }





        }




    [SerializeField]
    private GPUAnimation.Runtime.GPUAnimation m_GPUAnim;
}
