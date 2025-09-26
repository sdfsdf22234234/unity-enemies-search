// Copyright (c) 2021 Timothé Lapetite - nebukam@gmail.com
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Unity.Mathematics;
using static Unity.Mathematics.math;
using Nebukam.Common;

namespace Nebukam.ORCA
{

    public interface IAgent : IVertex
    {

        /// <summary>
        /// 代理的期望速度
        /// </summary>
        float3 prefVelocity { get; set; }
        /// <summary>
        /// 实际速度
        /// </summary>
        float3 velocity { get; set; }
        /// <summary>
        /// 代理的高度
        /// </summary>
        float height { get; set; }
        /// <summary>
        /// 解决代理-代理碰撞时的半径
        /// </summary>
        float radius { get; set; }
        /// <summary>
        ///  解决代理-障碍物碰撞时的半径
        /// </summary>
        float radiusObst { get; set; }
        /// <summary>
        /// 最大允许速度
        /// </summary>
        float maxSpeed { get; set; }

        /// <summary>
        /// 在仿真中考虑的最大邻居数量
        /// </summary>
        int maxNeighbors { get; set; }
        /// <summary>
        /// 考虑避开其他代理的最大距离
        /// </summary>
        float neighborDist { get; set; }

        [System.Obsolete("neighborElevation is ignored by the simulation")]
        float neighborElevation { get; set; }

        /// <summary>
        /// 调节与其他代理距离检查的时间视野
        /// </summary>
        float timeHorizon { get; set; }
        /// <summary>
        /// 调节与障碍物距离检查的时间视野
        /// </summary>
        float timeHorizonObst { get; set; }

        /// <summary>
        ///  代理所占据的层级
        /// </summary>
        ORCALayer layerOccupation { get; set; }
        /// <summary>
        /// 在解决仿真中被忽略的层级
        /// </summary>
        ORCALayer layerIgnore { get; set; }
        /// <summary>
        /// 是否允许仿真控制代理的导航
        /// </summary>
        bool navigationEnabled { get; set; }
        /// <summary>
        /// 是否允许代理的碰撞检测
        /// </summary>
        bool collisionEnabled { get; set; }
    }

    public class Agent : Vertex, IAgent
    {

        /// 
        /// Fields
        /// 

        protected internal float3 m_prefVelocity = float3(0f);
        protected internal float3 m_velocity { get; set; } = float3(0f);

        protected internal float m_height = 0.5f;
        protected internal float m_radius = 0.5f;
        protected internal float m_radiusObst = 0.5f;
        protected internal float m_maxSpeed = 20.0f;

        protected internal int m_maxNeighbors = 15;
        protected internal float m_neighborDist = 20.0f;
        protected internal float m_neighborElev = 0.5f;

        protected internal float m_timeHorizon = 15.0f;
        protected internal float m_timeHorizonObst = 1.2f;
        protected internal float m_avoidWeight = 0.5f;

        protected internal ORCALayer m_layerOccupation = ORCALayer.ANY;
        protected internal ORCALayer m_layerIgnore = ORCALayer.NONE;
        protected internal bool m_navigationEnabled = true;
        protected internal bool m_collisionEnabled = true;
        protected internal bool m_arrived;

        /// 
        /// Properties
        /// 
        public int Id { get; protected set; }
        public float4 clipId;
       // public int catalog;
        public ORCALayer camp;
        public float4 userdataVec4;
        /// <summary>
        /// 索敌半径
        /// </summary>
        public float searchRadius;
        /// <summary>
        /// 索敌数量,1单体攻击, >1群体攻击
        /// </summary>
        public int searchCount;
        public int rendererIndex;
        public float3 targetPosition;
        public quaternion rotation;

        /// <summary>
        /// 避让权重,值越小,越不避让
        /// </summary>
        public float avoidWeight
        {
            get => m_avoidWeight;
            set
            {
                m_avoidWeight = Unity.Mathematics.math.max(0.001f, value);
            }
        }

        /// <summary>
        /// 代理的期望速度，即理想的移动方向和速度。
        /// </summary>
        public float3 prefVelocity
        {
            get { return m_prefVelocity; }
            set { m_prefVelocity = value; }
        }
        /// <summary>
        /// 代理的实际速度，用于仿真中实际移动和碰撞检测。
        /// </summary>
        public float3 velocity
        {
            get { return m_velocity; }
            set { m_velocity = value; }
        }

        /// <summary>
        /// Height of the agent.
        /// </summary>
        public float height
        {
            get { return m_height; }
            set { m_height = value; }
        }
        /// <summary>
        /// Radius of the agent when resolving agent-agent collisions.
        /// </summary>
        public float radius
        {
            get { return m_radius; }
            set { m_radius = value; }
        }
        /// <summary>
        /// Radius of the agent when resolving agent-obstacle collisions.
        /// </summary>
        public float radiusObst
        {
            get { return m_radiusObst; }
            set { m_radiusObst = value; }
        }
        /// <summary>
        /// Maximum allowed speed of the agent.
        /// This is used to avoid deadlock situation where a slight
        /// boost in velocity could help solve more complex scenarios.
        /// </summary>
        public float maxSpeed
        {
            get { return m_maxSpeed; }
            set { m_maxSpeed = value; }
        }

        /// <summary>
        /// Maxmimum number of neighbors this agent accounts for in the simulation
        /// </summary>
        public int maxNeighbors
        {
            get { return m_maxNeighbors; }
            set { m_maxNeighbors = value; }
        }
        /// <summary>
        /// Maximum distance at which this agent consider avoiding other agents
        /// </summary>
        public float neighborDist
        {
            get { return m_neighborDist; }
            set { m_neighborDist = value; }
        }
        [System.Obsolete("neighborElevation is ignored by the simulation")]
        public float neighborElevation
        {
            get { return m_neighborElev; }
            set { m_neighborElev = value; }
        }
        /// <summary>
        /// Used to modulate distance checks toward other Agents within the simulation.
        /// </summary>
        public float timeHorizon
        {
            get { return m_timeHorizon; }
            set { m_timeHorizon = value; }
        }
        /// <summary>
        /// Used to modulate distance checks toward Obstacles within the simulation.
        /// </summary>
        public float timeHorizonObst
        {
            get { return m_timeHorizonObst; }
            set { m_timeHorizonObst = value; }
        }

        /// <summary>
        /// Layers on which this agent is physically present, and thus will affect
        /// other agents navigation.
        /// </summary>
        public ORCALayer layerOccupation
        {
            get { return m_layerOccupation; }
            set { m_layerOccupation = value; }
        }
        /// <summary>
        /// Ignored layers while resolving the simulation.
        /// </summary>
        public ORCALayer layerIgnore
        {
            get { return m_layerIgnore; }
            set { m_layerIgnore = value; }
        }
        /// <summary>
        /// 是否该代理的导航由仿真控制。
        /// 此属性优先于层级。
        /// </summary>
        public bool navigationEnabled
        {
            get { return m_navigationEnabled; }
            set { m_navigationEnabled = value; }
        }
        /// <summary>
        /// Whether this agent's collision is enabled.
        /// This property has precedence over layers.
        /// </summary>
        public bool collisionEnabled
        {
            get { return m_collisionEnabled; }
            set { m_collisionEnabled = value; }
        }
        /// <summary>
        /// 获取或设置一个值，指示是否到达。
        /// </summary>
        public bool arrived
        {
            get
            {
                return m_arrived;
            }
           
            internal set
            {
                m_arrived = value;
            }
        }

        public bool stopMoveSelf;
        public ORCALayer searchTag;
        public ORCALayer ignoreSearchTag;
        public Agent()
        {
            m_height = 0.5f;            // 代理高度
            m_radius = 0.5f;            // 代理半径
            m_radiusObst = 0.5f;        // 障碍物检测半径

            // 速度相关
            m_prefVelocity = new float3();             // 期望速度
            m_velocity = new float3();               // 当前速度
            m_maxSpeed = 20.0f;                        // 最大速度

            // 邻居检测参数
            m_maxNeighbors = 15;                       // 最大邻居数量
            m_neighborDist = 20.0f;                    // 邻居检测距离
            m_neighborElev = 0.5f;                     // 邻居高度差阈值

            // 时间参数
            m_timeHorizon = 15.0f;                     // 时间视野（用于预测碰撞）
            m_timeHorizonObst = 1.2f;                 // 障碍物时间视野

            // 避障参数
            m_avoidWeight = 0.5f;                      // 避障权重
            m_layerOccupation = ORCALayer.NONE;           // 层级占用掩码

            // 导航控制
            m_navigationEnabled = true;                // 是否启用导航
            stopMoveSelf = true;                      // 是否允许自行停止移动
            searchTag = ORCALayer.L0;                            // 搜索标签
        }
    }

}
