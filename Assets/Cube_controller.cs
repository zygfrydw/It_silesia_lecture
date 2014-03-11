using UnityEngine;
using System.Collections;

public class Cube_controller : MonoBehaviour
{
	public float speed;

    public bool RotateCube;

	void Start ()
	{
	    RotateCube = true;
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
