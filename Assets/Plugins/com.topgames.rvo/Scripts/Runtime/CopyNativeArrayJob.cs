using System; 
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Nebukam.ORCA
{ 
	[BurstCompile]
	public struct CopyNativeArrayJob<T> : IJobParallelFor where T : struct
	{ 
		public void Execute(int index)
		{
			ArrDst[index] = ArrSrc[index];
		}

	 
		[ReadOnly]
		public NativeArray<T> ArrSrc;

		 
		[NativeDisableParallelForRestriction]
		[WriteOnly]
		public NativeArray<T> ArrDst;
	}
}