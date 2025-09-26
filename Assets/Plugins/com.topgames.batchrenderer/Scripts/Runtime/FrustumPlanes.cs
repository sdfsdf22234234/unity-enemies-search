using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace BRGExtension
{

    public struct FrustumPlanes
    {

        [BurstCompile(CompileSynchronously = true)]
        public static bool CheckCulling(NativeArray<FrustumPlanes.PlanePacket4> cullingPlanes, NativeArray<CullingSplit> cullingSplits, AABB aabb)
        {
            return FrustumPlanes.Intersect2NoPartialMulti(cullingPlanes, cullingSplits, aabb) == 0U;
        }


        [BurstCompile(CompileSynchronously = true)]
        public static bool CheckCulling(NativeArray<Plane> cullingPlanes, AABB aabb)
        {
            return FrustumPlanes.Intersect(cullingPlanes, aabb) == FrustumPlanes.IntersectResult.Out;
        }


        [BurstCompile(CompileSynchronously = true)]
        public static FrustumPlanes.IntersectResult Intersect(NativeArray<Plane> cullingPlanes, AABB a)
        {
            float3 center = a.Center;
            float3 extents = a.Extents;
            int num = 0;
            for (int i = 0; i < cullingPlanes.Length; i++)
            {
                float3 x = cullingPlanes[i].normal;
                float num2 = math.dot(x, center) + cullingPlanes[i].distance;
                float num3 = math.dot(extents, math.abs(x));
                if (num2 + num3 <= 0f)
                {
                    return FrustumPlanes.IntersectResult.Out;
                }
                if (num2 > num3)
                {
                    num++;
                }
            }
            if (num != cullingPlanes.Length)
            {
                return FrustumPlanes.IntersectResult.Partial;
            }
            return FrustumPlanes.IntersectResult.In;
        }


        [BurstCompile(CompileSynchronously = true)]
        public static NativeArray<FrustumPlanes.PlanePacket4> BuildSOAPlanePacketsMulti(NativeArray<Plane> cullingPlanes, NativeArray<CullingSplit> splitCounts, Allocator allocator)
        {
            int num = 0;
            for (int i = 0; i < splitCounts.Length; i++)
            {
                num += splitCounts[i].cullingPlaneCount + 3 >> 2;
            }
            NativeArray<FrustumPlanes.PlanePacket4> result = new NativeArray<FrustumPlanes.PlanePacket4>(num, allocator, NativeArrayOptions.UninitializedMemory);
            int num2 = 0;
            int num3 = 0;
            for (int j = 0; j < splitCounts.Length; j++)
            {
                int cullingPlaneCount = splitCounts[j].cullingPlaneCount;
                int num4 = cullingPlaneCount + 3 >> 2;
                for (int k = 0; k < cullingPlaneCount; k++)
                {
                    FrustumPlanes.PlanePacket4 value = result[num2 + (k >> 2)];
                    value.Xs[k & 3] = cullingPlanes[num3 + k].normal.x;
                    value.Ys[k & 3] = cullingPlanes[num3 + k].normal.y;
                    value.Zs[k & 3] = cullingPlanes[num3 + k].normal.z;
                    value.Distances[k & 3] = cullingPlanes[num3 + k].distance;
                    result[num2 + (k >> 2)] = value;
                }
                for (int l = cullingPlaneCount; l < 4 * num4; l++)
                {
                    FrustumPlanes.PlanePacket4 value2 = result[num2 + (l >> 2)];
                    value2.Xs[l & 3] = 1f;
                    value2.Ys[l & 3] = 0f;
                    value2.Zs[l & 3] = 0f;
                    value2.Distances[l & 3] = 1E+09f;
                    result[num2 + (l >> 2)] = value2;
                }
                num3 += cullingPlaneCount;
                num2 += num4;
            }
            return result;
        }


        [BurstCompile(CompileSynchronously = true)]
        public static uint Intersect2NoPartialMulti(NativeArray<FrustumPlanes.PlanePacket4> cullingPlanePackets, NativeArray<CullingSplit> splitCounts, AABB a)
        {
            float4 xxxx = a.Center.xxxx;
            float4 yyyy = a.Center.yyyy;
            float4 zzzz = a.Center.zzzz;
            float4 xxxx2 = a.Extents.xxxx;
            float4 yyyy2 = a.Extents.yyyy;
            float4 zzzz2 = a.Extents.zzzz;
            uint num = 0U;
            int num2 = 0;
            for (int i = 0; i < splitCounts.Length; i++)
            {
                int num3 = splitCounts[i].cullingPlaneCount + 3 >> 2;
                int4 @int = 0;
                for (int j = 0; j < num3; j++)
                {
                    FrustumPlanes.PlanePacket4 planePacket = cullingPlanePackets[num2 + j];
                    float4 lhs = FrustumPlanes.dot4(planePacket.Xs, planePacket.Ys, planePacket.Zs, xxxx, yyyy, zzzz) + planePacket.Distances;
                    float4 rhs = FrustumPlanes.dot4(xxxx2, yyyy2, zzzz2, math.abs(planePacket.Xs), math.abs(planePacket.Ys), math.abs(planePacket.Zs));
                    @int += (int4)(lhs + rhs <= 0f);
                }
                if (math.csum(@int) == 0)
                {
                    num |= 1U << i;
                }
                num2 += num3;
            }
            return num;
        }


        [BurstCompile(CompileSynchronously = true)]
        public static NativeArray<FrustumPlanes.PlanePacket4> BuildSOAPlanePackets(NativeArray<Plane> cullingPlanes, int offset, int count, Allocator allocator)
        {
            int num = count + 3 >> 2;
            NativeArray<FrustumPlanes.PlanePacket4> result = new NativeArray<FrustumPlanes.PlanePacket4>(num, allocator, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < count; i++)
            {
                FrustumPlanes.PlanePacket4 value = result[i >> 2];
                value.Xs[i & 3] = cullingPlanes[i + offset].normal.x;
                value.Ys[i & 3] = cullingPlanes[i + offset].normal.y;
                value.Zs[i & 3] = cullingPlanes[i + offset].normal.z;
                value.Distances[i & 3] = cullingPlanes[i + offset].distance;
                result[i >> 2] = value;
            }
            for (int j = count; j < 4 * num; j++)
            {
                FrustumPlanes.PlanePacket4 value2 = result[j >> 2];
                value2.Xs[j & 3] = 1f;
                value2.Ys[j & 3] = 0f;
                value2.Zs[j & 3] = 0f;
                value2.Distances[j & 3] = 1E+09f;
                result[j >> 2] = value2;
            }
            return result;
        }


        [BurstCompile(CompileSynchronously = true)]
        public static FrustumPlanes.IntersectResult Intersect2NoPartial(NativeArray<FrustumPlanes.PlanePacket4> cullingPlanePackets, AABB a)
        {
            float4 xxxx = a.Center.xxxx;
            float4 yyyy = a.Center.yyyy;
            float4 zzzz = a.Center.zzzz;
            float4 xxxx2 = a.Extents.xxxx;
            float4 yyyy2 = a.Extents.yyyy;
            float4 zzzz2 = a.Extents.zzzz;
            int4 @int = 0;
            for (int i = 0; i < cullingPlanePackets.Length; i++)
            {
                FrustumPlanes.PlanePacket4 planePacket = cullingPlanePackets[i];
                float4 lhs = FrustumPlanes.dot4(planePacket.Xs, planePacket.Ys, planePacket.Zs, xxxx, yyyy, zzzz) + planePacket.Distances;
                float4 rhs = FrustumPlanes.dot4(xxxx2, yyyy2, zzzz2, math.abs(planePacket.Xs), math.abs(planePacket.Ys), math.abs(planePacket.Zs));
                @int += (int4)(lhs + rhs <= 0f);
            }
            if (math.csum(@int) <= 0)
            {
                return FrustumPlanes.IntersectResult.In;
            }
            return FrustumPlanes.IntersectResult.Out;
        }


        [BurstCompile(CompileSynchronously = true)]
        private static float4 dot4(float4 xs, float4 ys, float4 zs, float4 mx, float4 my, float4 mz)
        {
            return xs * mx + ys * my + zs * mz;
        }


        public enum IntersectResult
        {

            Out,

            In,

            Partial
        }


        public struct PlanePacket4
        {

            public float4 Xs;


            public float4 Ys;


            public float4 Zs;


            public float4 Distances;
        }
    }
}
