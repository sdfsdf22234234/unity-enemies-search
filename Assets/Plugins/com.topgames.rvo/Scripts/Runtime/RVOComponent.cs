using System;
using System.Collections.Generic;
using Nebukam.Common;
using Nebukam.ORCA;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;



public class RVOComponent : MonoBehaviour
{


    private static int m_EntityId = 0;
    public IAgentProvider AgentProvider { get; private set; }

    public static RVOComponent Instance { get; private set; }
    private void Awake()
    {
        Instance = this;
        this.m_Rvo = new ORCA();
        this.m_Agents = new AgentGroup<RVOAgent>();
        this.m_StaticObstacles = new ObstacleGroup();
        this.m_AgentsDictionary = new Dictionary<int, RVOAgent>();
        this.m_GridSearch = new AgentsGridSearcher(this.m_GridResolution, this.m_GridSize);
    }


    private void Start()
    {
        this.m_Rvo.plane = this.m_Plane;
        this.m_Rvo.agents = this.m_Agents;
        this.m_Rvo.staticObstacles = this.m_StaticObstacles;
        IAgentProvider agentProvider;
        if (this.m_Rvo.TryGetFirst<IAgentProvider>(-1, out agentProvider, true))
        {
            this.AgentProvider = agentProvider;
        }
    }


    private void FixedUpdate()
    {
        this.m_Rvo.Schedule(Time.fixedDeltaTime, null);
        if (this.m_Rvo.TryComplete() && this.m_AutoUpdateEntityTrans)
        {
            RVOAgent agent;
            for (int i = 0; i < this.m_Agents.Count; i++)
            {
                agent = m_Agents[i];
                if (agent.transform == null) continue;
                agent.transform.position = agent.pos;
                agent.transform.rotation = agent.rotation;
            }
        }
    }


    private void OnDestroy()
    {
        this.m_Rvo.DisposeAll();
        this.m_Agents.Clear(false);
        this.m_StaticObstacles.Clear(false);
        this.m_AgentsDictionary.Clear();
    }


    public bool TryGetAgentData(out NativeArray<AgentData> agentData)
    {
        agentData = default(NativeArray<AgentData>);
        if (this.AgentProvider.locked || this.AgentProvider.outputAgents.Length == 0)
        {
            return false;
        }
        agentData = this.AgentProvider.outputAgents;
        return true;
    }


    public RVOAgent AddAgent(Transform bindEntity, float radius, float radiusObst, float maxSpeed, int maxNeighbors, float neighborDist, float timeHorizon, float timeHorizonObst, float weight = 0.5f, bool navigationEnable = true, bool collisionEnable = true, ORCALayer layerOccupation = ORCALayer.ANY, ORCALayer layerIgnore = ORCALayer.NONE)
    {
        if (bindEntity == null )
        {
           
            return null;
        }
        Vector3 position = bindEntity.position;
        if (this.m_Plane == AxisPair.XZ)
        {
            position.y = 0f;
        }
        else
        {
            position.z = 0f;
        }
        RVOAgent rvoagent = this.m_Agents.Add(position);
        rvoagent.BindTransform(++m_EntityId, bindEntity);
        rvoagent.radius = radius;
        rvoagent.radiusObst = radiusObst;
        rvoagent.maxSpeed = maxSpeed;
        rvoagent.maxNeighbors = maxNeighbors;
        rvoagent.neighborDist = neighborDist;
        rvoagent.timeHorizon = timeHorizon;
        rvoagent.timeHorizonObst = timeHorizonObst;
        rvoagent.navigationEnabled = navigationEnable;
        rvoagent.collisionEnabled = collisionEnable;
        rvoagent.layerOccupation = layerOccupation;
        rvoagent.layerIgnore = layerIgnore;
        rvoagent.avoidWeight = weight;
        rvoagent.searchCount = 1;
        rvoagent.searchRadius = rvoagent.radius + 1f;
        this.m_AgentsDictionary.Add(rvoagent.Id, rvoagent);
        return rvoagent;
    }


    public RVOAgent AddAgent(Transform bindEntity)
    {
        return this.AddAgent(bindEntity, this.m_DefaultAgent.radius, this.m_DefaultAgent.radiusObst, this.m_DefaultAgent.maxSpeed, this.m_DefaultAgent.maxNeighbors, this.m_DefaultAgent.neighborDist, this.m_DefaultAgent.timeHorizon, this.m_DefaultAgent.timeHorizonObst, this.m_DefaultAgent.avoidWeight, this.m_DefaultAgent.navigationEnabled, this.m_DefaultAgent.collisionEnabled, this.m_DefaultAgent.layerOccupation, this.m_DefaultAgent.layerIgnore);
    }


    public RVOAgent AddAgent(int id, float3 pos)
    {
        float3 v = pos;
        if (this.m_Plane == AxisPair.XZ)
        {
            v.y = 0f;
        }
        else
        {
            v.z = 0f;
        }
        RVOAgent rvoagent = this.m_Agents.Add(v);
        Transform noTransform = null;
        rvoagent.BindTransform(id, noTransform);
        rvoagent.radius = this.m_DefaultAgent.radius;
        rvoagent.radiusObst = this.m_DefaultAgent.radiusObst;
        rvoagent.maxSpeed = this.m_DefaultAgent.maxSpeed;
        rvoagent.maxNeighbors = this.m_DefaultAgent.maxNeighbors;
        rvoagent.neighborDist = this.m_DefaultAgent.neighborDist;
        rvoagent.timeHorizon = this.m_DefaultAgent.timeHorizon;
        rvoagent.timeHorizonObst = this.m_DefaultAgent.timeHorizonObst;
        rvoagent.navigationEnabled = this.m_DefaultAgent.navigationEnabled;
        rvoagent.collisionEnabled = this.m_DefaultAgent.collisionEnabled;
        rvoagent.layerOccupation = this.m_DefaultAgent.layerOccupation;
        rvoagent.layerIgnore = this.m_DefaultAgent.layerIgnore;
        rvoagent.searchCount = 1;
        rvoagent.searchRadius = rvoagent.radius + 1f;
        rvoagent.avoidWeight = this.m_DefaultAgent.avoidWeight;
        this.m_AgentsDictionary.Add(rvoagent.Id, rvoagent);
        return rvoagent;
    }
    public RVOAgent AddAgent(float3 pos)
    {
        float3 v = pos;
        if (this.m_Plane == AxisPair.XZ)
        {
            v.y = 0f;
        }
        else
        {
            v.z = 0f;
        }
        RVOAgent rvoagent = this.m_Agents.Add(v);
        Transform noTransform = null;
        rvoagent.BindTransform(++m_EntityId, noTransform);
        rvoagent.radius = this.m_DefaultAgent.radius;
        rvoagent.radiusObst = this.m_DefaultAgent.radiusObst;
        rvoagent.maxSpeed = this.m_DefaultAgent.maxSpeed;
        rvoagent.maxNeighbors = this.m_DefaultAgent.maxNeighbors;
        rvoagent.neighborDist = this.m_DefaultAgent.neighborDist;
        rvoagent.timeHorizon = this.m_DefaultAgent.timeHorizon;
        rvoagent.timeHorizonObst = this.m_DefaultAgent.timeHorizonObst;
        rvoagent.navigationEnabled = this.m_DefaultAgent.navigationEnabled;
        rvoagent.collisionEnabled = this.m_DefaultAgent.collisionEnabled;
        rvoagent.layerOccupation = this.m_DefaultAgent.layerOccupation;
        rvoagent.layerIgnore = this.m_DefaultAgent.layerIgnore;
        rvoagent.searchCount = 1;
        rvoagent.searchRadius = rvoagent.radius + 1f;
        rvoagent.avoidWeight = this.m_DefaultAgent.avoidWeight;
        this.m_AgentsDictionary.Add(rvoagent.Id, rvoagent);
        return rvoagent;
    }

    public Obstacle AddObstacle(IList<float3> obstaclePoints, bool inverse = false)
    {
        Obstacle obstacle = this.m_StaticObstacles.Add(obstaclePoints, inverse, 10f);
        obstacle.Init();
        return obstacle;
    }


    public void RemoveObstacle(Obstacle obstacle)
    {
        this.m_StaticObstacles.Remove(obstacle);
    }


    public void ClearAll()
    {
        this.m_Agents.Clear(false);
        this.m_StaticObstacles.Clear(false);
        this.m_GridSearch.Dispose();
    }


    public RVOAgent GetAgent(int agentId)
    {
        RVOAgent result;
        if (this.m_AgentsDictionary.TryGetValue(agentId, out result))
        {
            return result;
        }
        return null;
    }


    public void RemoveAgent(RVOAgent agent)
    {
        if (agent == null)
        {
            return;
        }
        this.m_AgentsDictionary.Remove(agent.Id);
        this.m_Agents.Remove(agent, true);
    }


    public NativeArray<int> SearchAgentsNearest(NativeArray<AgentData> agents)
    {
        this.TryBuildGrid();
        return this.m_GridSearch.SearchAgentsNearest(agents);
    }


    public NativeArray<int2> SearchAgentsNearestAndCheck(NativeArray<AgentData> agents)
    {
        this.TryBuildGrid();
        return this.m_GridSearch.SearchAgentsNearestRadiusCheck(agents);
    }


    public NativeArray<int> SearchAgentsWithin(NativeArray<float3> queryPoints, NativeArray<float> queryRadiusArr, NativeArray<int> queryCountArr, NativeArray<ORCALayer> selfCamps, NativeArray<ORCALayer> ignoreCampLayers)
    {
        this.TryBuildGrid();
        return this.m_GridSearch.SearchAgentsWithin(queryPoints, queryRadiusArr, queryCountArr, selfCamps, ignoreCampLayers);
    }


    public NativeArray<int> SearchAgentsWithin(NativeArray<float3> queryPoints, float searchRadius, int searchCount = 1, ORCALayer selfCamp = ORCALayer.NONE, ORCALayer ignoreCampLayer = ORCALayer.NONE)
    {
        this.TryBuildGrid();
        return this.m_GridSearch.SearchAgentsWithin(queryPoints, searchRadius, searchCount, selfCamp, ignoreCampLayer);
    }


    public NativeArray<int> SearchAgentsWithin(NativeArray<AgentData> agents, int searchCount)
    {
        this.TryBuildGrid();
        return this.m_GridSearch.SearchAgentsWithin(agents, searchCount);
    }


    public NativeArray<int> SearchAgentsWithin(NativeArray<AgentData> agents)
    {
        this.TryBuildGrid();
        return this.m_GridSearch.SearchAgentsWithin(agents);
    }


    public NativeArray<int> SearchAgentsNearestWithin(NativeArray<AgentData> agents)
    {
        this.TryBuildGrid();
        return this.m_GridSearch.SearchAgentsNearestWithin(agents);
    }


    public NativeArray<int> SearchAgentsNearestWithin(NativeArray<float3> queryPoints, float searchRadius, ORCALayer selfCamp = ORCALayer.NONE, ORCALayer ignoreCampLayer = ORCALayer.NONE)
    {
        this.TryBuildGrid();
        return this.m_GridSearch.SearchAgentsNearestWithin(queryPoints, searchRadius, selfCamp, ignoreCampLayer);
    }

    public void SubscribeAgentDataApplyEvent(UnityAction<NativeArray<AgentData>> action)
    {
       // m_AgentDataApplyEvent = action;
    }

    private void TryBuildGrid()
    {
        float fixedTime = Time.fixedTime;
        NativeArray<AgentData> agents;
        if (fixedTime > this.m_LastBuildGridTime && this.TryGetAgentData(out agents))
        {
            this.m_GridSearch.BuildGrid(agents);
            this.m_LastBuildGridTime = fixedTime + this.m_BuildGridTimeInterval;
        }
    }


    [SerializeField]
    private AxisPair m_Plane = AxisPair.XZ;


    [SerializeField]
    private bool m_AutoUpdateEntityTrans = true;


    [SerializeField]
    private AgentData m_DefaultAgent = new AgentData
    {
        height = 0.5f,
        radius = 0.5f,
        radiusObst = 0.5f,
        maxSpeed = 10f,
        maxNeighbors = 10,
        neighborDist = 10f,
        timeHorizon = 5f,
        timeHorizonObst = 5f,
        avoidWeight = 0.5f,
        layerOccupation = ORCALayer.ANY,
        layerIgnore = ORCALayer.NONE,
        navigationEnabled = true,
        collisionEnabled = true
    };


    [Header("Grid Search:")]
    [SerializeField]
    private float m_GridResolution = 5f;


    [Range(32f, 256f)]
    [SerializeField]
    private int m_GridSize = 32;


    [SerializeField]
    [Range(0f, 1f)]
    private float m_BuildGridTimeInterval = 0.02f;


    private float m_LastBuildGridTime = -1f;


    private AgentsGridSearcher m_GridSearch;


    private AgentGroup<RVOAgent> m_Agents;


    private ObstacleGroup m_StaticObstacles;


    private ORCA m_Rvo;


    private Dictionary<int, RVOAgent> m_AgentsDictionary;
}
