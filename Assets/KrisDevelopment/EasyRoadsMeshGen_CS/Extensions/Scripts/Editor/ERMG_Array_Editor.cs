using UnityEngine;
using UnityEditor;

namespace ERMG
{
	[CustomEditor(typeof(EasyRoadsMeshGen_Array))]
	public class ERMG_Array_Editor : Editor
	{
		//static bool expandArray = true;

		EasyRoadsMeshGen_Array script;

		public override void OnInspectorGUI()
		{
			EditorGUI.BeginChangeCheck();

			script = (EasyRoadsMeshGen_Array)target;
			SerializedObject _so_ermgArr = new SerializedObject(script);
			SerializedProperty
				_so_ermgArr_arrayObject = _so_ermgArr.FindProperty("arrayObjects"),
				_so_ermgArr_path = _so_ermgArr.FindProperty("path"),
				_so_ermgArr_length = _so_ermgArr.FindProperty("length");

			// Since for now there won't be multiple objects per array component,
			// set the array to arraySize 1
			_so_ermgArr_arrayObject.arraySize = 1;
			_so_ermgArr.ApplyModifiedPropertiesWithoutUndo();
			_so_ermgArr.Update();

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.PropertyField(_so_ermgArr_path);
			if (_so_ermgArr_path.objectReferenceValue == null)
			{
				if (GUILayout.Button("Add", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
				{
					_so_ermgArr_path.objectReferenceValue = Undo.AddComponent<ERPathTracer>(script.gameObject);
				}
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.PropertyField(_so_ermgArr_length, new GUIContent("Fit Length", "[EasyRoadsMeshGen_Array.length]\nIf an array object uses FitType.FitLength, this value will serve as the reference length."));

			for (int i = 0; i < script.arrayObjects.Count; i++)
			{
				SerializedProperty
					_ermgArrEl = _so_ermgArr_arrayObject.GetArrayElementAtIndex(i),
					_prefab = _ermgArrEl.FindPropertyRelative("prefab"),
					_fitType = _ermgArrEl.FindPropertyRelative("fitType"),
					_amount = _ermgArrEl.FindPropertyRelative("amount"),
					_elementLength = _ermgArrEl.FindPropertyRelative("length"),
					_verticalOffset = _ermgArrEl.FindPropertyRelative("verticalOffset"),
					_horizontalOffset = _ermgArrEl.FindPropertyRelative("horizontalOffset"),
					_pathOffset = _ermgArrEl.FindPropertyRelative("pathOffset"),
					_rotation = _ermgArrEl.FindPropertyRelative("rotation"),
					_invert = _ermgArrEl.FindPropertyRelative("invert");

				GUILayout.BeginVertical(EditorStyles.helpBox);

				EditorGUILayout.PropertyField(_prefab);
				GUILayout.BeginHorizontal();
				{
					EditorGUILayout.PropertyField(_elementLength, new GUIContent("Object Length"));
					if (GUILayout.Button(new GUIContent("Auto", "Automatically determine the object length."), EditorStyles.miniButton))
					{
						_elementLength.floatValue = script.GetAutoLength(i);
					}
				}
				GUILayout.EndHorizontal();
				EditorGUILayout.PropertyField(_fitType);
				if (script.arrayObjects[i].fitType == EasyRoadsMeshGen_Array.FitType.FixedAmount)
				{
					EditorGUILayout.PropertyField(_amount);
				}
				EditorGUILayout.PropertyField(_verticalOffset);
				EditorGUILayout.PropertyField(_horizontalOffset);
				EditorGUILayout.PropertyField(_pathOffset);
				EditorGUILayout.PropertyField(_rotation);
				EditorGUILayout.PropertyField(_invert);

				GUILayout.EndVertical();
			}

			if (EditorGUI.EndChangeCheck())
			{
				_so_ermgArr.ApplyModifiedProperties();
				script.UpdateData();
			}
		}
	}
}