///Matt Schoen
///5-29-2013
///
///This software is the copyrighted material of its author, Matt Schoen, and his company
///Defective Studios.  It is available for sale on the Unity Asset store and is subject to its
///restrictions and limitations, as well as the following: You shall not reproduce or re-distribute
///this software without the express written (e-mail is fine) permission of the author. If permission
///is granted, the code (this file and related files) must bear this license in its entirety. Anyone
///who purchases the script is welcome to modify and re-use the code at their personal risk and under
///the condition that it not be included in any distribution builds. UniMerge is provided “as is”
///without any warranties and indemnities and the author bears no responsibility for damages or losses
///caused by the software.  Enjoy it; it's yours, but just don't try to sell it, OK?

using UnityEditor;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SceneMerge : EditorWindow {
	const string messagePath = "Assets/merges.txt";
	public static Object mine, theirs;
	static bool merged;
	static GameObject mineContainer, theirsContainer;

	[MenuItem("Window/UniMerge/Scene Merge")]
	static void Init() {
		EditorWindow.GetWindow(typeof(SceneMerge));
	}

	void OnGUI() {
		//Ctrl + w to close
		if(Event.current.Equals(Event.KeyboardEvent("^w"))){
			Close();
			GUIUtility.ExitGUI();
		}
		#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
		EditorGUIUtility.LookLikeControls();
		mine = EditorGUILayout.ObjectField("Mine", mine, typeof(Object));
		theirs = EditorGUILayout.ObjectField("Theirs", theirs, typeof(Object));
		#else
		mine = EditorGUILayout.ObjectField("Mine", mine, typeof(Object), true);
		theirs = EditorGUILayout.ObjectField("Theirs", theirs, typeof(Object), true);
		#endif
		if(mine == null || theirs == null
			|| mine.GetType() != typeof(Object) || mine.GetType() != typeof(Object)
			)//|| !AssetDatabase.GetAssetPath(mine).Contains(".unity") || !AssetDatabase.GetAssetPath(theirs).Contains(".unity"))
			merged = GUI.enabled = false;
		if(GUILayout.Button("Merge")){
			Merge(AssetDatabase.GetAssetPath(mine), AssetDatabase.GetAssetPath(theirs));
			GUIUtility.ExitGUI();
		}
		GUI.enabled = merged;
		GUILayout.BeginHorizontal();{
			GUI.enabled = mineContainer;
			if(GUILayout.Button("Unpack Mine")) {
				Object.DestroyImmediate(theirsContainer);
				List<Transform> tmp = new List<Transform>();
				foreach(Transform t in mineContainer.transform)
					tmp.Add(t);
				foreach(Transform t in tmp)
					t.parent = null;
				Object.DestroyImmediate(mineContainer);
			}
			GUI.enabled = theirsContainer;
			if(GUILayout.Button("Unpack Theirs")) {
				Object.DestroyImmediate(mineContainer);
				List<Transform> tmp = new List<Transform>();
				foreach(Transform t in theirsContainer.transform)
					tmp.Add(t);
				foreach(Transform t in tmp)
					t.parent = null;
				Object.DestroyImmediate(theirsContainer);
			}
		}
		GUILayout.EndHorizontal();

	}
	public static void CLIIn() {
		string[] args = System.Environment.GetCommandLineArgs();
		foreach(string arg in args)
			Debug.Log(arg);
		Merge(args[args.Length - 2], args[args.Length - 1]);
	}
	void Update() {
		TextAsset mergeFile = (TextAsset)AssetDatabase.LoadAssetAtPath(messagePath, typeof(TextAsset));
		if(mergeFile) {
			string[] files = mergeFile.text.Split('\n');
			AssetDatabase.DeleteAsset(messagePath);
			foreach(string file in files)
				Debug.Log(file);
			//TODO: Add prefab case
			DoMerge(files);
		}
	}
	public static void DoMerge(string[] paths) {
		if(paths.Length > 2) {
			Merge(paths[0], paths[1]);
		} else Debug.LogError("need at least 2 paths, " + paths.Length + " given");
	}
	public static void Merge(string myPath, string theirPath) {
		if(AssetDatabase.LoadAssetAtPath(myPath, typeof(Object)) && AssetDatabase.LoadAssetAtPath(theirPath, typeof(Object))) {
			if(EditorApplication.SaveCurrentSceneIfUserWantsTo()) {
				EditorApplication.OpenScene(myPath);
				mineContainer = new GameObject();
				mineContainer.name = "mine";
				GameObject[] allObjects = (GameObject[])Resources.FindObjectsOfTypeAll(typeof(GameObject));
				#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
				foreach(GameObject obj in allObjects) {
					if(obj.transform.parent == null
						&& EditorUtility.GetPrefabType(obj) != PrefabType.Prefab
						&& EditorUtility.GetPrefabType(obj) != PrefabType.ModelPrefab
						&& obj.hideFlags == 0)		//Want a better way to filter out "internal" objects
						obj.transform.parent = mineContainer.transform;
				}
				#else
				foreach(GameObject obj in allObjects) {
					if(obj.transform.parent == null
						&& PrefabUtility.GetPrefabType(obj) != PrefabType.Prefab
						&& PrefabUtility.GetPrefabType(obj) != PrefabType.ModelPrefab
						&& obj.hideFlags == 0)		//Want a better way to filter out "internal" objects
						obj.transform.parent = mineContainer.transform;
				}
				#endif
				EditorApplication.OpenSceneAdditive(theirPath);
				theirsContainer = new GameObject();
				theirsContainer.name = "theirs";
				allObjects = (GameObject[])GameObject.FindObjectsOfType(typeof(GameObject));
				allObjects = (GameObject[])Resources.FindObjectsOfTypeAll(typeof(GameObject));
				#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
				foreach(GameObject obj in allObjects) {
					if(obj.transform.parent == null && obj.name != "mine"
						&& EditorUtility.GetPrefabType(obj) != PrefabType.Prefab
						&& EditorUtility.GetPrefabType(obj) != PrefabType.ModelPrefab
						&& obj.hideFlags == 0)		//Want a better way to filter out "internal" objects
						obj.transform.parent = theirsContainer.transform;
				}
				#else
				foreach(GameObject obj in allObjects) {
					if(obj.transform.parent == null && obj.name != "mine"
						&& PrefabUtility.GetPrefabType(obj) != PrefabType.Prefab
						&& PrefabUtility.GetPrefabType(obj) != PrefabType.ModelPrefab
						&& obj.hideFlags == 0)		//Want a better way to filter out "internal" objects
						obj.transform.parent = theirsContainer.transform;
				}
				#endif

				EditorWindow.GetWindow(typeof(ObjectMerge));
				ObjectMerge.mine = mineContainer;
				ObjectMerge.theirs = theirsContainer;
			}
			merged = true;
		}
	}
}