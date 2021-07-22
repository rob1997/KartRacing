using UnityEngine;

/// <summary>
/// This class exists to help with porting from the legacy JS code to the C# version
/// </summary>
[AddComponentMenu("Easy Roads Mesh Gen/ER Helper")]
public class ERHelper : MonoBehaviour
{
	public ERMeshGen meshGen;
	
	public void Init () {
		if(!meshGen){
			meshGen = GetComponent<ERMeshGen>();	
			
			if(!meshGen)
				meshGen = (ERMeshGen) GameObject.FindObjectOfType(typeof(ERMeshGen));
		}
	}
	
	public void AutoFix () {
		FindNavPoints();
		FindBorders();
	}
	
	public void FindNavPoints(){
		meshGen.FindNavPoints();	
	}
	
	public void FindBorders () {
		meshGen.FindLeftBorder();
		meshGen.FindRightBorder();
	}
}
