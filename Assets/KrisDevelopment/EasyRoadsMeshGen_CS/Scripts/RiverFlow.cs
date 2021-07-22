using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ERMG
{
	[AddComponentMenu("Easy Roads Mesh Gen/River Flow")]
	[RequireComponent(typeof(Renderer))]
	public class RiverFlow : MonoBehaviour
	{
		public Vector2 direction = new Vector2(0, 1);

		public bool  bumpmap = true;

		private float x = 0;
		private float y = 0;

		private Material targetMaterial;

		void  Update (){
			if(targetMaterial == null)
				targetMaterial = GetComponent<Renderer>().material;

			x += direction.x * Time.deltaTime;
			y += direction.y * Time.deltaTime;
			x %= 1.0f;
			y %= 1.0f;
			
			targetMaterial.SetTextureOffset("_MainTex", new Vector2(x,y));
			if(bumpmap)
				targetMaterial.SetTextureOffset("_BumpMap", new Vector2(x,y));
		}
	}
}