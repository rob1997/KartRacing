using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ERMG
{
	public class RuntimeExample : MonoBehaviour
	{
		public ERMeshGen meshGen;
		private Camera cam;
		
		private void Update ()
		{
			if(cam == null)
				cam = GetComponent<Camera>();

			Ray _ray;
			_ray = cam.ScreenPointToRay(Input.mousePosition);

			RaycastHit _hit;

			if(Input.GetMouseButtonDown(0)) {
				if(Physics.Raycast(_ray, out _hit)) {
					var _navPoint = meshGen.CreateNavPoint();
					_navPoint.transform.position = _hit.point + _hit.normal * meshGen.groundOffset;
					meshGen.UpdateData();
				}
			}
		}

		private void OnGUI ()
		{
			GUILayout.Box("Click anywhere to create nav point");
			if(GUILayout.Button("Delete Last Point")){
				meshGen.DeleteNavPoint();
			}
		}
	}
}