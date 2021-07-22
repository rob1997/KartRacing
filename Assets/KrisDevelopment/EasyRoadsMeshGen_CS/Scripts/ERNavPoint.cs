using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

using ERMG;
using SETUtil.SceneUI;

namespace ERMG {
	public enum NavAction {
		Add,
		Delete,
	}
}
	
public class ERNavPoint : MonoBehaviour
{
	public int lastKnownIndex = -1;
	const float gizmoSize = 1f;
	public ERMeshGen assignedMeshGen; //assigned mesh gen scripts
	public bool lockSize = false;
	public float lockedWidth = 0f;
	
#if UNITY_EDITOR
	
	[System.NonSerialized]
	private GUIButton addButtonElement = null;

	[System.NonSerialized]
	private GUILabel labelElement = null;

#endif


	#if UNITY_EDITOR
	void  OnDrawGizmos ()
	{
		Gizmos.DrawIcon(transform.position, "EasyRoadsMeshGen/waypoint_icon.png", true);
	}

	void OnDrawGizmosSelected ()
	{
		Gizmos.color = Color.yellow;
		if(assignedMeshGen){
			Gizmos.DrawLine(transform.position,transform.position - transform.forward*transform.localScale.z);
				Gizmos.DrawLine(transform.position,transform.position + transform.forward*transform.localScale.z);
			Gizmos.DrawWireSphere(transform.position + transform.forward*transform.localScale.z,0.08f * gizmoSize);
			Gizmos.DrawWireSphere(transform.position - transform.forward*transform.localScale.z,0.08f * gizmoSize);
		}
		
		Gizmos.color = Color.red;
		Gizmos.DrawLine(transform.position,transform.position+ transform.right*gizmoSize/2f);
		Gizmos.color = Color.green;
		Gizmos.DrawLine(transform.position,transform.position+ transform.up*gizmoSize/2f);
		Gizmos.color = Color.blue;
		Gizmos.DrawLine(transform.position,transform.position+ transform.forward*gizmoSize/2f);
		
		if(labelElement == null){
			labelElement = new GUILabel(gameObject.name);
		}

		labelElement.text = gameObject.name;
		labelElement.position = transform.position; 

		SETUtil.EditorUtil.DrawSceneElement(labelElement);
		
		if(addButtonElement == null || addButtonElement.onClick == null)
			addButtonElement = new GUIButton("+", Vector3.zero, new Rect(0,0,0,0), AddNavPointDelegate);
		addButtonElement.position = transform.position;
		addButtonElement.rect = new Rect(-11,11,22,22);
		
		SETUtil.EditorUtil.DrawSceneElement(addButtonElement);
	}

	void AddNavPointDelegate ()
	{
		NavPointAction(NavAction.Add);
	}

	#endif
	
	public void NavPointAction (NavAction act)
	{
		if(assignedMeshGen == null){
			SETUtil.EditorUtil.Debug("[ERROR ERMG.ERNavPoint] No assigned MeshGen at " + name);
			return;
		}

		if(act == NavAction.Add) {
			if(lastKnownIndex > -1) {
				assignedMeshGen.CreateNavPoint(lastKnownIndex);
			} else {
				assignedMeshGen.CreateNavPoint();
			}
		} else if(act == NavAction.Delete){
			if(lastKnownIndex > -1) {
				assignedMeshGen.DeleteNavPoint(lastKnownIndex);
			} else {
				assignedMeshGen.DeleteNavPoint();
			}
		}	
	}
	
	#if UNITY_EDITOR
		public void LockSize (bool state)
		{
			SerializedObject so = new SerializedObject(this);
			SerializedProperty so_lockSize = so.FindProperty("lockSize");
			SerializedProperty so_lockedWidth = so.FindProperty("lockedWidth");
			
			if(!assignedMeshGen) {
				Debug.Log("No MeshGen script assigned! Lock operation failed!");
				so_lockSize.boolValue = false;
			} else {
				so_lockSize.boolValue = state;
				so_lockedWidth.floatValue = assignedMeshGen.deltaWidth;
			}
			so.ApplyModifiedProperties();
		}
	#endif

	public void SetIndex (int index)
	{
		#if UNITY_EDITOR
		SerializedObject so = new SerializedObject(this);
		SerializedProperty so_lockSize = so.FindProperty("lastKnownIndex");
		
		if(!assignedMeshGen) {
			Debug.Log("No MeshGen script assigned!");
			so_lockSize.intValue = -1;
		} else {
			so_lockSize.intValue = index;
		}
		so.ApplyModifiedPropertiesWithoutUndo();
		#else
		lastKnownIndex = index;
		#endif
	}
}
