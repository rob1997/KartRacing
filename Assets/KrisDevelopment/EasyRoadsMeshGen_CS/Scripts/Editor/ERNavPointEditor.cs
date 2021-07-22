#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections;
using ERMG;

[CustomEditor(typeof(ERNavPoint))]
public class ERNavPointEditor : Editor {
	public override void OnInspectorGUI (){
		ERNavPoint myScirpt = (ERNavPoint)target;
		
		if(myScirpt.assignedMeshGen){
			SETUtil.EditorUtil.BeginColorPocket(Color.green);

			if(GUILayout.Button("Add Nav Point")){
				myScirpt.NavPointAction(NavAction.Add);
			}

			SETUtil.EditorUtil.EndColorPocket();
				
			if(myScirpt.lastKnownIndex > -1 && GUILayout.Button("Delete Nav Point"))
				myScirpt.NavPointAction(NavAction.Delete);
		}
		
		if(myScirpt.assignedMeshGen){
			string lockLabel = (myScirpt.lockSize) ? "Unlock Width (Locked!)" : "Lock Width";
			if(GUILayout.Button(lockLabel))
				myScirpt.LockSize(!myScirpt.lockSize);
		}
	}
}
#endif