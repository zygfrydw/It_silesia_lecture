using UnityEngine;
using System.Collections;
using TouchScript.Gestures;

public class Cube_controller : MonoBehaviour
{
	public float speed;
	
    public bool RotateCube;
	MeshRenderer cubeMaterial;	

	
	

	void HandleStateChanged (object sender, TouchScript.Events.GestureStateChangeEventArgs e)
	{
			
		if(cubeMaterial.renderer.material.color == Color.red)
		cubeMaterial.renderer.material.color = Color.blue;
		else 
		cubeMaterial.renderer.material.color = Color.red;
		
	}
	void Start ()
	{
	    RotateCube = true;
		
		
		GetComponent<TapGesture>().StateChanged += HandleStateChanged;
		cubeMaterial = GetComponent<MeshRenderer>();
	}
	
	
		
    void Update() {
        
	    if (RotateCube)
	    {
            transform.Rotate(Vector3.right * Time.deltaTime * speed);
        	transform.Rotate(Vector3.up * Time.deltaTime * speed /*Space.World*/);

	    }
	}

    void OnGUI()
    {
        if (GUI.Button(new Rect(10, 70, 100, 20), "Stop Rotation"))
        {
            RotateCube = false;
        }

        if (GUI.Button(new Rect(10, 110, 100, 20), "Start Rotation"))
        {
            RotateCube = true;
        }
    }
}
