using System.Collections;
using System.Collections.Generic;
using SETUtil.Extend;
using UnityEngine;

namespace ERMG
{
	public static class Util
	{
		#if UNITY_EDITOR
		
			[UnityEditor.MenuItem("GameObject/3D Object/ER Mesh Gen")]
			public static void CreateERMeshGen ()
			{
				var newObject = new GameObject("ERMeshGen");
				var meshGen = newObject.AddComponent<ERMeshGen>();
				meshGen.CreateNavPoint();
				meshGen.CreateNavPoint();
				meshGen.GetComponent<MeshRenderer>().sharedMaterial = GetDefaultMaterial();
				meshGen.pointControl = PointControl.Automatic;
			}

		#endif

		public static Vector3  GetTangent ( Vector3 d ,   Vector3 e  )
		{ //get the tangent of the subdivision curve
			return (d - e)/Vector3.Distance(d,e);
		}

		public static Vector3  GetBinormal (Vector3 tng, Vector3 upVectorA, Vector3 upVectorB, float cof)
		{ //get the normal (y dir) of the subdivision curve
			Vector3 binormal = Vector3.Cross(Vector3.Lerp(upVectorA, upVectorB, cof), tng).normalized;
			return binormal;
		}

		public static Vector3 GetNormal (Vector3 tng, Vector3 bnrm)
		{
			return Vector3.Cross(tng, bnrm);
		}
		
		public static Material GetDefaultMaterial ()
		{
			var _sample = GameObject.CreatePrimitive(PrimitiveType.Quad);
			_sample.hideFlags = HideFlags.DontSave;
			var _material = _sample.GetComponent<Renderer>().sharedMaterial;
			SETUtil.SceneUtil.SmartDestroy(_sample);
			return _material;
		}

		public static OrientationData[] TransformToOrientationArray (Transform[] trArr)
		{
			OrientationData[] _p = new OrientationData[trArr.Length];
			for(int i = 0; i < _p.Length; i++){
				_p[i] = new OrientationData();
				if(trArr[i])
					_p[i].Set(trArr[i].position, trArr[i].forward, trArr[i].up);
			}
			return _p;
		}
		
		public static List<Transform> FindMatchingChildren (Transform parent, string baseName, System.Type tp = null)
		{
			List<Transform> _pts = new List<Transform>(0);
			foreach (Transform t in parent){
				if(t.name.Contains(baseName) || (tp != null && t.GetComponent(tp) != null)){ 
					_pts.Add(t);
				}
			}
			return _pts;
		}

		public static void ExportToOBJ (GameObject root)
		{
			var path = Application.dataPath;

			#if UNITY_EDITOR
				path = UnityEditor.EditorUtility.OpenFolderPanel("Export OBJ", path, "MeshGenExport");
			#else
				path = SETUtil.FileUtil.CreateFolderPathString("MeshGen", true);
			#endif

			var objectsToExport = new List<GameObject>();
			objectsToExport.AddRange(SETUtil.SceneUtil.CollectAllChildren(root.transform, true).ToGameObjectArray());
			foreach(var objectToExport in objectsToExport) {
				if(objectToExport.GetComponent<MeshFilter>()) {
					SETUtil.MeshExporter.OBJExporter.ExportObject(path + "/" + objectToExport.name + ".obj", objectToExport);
				}
			}
		}
	}
	
	public enum PointControl
	{
		Manual,
		//Parented,
		Automatic,
	}

	public enum RuntimeBehaviorOption
	{
		FollowUpdateMode,
		Manual,
		Realtime,
	}

	public enum UpdateMode
	{
		Automatic,
		Manual,
		VerticesOnly,
		Realtime,
	}

	public class Border 
	{
		public GameObject gameObject;
		public MeshFilter meshFilter;
		public MeshCollider collider;
	}

	public class OrientationData
	{
		public Vector3
			position = Vector3.zero,
			tangent = Vector3.forward,
			binormal = Vector3.up;
		
		public OrientationData () {
			position = Vector3.zero;
			tangent = Vector3.forward;
			binormal = Vector3.up;
		}
		
		public OrientationData (Vector3 pos, Vector3 tan, Vector3 bnrm){
			Set(pos, tan, bnrm);
		}
		
		public void Set (Vector3 pos, Vector3 tan, Vector3 bnrm){
			position = pos;
			tangent = tan;
			binormal = bnrm;
		}
		
		public Quaternion ToQuaternion () {
			return Quaternion.LookRotation(tangent, binormal);
		}
	}
	
	public class PointData : OrientationData
	{
		public float distance = 0f;
		
		public PointData () : base() {
			distance = 0f;
		}
	}

	[System.Serializable]
	public class NavPointReference
	{
		public Transform transform;
		
		public GameObject gameObject {
			get{ return (transform != null) ? transform.gameObject : null; }
		}

		public Vector3 forward {
			get{ return transform.forward; }
		}

		public Vector3 up {
			get{ return transform.up; }
		}

		public Vector3 right {
			get{ return transform.right; }
		}

		public Vector3 position {
			get{ return transform.position; }
			set{ transform.position = value; }
		}

		public Vector3 localScale {
			get{ return transform.localScale; }
			set{ transform.localScale = value; }
		}

		public Quaternion rotation {
			get{ return transform.rotation; }
			set{ transform.rotation = value; }
		}

		public Vector3 eulerAngles {
			get{ return transform.eulerAngles; }
			set{ transform.eulerAngles = value; }
		}

		public string name {
			get{ return transform.name; }
			set{ transform.name = value; }
		}


		public ERPointSnap pointSnapComponent;
		public ERNavPoint navPointComponent;

		public NavPointReference(){}

		public NavPointReference(Transform transform)
		{
			this.transform = transform;
			pointSnapComponent = transform.GetComponent<ERPointSnap>();
			navPointComponent = transform.GetComponent<ERNavPoint>();
		}

		public void Update ()
		{
			if(pointSnapComponent != null) {
				pointSnapComponent.UpdatePos();
			}
		}

		public void SetParent (Transform transform)
		{
			this.transform.SetParent(transform);
		}
	}
	
	//extension methods
	public static class ERMGExtend
	{
		public static void ValidateComponent <T> (this GameObject gameObject, ref T componentVar) where T : Component
		{
			if(componentVar != null)
				return;
			componentVar = gameObject.GetComponent<T>();
			if(componentVar == null)
				componentVar = gameObject.AddComponent<T>();
		}

		public static PointData[] ToPointDataArray(this OrientationData[] o){
			PointData[] _p = new PointData[o.Length];
			for(int i = 0; i < o.Length; i++){
				_p[i] = new PointData();
				if(o[i] != null){
					_p[i].Set(o[i].position, o[i].tangent, o[i].binormal);
				}
			}
			
			return _p;
		}
 
		/// <summary>
		/// Custom nav points equality check
		/// </summary>
		public static bool EqualsTo (this NavPointReference lhs, NavPointReference rhs)
		{
			//null check
			if(rhs == null) { 
				return lhs == null || lhs.transform == null;	
			}

			//compare
			return lhs == rhs && lhs.transform == rhs.transform;
		}
	}
}