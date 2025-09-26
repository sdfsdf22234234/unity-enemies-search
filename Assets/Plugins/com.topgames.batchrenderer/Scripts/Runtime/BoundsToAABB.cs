#if UNITY_2022_1_OR_NEWER
using UnityEngine;

namespace BRGExtension
{
    public static class AABBExtensions
    {
        public static AABB ConvertToAABB(this Bounds bounds)
        {
            return new AABB { Center = bounds.center, Extents = bounds.extents };
        }

        public static Bounds ConvertToBounds(this AABB aabb)
        {
            return new Bounds { center = aabb.Center, extents = aabb.Extents };
        }
    }
}
#endif
