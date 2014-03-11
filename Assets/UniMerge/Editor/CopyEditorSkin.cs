using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Reflection;

public class CopyEditorSkin : EditorWindow {
	public GUISkin skin;

	[MenuItem("Window/UniMerge/Copy Editor Skin")]
	public static void Init() {
		EditorWindow.GetWindow(typeof(CopyEditorSkin));
	}

	public void OnGUI() {
		//Ctrl + w to close
		if(Event.current.Equals(Event.KeyboardEvent("^w"))){
			Close();
			GUIUtility.ExitGUI();
		}
		#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
		skin = EditorGUILayout.ObjectField(skin, typeof(GUISkin)) as GUISkin;
		#else
		skin = EditorGUILayout.ObjectField(skin, typeof(GUISkin), true) as GUISkin;
		#endif
		if(skin == null)
			GUI.enabled = false;

		if(GUILayout.Button("Copy Editor Skin")) {
			GUISkin builtinSkin = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector);
			PropertyInfo[] properties = typeof(GUISkin).GetProperties();
			foreach(PropertyInfo property in properties) {
				if(property.PropertyType == typeof(GUIStyle)) {
					property.SetValue(skin, property.GetValue(builtinSkin, null), null);
				}
			}
		}
	}
}