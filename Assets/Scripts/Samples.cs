using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

public class Samples : MonoBehaviour
{
    // Start is called before the first frame update
    BatchRendererComponent brg;
    RVOComponent rvo;

    void Start()
    {
        brg = BatchRendererComponent.Instance;
        rvo = RVOComponent.Instance;

        //海量物体同屏方案接口使用示例
        new PlayerEntity(0, float3.zero).SetMovePosition(Vector3.forward*10);
    }

    // Update is called once per frame
    void Update()
    {

    }
}

public class PlayerEntity
{
    private RVOAgent agent;
    private RendererNodeId rendererId;
    public PlayerEntity(int resId, float3 pos)
    {
        agent = RVOComponent.Instance.AddAgent(pos);
        rendererId = BatchRendererComponent.Instance.AddRenderer(resId, pos, quaternion.identity, Vector3.one);

        agent.rendererIndex = rendererId.Index;
    }

    public void Destroy()
    {
        RVOComponent.Instance.RemoveAgent(agent);
        BatchRendererComponent.Instance.RemoveRenderer(rendererId);
    }
    public void SetMovePosition(Vector3 pos)
    {
        agent.targetPosition = pos;
    }
}

