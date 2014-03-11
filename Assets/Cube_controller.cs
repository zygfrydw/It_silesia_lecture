using UnityEngine;
using System.Collections;

public class Cube_controller : MonoBehaviour
{

    public bool RotateCube;

	// Use this for initialization
	void Start ()
	{
	    RotateCube = true;
	}
	
	// Update is called once per frame
	void Update () {
	    if (RotateCube)
	    {
            transform.Rotate(new Vector3(30,30,10) * Time.deltaTime);
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
