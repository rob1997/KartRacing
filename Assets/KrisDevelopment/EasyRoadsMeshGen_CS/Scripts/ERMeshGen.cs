using System.Collections.Generic;
using UnityEngine;
using ERMG;

#if UNITY_EDITOR
using UnityEditor;
#endif

[
	AddComponentMenu("Easy Roads Mesh Gen/Mesh Gen"),
	ExecuteInEditMode
]
public class ERMeshGen : MonoBehaviour
{
	public enum UnwrapOption
	{
		PerSegment = 0,
		TopProject = 1,
		WidthToLength = 2,
		StretchSingleTexture = 3,
	}

	public enum BorderUnwarpOption
	{
		StraightUnwrap = 0,
		TopProject = 1,
	}

	private class TerrainStrokeInfo
	{
		public Vector3 terrainSize;
		public float heightTexelDistanceX;
		public float heightTexelDistanceY;
		public float radiusFlat;
		public float radiusSmooth;
		public float totalRaidus;
		public int heightmapWidth;
		public int heightmapHeight;
		// public int detailmapWidth;
		// public int detailmapHeight;
		public Vector3 terrainPosition;
		public int brushSamplesX;
		public int brushSamplesY;
		public OrientationData[] cachedPathPoints;
	}


	public System.Action onUpdateData;

	public const string NAV_POINT_NAMES = "Nav Point";
	public const string RIGHT_BORDER_NAME = "rightBorderMeshObj";
	public const string LEFT_BORDER_NAME = "leftBorderMeshObj";

	public static ERMeshGen lastSelectedMeshGen;
	public static bool dontClearOnDestroy = false;

	public List<NavPointReference> navPoints = new List<NavPointReference>();

	public float deltaWidth = 1.2f;
	public int subdivision = 1;
	public float uvScale = 1;
	public Vector3[] navPointsBeta_p = new Vector3[0]; //nav points positions after subdivision
	private Vector3[] newVertices = new Vector3[0];
	private Vector2[] newUV = new Vector2[0];
	private int[] newTriangles = new int[0];
	private int[] quadMatrix = { 2, 1, 0, 2, 3, 1 };
	private int uvSetCount = 0;

	public float groundOffset = 0.1f;

	public PointControl pointControl = PointControl.Manual;

	public int
		updatePointsMode = 0,
		includeCollider = 1,
		enableMeshBorders = 0;

	private const int terrainBrushFrequency = 1;
	public float terrainBrushSmoothingRange = 1f;

	public UnwrapOption uvSet = UnwrapOption.WidthToLength;
	public RuntimeBehaviorOption runtimeBehavior = RuntimeBehaviorOption.Manual;

	public AnimationCurve borderCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.3f, 0.6f), new Keyframe(1, 0.6f)); //the points in 2d plane
	public float borderUvScale = 1f;
	public BorderUnwarpOption borderUvSet = BorderUnwarpOption.StraightUnwrap;

	public GameObject
		leftBorder,
		rightBorder;

	private Vector2[] borderSectionPoints; //the points in the 2d section graph
	private Vector3[] leftBorderVertices;
	private Vector2[] leftUV = new Vector2[0];
	private int[] leftTriangles = new int[0];
	private Vector3[] rightBorderVertices;
	private Vector2[] rightUV = new Vector2[0];
	private int[] rightTriangles = new int[0];
	private int borderCount = 0;
	private MeshFilter meshFilter;


	private MeshRenderer meshRenderer;
	private MeshCollider meshCollider;


#if UNITY_EDITOR

	void OnDrawGizmos()
	{
		if (updatePointsMode == (int)UpdateMode.Automatic)
		{
			if (!SelectionContainsSource())
				return;
		}

		for (int v = 0; v < newVertices.Length; v++)
			Gizmos.DrawIcon(newVertices[v] + transform.position, "EasyRoadsMeshGen/vertex_icon.png", true);
	}

	void OnEnable()
	{
		UpdateNavPoints();
	}

#endif

	void Update()
	{
		if (Application.isPlaying)
		{
			if (runtimeBehavior == RuntimeBehaviorOption.Manual)
			{
				updatePointsMode = (int)UpdateMode.Manual;
			}
			else if (runtimeBehavior == RuntimeBehaviorOption.Realtime)
			{
				updatePointsMode = (int)UpdateMode.Realtime;
			}
		}

		if (updatePointsMode == (int)UpdateMode.Manual)
			return;

#if UNITY_EDITOR
		if (updatePointsMode == (int)UpdateMode.Automatic)
		{
			if (!SelectionContainsSource())
				return;
		}
#endif


		if (updatePointsMode == (int)UpdateMode.VerticesOnly)
		{
			subdivision = Mathf.Max(subdivision, 1);
			SetVerts();
		}

		if (updatePointsMode == (int)UpdateMode.Realtime || updatePointsMode == (int)UpdateMode.Automatic)
			UpdateData();
	}

#if UNITY_EDITOR
	private bool SelectionContainsSource()
	{
		bool _contains = false;
		if (!Selection.Contains(gameObject))
		{
			for (int s = 0; s < navPoints.Count; s++)
			{
				if (!navPoints[s].EqualsTo(null))
				{
					if (Selection.Contains(navPoints[s].gameObject))
						_contains = true;
					else
					{
						var _erPointSnapComponent = navPoints[s].pointSnapComponent;
						if (_erPointSnapComponent && _erPointSnapComponent.snapped && _erPointSnapComponent.snappedToPoint != null)
						{
							if (Selection.Contains(_erPointSnapComponent.snappedToPoint.root.gameObject) ||
								Selection.Contains(_erPointSnapComponent.snappedToPoint.gameObject))
							{
								_contains = true;
							}
						}
					}
				}
			}
		}
		else
			_contains = true;
		return _contains;
	}
#endif

	public void UpdateData()
	{
		subdivision = Mathf.Max(subdivision, 1);

#if UNITY_EDITOR
		for (int key = 0; key < borderCurve.length; key++)
		{
			AnimationUtility.SetKeyLeftTangentMode(borderCurve, key, AnimationUtility.TangentMode.Linear);
			AnimationUtility.SetKeyRightTangentMode(borderCurve, key, AnimationUtility.TangentMode.Linear);
		}
#endif

		GenerateMesh();

		if (onUpdateData != null)
			onUpdateData();
	}

	///<summary>
	/// Creates a new nav point and adds it at the specified index,
	/// if no index has been specified the new point will be added as last.
	///</summary>
	public GameObject CreateNavPoint(int? insertAtIndex = null)
	{
		if (navPoints.Count > 0 && insertAtIndex != null)
		{
			var i = (int)insertAtIndex;
			var _erPointSnapComponent = navPoints[i].pointSnapComponent;
			if (_erPointSnapComponent != null && _erPointSnapComponent.snapped)
			{
				Debug.LogWarning("Easy Roads: End point " + navPoints[i].gameObject.name + " is snapped. Unsnap it to add new points!");
				return null;
			}
		}

		GameObject navPointObject = new GameObject();
		navPointObject.name = NAV_POINT_NAMES + " " + navPoints.Count;
		navPointObject.transform.SetParent(transform);
		navPointObject.AddComponent<ERNavPoint>();
		navPointObject.GetComponent<ERNavPoint>().assignedMeshGen = this;
		navPointObject.AddComponent<ERPointSnap>();

#if UNITY_EDITOR
		if (navPoints.Count >= 1)
			Selection.activeGameObject = navPointObject;
#endif

		if (navPoints.Count > 0)
		{
			int _poistionIndex = insertAtIndex ?? navPoints.Count - 1;
			navPointObject.transform.position = navPoints[_poistionIndex].transform.forward * deltaWidth + navPoints[_poistionIndex].position;
			navPointObject.transform.rotation = navPoints[_poistionIndex].rotation;
		}
		else
		{
			navPointObject.transform.position = transform.position;
		}

		if (insertAtIndex == null)
		{
			navPoints.Add(new NavPointReference(navPointObject.transform));
		}
		else
		{
			navPoints.Insert((int)insertAtIndex + 1, new NavPointReference(navPointObject.transform));
		}

		UpdateNavPoints();
		UpdateData();

		return navPointObject;
	}

	///<summary>
	/// Deletes the specified NavPoint,
	/// if no nav point index has been provided the last added one will be deleted.
	///</summary>
	public void DeleteNavPoint(int? removeAtIndex = null)
	{
		int _index = removeAtIndex ?? navPoints.Count - 1;
		SETUtil.SceneUtil.SmartDestroy(navPoints[_index].gameObject);
		navPoints.RemoveAt(_index);
		UpdateNavPoints();
		UpdateData();
	}

	private void GenerateMesh()
	{
		subdivision = Mathf.Max(subdivision, 1);

		gameObject.ValidateComponent(ref meshFilter);
		gameObject.ValidateComponent(ref meshRenderer);

		UpdateNavPoints();
		SetVerts();
		SetTriangles();
		SetUVs();

		if (enableMeshBorders == 1)
			SetBorderUVs();

		Mesh mesh = new Mesh();
		meshFilter.sharedMesh = mesh;
		mesh.vertices = newVertices;
		mesh.uv = newUV;
		mesh.triangles = newTriangles;
		mesh.RecalculateNormals();

		EnableBorders(enableMeshBorders == 1);
		UpdateBorders();
		UpdateCollider(mesh);
	}

#if UNITY_EDITOR
	public void OnDuplicationEvent()
	{
		for (int a = 0; a < navPoints.Count; a++)
		{
			if (!navPoints[a].EqualsTo(null))
			{
				if (navPoints[a].pointSnapComponent != null)
				{
					navPoints[a].pointSnapComponent.ClearSnap();
				}
			}
		}
	}
#endif

	private void UpdateNavPoints()
	{
		if (navPoints.Count == 0)
		{
			FindNavPoints();
		}

		navPoints.RemoveAll(a => a.EqualsTo(null) || a.gameObject == null);
		ScaleControlNavPoints();
		UpdateNavPointIndexes();

		const float SCALE_DIVISOR = 2.5f;
		const float MIN_SCALE = 0.5f;

		for (int _current = 0; _current < navPoints.Count; _current++)
		{
			// assign the point rotation
			int _previous = Mathf.Max(0, _current - 1); //previous point in array
			int _previousPrim = Mathf.Max(0, _previous - 1);
			int _next = Mathf.Min(navPoints.Count - 1, _current + 1); //next point in array

			var _previousPrimPoint = navPoints[_previousPrim];
			var _previousPoint = navPoints[_previous];
			var _currentPoint = navPoints[_current];
			var _nextPoint = navPoints[_next];

			if (pointControl == PointControl.Automatic)
			{
				// calculate orientation
				if (_currentPoint.pointSnapComponent == null || !_currentPoint.pointSnapComponent.snapped)
				{

					float _zScalePrevious =
					Mathf.Max(
						Mathf.Min(
							Vector3.Distance(_previousPoint.position, _previousPrimPoint.position),
							Vector3.Distance(_previousPoint.position, _currentPoint.position)
						) / SCALE_DIVISOR,
						MIN_SCALE
					);

					Vector3 _forwardPrev = (_currentPoint.position - _previousPoint.position).normalized;
					Vector3 _forwardNext = (_nextPoint.position - _currentPoint.position).normalized;

					if (_previous == _current)
					{
						_currentPoint.rotation = Quaternion.LookRotation(_forwardNext, transform.up);
					}
					else if (_next == _current)
					{
						_currentPoint.rotation = Quaternion.LookRotation(_forwardPrev, transform.up);
					}
					else
					{
						Vector3 _previousBezeirPoint = _previousPoint.position + _previousPoint.forward * _zScalePrevious;
						Vector3 _previousBias = (_currentPoint.position - _previousBezeirPoint).normalized;
						Vector3 _nextBias = (_nextPoint.position - _currentPoint.position).normalized;

						float _distanceFromPrevious = Vector3.Distance(_currentPoint.position, _previousPoint.position);
						float _lerp = _distanceFromPrevious / (_distanceFromPrevious + Vector3.Distance(_currentPoint.position, _nextPoint.position));
						_currentPoint.rotation = Quaternion.LookRotation(Vector3.Slerp(_previousBias, _nextBias, _lerp), transform.up);
					}
				}
				else
				{
					_currentPoint.pointSnapComponent.UpdatePos();
				}

				// scale z for better curvature
				float _zScale =
					Mathf.Max(
						Mathf.Min(
							Vector3.Distance(_currentPoint.position, _previousPoint.position),
							Vector3.Distance(_currentPoint.position, _nextPoint.position)
						) / SCALE_DIVISOR,
						MIN_SCALE
					);

				if (_next != _current && _previous == _current)
				{
					// correct z scale for the first element
					_zScale = Mathf.Max(Vector3.Distance(_currentPoint.position, _nextPoint.position) / 2f, 1f);
				}
				else if (_next == _current && _previous != _current)
				{
					// correct z scale for the last element
					_zScale = Mathf.Max(Vector3.Distance(_currentPoint.position, _previousPoint.position) / 2f, 1f);
				}

				// apply z scale
				_currentPoint.localScale = new Vector3(_currentPoint.localScale.x, _currentPoint.localScale.y, _zScale);
			}
		}
	}

	///<summary> Updates the nav point names and their component index records. This is a somewhat expensive operation. </summary>
	private void UpdateNavPointIndexes()
	{
		//rename the points to match their index
		for (int i = 0; i < navPoints.Count; i++)
		{
			navPoints[i].name = NAV_POINT_NAMES + " " + i;
			var _navPointComp = navPoints[i].navPointComponent;

			if (_navPointComp != null)
				_navPointComp.SetIndex(i);
		}
	}

	private void ScaleControlNavPoints()
	{
		foreach (var navPoint in navPoints)
		{
			if (!navPoint.EqualsTo(null))
			{
				navPoint.localScale = new Vector3(Mathf.Max(navPoint.localScale.x, 0.001f), Mathf.Max(navPoint.localScale.y, 0.001f), Mathf.Max(navPoint.localScale.z, 0.001f));
			}
		}
	}

	private void SetVerts()
	{
		if (navPoints.Count > 0)
		{
			navPointsBeta_p = new Vector3[(navPoints.Count - 1) * subdivision];
		}
		else
		{
			navPointsBeta_p = new Vector3[0];
		}

		newVertices = new Vector3[(navPointsBeta_p.Length + 1) * 2];
		borderSectionPoints = new Vector2[borderCurve.length];

		for (int v = 0; v < borderCurve.length; v++)
		{ //assign the borderNavPoints : Vector2 values based on the borderCurve
			borderSectionPoints[v] = new Vector2(borderCurve.keys[v].time, borderCurve.keys[v].value);
		}

		if (borderSectionPoints.Length > 0)
		{
			leftBorderVertices = new Vector3[borderCurve.length * navPointsBeta_p.Length + borderCurve.length];
			rightBorderVertices = new Vector3[borderCurve.length * navPointsBeta_p.Length + borderCurve.length];
		}

		borderCount = 0;

		for (int a = 0; a < navPoints.Count; a++)
		{
			int previous = Mathf.Max(0, a - 1); //previous point in array
			int next = Mathf.Min(navPoints.Count - 1, a + 1); //next point in array

			if (!navPoints[previous].EqualsTo(null) && !navPoints[a].EqualsTo(null) && !navPoints[next].EqualsTo(null))
			{
				//Subdivision points position
				if (a < navPoints.Count - 1)
				{ //inbetween the points
					navPointsBeta_p[a * subdivision] = navPoints[a].position; //set overlapping points

					float xCof;
					for (int b = 1; b < subdivision; b++)
					{ //in-between points
						xCof = (float)b / subdivision;
						Vector3 ap; Vector3 bp; Vector3 cp; Vector3 dp; Vector3 ep; Vector3 fp;
						ap = Vector3.Lerp(navPoints[a].position, navPoints[a].forward * GetPointScale(a).z + navPoints[a].position, xCof);
						cp = Vector3.Lerp(-navPoints[next].forward * GetPointScale(next).z + navPoints[next].position, navPoints[next].position, xCof);
						bp = Vector3.Lerp(navPoints[a].forward * GetPointScale(a).z + navPoints[a].position, -navPoints[next].forward * GetPointScale(next).z + navPoints[next].position, xCof);
						dp = Vector3.Lerp(ap, bp, xCof);
						ep = Vector3.Lerp(bp, cp, xCof);
						fp = Vector3.Lerp(dp, ep, xCof);
						navPointsBeta_p[a * subdivision + b] = fp;

						//Post-Subdivision Vertices <<<----
						Vector3 gTng = Util.GetTangent(dp, ep);
						Vector3 leftRight = -Util.GetBinormal(gTng, navPoints[a].up, navPoints[next].up, xCof);
						newVertices[(a * subdivision + b) * 2] = navPointsBeta_p[a * subdivision + b] - leftRight * (deltaWidth / 2)
						* Mathf.Lerp(GetPointScale(a).x, GetPointScale(next).x, (float)b / subdivision) - transform.position;
						newVertices[(a * subdivision + b) * 2 + 1] = navPointsBeta_p[a * subdivision + b] + leftRight * (deltaWidth / 2)
						* Mathf.Lerp(GetPointScale(a).x, GetPointScale(next).x, (float)b / subdivision) - transform.position;
					}
				}//a-1 restriction:end

				for (int bord = 0; bord < subdivision; bord++)
				{ //the beta points loop inside each segment
					float xCofb = (float)bord / subdivision;
					Vector3 apb, bpb, cpb, dpb, epb, fpb;
					apb = Vector3.Lerp(navPoints[a].position, navPoints[a].forward * GetPointScale(a).z + navPoints[a].position, xCofb);
					cpb = Vector3.Lerp(-navPoints[next].forward * GetPointScale(next).z + navPoints[next].position, navPoints[next].position, xCofb);
					bpb = Vector3.Lerp(navPoints[a].forward * GetPointScale(a).z + navPoints[a].position, -navPoints[next].forward * GetPointScale(next).z + navPoints[next].position, xCofb);
					dpb = Vector3.Lerp(apb, bpb, xCofb);
					epb = Vector3.Lerp(bpb, cpb, xCofb);
					fpb = Vector3.Lerp(dpb, epb, xCofb);
					Vector3 leftRightb = Util.GetBinormal(Util.GetTangent(dpb, epb), navPoints[a].up, navPoints[next].up, xCofb);

					for (int cl = 0; cl < borderCurve.length; cl++)
					{ //the points loop 0 1 2 3 0 1 2 3...

						if (borderCount * borderCurve.length + cl < rightBorderVertices.Length)
						{
							rightBorderVertices[borderCount * borderCurve.length + cl] = fpb + leftRightb * (deltaWidth / 2)
								* Mathf.Lerp(GetPointScale(a).x, GetPointScale(next).x, (float)bord / subdivision)
								+ leftRightb * borderSectionPoints[cl].x
								+ Util.GetNormal(Util.GetTangent(dpb, epb), leftRightb) * borderSectionPoints[cl].y - transform.position;

							leftBorderVertices[borderCount * borderCurve.length + cl] = fpb - leftRightb * (deltaWidth / 2)
								* Mathf.Lerp(GetPointScale(a).x, GetPointScale(next).x, (float)bord / subdivision)
								- leftRightb * borderSectionPoints[cl].x
								+ Util.GetNormal(Util.GetTangent(dpb, epb), leftRightb) * borderSectionPoints[cl].y - transform.position;
						}
					}

					borderCount++;
				}

				//assign main vertices
				if (a < navPoints.Count - 1)
				{
					newVertices[a * subdivision * 2] = navPoints[a].position + navPoints[a].right * -(deltaWidth / 2 * GetPointScale(a).x) - transform.position;
					newVertices[a * subdivision * 2 + 1] = navPoints[a].position + navPoints[a].right * (deltaWidth / 2 * GetPointScale(a).x) - transform.position;
				}
				else
				{
					newVertices[newVertices.Length - 2] = navPoints[navPoints.Count - 1].position + navPoints[navPoints.Count - 1].right * -(deltaWidth / 2 * GetPointScale(navPoints.Count - 1).x) - transform.position;
					newVertices[newVertices.Length - 1] = navPoints[navPoints.Count - 1].position + navPoints[navPoints.Count - 1].right * (deltaWidth / 2 * GetPointScale(navPoints.Count - 1).x) - transform.position;
				}
			}
		}
	}

	private Vector3 GetPointScale(int index)
	{
		if (index < navPoints.Count)
		{
			ERNavPoint npc = navPoints[index].navPointComponent;
			Vector3 npls = navPoints[index].localScale;
			if (npc)
			{
				if (npc.lockSize)
				{
					return new Vector3((npls.x * deltaWidth - deltaWidth + npc.lockedWidth) / deltaWidth, npls.y, npls.z);
				}
			}
			return npls;
		}
		return Vector3.one;
	}

	private void SetTriangles()
	{
		int qCount = (navPoints.Count - 1) * subdivision + 1;
		if (navPoints.Count > 1) //if there is room for triangles to be drawn
			newTriangles = new int[qCount * 6];
		else
			newTriangles = new int[0];

		for (int quad = 1; quad < qCount; quad++)
		{
			for (int s2 = 0; s2 < 6; s2++)
			{
				//assign numbers
				newTriangles[(quad - 1) * 6 + s2] = quadMatrix[s2] + ((quad * 2) - 2);
			}
		}

		//BORDER
		qCount = ((navPoints.Count - 1) * subdivision) * borderCurve.length;//*borderCurve.length;
		if (navPoints.Count > 1)
		{ //if there is room for triangles to be drawn
			if (enableMeshBorders == 1)
			{
				rightTriangles = new int[qCount * 6];
				leftTriangles = new int[qCount * 6];
			}
		}
		else
		{
			rightTriangles = new int[0];
			leftTriangles = new int[0];
		}

		int triCount = 0; //re use variable
		for (int ll = 0; ll < (navPoints.Count - 1) * subdivision; ll++)
		{ //Length index (horizontal index) X
			for (int lb = 0; lb < borderCurve.length - 1; lb++)
			{//vertical index Y quad
				for (int bct = 0; bct < 6; bct++)
				{ //under each quad - for matrix (assign tri points to curve points)
					int[] borderQuadMatrix = { 1, borderCurve.length + 1, 0, borderCurve.length, 0, borderCurve.length + 1 };
					//1,3,0,2,0,3
					int[] borderQuadMatrixLeft = { 0, borderCurve.length + 1, 1, borderCurve.length + 1, 0, borderCurve.length };
					if (triCount < rightTriangles.Length)
					{
						rightTriangles[triCount] = borderQuadMatrix[bct] + lb + borderCurve.length * ll;
						leftTriangles[triCount] = borderQuadMatrixLeft[bct] + lb + borderCurve.length * ll;
						triCount++;
					}
				}
			}
		}
	}

	private void SetUVs()
	{
		int uvs_y_array = newVertices.Length / 2;
		newUV = new Vector2[newVertices.Length];

		//get point-to-point distance and mesh Length
		float previousDistance = 0;
		float[] ptpDistance = new float[navPointsBeta_p.Length];
		for (int ptp = 0; ptp < ptpDistance.Length - 1; ptp++)
		{
			ptpDistance[ptp] = Vector3.Distance(navPointsBeta_p[ptp], navPointsBeta_p[ptp + 1]);
		}

		switch (uvSet)
		{
			case UnwrapOption.PerSegment: //per segment
				uvSetCount = 0;
				for (int uvy = 0; uvy < uvs_y_array; uvy++)
				{
					for (int uvx = 0; uvx < 2; uvx++)
					{
						newUV[uvSetCount] = new Vector2(uvx * uvScale, uvy * uvScale);

						uvSetCount++;
					}
				}
				break;
			case UnwrapOption.TopProject: //top projection
				for (int uvp = 0; uvp < newUV.Length; uvp++)
				{
					newUV[uvp] = new Vector2(newVertices[uvp].x * uvScale, newVertices[uvp].z * uvScale);
				}
				break;
			case UnwrapOption.WidthToLength: //width-to-Length (match width)
				uvSetCount = 0;
				previousDistance = 0;

				for (int uvny = 0; uvny < ptpDistance.Length && uvny < newUV.Length; uvny++)
				{
					for (int uvnx = 0; uvnx < 2; uvnx++)
					{
						newUV[uvSetCount] = new Vector2(uvnx * uvScale, previousDistance * uvScale / deltaWidth);
						uvSetCount++;
					}
					previousDistance += ptpDistance[uvny];
				}
				//fix last segment uvs
				if (newUV.Length >= 4 && navPointsBeta_p.Length > 0)
				{
					float lastPointDistance = Vector3.Distance(navPointsBeta_p[navPointsBeta_p.Length - 1], navPoints[navPoints.Count - 1].position);
					newUV[newUV.Length - 1] = newUV[newUV.Length - 3] + new Vector2(0, 1) * lastPointDistance * uvScale / deltaWidth;
					newUV[newUV.Length - 2] = newUV[newUV.Length - 4] + new Vector2(0, 1) * lastPointDistance * uvScale / deltaWidth;

				}

				break;
			case UnwrapOption.StretchSingleTexture: //stretch single texture
				for (int uvny = 0; uvny < navPointsBeta_p.Length + 1; uvny++)
				{
					for (int uvnx = 0; uvnx < 2; uvnx++)
					{
						int uvIndex = uvnx + uvny * 2;
						newUV[uvIndex] = new Vector2(uvnx * uvScale, 1f / navPointsBeta_p.Length * uvny * uvScale);
					}
				}
				break;
		}
	}

	void SetBorderUVs()
	{
		rightUV = new Vector2[rightBorderVertices.Length];
		leftUV = new Vector2[rightBorderVertices.Length];

		//get point-to-point distance and mesh Length
		float previousDistance = 0f;
		float[] ptpDistance = new float[navPointsBeta_p.Length];
		for (int ptp = 0; ptp < ptpDistance.Length - 1; ptp++)
		{
			ptpDistance[ptp] = Vector3.Distance(navPointsBeta_p[ptp], navPointsBeta_p[ptp + 1]);
		}

		switch (borderUvSet)
		{
			case BorderUnwarpOption.StraightUnwrap: //straight unwrap
				uvSetCount = 0;
				previousDistance = 0;

				for (int uvny = 0; uvny < ptpDistance.Length && uvny < rightUV.Length; uvny++)
				{
					for (int uvnx = 0; uvnx < borderCurve.length; uvnx++)
					{
						float keyDst = Mathf.Sqrt(Mathf.Pow(borderCurve.keys[uvnx].time, 2) + Mathf.Pow(borderCurve.keys[uvnx].value, 2)); //the distance between the vertices based on key time and value
						rightUV[uvSetCount] = new Vector2(keyDst * borderUvScale, previousDistance * borderUvScale);
						leftUV[uvSetCount] = new Vector2(keyDst * borderUvScale, previousDistance * borderUvScale);
						uvSetCount++;
					}
					previousDistance += ptpDistance[uvny];
				}
				//fix last segment uvs
				if (rightUV.Length >= borderCurve.length && leftUV.Length >= borderCurve.length && navPointsBeta_p.Length > 0)
				{
					float lastPointDistance = Vector3.Distance(navPointsBeta_p[navPointsBeta_p.Length - 1], navPoints[navPoints.Count - 1].position);
					for (int uvnx1 = 0; uvnx1 < borderCurve.length && uvnx1 < rightUV.Length; uvnx1++)
					{
						rightUV[rightUV.Length - uvnx1 - 1] = rightUV[rightUV.Length - uvnx1 - borderCurve.length - 1] + new Vector2(0, 1) * lastPointDistance * uvScale;
						leftUV[rightUV.Length - uvnx1 - 1] = leftUV[rightUV.Length - uvnx1 - borderCurve.length - 1] + new Vector2(0, 1) * lastPointDistance * uvScale;
					}
				}
				break;
			case BorderUnwarpOption.TopProject:
				for (int uvpb = 0; uvpb < rightUV.Length && uvpb < leftUV.Length; uvpb++)
				{
					rightUV[uvpb] = new Vector2(rightBorderVertices[uvpb].x * borderUvScale, rightBorderVertices[uvpb].z * borderUvScale);
					leftUV[uvpb] = new Vector2(leftBorderVertices[uvpb].x * borderUvScale, leftBorderVertices[uvpb].z * borderUvScale);
				}
				break;
		}
	}

	///<summary>
	/// Moves all nav points to the surface of whatever object lies underneath,
	/// while keeping the specified offset.
	///</summary>
	public void GroundPoints(float offset)
	{
		RaycastHit hit;
		Vector3[] pointPos = new Vector3[navPoints.Count]; //temporary variable used to store the position of the points to be used to cast a ray from there
														   //save the position data
		for (int p = 0; p < pointPos.Length; p++)
		{
			pointPos[p] = navPoints[p].position;
		}

		for (int vg = 0; vg < navPoints.Count; vg++)
		{
			if (Physics.Raycast(pointPos[vg], Vector3.down, out hit))
			{
				navPoints[vg].position = hit.point + hit.normal * offset;
				Quaternion normalQuaternion = Quaternion.FromToRotation(Vector3.up, hit.normal);
				if (navPoints[vg].pointSnapComponent)
				{ //Snap Points Check
					if (!navPoints[vg].pointSnapComponent.snapped)
						navPoints[vg].eulerAngles = new Vector3(normalQuaternion.eulerAngles.x, navPoints[vg].eulerAngles.y, normalQuaternion.eulerAngles.z);
				}
				else
				{
					navPoints[vg].eulerAngles = new Vector3(normalQuaternion.eulerAngles.x, navPoints[vg].eulerAngles.y, normalQuaternion.eulerAngles.z);
				}
			}
		}

		UpdateData();
	}

	public void ProcessUnderlyingTerrains()
	{
		HashSet<Terrain> _terrains = new HashSet<Terrain>();
		foreach (var _navPoint in GetNavPoints())
		{
			RaycastHit _hit;
			if (Physics.Raycast(_navPoint.position, Vector3.down, out _hit))
			{
				var _hitTerrain = _hit.transform.GetComponent<Terrain>();

				if (_hitTerrain != null)
				{
					_terrains.Add(_hitTerrain);
				}
			}
		}

#if UNITY_EDITOR
		if(_terrains.Count == 0){
			EditorUtility.DisplayDialog("Morph Terrain Failed", "Could not find any underlying terrain!", "Close");
		}
#endif

		int _progress = 0;
		foreach (var _terrain in _terrains)
		{
			MorphTerrain(_terrain, string.Format("Processing Terrain {0}/{1}", ++_progress, _terrains.Count));
		}
	}

	public void MorphTerrain(Terrain terrain, string progressMessage = "")
	{
#if UNITY_EDITOR
		EditorUtility.DisplayProgressBar(progressMessage, "Collecting path points", 0f);
#endif
		var _pathPoints = GetOrientedPathPoints();

		if(_pathPoints.Length == 0)
			goto end;

		float _brushFlatRadius = deltaWidth / 2;
		float _brushSmoothRadius = terrainBrushSmoothingRange;

		var _terrainData = terrain.terrainData;
		var _terrainPosition = terrain.transform.position;
		int _heightmapHeight = (int)_terrainData.heightmapResolution;
		int _heightmapWidth = (int)_terrainData.heightmapResolution;
		float[,] _terrainHeights = _terrainData.GetHeights(0, 0, _heightmapHeight, _heightmapWidth);
		List<int[,]> _terrainDetails = new List<int[,]>();
		var _terrainSize = _terrainData.size;

		// collect details
		for(int i = 0; i < terrain.terrainData.detailPrototypes.Length; i++){
			_terrainDetails.Add(_terrainData.GetDetailLayer(0,0, _terrainData.detailWidth, _terrainData.detailHeight, i));
		}

		var _totalRadius = _brushFlatRadius + _brushSmoothRadius;
		var _heightTexelDistanceX = _terrainSize.x / _heightmapWidth;
		var _heightTexelDistanceY = _terrainSize.z / _heightmapHeight;

		var _terrainStrokeInfo = new TerrainStrokeInfo()
		{
			terrainSize = _terrainSize,
			heightTexelDistanceX = _heightTexelDistanceX,
			heightTexelDistanceY = _heightTexelDistanceY,
			radiusFlat = _brushFlatRadius,
			radiusSmooth = _brushSmoothRadius,
			totalRaidus = _totalRadius,
			heightmapWidth = _heightmapWidth,
			heightmapHeight = _heightmapHeight,
			terrainPosition = terrain.GetPosition(),
			// detailmapHeight = _terrainData.detailHeight,
			// detailmapWidth = _terrainData.detailWidth,
			brushSamplesX = (int)(_totalRadius / _heightTexelDistanceX),
			brushSamplesY = (int)(_totalRadius / _heightTexelDistanceY),
			cachedPathPoints = GetOrientedPathPoints(),
		};

		int _progress = 0;
		var _lastPathPoint = _pathPoints[0];

		for (int i = 0; i < _pathPoints.Length - 1; i++)
		{
			
#if UNITY_EDITOR
		EditorUtility.DisplayProgressBar(progressMessage, "Processing...", (float)_progress++ / _pathPoints.Length);
#endif
			if(_lastPathPoint != _pathPoints[i] && Vector3.Distance(_lastPathPoint.position, _pathPoints[i].position) < _brushFlatRadius){
				if(Vector3.Distance(_pathPoints[i + 1].position, _pathPoints[i].position) < _brushFlatRadius * 2){ 
					continue;
				}
			}

			_lastPathPoint = _pathPoints[i];

			// for each quad
			var _pointA = _pathPoints[i];
			var _pointB = _pathPoints[i + 1];

			int _positionsCount = Mathf.CeilToInt((_pointB.position - _pointA.position).magnitude / _brushFlatRadius) * terrainBrushFrequency;
			var _strokePositions = new List<Vector3>(_positionsCount);

			for (int p = 0; p < _positionsCount; p++)
			{
				_strokePositions.Add(Vector3.Lerp(_pointA.position, _pointB.position, (float)p / _positionsCount));
			}

			// apply stroke positions
			foreach (var _strokePosition in _strokePositions)
			{
				int _strokeHeightmapCenterX = (int)(_strokePosition.x / _terrainStrokeInfo.heightTexelDistanceX);
				int _strokeHeightmapCenterY = (int)(_strokePosition.z / _terrainStrokeInfo.heightTexelDistanceY);

				ApplyBrushOnTerrainHeight(
					ref _terrainHeights,
					_strokeHeightmapCenterX,
					_strokeHeightmapCenterY,
					_terrainStrokeInfo,
					_pointA,
					_pointB,
					_strokePosition
				);
			}
		}

		_terrainData.SetHeights(0, 0, _terrainHeights);
		
		for(int i = 0; i < _terrainDetails.Count; i++){
			_terrainData.SetDetailLayer(0,0,i,_terrainDetails[i]);
		}

#if UNITY_EDITOR
		EditorUtility.SetDirty(_terrainData);
#endif

		end:;
#if UNITY_EDITOR
		EditorUtility.ClearProgressBar();
#endif
	}

	private void ApplyBrushOnTerrainHeight(ref float[,] heights, int centerX, int centerY, TerrainStrokeInfo strokeInfo, OrientationData pointA, OrientationData pointB, Vector3 strokePosition)
	{
		// loop through the brush's area
		for (int x = -strokeInfo.brushSamplesX; x < strokeInfo.brushSamplesX * 2; x++)
		{
			for (int y = -strokeInfo.brushSamplesY; y < strokeInfo.brushSamplesY * 2; y++)
			{
				int _xSample = centerX + x;
				int _ySample = centerY + y;

				if (_xSample < 0 || _xSample >= strokeInfo.heightmapWidth || _ySample < 0 || _ySample >= strokeInfo.heightmapHeight)
				{
					continue;
				}

				var _currentRealTerrainHeight = heights[_ySample, _xSample] * strokeInfo.terrainSize.y;
				var _point3f = new Vector3(x * strokeInfo.heightTexelDistanceX, _currentRealTerrainHeight, y * strokeInfo.heightTexelDistanceY) + strokePosition;
				var _projectedPoint = SnapPointToPath(_point3f, strokeInfo.cachedPathPoints);
				var _horizontalPlaneDistance = Vector2.Distance(new Vector2(_point3f.x, _point3f.z), new Vector2(_projectedPoint.x, _projectedPoint.z));
				var _interval = Mathf.Max(0, _horizontalPlaneDistance - strokeInfo.radiusFlat) / (strokeInfo.radiusSmooth != 0 ? strokeInfo.radiusSmooth : 1);
				var _finalHeight = Mathf.Lerp(_projectedPoint.y - groundOffset, _currentRealTerrainHeight, _interval);

				heights[_ySample, _xSample] = (_finalHeight) / strokeInfo.terrainSize.y;
			}
		}
	}

	public Vector3 SnapPointToPath(Vector3 point, OrientationData[] cachedPathPoints = null)
	{
		var _pathPoints = cachedPathPoints ?? GetOrientedPathPoints();
		
		// no points
		if(_pathPoints.Length == 0){
			return point;
		}

		// single point
		if(_pathPoints.Length == 1){
			return _pathPoints[0].position;
		}
		
		int _closestPointIndex = 0;
		float _closestDistance = float.MaxValue;

		for(int i = 0; i < _pathPoints.Length; i++){
			var _distance = Vector3.Distance(point, _pathPoints[i].position);
			if(_distance < _closestDistance){
				_closestPointIndex = i;
				_closestDistance = _distance;
			}
		}

		Vector3 _projectionVector = Vector3.zero;
		Vector3 _closestPointPosition = _pathPoints[_closestPointIndex].position;

		if(_closestPointIndex == 0){
			// is first
			_projectionVector = (_pathPoints[_closestPointIndex + 1].position - _closestPointPosition).normalized;
			return Vector3.Project(point - _closestPointPosition, _projectionVector) + _closestPointPosition;
		}else if (_closestPointIndex == _pathPoints.Length - 1){
			// is last
			_projectionVector = (_pathPoints[_closestPointIndex - 1].position - _closestPointPosition).normalized;
			return Vector3.Project(point - _closestPointPosition, _projectionVector) + _closestPointPosition;
		}


		// is somewhere in the middle
		var _projectionVectorNext =  (_pathPoints[_closestPointIndex + 1].position - _closestPointPosition);
		var _projectionVectorPrev =  (_pathPoints[_closestPointIndex - 1].position - _closestPointPosition);
		
		var _projectedNext = Vector3.Project(point - _closestPointPosition, _projectionVectorNext) + _closestPointPosition;
		var _projectedPrev = Vector3.Project(point - _closestPointPosition, _projectionVectorPrev) + _closestPointPosition;
		
		if(_projectionVectorPrev.magnitude < Vector3.Distance(_projectedPrev, _pathPoints[_closestPointIndex - 1].position)
			&& Vector3.Distance(_projectedNext, _pathPoints[_closestPointIndex + 1].position) < _projectionVectorNext.magnitude){
			return _projectedNext;
		}else{
			return _projectedPrev;
		}
	}

	public void ResetMesh()
	{
		for (int nav = 0; nav < navPoints.Count; nav++)
		{
			if (!navPoints[nav].EqualsTo(null))
			{
				SETUtil.SceneUtil.SmartDestroy(navPoints[nav].gameObject);
			}
		}

		navPoints.Clear();
		newVertices = new Vector3[0];
		newUV = new Vector2[0];
		newTriangles = new int[0];

		CreateNavPoint();
		GenerateMesh();
	}

#if UNITY_EDITOR
	/// <summary>
	/// This will remove all components except MeshCollider, MeshFilter and MeshRenderer and will export all related mesh assets
	/// </summary>
	public void FinalizeMeshGen()
	{
		var _path = Application.dataPath;

		_path = UnityEditor.EditorUtility.OpenFolderPanel("Export Assets", _path, "MeshGenExport");
		if (_path.StartsWith(Application.dataPath))
		{
			_path = "Assets" + _path.Substring(Application.dataPath.Length);
		}

		var _finalName = name.Replace(' ', '_');
		var _meshGenMeshPath = _path + "/" + _finalName + "_Mesh.asset";
		var _leftBorderMeshPath = _path + "/" + _finalName + "_LBorderMesh.asset";
		var _rightBorderMeshPath = _path + "/" + _finalName + "_RBorderMesh.asset";

		FinalizeERObject(gameObject, _meshGenMeshPath);

		if (leftBorder != null)
		{
			FinalizeERObject(leftBorder, _leftBorderMeshPath);
		}

		if (rightBorder != null)
		{
			FinalizeERObject(rightBorder, _rightBorderMeshPath);
		}

		foreach (var navPoint in navPoints)
		{
			SETUtil.SceneUtil.SmartDestroy(navPoint.gameObject);
		}
		navPoints.Clear();

		dontClearOnDestroy = true;
		foreach (Component component in GetComponents<Component>())
		{
			System.Type _componentType = component.GetType();
			if (_componentType != typeof(Transform) && _componentType != typeof(MeshFilter) && _componentType != typeof(MeshRenderer))
			{
				SETUtil.SceneUtil.SmartDestroy(component);
			}
		}
		dontClearOnDestroy = false;
	}

	private void FinalizeERObject(GameObject border, string path)
	{
		var _borderMeshFilter = border.GetComponent<MeshFilter>();
		path = AssetDatabase.GenerateUniqueAssetPath(path);
		AssetDatabase.CreateAsset(_borderMeshFilter.sharedMesh, path);
		_borderMeshFilter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
		EditorUtility.SetDirty(_borderMeshFilter);
	}
#endif

	private void EnableBorders(bool state)
	{
		if (leftBorder != null && rightBorder != null && state)
			return;

		if (leftBorder == null && rightBorder == null && !state)
			return;

		if (state == true)
		{
			var _material = Util.GetDefaultMaterial();

			/*#if UNITY_EDITOR
			SerializedObject _so = new SerializedObject(this);
			SerializedProperty _so_leftBorder = _so.FindProperty("leftBorder");
			SerializedProperty _so_rightBorder = _so.FindProperty("rightBorder");
			#endif*/

			if (!FindLeftBorder())
			{
				var _newGameObject = new GameObject();
				/*#if UNITY_EDITOR
				_so_leftBorder.objectReferenceValue = _newGameObject;
				_so.ApplyModifiedPropertiesWithoutUndo();
				#else*/
				leftBorder = _newGameObject;
				//#endif

				leftBorder.name = LEFT_BORDER_NAME;
				leftBorder.transform.position = transform.position;
				leftBorder.transform.eulerAngles = Vector3.zero;
				leftBorder.AddComponent<MeshFilter>();
				var _renderer = leftBorder.AddComponent<MeshRenderer>();
				_renderer.sharedMaterial = _material;
				leftBorder.transform.parent = this.transform;
			}
			if (!FindRightBorder())
			{
				var _newGameObject = new GameObject();
				/*#if UNITY_EDITOR
				_so_rightBorder.objectReferenceValue = _newGameObject;
				_so.ApplyModifiedPropertiesWithoutUndo();
				#else*/
				rightBorder = _newGameObject;
				//#endif

				rightBorder.name = RIGHT_BORDER_NAME;
				rightBorder.transform.position = transform.position;
				rightBorder.transform.eulerAngles = Vector3.zero;
				rightBorder.AddComponent<MeshFilter>();
				var _renderer = rightBorder.AddComponent<MeshRenderer>();
				_renderer.sharedMaterial = _material;
				rightBorder.transform.parent = this.transform;
			}

			leftBorder.GetComponent<MeshFilter>().sharedMesh = new Mesh();
			rightBorder.GetComponent<MeshFilter>().sharedMesh = new Mesh();

		}
		else
		{
			if (FindRightBorder())
				SETUtil.SceneUtil.SmartDestroy(rightBorder);
			rightBorder = null;
			if (FindLeftBorder())
				SETUtil.SceneUtil.SmartDestroy(leftBorder);
			leftBorder = null;
		}
	}

	private void UpdateBorders()
	{
		if (enableMeshBorders == 0)
		{
			return;
		}

		if (leftBorder != null)
		{
			var _borderMeshFilter = leftBorder.GetComponent<MeshFilter>();
			var _mesh = _borderMeshFilter.sharedMesh;
			if (_mesh == null)
			{
				_mesh = _borderMeshFilter.sharedMesh = new Mesh();
			}
			_mesh.Clear();
			_mesh.vertices = leftBorderVertices;
			_mesh.uv = leftUV;
			_mesh.triangles = leftTriangles;
			_mesh.RecalculateNormals();
		}


		if (rightBorder != null)
		{
			var _borderMeshFilter = rightBorder.GetComponent<MeshFilter>();
			var _mesh = _borderMeshFilter.sharedMesh;
			if (_mesh == null)
			{
				_mesh = _borderMeshFilter.sharedMesh = new Mesh();
			}
			_mesh.Clear();
			_mesh.vertices = rightBorderVertices;
			_mesh.uv = rightUV;
			_mesh.triangles = rightTriangles;
			_mesh.RecalculateNormals();
		}
	}

	public bool FindLeftBorder()
	{
		if (leftBorder != null)
			return true;

		Transform t = transform.Find(ERMeshGen.LEFT_BORDER_NAME);
		if (t != null)
		{
			leftBorder = (GameObject)t.gameObject;
			return true;
		}

		//return false if border has not been found
		return false;
	}

	public bool FindRightBorder()
	{
		if (rightBorder != null)
			return true;

		Transform t = transform.Find(ERMeshGen.RIGHT_BORDER_NAME);
		if (t != null)
		{
			rightBorder = (GameObject)t.gameObject;
			return true;
		}

		//return false if border has not been found
		return false;
	}

	/// <summary>
	/// Parents children nav points where each next nav point is a child of the previous.
	/// This method is prone to breaking the proper behavior of the road, so use it carefully.
	/// </summary>
	public void ReparentPoints(bool parent)
	{
		for (int p = 1; p < navPoints.Count; p++)
		{
			if (parent)
			{
				navPoints[p].SetParent(navPoints[p - 1].transform);
			}
			else
			{
				navPoints[p].SetParent(transform);
			}
		}
	}

	private void UpdateCollider(Mesh colMesh)
	{
		if (includeCollider == 1)
		{
			gameObject.ValidateComponent(ref meshCollider);
			meshCollider.sharedMesh = colMesh; //assign the updated mesh to the collider;

			if (enableMeshBorders == 1)
			{
				var _rightBorderCol = rightBorder.GetComponent<MeshCollider>();
				var _leftBorderCol = leftBorder.GetComponent<MeshCollider>();

				if (_rightBorderCol == null)
					_rightBorderCol = rightBorder.AddComponent<MeshCollider>();
				if (_leftBorderCol == null)
					_leftBorderCol = leftBorder.AddComponent<MeshCollider>();

				_rightBorderCol.sharedMesh = rightBorder.GetComponent<MeshFilter>().sharedMesh; //assign the updated mesh to the collider;
				_leftBorderCol.sharedMesh = leftBorder.GetComponent<MeshFilter>().sharedMesh; //assign the updated mesh to the collider;
			}
		}
		else
		{
			if (meshCollider)
			{
				SETUtil.SceneUtil.SmartDestroy(meshCollider);
			}
			if (FindRightBorder())
			{
				var _rightBorderCol = rightBorder.GetComponent<MeshCollider>();
				if (_rightBorderCol)
					SETUtil.SceneUtil.SmartDestroy(_rightBorderCol);
			}
			if (FindLeftBorder())
			{
				var _leftBorderCol = leftBorder.GetComponent<MeshCollider>();
				if (_leftBorderCol)
					SETUtil.SceneUtil.SmartDestroy(_leftBorderCol);
			}
		}
	}

	/// <summary>
	/// In the case nav points array is out of date, try to find nav point children by name
	/// </summary>
	public void FindNavPoints()
	{
		bool foundAllNavPoints = false;
		int navPointCounter = 0;
		string navPointNames = NAV_POINT_NAMES + " ";
		List<NavPointReference> _navPoints = new List<NavPointReference>();

		while (!foundAllNavPoints)
		{
			Transform point = transform.Find(navPointNames + navPointCounter);
			if (point != null)
			{
				_navPoints.Add(new NavPointReference(point));
				var _erNavPointComponent = point.GetComponent<ERNavPoint>();
				if (_erNavPointComponent != null)
					_erNavPointComponent.assignedMeshGen = this;

				navPointCounter++;
			}
			else
				foundAllNavPoints = true;
		}

		navPoints = _navPoints;
	}

	/// <summary>
	/// Returns data about the nav point transforms
	/// </summary>
	public NavPointReference[] GetNavPoints()
	{
		return navPoints.ToArray();
	}

	/// <summary>
	/// Returns an array with the path information after subdivision (offset = 0,0)
	/// </summary>
	public OrientationData[] GetOrientedPathPoints()
	{
		return GetOrientedPathPoints(0f, 0f);
	}

	/// <summary>
	/// Returns an array with the path information after subdivision
	/// </summary>
	public OrientationData[] GetOrientedPathPoints(float horizontalOffset, float verticalOffset)
	{
		OrientationData[] _p = new OrientationData[navPointsBeta_p.Length + 1];
		int _index = 0;

		Vector3
			_tangent = Vector3.forward,
			_up = Vector3.up,
			_right = Vector3.right;

		for (int a = 0; a < navPoints.Count - 1; a++)
		{
			for (int b = 0; b < subdivision; b++)
			{
				_index = a * subdivision + b;
				_up = Vector3.Lerp(navPoints[a].up, navPoints[a + 1].up, (float)b / (float)subdivision);
				if (_index < navPointsBeta_p.Length - 2)
				{
					_tangent = (navPointsBeta_p[_index + 1] - navPointsBeta_p[_index]).normalized;
					_right = Vector3.Cross(_up, _tangent);
				}
				_p[_index] = new OrientationData(navPointsBeta_p[_index] + _up * verticalOffset + _right * horizontalOffset, _tangent, _up);
			}
		}

		//assign the last nav point:
		_index = navPoints.Count - 1;
		_up = navPoints[_index].up;
		_right = navPoints[_index].right;
		_tangent = navPoints[_index].forward;

		_p[_p.Length - 1] = new OrientationData();
		_p[_p.Length - 1].Set(navPoints[_index].position + _up * verticalOffset + _right * horizontalOffset, _tangent, _up);

		return _p;
	}
}
