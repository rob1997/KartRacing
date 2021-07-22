#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections;

[CustomEditor(typeof(ERHelper))]
public class ERHelperEditor : Editor {

	ERHelper mainScript;
	
	public void OnEnable () {
		mainScript = (ERHelper) target;
		mainScript.Init();
	}
	
	public override void OnInspectorGUI () {
		DrawDefaultInspector();
		
		if(GUILayout.Button("Fix Nav Points"))
			if(EditorUtility.DisplayDialog("Auto fill Nav Points", "This will automatically search for existing nav poitns and add them to the nav poitns array. Continue?", "Yes", "No"))
				mainScript.FindNavPoints();
	}
}
#endif