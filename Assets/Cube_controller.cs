using UnityEngine;
using System.Collections;
using TouchScript.Gestures;

public class Cube_controller : MonoBehaviour {
	
MeshRenderer cubeMaterial;	

	
	// Use this for initialization
	void Start () {
		GetComponent<TapGesture>().StateChanged += HandleStateChanged;
		cubeMaterial = GetComponent<MeshRenderer>();
	}

	void HandleStateChanged (object sender, TouchScript.Events.GestureStateChangeEventArgs e)
	{
			
		if(cubeMaterial.renderer.material.color == Color.red)
		cubeMaterial.renderer.material.color = Color.blue;
		else 
		cubeMaterial.renderer.material.color = Color.red;
		
	}
	
	// Update is called once per frame
	void Update () {
			
	
	}
}
