using Cysharp.Threading.Tasks;
using GPUAnimation.Runtime;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class ECSGraphicsAttachment : MonoBehaviour
{
    private int playerId;

    private int weaponId;

    private void Start()
    {
        var graphicsComponent = ECSGraphicsComponent.Instance;

        if (graphicsComponent == null)
        {
            Debug.LogError("ECSGraphicsComponent.Instance is null. Make sure it exists in the scene.");
            return;
        }

        float3 rightDirection = TransformHelpers.Right(float4x4.identity);
        quaternion rotation = quaternion.Euler(0, Mathf.PI / 2, 0);
    
        playerId = graphicsComponent.Add(
             renderId: 0,
             rightDirection,
             rotation,
             scale: 3.0f,
             parentId: -1);

        weaponId = graphicsComponent.AddAttachment(
            renderId: 1,
            parentEntityId: playerId,
            attachBoneId: 0,
            localpos: new float3(0, 0, 0),
            localrot: quaternion.identity,
            scale: 1.0f);

        graphicsComponent.PlayGPUAnimation(playerId, 4);
    }

    private void Update()
    {
        var graphicsComponent = ECSGraphicsComponent.Instance;

        if (graphicsComponent == null)
        {
            Debug.LogError("ECSGraphicsComponent.Instance is null");
            return;
        }
        NativeQueue<GPUAnimationEventInfo> triggerResults = new NativeQueue<GPUAnimationEventInfo>();
        if (graphicsComponent.GPUAnimationEventsUpdate(out triggerResults))
        {
            GPUAnimationEventInfo eventInfo;
            while (triggerResults.TryDequeue(out eventInfo))
            {

                GPUBoneData boneData = graphicsComponent.GetGPUAnimationBone(playerId, 1);
                float4x4 boneMatrix = float4x4.TRS(boneData.Position, boneData.Rotation, new float3(1, 1, 1));
                float3 rightDirection = TransformHelpers.Right(boneMatrix);


                var item = graphicsComponent.Add(
                     renderId: 2,
                      boneData.Position,
                      boneData.Rotation,
                     scale: 0.5f,
                     parentId: -1);
                UniTask
                    .Delay(500)
                    .ContinueWith(() =>
                    {

                        if (ECSGraphicsComponent.Instance != null)
                        {
                            ECSGraphicsComponent.Instance.Remove(item);
                        }
                    })
                    .Forget();
            }
        }
        triggerResults.Dispose();
    }


    public void AttachBoneIndex(int index)
    {
        var graphicsComponent = ECSGraphicsComponent.Instance;

        if (graphicsComponent == null)
        {
            Debug.LogError("ECSGraphicsComponent.Instance is null");
            return;
        }


        graphicsComponent.SetAttachmentBoneIndex(weaponId, index);
    }

    public void PlayAnimation(int index)
    {
        var graphicsComponent = ECSGraphicsComponent.Instance;

        if (graphicsComponent == null)
        {
            Debug.LogError("ECSGraphicsComponent.Instance is null");
            return;
        }
        graphicsComponent.PlayGPUAnimation(playerId, index);
    }
}
