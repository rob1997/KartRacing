using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ERMG
{
	[
		AddComponentMenu("Easy Roads Mesh Gen/Extensions/Array"),
		ExecuteInEditMode
	]
	public class EasyRoadsMeshGen_Array : MonoBehaviour
	{
		//types:
		public enum FitType
		{
			FixedAmount,
			FitLength,
			FitPath
		}

		[System.Serializable]
		public class ArrayObject
		{
			public GameObject prefab;
			public FitType fitType = FitType.FixedAmount;

			public int amount = 0;

			public float
				length,
				verticalOffset,
				horizontalOffset,
				pathOffset,
				rotation;
			public bool invert;

			public List<GameObject> instances = new List<GameObject>();

			public void Clear()
			{
				for (int i = 0; i < instances.Count; i++)
					if (instances[i] != null)
						SETUtil.SceneUtil.SmartDestroy(instances[i]);

				instances = new List<GameObject>();
			}
		}
		//--

		public float length;
		public ERPathTracer path = null;
		public List<ArrayObject> arrayObjects = new List<ArrayObject>();

		[System.NonSerialized]
		private bool subscribedToPath = false;

		public void SubToPath(ERPathTracer path)
		{
			path.onUpdatePoints -= UpdateData;
			path.onUpdatePoints += UpdateData;
			subscribedToPath = true;
		}

		public void UnsubFromPath()
		{
			if (path)
				path.onUpdatePoints -= UpdateData;
			subscribedToPath = false;
		}

		void UpdateSubscription()
		{
			if (path && !subscribedToPath)
				SubToPath(path);

			if (!path && subscribedToPath)
				UnsubFromPath();
		}

		void Update()
		{
			UpdateSubscription();

			if (!subscribedToPath) //if the array is not dependent on a path then update autonomously 
				UpdateData();
		}

		void OnDestroy()
		{
			UnsubFromPath();
			if (!ERMeshGen.dontClearOnDestroy)
			{
				Clear();
			}
		}

		public void UpdateData()
		{
			if (arrayObjects != null)
			{
				for (int i = 0; i < arrayObjects.Count; i++)
				{
					var arrayObject = arrayObjects[i];
					FindExistingInstances(arrayObject);

					if (path)
						path.TracePath(i, arrayObject.horizontalOffset, arrayObject.verticalOffset);

					if (arrayObject.fitType != FitType.FixedAmount)
						arrayObject.amount = CalculateFit(arrayObject.fitType, arrayObject.length);

					Generate(i);
					PositionInstances(i);
				}
			}
		}

		void Generate(int objectIndex)
		{
			var arrayObject = arrayObjects[objectIndex];
			arrayObject.amount = Mathf.Max(arrayObject.amount, 0);

			if(arrayObject.amount != arrayObject.instances.Count){
				Clear();
			}

#if UNITY_EDITOR
			UnityEditor.SerializedObject _so = new UnityEditor.SerializedObject(this);
			UnityEditor.SerializedProperty
				_so_arrayObjects = _so.FindProperty("arrayObjects"),
				_so_arrayObjectsEl = _so_arrayObjects.GetArrayElementAtIndex(objectIndex),
				_so_arrayObjects_instances = _so_arrayObjectsEl.FindPropertyRelative("instances");
			_so_arrayObjects_instances.arraySize = arrayObject.amount;
			_so.ApplyModifiedPropertiesWithoutUndo();
			_so.Update();
#else
			if(arrayObject.instances == null || arrayObject.instances.Count != arrayObject.amount)
				arrayObject.instances = new List<GameObject>();
#endif

			GameObject _instance = null;
			Vector3 _pos = transform.position;

			for (int i = 0; i < arrayObject.amount; i++)
			{
				if (!arrayObject.instances[i])
				{
					_pos = transform.position + transform.forward * i * arrayObject.length;
					_instance = SETUtil.SceneUtil.Instantiate(arrayObject.prefab, _pos);
					_instance.transform.SetParent(this.transform);
#if UNITY_EDITOR
					UnityEditor.SerializedProperty _so_arrayObjects_instances_e = _so_arrayObjects_instances.GetArrayElementAtIndex(i);
					_so_arrayObjects_instances_e.objectReferenceValue = _instance;
#else
					arrayObject.instances[i] = _instance;
#endif
				}
			}

#if UNITY_EDITOR
			_so.ApplyModifiedPropertiesWithoutUndo();
#endif
		}

		void FindExistingInstances(ArrayObject arrayObject)
		{
			if (arrayObject.prefab != null)
			{
				foreach (Transform child in transform)
				{
					if (child.name.Equals(arrayObject.prefab.name))
					{
						if (!arrayObject.instances.Contains(child.gameObject))
						{
							arrayObject.instances.Add(child.gameObject);
						}
					}
				}
			}
		}

		void PositionInstances(int objectIndex)
		{
			Quaternion _addedRotation = Quaternion.Euler(Vector3.up * arrayObjects[objectIndex].rotation);

			for (int i = 0; i < arrayObjects[objectIndex].instances.Count; i++)
			{
				if (arrayObjects[objectIndex].instances[i])
				{
					float _distance = (float)i * arrayObjects[objectIndex].length + arrayObjects[objectIndex].pathOffset;
					OrientationData _o = (path) ?
						path.Evaluate(objectIndex, _distance + (arrayObjects[objectIndex].invert ? arrayObjects[objectIndex].length : 0f), arrayObjects[objectIndex].length * (arrayObjects[objectIndex].invert ? -1f : 1f))
						: new OrientationData(transform.position + transform.forward * _distance, transform.forward, transform.up);
					Quaternion _rot = Quaternion.LookRotation(_o.tangent, _o.binormal) * _addedRotation;
					arrayObjects[objectIndex].instances[i].transform.position = _o.position;
					arrayObjects[objectIndex].instances[i].transform.rotation = _rot;
				}
			}
		}

		public void Clear()
		{
			foreach (var arrayObject in arrayObjects)
				arrayObject.Clear();
		}

		public float GetAutoLength(int objectIndex)
		{
			float output = 0f;
			GameObject _prefab = arrayObjects[objectIndex].prefab;
			if (_prefab)
			{
				MeshFilter _meshFilter = _prefab.GetComponent<MeshFilter>();

			outp:
				if (_meshFilter)
				{
					output = _meshFilter.sharedMesh.bounds.size.z * 2f;
				}
				else
				{
					foreach (Transform child in _prefab.transform)
					{
						MeshFilter _meshFilterInChild = child.GetComponent<MeshFilter>();
						if (_meshFilterInChild)
						{
							_meshFilter = _meshFilterInChild;
							goto outp;
						}
					}
				}
			}

			return output;
		}

		///<sumamry> Calculate how many objects can fit the given path length </summary>
		private int CalculateFit(FitType fitType, float objectLength)
		{
			//set the AMOUNT variable based on fit length
			if (fitType == FitType.FitPath)
			{
				var _fitLength = path.distanceRecord;
				int _fitAmount = (int)Mathf.Floor(_fitLength / objectLength);
				return _fitAmount;
			}

			if (fitType == FitType.FitLength)
			{
				int _fitAmount = (int)Mathf.Floor(length / objectLength);
				return _fitAmount;
			}

			return 0;
		}
	}
}
