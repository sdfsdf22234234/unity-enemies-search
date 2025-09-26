using System;

using UnityEngine;



public class GPUAnimator : MonoBehaviour
{




	public int ClipId
	{


		get
		{
			return m_ClipId;
		}


		set
		{
			m_ClipId = value;
		}
	}




	private void Awake()
	{
	}




	public GPUAnimator()
	{
	}



	private const string ClipIdKey = "_ClipId";




	[SerializeField]
	private MeshRenderer[] animMeshRenders;




	private int m_ClipId;
}
