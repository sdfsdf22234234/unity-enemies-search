using System;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace GPUAnimation.Runtime
{
	
	


		public struct GPUBoneData
		{
			public Vector3 Position { get; private set; }
			public Quaternion Rotation { get; private set; }
			public Vector3 Scale { get; private set; }
			public int CurrentFrame { get; private set; }
			public GPUBoneData(Vector3 pos, Quaternion rot, Vector3 scale, int curFrame)
			{
				this.Position = pos;
				this.Rotation = rot;
				this.Scale = scale;
				this.CurrentFrame = curFrame;
			}
		}



		
	
}
