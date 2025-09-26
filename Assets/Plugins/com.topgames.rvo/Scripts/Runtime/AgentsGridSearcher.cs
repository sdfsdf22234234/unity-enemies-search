using System;
using System.Collections.Generic;
using Nebukam.JobAssist;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Nebukam.ORCA
{

    public class AgentsGridSearcher : IDisposable
    {



        public int AgentsCount { get; private set; }


        public AgentsGridSearcher(float resolution, int targetGrid = 32)
        {
            if (resolution <= 0f && targetGrid > 0)
            {
                this.targetGridSize = targetGrid;
                return;
            }
            if (resolution <= 0f && targetGrid <= 0)
            {
                throw new Exception("Wrong target grid size. Choose a resolution > 0 or a target grid > 0");
            }
            this.gridReso = resolution;
        }


        public void BuildGrid(NativeArray<AgentData> agents)
        {
            if (this.m_Agents.Length >= agents.Length)
            {
                this.RebuildAgents(agents);
                return;
            }
            this.AgentsCount = agents.Length;
            this.InternalBuildGrid(agents);
        }


        public void RebuildAgents(NativeArray<AgentData> newPos)
        {
            this.AgentsCount = newPos.Length;
            new CopyNativeArrayJob<AgentData>
            {
                ArrSrc = newPos,
                ArrDst = this.m_Agents
            }.Schedule(this.AgentsCount, 64, default(JobHandle)).Complete();
            if (this.AgentsCount <= 1)
            {
                return;
            }
            this.getMinMaxCoords(this.m_Agents, ref this.minValue, ref this.maxValue);
            float3 @float = this.maxValue - this.minValue;
            int num = (int)math.ceil(math.max(@float.x, math.max(@float.y, @float.z)) / this.gridReso);
            this.gridDim = new int3(num, num, num);
            if (num > 256)
            {
                throw new Exception("Grid is to large, adjust the resolution");
            }
            this.cellStartEndCount = this.gridDim.x * this.gridDim.y * this.gridDim.z;
            Extensions.EnsureMinLength<int2>(ref this.cellStartEnd, this.cellStartEndCount, 256, Allocator.Persistent);
            new AgentsGridSearcher.AssignHashJob
            {
                oriGrid = this.minValue,
                invresoGrid = 1f / this.gridReso,
                gridDim = this.gridDim,
                pos = this.m_Agents,
                nbcells = this.cellStartEndCount,
                hashIndex = this.hashIndex
            }.Schedule(this.AgentsCount, 128, default(JobHandle)).Complete();
            NativeArray<AgentsGridSearcher.SortEntry> nativeArray = new NativeArray<AgentsGridSearcher.SortEntry>(this.AgentsCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            new AgentsGridSearcher.PopulateEntryJob
            {
                hashIndex = this.hashIndex,
                entries = nativeArray
            }.Schedule(this.AgentsCount, 128, default(JobHandle)).Complete();
            JobHandle parentHandle = default(JobHandle);
            MultithreadedSort.Sort<AgentsGridSearcher.SortEntry>(nativeArray, parentHandle).Complete();
            parentHandle.Complete();
            new AgentsGridSearcher.DePopulateEntryJob
            {
                hashIndex = this.hashIndex,
                entries = nativeArray
            }.Schedule(this.AgentsCount, 128, default(JobHandle)).Complete();
            nativeArray.Dispose();
            new AgentsGridSearcher.MemsetCellStartJob
            {
                cellStartEnd = this.cellStartEnd
            }.Schedule(this.cellStartEndCount, 256, default(JobHandle)).Complete();
            new AgentsGridSearcher.SortCellJob
            {
                pos = this.m_Agents,
                hashIndex = this.hashIndex,
                cellStartEnd = this.cellStartEnd,
                sortedPos = this.m_SortedAgents,
                agentsCount = this.AgentsCount
            }.Schedule(default(JobHandle)).Complete();
        }


        private void InternalBuildGrid(NativeArray<AgentData> agents)
        {
            Extensions.EnsureMinLength<AgentData>(ref this.m_Agents, this.AgentsCount, 256, Allocator.Persistent);
            new CopyNativeArrayJob<AgentData>
            {
                ArrDst = this.m_Agents,
                ArrSrc = agents
            }.Schedule(this.AgentsCount, 64, default(JobHandle)).Complete();
            this.getMinMaxCoords(this.m_Agents, ref this.minValue, ref this.maxValue);
            float3 @float = this.maxValue - this.minValue;
            float num = math.max(@float.x, math.max(@float.y, @float.z));
            if (this.gridReso <= 0f)
            {
                this.gridReso = num / (float)this.targetGridSize;
            }
            int num2 = math.max(1, (int)math.ceil(num / this.gridReso));
            this.gridDim = new int3(num2, num2, num2);
            if (num2 > 256)
            {
                throw new Exception("Grid is to large, adjust the resolution");
            }
            this.cellStartEndCount = this.gridDim.x * this.gridDim.y * this.gridDim.z;
            Extensions.EnsureMinLength<int2>(ref this.hashIndex, this.AgentsCount, 256, Allocator.Persistent);
            Extensions.EnsureMinLength<AgentData>(ref this.m_SortedAgents, this.AgentsCount, 256, Allocator.Persistent);
            Extensions.EnsureMinLength<int2>(ref this.cellStartEnd, this.cellStartEndCount, 256, Allocator.Persistent);
            new AgentsGridSearcher.AssignHashJob
            {
                oriGrid = this.minValue,
                invresoGrid = 1f / this.gridReso,
                gridDim = this.gridDim,
                pos = this.m_Agents,
                nbcells = this.cellStartEndCount,
                hashIndex = this.hashIndex
            }.Schedule(this.AgentsCount, 128, default(JobHandle)).Complete();
            NativeArray<AgentsGridSearcher.SortEntry> nativeArray = new NativeArray<AgentsGridSearcher.SortEntry>(this.AgentsCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            new AgentsGridSearcher.PopulateEntryJob
            {
                hashIndex = this.hashIndex,
                entries = nativeArray
            }.Schedule(this.AgentsCount, 128, default(JobHandle)).Complete();
            JobHandle parentHandle = default(JobHandle);
            MultithreadedSort.Sort<AgentsGridSearcher.SortEntry>(nativeArray, parentHandle).Complete();
            parentHandle.Complete();
            new AgentsGridSearcher.DePopulateEntryJob
            {
                hashIndex = this.hashIndex,
                entries = nativeArray
            }.Schedule(this.AgentsCount, 128, default(JobHandle)).Complete();
            nativeArray.Dispose();
            new AgentsGridSearcher.MemsetCellStartJob
            {
                cellStartEnd = this.cellStartEnd
            }.Schedule(this.cellStartEndCount, 256, default(JobHandle)).Complete();
            new AgentsGridSearcher.SortCellJob
            {
                pos = this.m_Agents,
                hashIndex = this.hashIndex,
                cellStartEnd = this.cellStartEnd,
                sortedPos = this.m_SortedAgents,
                agentsCount = this.AgentsCount
            }.Schedule(default(JobHandle)).Complete();
        }


        public void Dispose()
        {
            if (this.m_Agents.IsCreated)
            {
                this.m_Agents.Dispose();
            }
            if (this.hashIndex.IsCreated)
            {
                this.hashIndex.Dispose();
            }
            if (this.cellStartEnd.IsCreated)
            {
                this.cellStartEnd.Dispose();
            }
            if (this.m_SortedAgents.IsCreated)
            {
                this.m_SortedAgents.Dispose();
            }
        }


        public NativeArray<int> SearchAgentsNearest(NativeArray<float3> queryPoints, ORCALayer selfCamp, ORCALayer ignoreTag)
        {
            if (this.AgentsCount == 0)
            {
                return default(NativeArray<int>);
            }
            NativeArray<int> nativeArray = new NativeArray<int>(queryPoints.Length, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            if (this.AgentsCount == 1)
            {
                this.SearchNearestCountError(queryPoints, selfCamp, ignoreTag, ref nativeArray);
            }
            else
            {
                new AgentsGridSearcher.ClosestAgentsPositionJob
                {
                    agents = this.m_Agents,
                    oriGrid = this.minValue,
                    invresoGrid = 1f / this.gridReso,
                    gridDim = this.gridDim,
                    queryPos = queryPoints,
                    sortedPos = this.m_SortedAgents,
                    hashIndex = this.hashIndex,
                    cellStartEnd = this.cellStartEnd,
                    results = nativeArray,
                    selfCamp = selfCamp,
                    ignoreTag = ignoreTag,
                    agentsCount = this.AgentsCount
                }.Schedule(queryPoints.Length, 64, default(JobHandle)).Complete();
            }
            return nativeArray;
        }


        public NativeArray<int> SearchAgentsNearest(NativeArray<AgentData> qPoints)
        {
            if (this.AgentsCount == 0)
            {
                return default(NativeArray<int>);
            }
            NativeArray<int> nativeArray = new NativeArray<int>(qPoints.Length, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            if (this.AgentsCount == 1)
            {
                this.SearchNearestCountError(qPoints, ref nativeArray);
            }
            else
            {
                new AgentsGridSearcher.ClosestAgentsJob
                {
                    agents = this.m_Agents,
                    oriGrid = this.minValue,
                    invresoGrid = 1f / this.gridReso,
                    gridDim = this.gridDim,
                    queryPos = qPoints,
                    sortedPos = this.m_SortedAgents,
                    hashIndex = this.hashIndex,
                    cellStartEnd = this.cellStartEnd,
                    results = nativeArray,
                    agentsCount = this.AgentsCount
                }.Schedule(qPoints.Length, 64, default(JobHandle)).Complete();
            }
            return nativeArray;
        }


        public NativeArray<int2> SearchAgentsNearestRadiusCheck(NativeArray<AgentData> qPoints)
        {
            if (this.AgentsCount == 0)
            {
                return default(NativeArray<int2>);
            }
            NativeArray<int2> nativeArray = new NativeArray<int2>(qPoints.Length, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            if (this.AgentsCount == 1)
            {
                this.SearchNearestCountErrorRadiusCheck(qPoints, ref nativeArray);
            }
            else
            {
                new AgentsGridSearcher.ClosestAgentsRadiusCheckJob
                {
                    agents = this.m_Agents,
                    oriGrid = this.minValue,
                    invresoGrid = 1f / this.gridReso,
                    gridDim = this.gridDim,
                    queryPos = qPoints,
                    sortedPos = this.m_SortedAgents,
                    hashIndex = this.hashIndex,
                    cellStartEnd = this.cellStartEnd,
                    results = nativeArray,
                    agentsCount = this.AgentsCount
                }.Schedule(qPoints.Length, 64, default(JobHandle)).Complete();
            }
            return nativeArray;
        }


        public NativeArray<int> SearchAgentsNearestWithin(NativeArray<AgentData> qPoints)
        {
            if (this.AgentsCount == 0)
            {
                return default(NativeArray<int>);
            }
            NativeArray<int> nativeArray = new NativeArray<int>(qPoints.Length, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            if (this.AgentsCount == 1)
            {
                this.SearchNearestWithinCountError(qPoints, ref nativeArray);
            }
            else
            {
                new AgentsGridSearcher.ClosestWithinAgentsJob
                {
                    agents = this.m_Agents,
                    oriGrid = this.minValue,
                    invresoGrid = 1f / this.gridReso,
                    gridDim = this.gridDim,
                    queryPos = qPoints,
                    sortedPos = this.m_SortedAgents,
                    hashIndex = this.hashIndex,
                    cellStartEnd = this.cellStartEnd,
                    results = nativeArray,
                    agentsCount = this.AgentsCount
                }.Schedule(qPoints.Length, 64, default(JobHandle)).Complete();
            }
            return nativeArray;
        }


        public NativeArray<int> SearchAgentsNearestWithin(NativeArray<AgentData> qPoints, float searchRadius)
        {
            if (this.AgentsCount == 0)
            {
                return default(NativeArray<int>);
            }
            NativeArray<int> nativeArray = new NativeArray<int>(qPoints.Length, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            if (this.AgentsCount == 1)
            {
                this.SearchNearestWithinUniRadiusCountError(qPoints, searchRadius, ref nativeArray);
            }
            else
            {
                new AgentsGridSearcher.ClosestWithinUniRadiusAgentsJob
                {
                    agents = this.m_Agents,
                    oriGrid = this.minValue,
                    invresoGrid = 1f / this.gridReso,
                    gridDim = this.gridDim,
                    queryPos = qPoints,
                    sortedPos = this.m_SortedAgents,
                    hashIndex = this.hashIndex,
                    cellStartEnd = this.cellStartEnd,
                    results = nativeArray,
                    searchRadius = searchRadius,
                    agentsCount = this.AgentsCount
                }.Schedule(qPoints.Length, 64, default(JobHandle)).Complete();
            }
            return nativeArray;
        }


        public NativeArray<int> SearchAgentsNearestWithin(NativeArray<float3> queryPoints, float searchRadius, ORCALayer selfCamp, ORCALayer ignoreTag)
        {
            if (this.AgentsCount == 0)
            {
                return default(NativeArray<int>);
            }
            NativeArray<int> nativeArray = new NativeArray<int>(queryPoints.Length, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            if (this.AgentsCount == 1)
            {
                this.SearchNearestWithinUniRadiusCountError(queryPoints, searchRadius, selfCamp, ignoreTag, ref nativeArray);
            }
            else
            {
                new AgentsGridSearcher.ClosestAgentInRadiusJob
                {
                    agents = this.m_Agents,
                    oriGrid = this.minValue,
                    invresoGrid = 1f / this.gridReso,
                    gridDim = this.gridDim,
                    queryPos = queryPoints,
                    sortedPos = this.m_SortedAgents,
                    hashIndex = this.hashIndex,
                    cellStartEnd = this.cellStartEnd,
                    results = nativeArray,
                    searchRadius = searchRadius,
                    selfCamp = selfCamp,
                    ignoreTag = ignoreTag,
                    agentsCount = this.AgentsCount
                }.Schedule(queryPoints.Length, 64, default(JobHandle)).Complete();
            }
            return nativeArray;
        }


        public NativeArray<int> SearchAgentsWithin(NativeArray<float3> queryPoints, NativeArray<float> searchRadius, NativeArray<int> searchCountArr, NativeArray<ORCALayer> selfCamps, NativeArray<ORCALayer> ignoreTags)
        {
            if (this.AgentsCount == 0)
            {
                return default(NativeArray<int>);
            }
            NativeArray<int> result = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> nativeArray = new NativeArray<int>(queryPoints.Length, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            AgentsGridSearcher.GetQueryBeginIndexesJob jobData = new AgentsGridSearcher.GetQueryBeginIndexesJob
            {
                QueryCountArr = searchCountArr,
                Result = result,
                Indexes = nativeArray
            };
            jobData.Schedule(default(JobHandle)).Complete();
            int length = jobData.Result[0];
            result.Dispose();
            NativeArray<int> nativeArray2 = new NativeArray<int>(length, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            if (this.AgentsCount == 1)
            {
                this.SearchWithinCountError(queryPoints, searchRadius, searchCountArr, selfCamps, ignoreTags, ref nativeArray2);
            }
            else
            {
                new AgentsGridSearcher.FindAgentsUseDataArrayJob
                {
                    agents = this.m_Agents,
                    gridReso = this.gridReso,
                    oriGrid = this.minValue,
                    invresoGrid = 1f / this.gridReso,
                    gridDim = this.gridDim,
                    queryPos = queryPoints,
                    queryRadius = searchRadius,
                    queryCounts = searchCountArr,
                    selfCamps = selfCamps,
                    ignoreTags = ignoreTags,
                    sortedPos = this.m_SortedAgents,
                    hashIndex = this.hashIndex,
                    cellStartEnd = this.cellStartEnd,
                    beginIndexes = nativeArray,
                    results = nativeArray2
                }.Schedule(queryPoints.Length, 64, default(JobHandle)).Complete();
            }
            nativeArray.Dispose();
            return nativeArray2;
        }


        public NativeArray<int> SearchAgentsWithin(NativeArray<float3> queryPoints, float searchRadius, int maxNeighborPerQuery, ORCALayer selfCamp, ORCALayer ignoreTag)
        {
            if (this.AgentsCount == 0)
            {
                return default(NativeArray<int>);
            }
            NativeArray<int> nativeArray = new NativeArray<int>(queryPoints.Length * maxNeighborPerQuery, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            if (this.AgentsCount == 1)
            {
                this.SearchWithinCountError(queryPoints, searchRadius, maxNeighborPerQuery, selfCamp, ignoreTag, ref nativeArray);
            }
            else
            {
                new AgentsGridSearcher.FindAgentsWithinPositionJob
                {
                    agents = this.m_Agents,
                    gridReso = this.gridReso,
                    maxNeighbor = maxNeighborPerQuery,
                    oriGrid = this.minValue,
                    invresoGrid = 1f / this.gridReso,
                    gridDim = this.gridDim,
                    queryPos = queryPoints,
                    sortedPos = this.m_SortedAgents,
                    hashIndex = this.hashIndex,
                    cellStartEnd = this.cellStartEnd,
                    cellsToLoop = (int)math.ceil(searchRadius / this.gridReso),
                    selfCamp = selfCamp,
                    ignoreTag = ignoreTag,
                    searchRadius = searchRadius,
                    results = nativeArray
                }.Schedule(queryPoints.Length, 64, default(JobHandle)).Complete();
            }
            return nativeArray;
        }


        public NativeArray<int> SearchAgentsWithin(NativeArray<AgentData> queryPoints, int maxNeighborPerQuery)
        {
            if (this.AgentsCount == 0)
            {
                return default(NativeArray<int>);
            }
            NativeArray<int> nativeArray = new NativeArray<int>(queryPoints.Length * maxNeighborPerQuery, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            if (this.AgentsCount == 1)
            {
                this.SearchWithinCountError(queryPoints, maxNeighborPerQuery, ref nativeArray);
            }
            else
            {
                new AgentsGridSearcher.FindAgentsWithinJob
                {
                    agents = this.m_Agents,
                    gridReso = this.gridReso,
                    maxNeighbor = maxNeighborPerQuery,
                    oriGrid = this.minValue,
                    invresoGrid = 1f / this.gridReso,
                    gridDim = this.gridDim,
                    queryPos = queryPoints,
                    sortedPos = this.m_SortedAgents,
                    hashIndex = this.hashIndex,
                    cellStartEnd = this.cellStartEnd,
                    results = nativeArray
                }.Schedule(queryPoints.Length, 64, default(JobHandle)).Complete();
            }
            return nativeArray;
        }


        public NativeArray<int> SearchAgentsWithin(NativeArray<AgentData> queryPoints)
        {
            if (this.AgentsCount == 0)
            {
                return default(NativeArray<int>);
            }
            NativeArray<int> result = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> nativeArray = new NativeArray<int>(queryPoints.Length, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            AgentsGridSearcher.GetAgentsTotalSearchCountJob jobData = new AgentsGridSearcher.GetAgentsTotalSearchCountJob
            {
                QueryAgents = queryPoints,
                Result = result,
                Indexes = nativeArray
            };
            jobData.Schedule(default(JobHandle)).Complete();
            int length = jobData.Result[0];
            result.Dispose();
            NativeArray<int> nativeArray2 = new NativeArray<int>(length, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            if (this.AgentsCount == 1)
            {
                this.SearchWithinCountError(queryPoints, ref nativeArray2);
            }
            else
            {
                new AgentsGridSearcher.FindEachAgentsWithinJob
                {
                    agents = this.m_Agents,
                    gridReso = this.gridReso,
                    oriGrid = this.minValue,
                    invresoGrid = 1f / this.gridReso,
                    gridDim = this.gridDim,
                    queryPos = queryPoints,
                    sortedPos = this.m_SortedAgents,
                    hashIndex = this.hashIndex,
                    cellStartEnd = this.cellStartEnd,
                    beginIndexes = nativeArray,
                    results = nativeArray2
                }.Schedule(queryPoints.Length, 64, default(JobHandle)).Complete();
            }
            nativeArray.Dispose();
            return nativeArray2;
        }


        [BurstCompile(CompileSynchronously = true)]
        private static bool IsIgnoreTag(ORCALayer selfIgnoreTag, ORCALayer otherTag)
        {
            return (selfIgnoreTag & otherTag) == otherTag;
        }


        [BurstCompile(CompileSynchronously = true)]
        private static bool IsFriendlyCamp(ORCALayer selfCamp, ORCALayer otherCamp)
        {
            return (selfCamp & otherCamp) > ORCALayer.NONE;
        }


        [BurstCompile(CompileSynchronously = true)]
        public static bool CheckFriendlyOrIgnore(ORCALayer selfCamp, ORCALayer otherCamp, ORCALayer selfIgnoreTag, ORCALayer otherTag)
        {
            return AgentsGridSearcher.IsFriendlyCamp(selfCamp, otherCamp) || AgentsGridSearcher.IsIgnoreTag(selfIgnoreTag, otherTag);
        }


        [BurstCompile(CompileSynchronously = true)]
        public static bool CheckFriendlyOrIgnore(ref AgentData self, ref AgentData other)
        {
            return AgentsGridSearcher.IsFriendlyCamp(self.camp, other.camp) || AgentsGridSearcher.IsIgnoreTag(self.ignoreSearchTag, other.searchTag);
        }


        private void SearchNearestCountError(NativeArray<float3> queryPoints, ORCALayer selfCamp, ORCALayer ignoreTag, ref NativeArray<int> results)
        {
            AgentData agentData = this.m_Agents[0];
            for (int i = 0; i < queryPoints.Length; i++)
            {
                results[i] = -1;
                if (!AgentsGridSearcher.CheckFriendlyOrIgnore(selfCamp, agentData.camp, ignoreTag, agentData.searchTag))
                {
                    results[i] = agentData.id;
                }
            }
        }


        private void SearchNearestCountError(NativeArray<AgentData> queryPoints, ref NativeArray<int> results)
        {
            AgentData agentData = this.m_Agents[0];
            for (int i = 0; i < queryPoints.Length; i++)
            {
                AgentData agentData2 = queryPoints[i];
                results[i] = -1;
                if (!AgentsGridSearcher.CheckFriendlyOrIgnore(ref agentData2, ref agentData))
                {
                    results[i] = agentData.id;
                }
            }
        }


        private void SearchNearestCountErrorRadiusCheck(NativeArray<AgentData> queryPoints, ref NativeArray<int2> results)
        {
            AgentData agentData = this.m_Agents[0];
            for (int i = 0; i < queryPoints.Length; i++)
            {
                AgentData agentData2 = queryPoints[i];
                results[i] = new int2(-1, 0);
                if (!AgentsGridSearcher.CheckFriendlyOrIgnore(ref agentData2, ref agentData))
                {
                    results[i] = new int2(agentData.id, (math.distancesq(agentData.worldPosition, agentData2.worldPosition) > math.pow(agentData2.searchRadius + agentData.radius + agentData2.radius, 2f)) ? 0 : 1);
                }
            }
        }


        private void SearchNearestWithinCountError(NativeArray<AgentData> queryPoints, ref NativeArray<int> results)
        {
            AgentData agentData = this.m_Agents[0];
            for (int i = 0; i < queryPoints.Length; i++)
            {
                AgentData agentData2 = queryPoints[i];
                results[i] = -1;
                if (!AgentsGridSearcher.CheckFriendlyOrIgnore(ref agentData2, ref agentData) && math.distancesq(agentData.worldPosition, agentData2.worldPosition) <= math.pow(agentData2.searchRadius + agentData.radius + agentData2.radius, 2f))
                {
                    results[i] = agentData.id;
                }
            }
        }


        private void SearchNearestWithinUniRadiusCountError(NativeArray<float3> queryPoints, float searchRadius, ORCALayer selfCamp, ORCALayer ignoreTag, ref NativeArray<int> results)
        {
            AgentData agentData = this.m_Agents[0];
            for (int i = 0; i < queryPoints.Length; i++)
            {
                float3 y = queryPoints[i];
                results[i] = -1;
                if (!AgentsGridSearcher.CheckFriendlyOrIgnore(selfCamp, agentData.camp, ignoreTag, agentData.searchTag) && math.distancesq(agentData.worldPosition, y) <= math.pow(searchRadius + agentData.radius, 2f))
                {
                    results[i] = agentData.id;
                }
            }
        }


        private void SearchNearestWithinUniRadiusCountError(NativeArray<AgentData> queryPoints, float searchRadius, ref NativeArray<int> results)
        {
            AgentData agentData = this.m_Agents[0];
            for (int i = 0; i < queryPoints.Length; i++)
            {
                AgentData agentData2 = queryPoints[i];
                results[i] = -1;
                if (!AgentsGridSearcher.CheckFriendlyOrIgnore(ref agentData2, ref agentData) && math.distancesq(agentData.worldPosition, agentData2.worldPosition) <= math.pow(agentData2.radius + agentData.radius + searchRadius, 2f))
                {
                    results[i] = agentData.id;
                }
            }
        }


        private void SearchWithinCountError(NativeArray<float3> queryPoints, float searchRadius, int searchCount, ORCALayer selfCamp, ORCALayer ignoreTag, ref NativeArray<int> results)
        {
            AgentData agentData = this.m_Agents[0];
            for (int i = 0; i < queryPoints.Length; i++)
            {
                for (int j = 0; j < searchCount; j++)
                {
                    results[i * searchCount + j] = -1;
                }
                float3 x = queryPoints[i];
                if (!AgentsGridSearcher.CheckFriendlyOrIgnore(selfCamp, agentData.camp, ignoreTag, agentData.searchTag) && math.distancesq(x, agentData.worldPosition) <= math.pow(searchRadius + agentData.radius, 2f))
                {
                    results[i * searchCount] = agentData.id;
                }
            }
        }


        private void SearchWithinCountError(NativeArray<AgentData> queryPoints, ref NativeArray<int> results)
        {
            AgentData agentData = this.m_Agents[0];
            int num = 0;
            for (int i = 0; i < queryPoints.Length; i++)
            {
                AgentData agentData2 = queryPoints[i];
                int searchCount = agentData2.searchCount;
                for (int j = num; j < num + searchCount; j++)
                {
                    results[j] = -1;
                }
                if (!AgentsGridSearcher.CheckFriendlyOrIgnore(ref agentData2, ref agentData))
                {
                    if (math.distancesq(agentData2.worldPosition, agentData.worldPosition) <= math.pow(agentData.radius + agentData2.radius + agentData2.searchRadius, 2f))
                    {
                        results[num] = agentData.id;
                    }
                    num += searchCount;
                }
            }
        }


        private void SearchWithinCountError(NativeArray<float3> queryPoints, NativeArray<float> searchRadius, NativeArray<int> searchCounts, NativeArray<ORCALayer> selfCamps, NativeArray<ORCALayer> ignoreTags, ref NativeArray<int> results)
        {
            AgentData agentData = this.m_Agents[0];
            int num = 0;
            for (int i = 0; i < queryPoints.Length; i++)
            {
                float3 x = queryPoints[i];
                int num2 = searchCounts[i];
                ORCALayer selfCamp = selfCamps[i];
                ORCALayer selfIgnoreTag = ignoreTags[i];
                float num3 = searchRadius[i];
                for (int j = num; j < num + num2; j++)
                {
                    results[j] = -1;
                }
                if (!AgentsGridSearcher.CheckFriendlyOrIgnore(selfCamp, agentData.camp, selfIgnoreTag, agentData.searchTag))
                {
                    if (math.distancesq(x, agentData.worldPosition) <= math.pow(num3 + agentData.radius, 2f))
                    {
                        results[num] = agentData.id;
                    }
                    num += num2;
                }
            }
        }


        private void SearchWithinCountError(NativeArray<AgentData> queryPoints, int searchCount, ref NativeArray<int> results)
        {
            AgentData agentData = this.m_Agents[0];
            for (int i = 0; i < queryPoints.Length; i++)
            {
                AgentData agentData2 = queryPoints[i];
                for (int j = 0; j < searchCount; j++)
                {
                    results[i * searchCount + j] = -1;
                }
                if (!AgentsGridSearcher.CheckFriendlyOrIgnore(ref agentData2, ref agentData) && math.distancesq(agentData2.worldPosition, agentData.worldPosition) <= math.pow(agentData2.searchRadius + agentData.radius + agentData2.radius, 2f))
                {
                    results[i * searchCount] = agentData.id;
                }
            }
        }


        private void getMinMaxCoords(NativeArray<AgentData> mpos, ref float3 minV, ref float3 maxV)
        {
            NativeArray<float3> minVal = new NativeArray<float3>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<float3> maxVal = new NativeArray<float3>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            new AgentsGridSearcher.getminmaxJob
            {
                minVal = minVal,
                maxVal = maxVal,
                pos = mpos
            }.Schedule(this.AgentsCount, default(JobHandle)).Complete();
            minV = minVal[0];
            maxV = maxVal[0];
            minVal.Dispose();
            maxVal.Dispose();
        }


        private static int3 spaceToGrid(float3 pos3D, float3 originGrid, float invdx)
        {
            return (int3)((pos3D - originGrid) * invdx);
        }


        private static int flatten3DTo1D(int3 id3d, int3 gridDim)
        {
            return id3d.z * gridDim.x * gridDim.y + id3d.y * gridDim.x + id3d.x;
        }


        private const int ARRAY_PADDING = 256;


        public const int MAXGRIDSIZE = 256;


        private NativeArray<AgentData> m_Agents;


        private NativeArray<AgentData> m_SortedAgents;


        private NativeArray<int2> hashIndex;


        private NativeArray<int2> cellStartEnd;


        private float3 minValue = float3.zero;


        private float3 maxValue = float3.zero;


        private int3 gridDim = int3.zero;


        private float gridReso = -1f;


        private int targetGridSize;


        private int cellStartEndCount;


        [BurstCompile(CompileSynchronously = true)]
        private struct GetAgentsTotalSearchCountJob : IJob
        {

            public void Execute()
            {
                int num = 0;
                for (int i = 0; i < this.QueryAgents.Length; i++)
                {
                    this.Indexes[i] = num;
                    num += this.QueryAgents[i].searchCount;
                }
                this.Result[0] = num;
            }


            [ReadOnly]
            public NativeArray<AgentData> QueryAgents;


            [WriteOnly]
            public NativeArray<int> Result;


            [WriteOnly]
            public NativeArray<int> Indexes;
        }


        [BurstCompile(CompileSynchronously = true)]
        private struct GetQueryBeginIndexesJob : IJob
        {

            public void Execute()
            {
                int num = 0;
                for (int i = 0; i < this.QueryCountArr.Length; i++)
                {
                    this.Indexes[i] = num;
                    num += this.QueryCountArr[i];
                }
                this.Result[0] = num;
            }


            [ReadOnly]
            public NativeArray<int> QueryCountArr;


            [WriteOnly]
            public NativeArray<int> Result;


            [WriteOnly]
            public NativeArray<int> Indexes;
        }


        [BurstCompile(CompileSynchronously = true)]
        private struct getminmaxJob : IJobFor
        {

            void IJobFor.Execute(int i)
            {
                if (i == 0)
                {
                    this.minVal[0] = this.pos[0].worldPosition;
                    this.maxVal[0] = this.pos[0].worldPosition;
                    return;
                }
                float x = math.min(this.minVal[0].x, this.pos[i].worldPosition.x);
                float y = math.min(this.minVal[0].y, this.pos[i].worldPosition.y);
                float z = math.min(this.minVal[0].z, this.pos[i].worldPosition.z);
                this.minVal[0] = new float3(x, y, z);
                x = math.max(this.maxVal[0].x, this.pos[i].worldPosition.x);
                y = math.max(this.maxVal[0].y, this.pos[i].worldPosition.y);
                z = math.max(this.maxVal[0].z, this.pos[i].worldPosition.z);
                this.maxVal[0] = new float3(x, y, z);
            }


            public NativeArray<float3> minVal;


            public NativeArray<float3> maxVal;


            [ReadOnly]
            public NativeArray<AgentData> pos;
        }


        [BurstCompile(CompileSynchronously = true)]
        private struct AssignHashJob : IJobParallelFor
        {

            void IJobParallelFor.Execute(int index)
            {
                int x = AgentsGridSearcher.flatten3DTo1D(math.clamp(AgentsGridSearcher.spaceToGrid(this.pos[index].worldPosition, this.oriGrid, this.invresoGrid), new int3(0, 0, 0), this.gridDim - new int3(1, 1, 1)), this.gridDim);
                x = math.clamp(x, 0, this.nbcells - 1);
                int2 value;
                value.x = x;
                value.y = index;
                this.hashIndex[index] = value;
            }


            [ReadOnly]
            public float3 oriGrid;


            [ReadOnly]
            public float invresoGrid;


            [ReadOnly]
            public int3 gridDim;


            [ReadOnly]
            public int nbcells;


            [ReadOnly]
            public NativeArray<AgentData> pos;


            public NativeArray<int2> hashIndex;
        }


        [BurstCompile(CompileSynchronously = true)]
        private struct MemsetCellStartJob : IJobParallelFor
        {

            void IJobParallelFor.Execute(int index)
            {
                int2 value;
                value.x = 2147483646;
                value.y = 2147483646;
                this.cellStartEnd[index] = value;
            }


            public NativeArray<int2> cellStartEnd;
        }


        [BurstCompile(CompileSynchronously = true)]
        private struct SortCellJob : IJob
        {

            void IJob.Execute()
            {
                for (int i = 0; i < this.agentsCount; i++)
                {
                    int x = this.hashIndex[i].x;
                    int y = this.hashIndex[i].y;
                    int num = -1;
                    if (i != 0)
                    {
                        num = this.hashIndex[i - 1].x;
                    }
                    int2 value;
                    if (i == 0 || x != num)
                    {
                        value.x = i;
                        value.y = this.cellStartEnd[x].y;
                        this.cellStartEnd[x] = value;
                        if (i != 0)
                        {
                            value.x = this.cellStartEnd[num].x;
                            value.y = i;
                            this.cellStartEnd[num] = value;
                        }
                    }
                    if (i == this.agentsCount - 1)
                    {
                        value.x = this.cellStartEnd[x].x;
                        value.y = i + 1;
                        this.cellStartEnd[x] = value;
                    }
                    this.sortedPos[i] = this.pos[y];
                }
            }


            [ReadOnly]
            public NativeArray<AgentData> pos;


            [ReadOnly]
            public NativeArray<int2> hashIndex;


            [ReadOnly]
            public int agentsCount;


            public NativeArray<int2> cellStartEnd;


            public NativeArray<AgentData> sortedPos;
        }


        [BurstCompile(CompileSynchronously = true)]
        private struct ClosestAgentsPositionJob : IJobParallelFor
        {

            void IJobParallelFor.Execute(int index)
            {
                this.results[index] = -1;
                float3 @float = this.queryPos[index];
                int3 @int = AgentsGridSearcher.spaceToGrid(@float, this.oriGrid, this.invresoGrid);
                @int = math.clamp(@int, new int3(0, 0, 0), this.gridDim - new int3(1, 1, 1));
                float num = float.MaxValue;
                int num2 = -1;
                @int = math.clamp(@int, new int3(0, 0, 0), this.gridDim - new int3(1, 1, 1));
                int index2 = AgentsGridSearcher.flatten3DTo1D(@int, this.gridDim);
                int x = this.cellStartEnd[index2].x;
                int y = this.cellStartEnd[index2].y;
                if (x < 2147483646)
                {
                    for (int i = x; i < y; i++)
                    {
                        AgentData agentData = this.sortedPos[i];
                        if (!AgentsGridSearcher.CheckFriendlyOrIgnore(this.selfCamp, agentData.camp, this.ignoreTag, agentData.searchTag))
                        {
                            float num3 = math.distancesq(@float, agentData.worldPosition);
                            if (num3 < num)
                            {
                                num2 = i;
                                num = num3;
                            }
                        }
                    }
                }
                if (num2 != -1)
                {
                    this.results[index] = this.agents[this.hashIndex[num2].y].id;
                    return;
                }
                for (int j = -1; j <= 1; j++)
                {
                    int3 int2;
                    int2.x = @int.x + j;
                    if (int2.x >= 0 && int2.x < this.gridDim.x)
                    {
                        for (int k = -1; k <= 1; k++)
                        {
                            int2.y = @int.y + k;
                            if (int2.y >= 0 && int2.y < this.gridDim.y)
                            {
                                for (int l = -1; l <= 1; l++)
                                {
                                    int2.z = @int.z + l;
                                    if (int2.z >= 0 && int2.z < this.gridDim.z)
                                    {
                                        int index3 = AgentsGridSearcher.flatten3DTo1D(int2, this.gridDim);
                                        int x2 = this.cellStartEnd[index3].x;
                                        int y2 = this.cellStartEnd[index3].y;
                                        if (x2 < 2147483646)
                                        {
                                            for (int m = x2; m < y2; m++)
                                            {
                                                AgentData agentData2 = this.sortedPos[m];
                                                if (!AgentsGridSearcher.CheckFriendlyOrIgnore(this.selfCamp, agentData2.camp, this.ignoreTag, agentData2.searchTag))
                                                {
                                                    float num4 = math.distancesq(@float, agentData2.worldPosition);
                                                    if (num4 < num)
                                                    {
                                                        num2 = m;
                                                        num = num4;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                if (num2 != -1)
                {
                    this.results[index] = this.agents[this.hashIndex[num2].y].id;
                    return;
                }
                for (int n = 0; n < this.agentsCount; n++)
                {
                    AgentData agentData3 = this.sortedPos[n];
                    if (!AgentsGridSearcher.CheckFriendlyOrIgnore(this.selfCamp, agentData3.camp, this.ignoreTag, agentData3.searchTag))
                    {
                        float num5 = math.distancesq(@float, agentData3.worldPosition);
                        if (num5 < num)
                        {
                            num2 = n;
                            num = num5;
                        }
                    }
                }
                if (num2 != -1)
                {
                    this.results[index] = this.agents[this.hashIndex[num2].y].id;
                }
            }


            [ReadOnly]
            public NativeArray<AgentData> agents;


            [ReadOnly]
            public float3 oriGrid;


            [ReadOnly]
            public float invresoGrid;


            [ReadOnly]
            public int3 gridDim;


            [ReadOnly]
            public NativeArray<float3> queryPos;


            [ReadOnly]
            public NativeArray<int2> cellStartEnd;


            [ReadOnly]
            public NativeArray<AgentData> sortedPos;


            [ReadOnly]
            public NativeArray<int2> hashIndex;


            [ReadOnly]
            public ORCALayer selfCamp;


            [ReadOnly]
            public ORCALayer ignoreTag;


            [ReadOnly]
            public int agentsCount;


            public NativeArray<int> results;
        }


        [BurstCompile(CompileSynchronously = true)]
        private struct ClosestAgentsJob : IJobParallelFor
        {

            void IJobParallelFor.Execute(int index)
            {
                this.results[index] = -1;
                AgentData agentData = this.queryPos[index];
                float3 worldPosition = agentData.worldPosition;
                int3 @int = AgentsGridSearcher.spaceToGrid(worldPosition, this.oriGrid, this.invresoGrid);
                @int = math.clamp(@int, new int3(0, 0, 0), this.gridDim - new int3(1, 1, 1));
                float num = float.MaxValue;
                int num2 = -1;
                @int = math.clamp(@int, new int3(0, 0, 0), this.gridDim - new int3(1, 1, 1));
                int index2 = AgentsGridSearcher.flatten3DTo1D(@int, this.gridDim);
                int x = this.cellStartEnd[index2].x;
                int y = this.cellStartEnd[index2].y;
                if (x < 2147483646)
                {
                    for (int i = x; i < y; i++)
                    {
                        AgentData agentData2 = this.sortedPos[i];
                        if (!AgentsGridSearcher.CheckFriendlyOrIgnore(ref agentData, ref agentData2))
                        {
                            float num3 = math.distancesq(worldPosition, agentData2.worldPosition);
                            if (num3 < num)
                            {
                                num2 = i;
                                num = num3;
                            }
                        }
                    }
                }
                if (num2 != -1)
                {
                    this.results[index] = this.agents[this.hashIndex[num2].y].id;
                    return;
                }
                for (int j = -1; j <= 1; j++)
                {
                    int3 int2;
                    int2.x = @int.x + j;
                    if (int2.x >= 0 && int2.x < this.gridDim.x)
                    {
                        for (int k = -1; k <= 1; k++)
                        {
                            int2.y = @int.y + k;
                            if (int2.y >= 0 && int2.y < this.gridDim.y)
                            {
                                for (int l = -1; l <= 1; l++)
                                {
                                    int2.z = @int.z + l;
                                    if (int2.z >= 0 && int2.z < this.gridDim.z)
                                    {
                                        int index3 = AgentsGridSearcher.flatten3DTo1D(int2, this.gridDim);
                                        int x2 = this.cellStartEnd[index3].x;
                                        int y2 = this.cellStartEnd[index3].y;
                                        if (x2 < 2147483646)
                                        {
                                            for (int m = x2; m < y2; m++)
                                            {
                                                AgentData agentData3 = this.sortedPos[m];
                                                if (!AgentsGridSearcher.CheckFriendlyOrIgnore(ref agentData, ref agentData3))
                                                {
                                                    float num4 = math.distancesq(worldPosition, agentData3.worldPosition);
                                                    if (num4 < num)
                                                    {
                                                        num2 = m;
                                                        num = num4;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                if (num2 != -1)
                {
                    this.results[index] = this.agents[this.hashIndex[num2].y].id;
                    return;
                }
                for (int n = 0; n < this.agentsCount; n++)
                {
                    AgentData agentData4 = this.sortedPos[n];
                    if (!AgentsGridSearcher.CheckFriendlyOrIgnore(ref agentData, ref agentData4))
                    {
                        float num5 = math.distancesq(worldPosition, agentData4.worldPosition);
                        if (num5 < num)
                        {
                            num2 = n;
                            num = num5;
                        }
                    }
                }
                if (num2 != -1)
                {
                    this.results[index] = this.agents[this.hashIndex[num2].y].id;
                }
            }


            [ReadOnly]
            public NativeArray<AgentData> agents;


            [ReadOnly]
            public float3 oriGrid;


            [ReadOnly]
            public float invresoGrid;


            [ReadOnly]
            public int3 gridDim;


            [ReadOnly]
            public NativeArray<AgentData> queryPos;


            [ReadOnly]
            public NativeArray<int2> cellStartEnd;


            [ReadOnly]
            public NativeArray<AgentData> sortedPos;


            [ReadOnly]
            public NativeArray<int2> hashIndex;


            [ReadOnly]
            public int agentsCount;


            [WriteOnly]
            public NativeArray<int> results;
        }


        [BurstCompile(CompileSynchronously = true)]
        private struct ClosestAgentsRadiusCheckJob : IJobParallelFor
        {

            void IJobParallelFor.Execute(int index)
            {
                this.results[index] = new int2(-1, 0);
                AgentData agentData = this.queryPos[index];
                float3 worldPosition = agentData.worldPosition;
                int3 @int = AgentsGridSearcher.spaceToGrid(worldPosition, this.oriGrid, this.invresoGrid);
                @int = math.clamp(@int, new int3(0, 0, 0), this.gridDim - new int3(1, 1, 1));
                float num = float.MaxValue;
                int num2 = -1;
                @int = math.clamp(@int, new int3(0, 0, 0), this.gridDim - new int3(1, 1, 1));
                int index2 = AgentsGridSearcher.flatten3DTo1D(@int, this.gridDim);
                int x = this.cellStartEnd[index2].x;
                int y = this.cellStartEnd[index2].y;
                if (x < 2147483646)
                {
                    for (int i = x; i < y; i++)
                    {
                        AgentData agentData2 = this.sortedPos[i];
                        if (!AgentsGridSearcher.CheckFriendlyOrIgnore(ref agentData, ref agentData2))
                        {
                            float num3 = math.distancesq(worldPosition, agentData2.worldPosition);
                            if (num3 < num)
                            {
                                num2 = i;
                                num = num3;
                            }
                        }
                    }
                }
                if (num2 != -1)
                {
                    AgentData agentData3 = this.agents[this.hashIndex[num2].y];
                    this.results[index] = new int2(agentData3.id, (math.distancesq(agentData3.worldPosition, agentData.worldPosition) > math.pow(agentData.searchRadius + agentData.radius + agentData3.radius, 2f)) ? 0 : 1);
                    return;
                }
                for (int j = -1; j <= 1; j++)
                {
                    int3 int2;
                    int2.x = @int.x + j;
                    if (int2.x >= 0 && int2.x < this.gridDim.x)
                    {
                        for (int k = -1; k <= 1; k++)
                        {
                            int2.y = @int.y + k;
                            if (int2.y >= 0 && int2.y < this.gridDim.y)
                            {
                                for (int l = -1; l <= 1; l++)
                                {
                                    int2.z = @int.z + l;
                                    if (int2.z >= 0 && int2.z < this.gridDim.z)
                                    {
                                        int index3 = AgentsGridSearcher.flatten3DTo1D(int2, this.gridDim);
                                        int x2 = this.cellStartEnd[index3].x;
                                        int y2 = this.cellStartEnd[index3].y;
                                        if (x2 < 2147483646)
                                        {
                                            for (int m = x2; m < y2; m++)
                                            {
                                                AgentData agentData4 = this.sortedPos[m];
                                                if (!AgentsGridSearcher.CheckFriendlyOrIgnore(ref agentData, ref agentData4))
                                                {
                                                    float num4 = math.distancesq(worldPosition, agentData4.worldPosition);
                                                    if (num4 < num)
                                                    {
                                                        num2 = m;
                                                        num = num4;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                if (num2 != -1)
                {
                    AgentData agentData5 = this.agents[this.hashIndex[num2].y];
                    this.results[index] = new int2(agentData5.id, (math.distancesq(agentData5.worldPosition, agentData.worldPosition) > math.pow(agentData.searchRadius + agentData.radius + agentData5.radius, 2f)) ? 0 : 1);
                    return;
                }
                for (int n = 0; n < this.agentsCount; n++)
                {
                    AgentData agentData6 = this.sortedPos[n];
                    if (!AgentsGridSearcher.CheckFriendlyOrIgnore(ref agentData, ref agentData6))
                    {
                        float num5 = math.distancesq(worldPosition, agentData6.worldPosition);
                        if (num5 < num)
                        {
                            num2 = n;
                            num = num5;
                        }
                    }
                }
                if (num2 != -1)
                {
                    AgentData agentData7 = this.agents[this.hashIndex[num2].y];
                    this.results[index] = new int2(agentData7.id, (math.distancesq(agentData7.worldPosition, agentData.worldPosition) > math.pow(agentData.searchRadius + agentData.radius + agentData7.radius, 2f)) ? 0 : 1);
                }
            }


            [ReadOnly]
            public NativeArray<AgentData> agents;


            [ReadOnly]
            public float3 oriGrid;


            [ReadOnly]
            public float invresoGrid;


            [ReadOnly]
            public int3 gridDim;


            [ReadOnly]
            public NativeArray<AgentData> queryPos;


            [ReadOnly]
            public NativeArray<int2> cellStartEnd;


            [ReadOnly]
            public NativeArray<AgentData> sortedPos;


            [ReadOnly]
            public NativeArray<int2> hashIndex;


            [ReadOnly]
            public int agentsCount;


            public NativeArray<int2> results;
        }


        [BurstCompile(CompileSynchronously = true)]
        private struct ClosestWithinUniRadiusAgentsJob : IJobParallelFor
        {

            void IJobParallelFor.Execute(int index)
            {
                this.results[index] = -1;
                AgentData agentData = this.queryPos[index];
                float3 worldPosition = agentData.worldPosition;
                int3 @int = AgentsGridSearcher.spaceToGrid(worldPosition, this.oriGrid, this.invresoGrid);
                @int = math.clamp(@int, new int3(0, 0, 0), this.gridDim - new int3(1, 1, 1));
                float num = float.MaxValue;
                int num2 = -1;
                @int = math.clamp(@int, new int3(0, 0, 0), this.gridDim - new int3(1, 1, 1));
                int index2 = AgentsGridSearcher.flatten3DTo1D(@int, this.gridDim);
                int x = this.cellStartEnd[index2].x;
                int y = this.cellStartEnd[index2].y;
                if (x < 2147483646)
                {
                    for (int i = x; i < y; i++)
                    {
                        AgentData agentData2 = this.sortedPos[i];
                        if (!AgentsGridSearcher.CheckFriendlyOrIgnore(ref agentData, ref agentData2))
                        {
                            float num3 = math.distancesq(worldPosition, agentData2.worldPosition);
                            if (num3 <= math.pow(agentData.searchRadius + agentData.radius + agentData2.radius, 2f) && num3 < num)
                            {
                                num2 = i;
                                num = num3;
                            }
                        }
                    }
                }
                if (num2 != -1)
                {
                    this.results[index] = this.agents[this.hashIndex[num2].y].id;
                    return;
                }
                for (int j = -1; j <= 1; j++)
                {
                    int3 int2;
                    int2.x = @int.x + j;
                    if (int2.x >= 0 && int2.x < this.gridDim.x)
                    {
                        for (int k = -1; k <= 1; k++)
                        {
                            int2.y = @int.y + k;
                            if (int2.y >= 0 && int2.y < this.gridDim.y)
                            {
                                for (int l = -1; l <= 1; l++)
                                {
                                    int2.z = @int.z + l;
                                    if (int2.z >= 0 && int2.z < this.gridDim.z)
                                    {
                                        int index3 = AgentsGridSearcher.flatten3DTo1D(int2, this.gridDim);
                                        int x2 = this.cellStartEnd[index3].x;
                                        int y2 = this.cellStartEnd[index3].y;
                                        if (x2 < 2147483646)
                                        {
                                            for (int m = x2; m < y2; m++)
                                            {
                                                AgentData agentData3 = this.sortedPos[m];
                                                if (!AgentsGridSearcher.CheckFriendlyOrIgnore(ref agentData, ref agentData3))
                                                {
                                                    float num4 = math.distancesq(worldPosition, agentData3.worldPosition);
                                                    if (num4 <= math.pow(agentData.searchRadius + agentData.radius + agentData3.radius, 2f) && num4 < num)
                                                    {
                                                        num2 = m;
                                                        num = num4;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                if (num2 != -1)
                {
                    this.results[index] = this.agents[this.hashIndex[num2].y].id;
                    return;
                }
                for (int n = 0; n < this.agentsCount; n++)
                {
                    AgentData agentData4 = this.sortedPos[n];
                    if (!AgentsGridSearcher.CheckFriendlyOrIgnore(ref agentData, ref agentData4))
                    {
                        float num5 = math.distancesq(worldPosition, agentData4.worldPosition);
                        if (num5 <= math.pow(agentData.searchRadius + agentData.radius + agentData4.radius, 2f) && num5 < num)
                        {
                            num2 = n;
                            num = num5;
                        }
                    }
                }
                if (num2 != -1)
                {
                    this.results[index] = this.agents[this.hashIndex[num2].y].id;
                }
            }


            [ReadOnly]
            public NativeArray<AgentData> agents;


            [ReadOnly]
            public float3 oriGrid;


            [ReadOnly]
            public float invresoGrid;


            [ReadOnly]
            public int3 gridDim;


            [ReadOnly]
            public NativeArray<AgentData> queryPos;


            [ReadOnly]
            public NativeArray<int2> cellStartEnd;


            [ReadOnly]
            public NativeArray<AgentData> sortedPos;


            [ReadOnly]
            public NativeArray<int2> hashIndex;


            [ReadOnly]
            public float searchRadius;


            [ReadOnly]
            public int agentsCount;


            public NativeArray<int> results;
        }


        [BurstCompile(CompileSynchronously = true)]
        private struct ClosestAgentInRadiusJob : IJobParallelFor
        {

            void IJobParallelFor.Execute(int index)
            {
                this.results[index] = -1;
                float3 @float = this.queryPos[index];
                int3 @int = AgentsGridSearcher.spaceToGrid(@float, this.oriGrid, this.invresoGrid);
                @int = math.clamp(@int, new int3(0, 0, 0), this.gridDim - new int3(1, 1, 1));
                float num = float.MaxValue;
                int num2 = -1;
                @int = math.clamp(@int, new int3(0, 0, 0), this.gridDim - new int3(1, 1, 1));
                int index2 = AgentsGridSearcher.flatten3DTo1D(@int, this.gridDim);
                int x = this.cellStartEnd[index2].x;
                int y = this.cellStartEnd[index2].y;
                if (x < 2147483646)
                {
                    for (int i = x; i < y; i++)
                    {
                        AgentData agentData = this.sortedPos[i];
                        if (!AgentsGridSearcher.CheckFriendlyOrIgnore(this.selfCamp, agentData.camp, this.ignoreTag, agentData.searchTag))
                        {
                            float num3 = math.distancesq(@float, agentData.worldPosition);
                            if (num3 <= math.pow(this.searchRadius + agentData.radius, 2f) && num3 < num)
                            {
                                num2 = i;
                                num = num3;
                            }
                        }
                    }
                }
                if (num2 != -1)
                {
                    this.results[index] = this.agents[this.hashIndex[num2].y].id;
                    return;
                }
                for (int j = -1; j <= 1; j++)
                {
                    int3 int2;
                    int2.x = @int.x + j;
                    if (int2.x >= 0 && int2.x < this.gridDim.x)
                    {
                        for (int k = -1; k <= 1; k++)
                        {
                            int2.y = @int.y + k;
                            if (int2.y >= 0 && int2.y < this.gridDim.y)
                            {
                                for (int l = -1; l <= 1; l++)
                                {
                                    int2.z = @int.z + l;
                                    if (int2.z >= 0 && int2.z < this.gridDim.z)
                                    {
                                        int index3 = AgentsGridSearcher.flatten3DTo1D(int2, this.gridDim);
                                        int x2 = this.cellStartEnd[index3].x;
                                        int y2 = this.cellStartEnd[index3].y;
                                        if (x2 < 2147483646)
                                        {
                                            for (int m = x2; m < y2; m++)
                                            {
                                                AgentData agentData2 = this.sortedPos[m];
                                                if (!AgentsGridSearcher.CheckFriendlyOrIgnore(this.selfCamp, agentData2.camp, this.ignoreTag, agentData2.searchTag))
                                                {
                                                    float num4 = math.distancesq(@float, agentData2.worldPosition);
                                                    if (num4 <= math.pow(this.searchRadius + agentData2.radius, 2f) && num4 < num)
                                                    {
                                                        num2 = m;
                                                        num = num4;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                if (num2 != -1)
                {
                    this.results[index] = this.agents[this.hashIndex[num2].y].id;
                    return;
                }
                for (int n = 0; n < this.agentsCount; n++)
                {
                    AgentData agentData3 = this.sortedPos[n];
                    if (!AgentsGridSearcher.CheckFriendlyOrIgnore(this.selfCamp, agentData3.camp, this.ignoreTag, agentData3.searchTag))
                    {
                        float num5 = math.distancesq(@float, agentData3.worldPosition);
                        if (num5 <= math.pow(this.searchRadius + agentData3.radius, 2f) && num5 < num)
                        {
                            num2 = n;
                            num = num5;
                        }
                    }
                }
                if (num2 != -1)
                {
                    this.results[index] = this.agents[this.hashIndex[num2].y].id;
                }
            }


            [ReadOnly]
            public NativeArray<AgentData> agents;


            [ReadOnly]
            public float3 oriGrid;


            [ReadOnly]
            public float invresoGrid;


            [ReadOnly]
            public int3 gridDim;


            [ReadOnly]
            public NativeArray<float3> queryPos;


            [ReadOnly]
            public NativeArray<int2> cellStartEnd;


            [ReadOnly]
            public NativeArray<AgentData> sortedPos;


            [ReadOnly]
            public NativeArray<int2> hashIndex;


            [ReadOnly]
            public float searchRadius;


            [ReadOnly]
            public ORCALayer selfCamp;


            [ReadOnly]
            public ORCALayer ignoreTag;


            [ReadOnly]
            public int agentsCount;


            public NativeArray<int> results;
        }


        [BurstCompile(CompileSynchronously = true)]
        private struct ClosestWithinAgentsJob : IJobParallelFor
        {

            void IJobParallelFor.Execute(int index)
            {
                this.results[index] = -1;
                AgentData agentData = this.queryPos[index];
                float3 worldPosition = agentData.worldPosition;
                int3 @int = AgentsGridSearcher.spaceToGrid(worldPosition, this.oriGrid, this.invresoGrid);
                @int = math.clamp(@int, new int3(0, 0, 0), this.gridDim - new int3(1, 1, 1));
                float num = float.MaxValue;
                int num2 = -1;
                @int = math.clamp(@int, new int3(0, 0, 0), this.gridDim - new int3(1, 1, 1));
                int index2 = AgentsGridSearcher.flatten3DTo1D(@int, this.gridDim);
                int x = this.cellStartEnd[index2].x;
                int y = this.cellStartEnd[index2].y;
                if (x < 2147483646)
                {
                    for (int i = x; i < y; i++)
                    {
                        AgentData agentData2 = this.sortedPos[i];
                        if (!AgentsGridSearcher.CheckFriendlyOrIgnore(ref agentData, ref agentData2))
                        {
                            float num3 = math.distancesq(worldPosition, agentData2.worldPosition);
                            if (num3 <= math.pow(agentData.searchRadius + agentData.radius + agentData2.radius, 2f) && num3 < num)
                            {
                                num2 = i;
                                num = num3;
                            }
                        }
                    }
                }
                if (num2 != -1)
                {
                    this.results[index] = this.agents[this.hashIndex[num2].y].id;
                    return;
                }
                for (int j = -1; j <= 1; j++)
                {
                    int3 int2;
                    int2.x = @int.x + j;
                    if (int2.x >= 0 && int2.x < this.gridDim.x)
                    {
                        for (int k = -1; k <= 1; k++)
                        {
                            int2.y = @int.y + k;
                            if (int2.y >= 0 && int2.y < this.gridDim.y)
                            {
                                for (int l = -1; l <= 1; l++)
                                {
                                    int2.z = @int.z + l;
                                    if (int2.z >= 0 && int2.z < this.gridDim.z)
                                    {
                                        int index3 = AgentsGridSearcher.flatten3DTo1D(int2, this.gridDim);
                                        int x2 = this.cellStartEnd[index3].x;
                                        int y2 = this.cellStartEnd[index3].y;
                                        if (x2 < 2147483646)
                                        {
                                            for (int m = x2; m < y2; m++)
                                            {
                                                AgentData agentData3 = this.sortedPos[m];
                                                if (!AgentsGridSearcher.CheckFriendlyOrIgnore(ref agentData, ref agentData3))
                                                {
                                                    float num4 = math.distancesq(worldPosition, agentData3.worldPosition);
                                                    if (num4 <= math.pow(agentData.searchRadius + agentData.radius + agentData3.radius, 2f) && num4 < num)
                                                    {
                                                        num2 = m;
                                                        num = num4;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                if (num2 != -1)
                {
                    this.results[index] = this.agents[this.hashIndex[num2].y].id;
                    return;
                }
                for (int n = 0; n < this.agentsCount; n++)
                {
                    AgentData agentData4 = this.sortedPos[n];
                    if (!AgentsGridSearcher.CheckFriendlyOrIgnore(ref agentData, ref agentData4))
                    {
                        float num5 = math.distancesq(worldPosition, agentData4.worldPosition);
                        if (num5 <= math.pow(agentData.searchRadius + agentData.radius + agentData4.radius, 2f) && num5 < num)
                        {
                            num2 = n;
                            num = num5;
                        }
                    }
                }
                if (num2 != -1)
                {
                    this.results[index] = this.agents[this.hashIndex[num2].y].id;
                }
            }


            [ReadOnly]
            public NativeArray<AgentData> agents;


            [ReadOnly]
            public float3 oriGrid;


            [ReadOnly]
            public float invresoGrid;


            [ReadOnly]
            public int3 gridDim;


            [ReadOnly]
            public NativeArray<AgentData> queryPos;


            [ReadOnly]
            public NativeArray<int2> cellStartEnd;


            [ReadOnly]
            public NativeArray<AgentData> sortedPos;


            [ReadOnly]
            public NativeArray<int2> hashIndex;


            [ReadOnly]
            public int agentsCount;


            public NativeArray<int> results;
        }


        [BurstCompile(CompileSynchronously = true)]
        private struct FindAgentsWithinJob : IJobParallelFor
        {

            void IJobParallelFor.Execute(int index)
            {
                for (int i = 0; i < this.maxNeighbor; i++)
                {
                    this.results[index * this.maxNeighbor + i] = -1;
                }
                AgentData agentData = this.queryPos[index];
                float3 worldPosition = agentData.worldPosition;
                int3 @int = AgentsGridSearcher.spaceToGrid(worldPosition, this.oriGrid, this.invresoGrid);
                @int = math.clamp(@int, new int3(0, 0, 0), this.gridDim - new int3(1, 1, 1));
                int num = 0;
                int index2 = AgentsGridSearcher.flatten3DTo1D(@int, this.gridDim);
                int x = this.cellStartEnd[index2].x;
                int y = this.cellStartEnd[index2].y;
                if (x < 2147483646)
                {
                    for (int j = x; j < y; j++)
                    {
                        AgentData agentData2 = this.sortedPos[j];
                        if (!AgentsGridSearcher.CheckFriendlyOrIgnore(ref agentData, ref agentData2) && math.distancesq(worldPosition, agentData2.worldPosition) <= math.pow(agentData.radius + agentData2.radius + agentData.searchRadius, 2f))
                        {
                            this.results[index * this.maxNeighbor + num] = this.agents[this.hashIndex[j].y].id;
                            num++;
                            if (num == this.maxNeighbor)
                            {
                                return;
                            }
                        }
                    }
                }
                int num2 = (int)math.ceil(agentData.searchRadius / this.gridReso);
                for (int k = -num2; k <= num2; k++)
                {
                    int3 int2;
                    int2.x = @int.x + k;
                    if (int2.x >= 0 && int2.x < this.gridDim.x)
                    {
                        for (int l = -num2; l <= num2; l++)
                        {
                            int2.y = @int.y + l;
                            if (int2.y >= 0 && int2.y < this.gridDim.y)
                            {
                                for (int m = -num2; m <= num2; m++)
                                {
                                    int2.z = @int.z + m;
                                    if (int2.z >= 0 && int2.z < this.gridDim.z && (k != 0 || l != 0 || m != 0))
                                    {
                                        int index3 = AgentsGridSearcher.flatten3DTo1D(int2, this.gridDim);
                                        int x2 = this.cellStartEnd[index3].x;
                                        int y2 = this.cellStartEnd[index3].y;
                                        if (x2 < 2147483646)
                                        {
                                            for (int n = x2; n < y2; n++)
                                            {
                                                AgentData agentData3 = this.sortedPos[n];
                                                if (!AgentsGridSearcher.CheckFriendlyOrIgnore(ref agentData, ref agentData3) && math.distancesq(worldPosition, agentData3.worldPosition) <= math.pow(agentData.radius + agentData3.radius + agentData.searchRadius, 2f))
                                                {
                                                    this.results[index * this.maxNeighbor + num] = this.agents[this.hashIndex[n].y].id;
                                                    num++;
                                                    if (num == this.maxNeighbor)
                                                    {
                                                        return;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }


            [ReadOnly]
            public float gridReso;


            [ReadOnly]
            public NativeArray<AgentData> agents;


            [ReadOnly]
            public int maxNeighbor;


            [ReadOnly]
            public float3 oriGrid;


            [ReadOnly]
            public float invresoGrid;


            [ReadOnly]
            public int3 gridDim;


            [ReadOnly]
            public NativeArray<AgentData> queryPos;


            [ReadOnly]
            public NativeArray<int2> cellStartEnd;


            [ReadOnly]
            public NativeArray<AgentData> sortedPos;


            [ReadOnly]
            public NativeArray<int2> hashIndex;


            [NativeDisableParallelForRestriction]
            public NativeArray<int> results;
        }


        [BurstCompile(CompileSynchronously = true)]
        private struct FindEachAgentsWithinJob : IJobParallelFor
        {

            void IJobParallelFor.Execute(int index)
            {
                AgentData agentData = this.queryPos[index];
                int num = this.beginIndexes[index];
                int searchCount = agentData.searchCount;
                for (int i = 0; i < searchCount; i++)
                {
                    this.results[num + i] = -1;
                }
                float3 worldPosition = agentData.worldPosition;
                int3 @int = AgentsGridSearcher.spaceToGrid(worldPosition, this.oriGrid, this.invresoGrid);
                @int = math.clamp(@int, new int3(0, 0, 0), this.gridDim - new int3(1, 1, 1));
                int num2 = 0;
                int index2 = AgentsGridSearcher.flatten3DTo1D(@int, this.gridDim);
                int x = this.cellStartEnd[index2].x;
                int y = this.cellStartEnd[index2].y;
                if (x < 2147483646)
                {
                    for (int j = x; j < y; j++)
                    {
                        AgentData agentData2 = this.sortedPos[j];
                        if (!AgentsGridSearcher.CheckFriendlyOrIgnore(ref agentData, ref agentData2) && math.distancesq(worldPosition, agentData2.worldPosition) <= math.pow(agentData.radius + agentData2.radius + agentData.searchRadius, 2f))
                        {
                            this.results[num + num2] = this.agents[this.hashIndex[j].y].id;
                            num2++;
                            if (num2 == searchCount)
                            {
                                return;
                            }
                        }
                    }
                }
                int num3 = (int)math.ceil(agentData.searchRadius / this.gridReso);
                for (int k = -num3; k <= num3; k++)
                {
                    int3 int2;
                    int2.x = @int.x + k;
                    if (int2.x >= 0 && int2.x < this.gridDim.x)
                    {
                        for (int l = -num3; l <= num3; l++)
                        {
                            int2.y = @int.y + l;
                            if (int2.y >= 0 && int2.y < this.gridDim.y)
                            {
                                for (int m = -num3; m <= num3; m++)
                                {
                                    int2.z = @int.z + m;
                                    if (int2.z >= 0 && int2.z < this.gridDim.z && (k != 0 || l != 0 || m != 0))
                                    {
                                        int index3 = AgentsGridSearcher.flatten3DTo1D(int2, this.gridDim);
                                        int x2 = this.cellStartEnd[index3].x;
                                        int y2 = this.cellStartEnd[index3].y;
                                        if (x2 < 2147483646)
                                        {
                                            for (int n = x2; n < y2; n++)
                                            {
                                                AgentData agentData3 = this.sortedPos[n];
                                                if (!AgentsGridSearcher.CheckFriendlyOrIgnore(ref agentData, ref agentData3) && math.distancesq(worldPosition, agentData3.worldPosition) <= math.pow(agentData.radius + agentData3.radius + agentData.searchRadius, 2f))
                                                {
                                                    this.results[num + num2] = this.agents[this.hashIndex[n].y].id;
                                                    num2++;
                                                    if (num2 == searchCount)
                                                    {
                                                        return;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }


            [ReadOnly]
            public float gridReso;


            [ReadOnly]
            public NativeArray<AgentData> agents;


            [ReadOnly]
            public float3 oriGrid;


            [ReadOnly]
            public float invresoGrid;


            [ReadOnly]
            public int3 gridDim;


            [ReadOnly]
            public NativeArray<AgentData> queryPos;


            [ReadOnly]
            public NativeArray<int2> cellStartEnd;


            [ReadOnly]
            public NativeArray<AgentData> sortedPos;


            [ReadOnly]
            public NativeArray<int2> hashIndex;


            [ReadOnly]
            public NativeArray<int> beginIndexes;


            [NativeDisableParallelForRestriction]
            public NativeArray<int> results;
        }


        [BurstCompile(CompileSynchronously = true)]
        private struct FindAgentsUseDataArrayJob : IJobParallelFor
        {

            void IJobParallelFor.Execute(int index)
            {
                int num = this.beginIndexes[index];
                int num2 = this.queryCounts[index];
                ORCALayer selfCamp = this.selfCamps[index];
                ORCALayer selfIgnoreTag = this.ignoreTags[index];
                float num3 = this.queryRadius[index];
                for (int i = 0; i < num2; i++)
                {
                    this.results[num + i] = -1;
                }
                float3 @float = this.queryPos[index];
                int3 @int = AgentsGridSearcher.spaceToGrid(@float, this.oriGrid, this.invresoGrid);
                @int = math.clamp(@int, new int3(0, 0, 0), this.gridDim - new int3(1, 1, 1));
                int num4 = 0;
                int index2 = AgentsGridSearcher.flatten3DTo1D(@int, this.gridDim);
                int x = this.cellStartEnd[index2].x;
                int y = this.cellStartEnd[index2].y;
                if (x < 2147483646)
                {
                    for (int j = x; j < y; j++)
                    {
                        AgentData agentData = this.sortedPos[j];
                        if (!AgentsGridSearcher.CheckFriendlyOrIgnore(selfCamp, agentData.camp, selfIgnoreTag, agentData.searchTag) && math.distancesq(@float, agentData.worldPosition) <= math.pow(num3 + agentData.radius, 2f))
                        {
                            this.results[num + num4] = this.agents[this.hashIndex[j].y].id;
                            num4++;
                            if (num4 == num2)
                            {
                                return;
                            }
                        }
                    }
                }
                int num5 = (int)math.ceil(num3 / this.gridReso);
                for (int k = -num5; k <= num5; k++)
                {
                    int3 int2;
                    int2.x = @int.x + k;
                    if (int2.x >= 0 && int2.x < this.gridDim.x)
                    {
                        for (int l = -num5; l <= num5; l++)
                        {
                            int2.y = @int.y + l;
                            if (int2.y >= 0 && int2.y < this.gridDim.y)
                            {
                                for (int m = -num5; m <= num5; m++)
                                {
                                    int2.z = @int.z + m;
                                    if (int2.z >= 0 && int2.z < this.gridDim.z && (k != 0 || l != 0 || m != 0))
                                    {
                                        int index3 = AgentsGridSearcher.flatten3DTo1D(int2, this.gridDim);
                                        int x2 = this.cellStartEnd[index3].x;
                                        int y2 = this.cellStartEnd[index3].y;
                                        if (x2 < 2147483646)
                                        {
                                            for (int n = x2; n < y2; n++)
                                            {
                                                AgentData agentData2 = this.sortedPos[n];
                                                if (!AgentsGridSearcher.CheckFriendlyOrIgnore(selfCamp, agentData2.camp, selfIgnoreTag, agentData2.searchTag) && math.distancesq(@float, agentData2.worldPosition) <= math.pow(num3 + agentData2.radius, 2f))
                                                {
                                                    this.results[num + num4] = this.agents[this.hashIndex[n].y].id;
                                                    num4++;
                                                    if (num4 == num2)
                                                    {
                                                        return;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }


            [ReadOnly]
            public float gridReso;


            [ReadOnly]
            public NativeArray<AgentData> agents;


            [ReadOnly]
            public float3 oriGrid;


            [ReadOnly]
            public float invresoGrid;


            [ReadOnly]
            public int3 gridDim;


            [ReadOnly]
            public NativeArray<float3> queryPos;


            [ReadOnly]
            public NativeArray<float> queryRadius;


            [ReadOnly]
            public NativeArray<int> queryCounts;


            [ReadOnly]
            public NativeArray<ORCALayer> selfCamps;


            [ReadOnly]
            public NativeArray<ORCALayer> ignoreTags;


            [ReadOnly]
            public NativeArray<int2> cellStartEnd;


            [ReadOnly]
            public NativeArray<AgentData> sortedPos;


            [ReadOnly]
            public NativeArray<int2> hashIndex;


            [ReadOnly]
            public NativeArray<int> beginIndexes;


            [NativeDisableParallelForRestriction]
            public NativeArray<int> results;
        }


        [BurstCompile(CompileSynchronously = true)]
        private struct FindAgentsWithinPositionJob : IJobParallelFor
        {

            void IJobParallelFor.Execute(int index)
            {
                for (int i = 0; i < this.maxNeighbor; i++)
                {
                    this.results[index * this.maxNeighbor + i] = -1;
                }
                float3 @float = this.queryPos[index];
                int3 @int = AgentsGridSearcher.spaceToGrid(@float, this.oriGrid, this.invresoGrid);
                @int = math.clamp(@int, new int3(0, 0, 0), this.gridDim - new int3(1, 1, 1));
                int num = 0;
                int index2 = AgentsGridSearcher.flatten3DTo1D(@int, this.gridDim);
                int x = this.cellStartEnd[index2].x;
                int y = this.cellStartEnd[index2].y;
                if (x < 2147483646)
                {
                    for (int j = x; j < y; j++)
                    {
                        AgentData agentData = this.sortedPos[j];
                        if (!AgentsGridSearcher.CheckFriendlyOrIgnore(this.selfCamp, agentData.camp, this.ignoreTag, agentData.searchTag) && math.distancesq(@float, agentData.worldPosition) <= math.pow(agentData.radius + this.searchRadius, 2f))
                        {
                            this.results[index * this.maxNeighbor + num] = this.agents[this.hashIndex[j].y].id;
                            num++;
                            if (num == this.maxNeighbor)
                            {
                                return;
                            }
                        }
                    }
                }
                for (int k = -this.cellsToLoop; k <= this.cellsToLoop; k++)
                {
                    int3 int2;
                    int2.x = @int.x + k;
                    if (int2.x >= 0 && int2.x < this.gridDim.x)
                    {
                        for (int l = -this.cellsToLoop; l <= this.cellsToLoop; l++)
                        {
                            int2.y = @int.y + l;
                            if (int2.y >= 0 && int2.y < this.gridDim.y)
                            {
                                for (int m = -this.cellsToLoop; m <= this.cellsToLoop; m++)
                                {
                                    int2.z = @int.z + m;
                                    if (int2.z >= 0 && int2.z < this.gridDim.z && (k != 0 || l != 0 || m != 0))
                                    {
                                        int index3 = AgentsGridSearcher.flatten3DTo1D(int2, this.gridDim);
                                        int x2 = this.cellStartEnd[index3].x;
                                        int y2 = this.cellStartEnd[index3].y;
                                        if (x2 < 2147483646)
                                        {
                                            for (int n = x2; n < y2; n++)
                                            {
                                                AgentData agentData2 = this.sortedPos[n];
                                                if (!AgentsGridSearcher.CheckFriendlyOrIgnore(this.selfCamp, agentData2.camp, this.ignoreTag, agentData2.searchTag) && math.distancesq(@float, agentData2.worldPosition) <= math.pow(agentData2.radius + this.searchRadius, 2f))
                                                {
                                                    this.results[index * this.maxNeighbor + num] = this.agents[this.hashIndex[n].y].id;
                                                    num++;
                                                    if (num == this.maxNeighbor)
                                                    {
                                                        return;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }


            [ReadOnly]
            public float gridReso;


            [ReadOnly]
            public NativeArray<AgentData> agents;


            [ReadOnly]
            public int maxNeighbor;


            [ReadOnly]
            public int cellsToLoop;


            [ReadOnly]
            public float3 oriGrid;


            [ReadOnly]
            public float invresoGrid;


            [ReadOnly]
            public int3 gridDim;


            [ReadOnly]
            public NativeArray<float3> queryPos;


            [ReadOnly]
            public NativeArray<int2> cellStartEnd;


            [ReadOnly]
            public NativeArray<AgentData> sortedPos;


            [ReadOnly]
            public NativeArray<int2> hashIndex;


            [ReadOnly]
            public float searchRadius;


            [ReadOnly]
            public ORCALayer selfCamp;


            [ReadOnly]
            public ORCALayer ignoreTag;


            [NativeDisableParallelForRestriction]
            public NativeArray<int> results;
        }


        [BurstCompile(CompileSynchronously = true)]
        private struct PopulateEntryJob : IJobParallelFor
        {

            public void Execute(int index)
            {
                this.entries[index] = new AgentsGridSearcher.SortEntry(this.hashIndex[index]);
            }


            [NativeDisableParallelForRestriction]
            public NativeArray<AgentsGridSearcher.SortEntry> entries;


            [ReadOnly]
            public NativeArray<int2> hashIndex;
        }


        [BurstCompile(CompileSynchronously = true)]
        private struct DePopulateEntryJob : IJobParallelFor
        {

            public void Execute(int index)
            {
                this.hashIndex[index] = this.entries[index].value;
            }


            [ReadOnly]
            public NativeArray<AgentsGridSearcher.SortEntry> entries;


            public NativeArray<int2> hashIndex;
        }


        public struct int2Comparer : IComparer<int2>
        {

            public int Compare(int2 lhs, int2 rhs)
            {
                return lhs.x.CompareTo(rhs.x);
            }
        }


        public static class ConcreteJobs
        {

            static ConcreteJobs()
            {
                default(MultithreadedSort.Merge<AgentsGridSearcher.SortEntry>).Schedule(default(JobHandle));
                default(MultithreadedSort.QuicksortJob<AgentsGridSearcher.SortEntry>).Schedule(default(JobHandle));
            }
        }


        public readonly struct SortEntry : IComparable<AgentsGridSearcher.SortEntry>
        {

            public SortEntry(int2 value)
            {
                this.value = value;
            }


            public int CompareTo(AgentsGridSearcher.SortEntry other)
            {
                int x = this.value.x;
                return x.CompareTo(other.value.x);
            }


            public readonly int2 value;
        }
    }
}
