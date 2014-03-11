///Matt Schoen
///5-29-2013
///
///This software (UniMerge) is the copyrighted material of its author, Matt Schoen, and his company
///Defective Studios.  It is available for sale on the Unity Asset Store and is subject to its
///restrictions and limitations, as well as the following: You shall not reproduce or re-distribute
///this software without the express written (e-mail is fine) permission of the author. If permission
///is granted, the code (this file and related files) must bear this license in its entirety. Anyone
///who purchases the script is welcome to modify and re-use the code at their personal risk and under
///the condition that it not be included in any distribution builds. UniMerge is provided “as is”
///without any warranties and indemnities and the author bears no responsibility for damages or losses
///caused by the software.  Enjoy it; it's yours, but just don't try to sell it, OK?

#define DEV

using UnityEditor;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public class ObjectMerge : EditorWindow {
	public static ObjectHelper root, lastRoot;
	public static bool alt;
	public static float colWidth;
	public static GameObject mine, theirs;
	public const float maxFrameTime = 0.05f;
	public const int RightColWidth = 75;
	public static int refreshCount, totalRefreshNum;
	public static System.Diagnostics.Stopwatch refreshWatch;
	const int PROGRESS_BAR_HEIGHT = 15;
	
#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
	static string filters = "", lastFilters;
	public static List<System.Type> filterTypes;
	List<string> badTypes;
	List<string> notComponents;
	System.Reflection.Assembly[] assemblies;
#else
	string[][] componentTypeStrings;
	public static System.Type[][] componentTypes;
#endif

	public static bool deepCopy = true;
	public static bool log = false;
	public static bool compareAttrs = true;
	public static int[] typeMask;

	public static IEnumerator refresh, refresh2;
	public static bool cancelRefresh = false;

	[MenuItem("Window/UniMerge/Object Merge %m")]
	static void Init() {
		EditorWindow.GetWindow(typeof(ObjectMerge));
	}
	Vector2 scroll;
	void OnEnable() {
		//Get path
#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
		//Unity 3 path stuff?
#else
		string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
		ObjectMergeConfig.DEFAULT_PATH = scriptPath.Substring(0, scriptPath.IndexOf("Editor") - 1);
#endif
		//Set up skin. We add the styles from the custom skin because there are a bunch (467!) of built in custom styles
		string guiSkinToUse = ObjectMergeConfig.DEFAULT_GUI_SKIN_FILENAME;
#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4
		//Alternate detection of dark skin
		if(UnityEditorInternal.InternalEditorUtility.HasPro() && EditorPrefs.GetInt("UserSkin") == 1)
			guiSkinToUse = ObjectMergeConfig.DARK_GUI_SKIN_FILENAME;
#else
		if(EditorGUIUtility.isProSkin)
			guiSkinToUse = ObjectMergeConfig.DARK_GUI_SKIN_FILENAME;
#endif
		GUISkin _usedSkin = AssetDatabase.LoadAssetAtPath(ObjectMergeConfig.DEFAULT_PATH + "/" + guiSkinToUse, typeof(GUISkin)) as GUISkin;

		GUISkin builtinSkin = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector);
		List<GUIStyle> customStyles = new List<GUIStyle>(builtinSkin.customStyles);
		//Clear styles from last enable, or for light/dark switch
		for(int i = 0; i < builtinSkin.customStyles.Length; i++) {
			if(builtinSkin.customStyles[i].name == ObjectMergeConfig.LIST_STYLE_NAME
				|| builtinSkin.customStyles[i].name == ObjectMergeConfig.LIST_ALT_STYLE_NAME
				|| builtinSkin.customStyles[i].name == ObjectMergeConfig.LIST_STYLE_NAME + ObjectMergeConfig.CONFLICT_SUFFIX
				|| builtinSkin.customStyles[i].name == ObjectMergeConfig.LIST_ALT_STYLE_NAME + ObjectMergeConfig.CONFLICT_SUFFIX)
				customStyles.Remove(builtinSkin.customStyles[i]);
		}
		customStyles.AddRange(_usedSkin.customStyles);
		builtinSkin.customStyles = customStyles.ToArray();

#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
		assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		//NB: For some reason, after a compile, filters starts out as "", though the field retains the value.  Then when it's modified the string is set... as a result sometime you see filter text with nothing being filtered
		ParseFilters();
#else
		var subclasses =
		from assembly in System.AppDomain.CurrentDomain.GetAssemblies()
		from type in assembly.GetTypes()
		where type.IsSubclassOf(typeof(Component))
		select type;
		List<List<string>> compTypeStrs = new List<List<string>>();
		compTypeStrs.Add(new List<string>());
		List<List<System.Type>> compTypes = new List<List<System.Type>>();
		compTypes.Add(new List<System.Type>());
		int setCount = 0;
		foreach(System.Type t in subclasses) {
			if(compTypes[setCount].Count == 31) {
				setCount++;
				compTypeStrs.Add(new List<string>());
				compTypes.Add(new List<System.Type>());
			}
			compTypeStrs[setCount].Add(t.Name);
			compTypes[setCount].Add(t);
		}
		componentTypes = new System.Type[setCount + 1][];
		componentTypeStrings = new string[setCount + 1][];
		typeMask = new int[setCount + 1];
		for(int i = 0; i < setCount + 1; i++) {
			typeMask[i] = -1;
			componentTypes[i] = compTypes[i].ToArray();
			componentTypeStrings[i] = compTypeStrs[i].ToArray();
		}
#endif
#if DEV
		mine = GameObject.Find("Obj1");
		theirs = GameObject.Find("Obj2");
#endif
	}
#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
	void ParseFilters(){
		filterTypes = new List<System.Type>();
		badTypes = new List<string>();
		notComponents = new List<string>();
		string[] tmp = filters.Replace(" ", "").Split(',');
		foreach(string filter in tmp){
			if(filter != null && filter != ""){
				bool found = false;
				foreach(System.Reflection.Assembly asm in assemblies){
					foreach(System.Type t in asm.GetTypes()){
						if(t.Name.ToLower() == filter.ToLower()){
							if(t.IsSubclassOf(typeof(Component))){
								filterTypes.Add(t);
							} else notComponents.Add(filter);
							found = true;
							break;
						}
					}
					if(found)
						break;
				}
				if(!found)
					badTypes.Add(filter);
			}
		}
	}
#endif

	public void Callback (object obj) {
        Debug.Log ("Selected: " + obj);
    }
	void OnGUI() {
		//Ctrl + w to close
		if(Event.current.Equals(Event.KeyboardEvent("^w"))){
			Close();
			GUIUtility.ExitGUI();
		}
		/*
		 * SETUP
		 */
#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
		EditorGUIUtility.LookLikeControls();
#endif
		alt = false;
		//Adjust colWidth as the window resizes
		colWidth = (position.width - ObjectMergeConfig.midWidth * 2 - ObjectMergeConfig.margin) / 2;
		if(root == null)
			root = new ObjectHelper();
		/*
		 * BEGIN GUI
		 */
		GUILayout.BeginHorizontal();{
			GUILayout.BeginVertical();
			deepCopy = EditorGUILayout.Toggle(new GUIContent("Deep Copy", "When enabled, copying GameObjects or Components will search for references to them and try to set them.  Disable if you do not want this behavior or if the window locks up on copy (too many objects)"), deepCopy);
			log = EditorGUILayout.Toggle(new GUIContent("Log", "When enabled, non-obvious events (like deep copy reference setting) will be logged"), log);
			compareAttrs = EditorGUILayout.Toggle(new GUIContent("Compare Attributes", "When disabled, attributes will not be included in comparison algorithm.  To choose which components are included, use the drop-downs to the right."), compareAttrs);
			if(GUILayout.Button("Expand Differences"))
				root.ExpandDiffs();
			GUILayout.Space(10);
			GUILayout.EndVertical();
			
			GUILayout.BeginVertical();
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5			
			//TODO: Better masking for U3
			GUILayout.BeginVertical();
			GUILayout.Label("Enter a list of component types to exclude, separated by commas");
			filters = EditorGUILayout.TextField("Filters", filters);
			if(filters != lastFilters){
				ParseFilters();
			}
			lastFilters = filters;
			string filt = "Filtering: ";
			if(filterTypes.Count > 0){
				foreach(System.Type bad in filterTypes)
					filt += bad.Name + ", ";
				GUILayout.Label(filt.Substring(0, filt.Length - 2));
			}
			string err = "Sorry, the following types are invalid: ";
			if(badTypes.Count > 0){
				foreach(string bad in badTypes)
					err += bad + ", ";
				GUILayout.Label(err.Substring(0, err.Length - 2));
			}
			string cerr = "Sorry, the following types aren't components: ";
			if(notComponents.Count > 0){
				foreach(string bad in notComponents)
					cerr += bad + ", ";
				GUILayout.Label(cerr.Substring(0, cerr.Length - 2));
			}
			GUILayout.EndVertical();
#else
			GUILayout.Label(new GUIContent("Comparison Filters", "Select which components should be included in the comparison.  This is a little bugged right now so its disabled.  You can't filter more than 31 things :("));
			for(int i = 0; i < componentTypeStrings.Length; i++) {
				typeMask[i] = EditorGUILayout.MaskField(typeMask[i], componentTypeStrings[i], GUILayout.Width(75));
				if(i % 3 == 2) {
					GUILayout.EndHorizontal();
					GUILayout.BeginHorizontal();
					GUILayout.FlexibleSpace();
				}
			}
#endif
			GUILayout.EndHorizontal();
			GUILayout.EndVertical();
		}
		GUILayout.EndHorizontal();
		GUILayout.BeginHorizontal();
		{
			GUILayout.BeginVertical(GUILayout.Width(colWidth));
			{
#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
				mine = root.mine = (GameObject)EditorGUILayout.ObjectField("Mine", mine, typeof(GameObject));
#else
				mine = root.mine = (GameObject)EditorGUILayout.ObjectField("Mine", mine, typeof(GameObject), true);
#endif
			}
			GUILayout.EndVertical();
			GUILayout.Space(ObjectMergeConfig.midWidth * 2);
			GUILayout.BeginVertical(GUILayout.Width(colWidth));
			{
#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
				theirs = root.theirs = (GameObject)EditorGUILayout.ObjectField("Theirs", theirs, typeof(GameObject));
#else
				theirs = root.theirs = (GameObject)EditorGUILayout.ObjectField("Theirs", theirs, typeof(GameObject), true);
#endif
			}
			GUILayout.EndVertical();
		}
		GUILayout.EndHorizontal();
		if(lastRoot == null) {
			lastRoot = new ObjectHelper();
		}
		if(root.mine != lastRoot.mine || root.theirs != lastRoot.theirs) {
			refresh = root.DoRefresh();
		}
		lastRoot.mine = root.mine;
		lastRoot.theirs = root.theirs;
		if(root.mine && root.theirs) {
			scroll = GUILayout.BeginScrollView(scroll);
			root.Draw();
			GUILayout.EndScrollView();
		}
		if(refresh != null) {
			Rect pbar = GUILayoutUtility.GetRect(position.width, PROGRESS_BAR_HEIGHT);
			EditorGUI.ProgressBar(pbar, (float)ObjectMerge.refreshCount / ObjectMerge.totalRefreshNum, ObjectMerge.refreshCount + "/" + ObjectMerge.totalRefreshNum);
			if(GUILayout.Button("Cancel")) {
				cancelRefresh = true;
				EditorGUIUtility.ExitGUI();
			}
		}
	}

	void Update() {
		/*
		 * Ad-hoc editor window coroutine:  Function returns and IEnumerator, and the Update function calls MoveNext
		 * Refresh will only run when the ObjectMerge window is focused
		 */
		if(cancelRefresh) {
			refresh = null;
			refresh2 = null;		   
		}
		if(refresh != null) {
			Repaint();
			if(!refresh.MoveNext())
				refresh = null;
		}
		if(refresh2 != null)
			if(!refresh2.MoveNext())
				refresh2 = null;
		cancelRefresh = false;
	}
	public delegate void MidButton();
	public static void DrawMidButtons(MidButton toMine, MidButton toTheirs) {
		DrawMidButtons(true, true, toMine, toTheirs, null, null);
	}
	public static void DrawMidButtons(bool hasMine, bool hasTheirs, MidButton toMine, MidButton toTheirs, MidButton delMine, MidButton delTheirs) {
		GUILayout.BeginVertical(GUILayout.Width(ObjectMergeConfig.midWidth * 2));
		GUILayout.Space(3);
		GUILayout.BeginHorizontal();
		if(hasTheirs) {
			if(GUILayout.Button(new GUIContent("<", "Copy theirs (properties and children) to mine"), GUILayout.Width(ObjectMergeConfig.midWidth))) {
				toMine.Invoke();
			}
		} else {
			if(GUILayout.Button(new GUIContent("X", "Delete mine"), GUILayout.Width(ObjectMergeConfig.midWidth))) {
				delMine.Invoke();
			}
		}
		if(hasMine) {
			if(GUILayout.Button(new GUIContent(">", "Copy mine (properties and children) to theirs"), GUILayout.Width(ObjectMergeConfig.midWidth))) {
				toTheirs.Invoke();
			}
		} else {
			if(GUILayout.Button(new GUIContent("X", "Delete theirs"), GUILayout.Width(ObjectMergeConfig.midWidth))) {
				delTheirs.Invoke();
			}
		}
		GUILayout.EndHorizontal();
		GUILayout.EndVertical();

	}
}
public class ObjectHelper {
	public ObjectHelper parent;
	public GameObject mine, theirs;
	public List<ObjectHelper> children = new List<ObjectHelper>();
	public List<ComponentHelper> components = new List<ComponentHelper>();
	public bool foldout, same, sameAttrs, show, showAttrs;
	public delegate void OnRefreshComplete(ObjectHelper self);
	
	public IEnumerator DoRefresh() { return DoRefresh(true); }
	public IEnumerator DoRefresh(OnRefreshComplete onComplete) { return DoRefresh(true, onComplete); }
	public IEnumerator DoRefresh(bool deep) { return DoRefresh(deep, null); }
	public IEnumerator DoRefresh(bool deep, OnRefreshComplete onComplete){
		if(mine) {
			List<GameObject> mineList = new List<GameObject>();
			UniMerge.Util.GameObjectToList(mine, mineList);
			ObjectMerge.totalRefreshNum = mineList.Count;
		}
		if(theirs) {
			List<GameObject> theirsList = new List<GameObject>();
			UniMerge.Util.GameObjectToList(theirs, theirsList);
			if(theirsList.Count > ObjectMerge.totalRefreshNum)
				ObjectMerge.totalRefreshNum = theirsList.Count;
		}
		ObjectMerge.refreshCount = 0;
		ObjectMerge.refreshWatch = new System.Diagnostics.Stopwatch();
		ObjectMerge.refreshWatch.Start();
		foreach(IEnumerable e in Refresh(deep))
			yield return e;
		if(onComplete != null)
			onComplete.Invoke(this);
	}
	public IEnumerable Refresh() { return Refresh(true); }
	public IEnumerable Refresh(bool deep) {
		ObjectMerge.refreshCount++;
		if(ObjectMerge.refreshWatch.Elapsed.TotalSeconds > ObjectMerge.maxFrameTime) {
			ObjectMerge.refreshWatch.Reset();
			yield return null;
			ObjectMerge.refreshWatch.Start();
		}
		same = true;
		//Deep refresh will refresh children (and thus re-construct the ObjectHelpers, which has the effect of collapsing the foldouts.
		//Non-deep refresh will only update sameness.  Ideally I'd like to go back to a single refresh which would preserve collapse state
		if(deep) {
			components.Clear();
			children.Clear();
			List<GameObject> myChildren = new List<GameObject>();
			List<GameObject> theirChildren = new List<GameObject>();
			List<Component> myComponents = new List<Component>();
			List<Component> theirComponents = new List<Component>();
			if(mine) {
				foreach(Transform t in mine.transform)
					myChildren.Add(t.gameObject);
				myComponents = new List<Component>(mine.GetComponents<Component>());
			}
			if(theirs) {
				foreach(Transform t in theirs.transform)
					theirChildren.Add(t.gameObject);
				theirComponents = new List<Component>(theirs.GetComponents<Component>());
			}
			//TODO: turn these two chunks into one function... somehow
			//Merge Components
			ComponentHelper ch = null;
			for(int i = 0; i < myComponents.Count; i++) {
				Component match = null;
				Component myComponent = null;
				myComponent = myComponents[i];
				if(myComponent != null) {
					foreach(Component g in theirComponents) {
						if(g != null) {
							if(myComponent.GetType() == g.GetType()) {
								match = g;
								break;
							}
						}
					}
					ch = new ComponentHelper(this, myComponent, match);
					foreach(IEnumerable e in ch.Refresh())
						yield return e;
					components.Add(ch);

					if(!ComponentIsFiltered(ch.type) && !ch.same)
						same = false;
					theirComponents.Remove(match);
				}
			}
			if(theirComponents.Count > 0) {
				foreach(Component g in theirComponents) {
					ch = new ComponentHelper(this, null, g);
					foreach(IEnumerable e in ch.Refresh())
						yield return e;
					if(!ComponentIsFiltered(ch.type) && !ch.same)
						same = false;
					components.Add(ch);
				}
			}
			//Merge Children
			for(int i = 0; i < myChildren.Count; i++) {
				GameObject match = null;
				foreach(GameObject g in theirChildren) {
					if(SameObject(myChildren[i], g)) {
						match = g;
						break;
					}
				}
				children.Add(new ObjectHelper { parent = this, mine = myChildren[i], theirs = match });
				theirChildren.Remove(match);
			}
			if(theirChildren.Count > 0) {
				this.same = false;
				foreach(GameObject g in theirChildren)
					children.Add(new ObjectHelper { parent = this, theirs = g });
			}
			List<ObjectHelper> tmp = new List<ObjectHelper>(children);
			foreach(ObjectHelper child in tmp) {
				foreach(IEnumerable e in child.Refresh(deep))
					yield return e;
				if(!child.same)
					same = false;
			}
			sameAttrs = CheckAttrs();
			if(!sameAttrs && ObjectMerge.compareAttrs)
				same = false;
		} else {
			foreach(ComponentHelper component in components) {
				foreach(IEnumerable e in component.Refresh())
					yield return e;
				if(!ComponentIsFiltered(component.type) && !component.same)
					same = false;
			}
			List<ObjectHelper> tmp = new List<ObjectHelper>(children);
			foreach(ObjectHelper child in tmp) {
				foreach(IEnumerable e in child.Refresh(deep))
					yield return e;
				if(!child.same)
					same = false;
			}
			sameAttrs = CheckAttrs();
			if(!sameAttrs && ObjectMerge.compareAttrs)
				same = false;
		}
		yield break;
	}
	bool ComponentIsFiltered(System.Type type) {
#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
		//TODO: Better U3 filtering
		for(int i = 0; i < ObjectMerge.filterTypes.Count; i++) {
			if(type == ObjectMerge.filterTypes[i])
				return true;
		}
#else
		for(int i = 0; i < ObjectMerge.componentTypes.Length; i++) {
			if(ObjectMerge.typeMask[i] == -1)	//This has everything, continue
				continue;
			int idx = ArrayUtility.IndexOf<System.Type>(ObjectMerge.componentTypes[i], type);
			if(idx != -1)
				return ((ObjectMerge.typeMask[i] >> idx) & 1) == 0;
		}
#endif
		return false;		//Assume not filtered
	}
	public void Draw() { Draw(1); }
	/// <summary>
	/// Draw this node in the tree.  This method 
	/// </summary>
	/// <param name="width"></param>
	public void Draw(float width) {
		//This object
		string style = ObjectMerge.alt ? ObjectMergeConfig.LIST_ALT_STYLE_NAME : ObjectMergeConfig.LIST_STYLE_NAME;
		if(!same)
			style += "Conflict";
		EditorGUILayout.BeginHorizontal(style);
		//Display mine
		DrawObject(true, width);
		//Swap buttons
		ObjectMerge.DrawMidButtons(mine, theirs, delegate() {
			//NB: This still thows a SerializedProperty error (at least in 3.0) gonna have to do a bit more poking.
			Copy(true);
			if(parent != null)
#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
				ObjectMerge.refresh = parent.DoRefresh();
#else
				ObjectMerge.refresh = parent.DoRefresh(false);
#endif
			ObjectMerge.refresh2 = DoRefresh();
		}, delegate() {
			Copy(false);
			if(parent != null)
#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
				ObjectMerge.refresh = parent.DoRefresh();
#else
				ObjectMerge.refresh = parent.DoRefresh(false);
#endif
			ObjectMerge.refresh2 = DoRefresh();
		}, delegate() {
			DestroyAndClearRefs(mine, true);
			ObjectMerge.refresh = parent.DoRefresh();
		}, delegate() {
			DestroyAndClearRefs(theirs, false);
			ObjectMerge.refresh = parent.DoRefresh();
		});
		//Display theirs
		DrawObject(false, width);
		GUILayout.BeginVertical();
		GUILayout.Space(3);
		GUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		if(GUILayout.Button(show ? "Hide" : "Show", GUILayout.Width(45))) {
			show = !show;
			if(Event.current.alt) {
				showAttrs = show;
				foreach(ComponentHelper component in components) {
					component.show = show;
				}
			}
		}
		if(GUILayout.Button(new GUIContent("R", "Refresh this object"), GUILayout.Width(25))) {
			ObjectMerge.refresh = DoRefresh();
		}
		GUILayout.EndHorizontal();
		GUILayout.EndVertical();
		EditorGUILayout.EndHorizontal();
		ObjectMerge.alt = !ObjectMerge.alt;
		if(show) {
			DrawAttributes(width + UniMerge.Util.TAB_SIZE * 2);
			if(!ComponentIsFiltered(null) && !sameAttrs)
				same = false;
			List<ComponentHelper> tmp = new List<ComponentHelper>(components);
			foreach(ComponentHelper component in tmp) {
				component.Draw(width + UniMerge.Util.TAB_SIZE * 2);
			}
			GUILayout.Space(10);
		}
		//Children
		if(foldout) {
			List<ObjectHelper> tmp = new List<ObjectHelper>(children);
			foreach(ObjectHelper helper in tmp)
				helper.Draw(width + UniMerge.Util.TAB_SIZE);
		}
	}

	public void DestroyAndClearRefs(Object obj, bool isMine) {
		List<PropertyHelper> properties = new List<PropertyHelper>();
		FindRefs(obj, isMine, properties);
		foreach(PropertyHelper property in properties) {
			property.GetProperty(isMine).objectReferenceValue = null;
			if(ObjectMerge.log)
				Debug.Log("Set reference to null in " + property.GetProperty(isMine).serializedObject.targetObject
					+ "." + property.GetProperty(isMine).name, property.GetProperty(isMine).serializedObject.targetObject);
			if(property.GetProperty(!isMine).serializedObject.targetObject != null)
				property.GetProperty(isMine).serializedObject.ApplyModifiedProperties();
		}
		ObjectMerge.root.UnsetFlagRecursive();
		Object.DestroyImmediate(obj);
	}
	bool findAndSetFlag = false;
	void Copy(bool toMine) {
		if(toMine) {
			//NB: I thought I should use EditorUitlity.CopySerialized here but it don't work right
			if(parent == null) {	//Top-level object
				if(mine){
					Object.DestroyImmediate(mine);
				}
				mine = Copy(theirs, false);
				ObjectMerge.mine = mine;
			} else if(parent.mine) {
				if(mine){
					Object.DestroyImmediate(mine);
				}
				mine = Copy(theirs, false);
			} else Debug.LogWarning("Can't copy this object.  Destination parent doesn't exist!");
		} else {
			if(parent == null) {	//Top-level object
				if(theirs){
					Object.DestroyImmediate(theirs);
				}
				theirs = Copy(mine, true);
				ObjectMerge.theirs = theirs;
			} else if(parent.theirs) {
				if(theirs){
					Object.DestroyImmediate(theirs);
				}
				theirs = Copy(mine, true);
			} else Debug.LogWarning("Can't copy this object.  Destination parent doesn't exist!");
		}
	}
	/// <summary>
	/// Do the actual GameObject copy.  This is generalized because the process is totally symmetrical. Come to think of it so is the 
	/// other version of Copy.  I can probably merge these back together.
	/// </summary>
	/// <param name="original">The object to be copied</param>
	/// <param name="isMine">Whether "original" is mine or theirs</param>
	/// <returns>The copied object</returns>
	GameObject Copy(GameObject original, bool isMine) {
		GameObject copy = null;
#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
		if(UniMerge.Util.IsPrefabParent(original) && EditorUtility.GetPrefabType(original) == PrefabType.PrefabInstance){
			copy = (GameObject)EditorUtility.InstantiatePrefab(EditorUtility.GetPrefabParent(original));
#else
		if(UniMerge.Util.IsPrefabParent(original) && PrefabUtility.GetPrefabType(original) == PrefabType.PrefabInstance) {
			copy = (GameObject)PrefabUtility.InstantiatePrefab(PrefabUtility.GetPrefabParent(original));
#endif
			//Copy all properties in case they differ from prefab
			List<GameObject> sourceList = new List<GameObject>();
			List<GameObject> copyList = new List<GameObject>();
			UniMerge.Util.GameObjectToList(original, sourceList);
			UniMerge.Util.GameObjectToList(copy, copyList);
			for(int i = 0; i < sourceList.Count; i++) {
				copyList[i].name = sourceList[i].name;
				copyList[i].layer = sourceList[i].layer;
				copyList[i].tag = sourceList[i].tag;
				copyList[i].isStatic = sourceList[i].isStatic;
				copyList[i].hideFlags = sourceList[i].hideFlags;
#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
				copyList[i].active = sourceList[i].active;
#else
				copyList[i].SetActive(sourceList[i].activeSelf);
#endif
				Component[] sourceComps = sourceList[i].GetComponents<Component>();
				Component[] copyComps = copyList[i].GetComponents<Component>();
				for(int j = 0; j < sourceComps.Length; j++) {
					EditorUtility.CopySerialized(sourceComps[j], copyComps[j]);
				}
			}
		} else
			copy = (GameObject)Object.Instantiate(original);
		copy.name = original.name;		//because of stupid (clone)
		if(parent != null)
			copy.transform.parent = parent.GetObject(!isMine).transform;
		copy.transform.localPosition = original.transform.localPosition;
		copy.transform.localRotation = original.transform.localRotation;
		copy.transform.localScale = original.transform.localScale;
		//Set any references on their side to this object
		//Q: is this neccessary when copying top-object?
		if(ObjectMerge.deepCopy)
			FindAndSetRefs(original, copy, isMine);
		return copy;
	}
	#region ATTRIBUTES
	/* Compared attributes:
	 * Name
	 * Active
	 * Static
	 * Tag
	 * Layer
	 * HideFlags
	 */

	private void DrawAttributes(float width) {
		//TODO: Draw GO fields as serializedProperty or something
		string style = ObjectMerge.alt ? ObjectMergeConfig.LIST_ALT_STYLE_NAME : ObjectMergeConfig.LIST_STYLE_NAME;
		sameAttrs = CheckAttrs();
		if(!sameAttrs)
			style += "Conflict";
		GUILayout.BeginHorizontal(style);
		GUILayout.BeginVertical(GUILayout.Width(ObjectMerge.colWidth));
		UniMerge.Util.Indent(width, delegate() {
			if(GetObject(true))
				showAttrs = EditorGUILayout.Foldout(showAttrs, "Attributes");
			else
				GUILayout.Label("");
		});
		GUILayout.EndVertical();
		if(GetObject(true) && GetObject(false)) {
			ObjectMerge.DrawMidButtons(delegate() {
				SetAttrs(true);
			}, delegate() {
				SetAttrs(false);
			});
		} else GUILayout.Space(ObjectMergeConfig.midWidth * 2);
		GUILayout.BeginVertical(GUILayout.Width(ObjectMerge.colWidth));
		UniMerge.Util.Indent(width, delegate() {
			if(GetObject(false))
				showAttrs = EditorGUILayout.Foldout(showAttrs, "Attributes");
			else
				GUILayout.Label("");
		});
		GUILayout.EndVertical();
		GUILayout.FlexibleSpace();
		if(GUILayout.Button(new GUIContent("R", "Refresh this object"), GUILayout.Width(25))) {
			ObjectMerge.refresh = DoRefresh();
		}
		GUILayout.EndHorizontal();
		ObjectMerge.alt = !ObjectMerge.alt;

		//Note: I know this hardcoded stuff is stupid.  Someday I'll make it smarter.
		if(showAttrs) {
			DrawName(width + UniMerge.Util.TAB_SIZE);
			DrawActive(width + UniMerge.Util.TAB_SIZE);
			DrawStatic(width + UniMerge.Util.TAB_SIZE);
			DrawTag(width + UniMerge.Util.TAB_SIZE);
			DrawLayer(width + UniMerge.Util.TAB_SIZE);
			DrawHideFlags(width + UniMerge.Util.TAB_SIZE);
		}
	}
	void DrawName(float width) {
		string style = ObjectMerge.alt ? ObjectMergeConfig.LIST_ALT_STYLE_NAME : ObjectMergeConfig.LIST_STYLE_NAME;
		bool same = GetObject(true);
		if(same)
			same = GetObject(false);
		if(same)
			same = GetObject(true).name == GetObject(false).name;
		if(GetObject(true) && GetObject(false) && !same)
			same = GetObject(true).name == "mine" && GetObject(false).name == "theirs";
		if(!same) {
			style += "Conflict";
			sameAttrs = same;
		}
		GUILayout.BeginHorizontal(style);
		GUILayout.BeginVertical(GUILayout.Width(ObjectMerge.colWidth));
		UniMerge.Util.Indent(width, delegate() {
			if(GetObject(true))
				GetObject(true).name = EditorGUILayout.TextField("Name", GetObject(true).name);
		});
		GUILayout.EndVertical();
		if(mine && theirs) {
			ObjectMerge.DrawMidButtons(delegate() {
				GetObject(true).name = GetObject(false).name;
				if(parent != null)
					ObjectMerge.refresh = parent.DoRefresh(false);
				ObjectMerge.refresh2 = DoRefresh();
			}, delegate() {
				GetObject(false).name = GetObject(true).name;
				if(parent != null)
					ObjectMerge.refresh = parent.DoRefresh(false);
				ObjectMerge.refresh2 = DoRefresh();
			});
		} else GUILayout.Space(ObjectMergeConfig.midWidth * 2);
		GUILayout.BeginVertical(GUILayout.Width(ObjectMerge.colWidth));
		UniMerge.Util.Indent(width, delegate() {
			if(GetObject(false))
				GetObject(false).name = EditorGUILayout.TextField("Name", GetObject(false).name);
		});
		GUILayout.EndVertical();
		//Create space to offset for no reset button
		GUILayout.Space(ObjectMerge.RightColWidth);
		GUILayout.EndHorizontal();
		ObjectMerge.alt = !ObjectMerge.alt;
	}
	void DrawLayer(float width) {
		string style = ObjectMerge.alt ? ObjectMergeConfig.LIST_ALT_STYLE_NAME : ObjectMergeConfig.LIST_STYLE_NAME;
		bool same = GetObject(true);
		if(same)
			same = GetObject(false);
		if(same)
			same = GetObject(true).layer == GetObject(false).layer;
		if(!same) {
			style += "Conflict";
			sameAttrs = same;
		}
		GUILayout.BeginHorizontal(style);
		GUILayout.BeginVertical(GUILayout.Width(ObjectMerge.colWidth));
		UniMerge.Util.Indent(width, delegate() {
			if(GetObject(true))
				GetObject(true).layer = EditorGUILayout.IntField("Layer", GetObject(true).layer);
		});
		GUILayout.EndVertical();
		if(mine && theirs) {
			ObjectMerge.DrawMidButtons(delegate() {
				GetObject(true).layer = GetObject(false).layer;
				if(parent != null)
					ObjectMerge.refresh = parent.DoRefresh(false);
				ObjectMerge.refresh2 = DoRefresh();
			}, delegate() {
				GetObject(false).layer = GetObject(true).layer;
				if(parent != null)
					ObjectMerge.refresh = parent.DoRefresh(false);
				ObjectMerge.refresh2 = DoRefresh();
			});
		} else GUILayout.Space(ObjectMergeConfig.midWidth * 2);
		GUILayout.BeginVertical(GUILayout.Width(ObjectMerge.colWidth));
		UniMerge.Util.Indent(width, delegate() {
			if(GetObject(false))
				GetObject(false).layer = EditorGUILayout.IntField("Layer", GetObject(false).layer);
		});
		GUILayout.EndVertical();
		//Create space to offset for no reset button
		GUILayout.Space(ObjectMerge.RightColWidth);
		GUILayout.EndHorizontal();
		ObjectMerge.alt = !ObjectMerge.alt;
	}
	void DrawHideFlags(float width) {
		string style = ObjectMerge.alt ? ObjectMergeConfig.LIST_ALT_STYLE_NAME : ObjectMergeConfig.LIST_STYLE_NAME;
		bool same = GetObject(true);
		if(same)
			same = GetObject(false);
		if(same)
			same = GetObject(true).hideFlags == GetObject(false).hideFlags;
		if(!same) {
			style += "Conflict";
			sameAttrs = same;
		}
		GUILayout.BeginHorizontal(style);
		GUILayout.BeginVertical(GUILayout.Width(ObjectMerge.colWidth));
		UniMerge.Util.Indent(width, delegate() {
			if(GetObject(true))
				GetObject(true).hideFlags = (HideFlags)EditorGUILayout.IntField("HideFlags", (int)GetObject(true).hideFlags);
		});
		GUILayout.EndVertical();
		if(mine && theirs) {
			ObjectMerge.DrawMidButtons(delegate() {
				GetObject(true).hideFlags = GetObject(false).hideFlags;
				if(parent != null)
					ObjectMerge.refresh = parent.DoRefresh(false);
				ObjectMerge.refresh2 = DoRefresh();
			}, delegate() {
				GetObject(false).hideFlags = GetObject(true).hideFlags;
				if(parent != null)
					ObjectMerge.refresh = parent.DoRefresh(false);
				ObjectMerge.refresh2 = DoRefresh();
			});
		} else GUILayout.Space(ObjectMergeConfig.midWidth * 2);
		GUILayout.BeginVertical(GUILayout.Width(ObjectMerge.colWidth));
		UniMerge.Util.Indent(width, delegate() {
			if(GetObject(false))
				GetObject(false).hideFlags = (HideFlags)EditorGUILayout.IntField("HideFlags", (int)GetObject(false).hideFlags);
		});
		GUILayout.EndVertical();
		//Create space to offset for no reset button
		GUILayout.Space(ObjectMerge.RightColWidth);
		GUILayout.EndHorizontal();
		ObjectMerge.alt = !ObjectMerge.alt;
	}
	void DrawTag(float width) {
		string style = ObjectMerge.alt ? ObjectMergeConfig.LIST_ALT_STYLE_NAME : ObjectMergeConfig.LIST_STYLE_NAME;
		bool same = GetObject(true);
		if(same)
			same = GetObject(false);
		if(same)
			same = GetObject(true).tag == GetObject(false).tag;
		if(!same) {
			style += "Conflict";
			sameAttrs = same;
		}
		GUILayout.BeginHorizontal(style);
		GUILayout.BeginVertical(GUILayout.Width(ObjectMerge.colWidth));
		UniMerge.Util.Indent(width, delegate() {
			if(GetObject(true))
				GetObject(true).tag = EditorGUILayout.TextField("Tag", GetObject(true).tag);
		});
		GUILayout.EndVertical();
		if(mine && theirs) {
			ObjectMerge.DrawMidButtons(delegate() {
				GetObject(true).tag = GetObject(false).tag;
				if(parent != null)
					ObjectMerge.refresh = parent.DoRefresh(false);
				ObjectMerge.refresh2 = DoRefresh();
			}, delegate() {
				GetObject(false).tag = GetObject(true).tag;
				if(parent != null)
					ObjectMerge.refresh = parent.DoRefresh(false);
				ObjectMerge.refresh2 = DoRefresh();
			});
		} else GUILayout.Space(ObjectMergeConfig.midWidth * 2);
		GUILayout.BeginVertical(GUILayout.Width(ObjectMerge.colWidth));
		UniMerge.Util.Indent(width, delegate() {
			if(GetObject(false))
				GetObject(false).tag = EditorGUILayout.TextField("Tag", GetObject(false).tag);
		});
		GUILayout.EndVertical();
		//Create space to offset for no reset button
		GUILayout.Space(ObjectMerge.RightColWidth);
		GUILayout.EndHorizontal();
		ObjectMerge.alt = !ObjectMerge.alt;
	}
	void DrawStatic(float width) {
		string style = ObjectMerge.alt ? ObjectMergeConfig.LIST_ALT_STYLE_NAME : ObjectMergeConfig.LIST_STYLE_NAME;
		bool same = GetObject(true);
		if(same)
			same = GetObject(false);
		if(same)
			same = GetObject(true).isStatic == GetObject(false).isStatic;
		if(!same) {
			style += "Conflict";
			sameAttrs = same;
		}
		GUILayout.BeginHorizontal(style);
		GUILayout.BeginVertical(GUILayout.Width(ObjectMerge.colWidth));
		UniMerge.Util.Indent(width, delegate() {
			if(GetObject(true))
				GetObject(true).isStatic = EditorGUILayout.Toggle("Static", GetObject(true).isStatic);
		});
		GUILayout.EndVertical();
		if(mine && theirs) {
			ObjectMerge.DrawMidButtons(delegate() {
				GetObject(true).isStatic = GetObject(false).isStatic;
				if(parent != null)
					ObjectMerge.refresh = parent.DoRefresh(false);
				ObjectMerge.refresh2 = DoRefresh();
			}, delegate() {
				GetObject(false).isStatic = GetObject(true).isStatic;
				if(parent != null)
					ObjectMerge.refresh = parent.DoRefresh(false);
				ObjectMerge.refresh2 = DoRefresh();
			});
		} else GUILayout.Space(ObjectMergeConfig.midWidth * 2);
		GUILayout.BeginVertical(GUILayout.Width(ObjectMerge.colWidth));
		UniMerge.Util.Indent(width, delegate() {
			if(GetObject(false))
				GetObject(false).isStatic = EditorGUILayout.Toggle("Static", GetObject(false).isStatic);
		});
		GUILayout.EndVertical();
		//Create space to offset for no reset button
		GUILayout.Space(ObjectMerge.RightColWidth);
		GUILayout.EndHorizontal();
		ObjectMerge.alt = !ObjectMerge.alt;
	}
	void DrawActive(float width) {
		string style = ObjectMerge.alt ? ObjectMergeConfig.LIST_ALT_STYLE_NAME : ObjectMergeConfig.LIST_STYLE_NAME;
		bool same = GetObject(true);
		if(same)
			same = GetObject(false);
		if(same)
#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
			same = GetObject(true).active == GetObject(false).active;
#else
			same = GetObject(true).activeSelf == GetObject(false).activeSelf;
#endif
		if(!same) {
			style += "Conflict";
		}
		GUILayout.BeginHorizontal(style);
		GUILayout.BeginVertical(GUILayout.Width(ObjectMerge.colWidth));
		UniMerge.Util.Indent(width, delegate() {
			if(GetObject(true))
#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
				GetObject(true).active = EditorGUILayout.Toggle("Active", GetObject(true).active);
#else
				GetObject(true).SetActive(EditorGUILayout.Toggle("Active", GetObject(true).activeSelf));
#endif
		});
		GUILayout.EndVertical();
		if(mine && theirs) {
			ObjectMerge.DrawMidButtons(delegate() {
#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
				GetObject(true).active = GetObject(false).active;
#else
				GetObject(true).SetActive(GetObject(false).activeSelf);
#endif
				if(parent != null)
					ObjectMerge.refresh = parent.DoRefresh(false);
				ObjectMerge.refresh2 = DoRefresh();
			}, delegate() {
#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
				GetObject(false).active = GetObject(true).active;
#else
				GetObject(false).SetActive(GetObject(true).activeSelf);
#endif
				if(parent != null)
					ObjectMerge.refresh = parent.DoRefresh(false);
				ObjectMerge.refresh2 = DoRefresh();
			});
		} else GUILayout.Space(ObjectMergeConfig.midWidth * 2);
		GUILayout.BeginVertical(GUILayout.Width(ObjectMerge.colWidth));
		UniMerge.Util.Indent(width, delegate() {
			if(GetObject(false))
#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
				GetObject(false).active = EditorGUILayout.Toggle("Active", GetObject(false).active);
#else
				GetObject(false).SetActive(EditorGUILayout.Toggle("Active", GetObject(false).activeSelf));
#endif
		});
		GUILayout.EndVertical();
		//Create space to offset for no reset button
		GUILayout.Space(ObjectMerge.RightColWidth);
		GUILayout.EndHorizontal();
		ObjectMerge.alt = !ObjectMerge.alt;
	}
	bool CheckAttrs() {
		if(GetObject(true) == null && GetObject(false) == null)
			return true;
		if(GetObject(true) == null)
			return false;
		if(GetObject(false) == null)
			return false;
		if(GetObject(true).name == "mine" && GetObject(false).name == "theirs")
			return true;
		if(GetObject(true).name != GetObject(false).name)
			return false;
		if(GetObject(true).layer != GetObject(false).layer)
			return false;
		if(GetObject(true).hideFlags != GetObject(false).hideFlags)
			return false;
#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
		if(GetObject(true).active != GetObject(false).active)
#else
		if(GetObject(true).activeSelf != GetObject(false).activeSelf)
#endif
			return false;
		if(GetObject(true).isStatic != GetObject(false).isStatic)
			return false;
		if(GetObject(true).tag != GetObject(false).tag)
			return false;
		return true;
	}
	void SetAttrs(bool toMine) {
		GetObject(toMine).name = GetObject(!toMine).name;
		GetObject(toMine).layer = GetObject(!toMine).layer;
#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
		GetObject(toMine).active = GetObject(!toMine).active;
#else
		GetObject(toMine).SetActive(GetObject(!toMine).activeSelf);
#endif
		GetObject(toMine).isStatic = GetObject(!toMine).isStatic;
		GetObject(toMine).tag = GetObject(!toMine).tag;
		sameAttrs = true;
	}
	#endregion
	/// <summary>
	/// Find references of source in Mine, and set their counterparts in Theirs to copy. This "start" function calls
	/// FindRefs which searches the whole object's heirarchy, and then calls UnsetFlagRecursive to reset the flag
	/// used to avoid searching the same object twice
	/// </summary>
	/// <param name="source">The source object being referenced</param>
	/// <param name="copy">A new copy of the source object, which will be referenced</param>
	/// <param name="isMine">Whether the source object is on the Mine (left) side</param>
	void FindAndSetRefs(GameObject source, GameObject copy, bool isMine) {
		//NOTE: There may be some possibilities of missing references going on in this function. Has not been exhaustively tested yet.
		List<PropertyHelper> properties = new List<PropertyHelper>();

		List<GameObject> sourceList = new List<GameObject>();
		List<GameObject> copyList = new List<GameObject>();
		UniMerge.Util.GameObjectToList(source, sourceList);
		UniMerge.Util.GameObjectToList(copy, copyList);

		for(int i = 0; i < sourceList.Count; i++) {
			properties.Clear();
			FindRefs(sourceList[i], isMine, properties);
			foreach(PropertyHelper property in properties) {
				//Sometimes you get an error here in older versions of Unity about using a SerializedProperty after the object has been deleted.  Don't know how else to detect this
				if(property.GetProperty(!isMine).serializedObject.targetObject != null){
					property.GetProperty(!isMine).objectReferenceValue = copyList[i];
					if(ObjectMerge.log)
						Debug.Log("Set reference to " + copyList[i] + " in " + property.GetProperty(!isMine).serializedObject.targetObject
							+ "." + property.GetProperty(!isMine).name, property.GetProperty(!isMine).serializedObject.targetObject);
					property.GetProperty(!isMine).serializedObject.ApplyModifiedProperties();
				}
			}
			ObjectMerge.root.UnsetFlagRecursive();
			Component[] sourceComps = sourceList[i].GetComponents<Component>();
			Component[] copyComps = copyList[i].GetComponents<Component>();
			for(int j = 0; j < sourceComps.Length; j++) {
				properties.Clear();
				FindRefs(sourceComps[j], isMine, properties);
				foreach(PropertyHelper property in properties) {
					//Sometimes you get an error here in older versions of Unity about using a SerializedProperty after the object has been deleted.  Don't know how else to detect this
					if(property.GetProperty(!isMine).serializedObject.targetObject != null){
						property.GetProperty(!isMine).objectReferenceValue = copyComps[j];
						if(ObjectMerge.log)
							Debug.Log("Set reference to " + copyComps[j] + " in " + property.GetProperty(!isMine).serializedObject.targetObject
								+ "." + property.GetProperty(!isMine).name, property.GetProperty(!isMine).serializedObject.targetObject);
						property.GetProperty(!isMine).serializedObject.ApplyModifiedProperties();
					}
				}
				ObjectMerge.root.UnsetFlagRecursive();
			}
		}
	}
	public void FindRefs(Object source, bool isMine, List<PropertyHelper> properties) {
		if(findAndSetFlag)
			return;
		findAndSetFlag = true;
		foreach(ComponentHelper component in components) {
			foreach(PropertyHelper property in component.properties) {
				if(property.GetProperty(isMine) != null && property.GetProperty(!isMine) != null) {
					if(property.GetProperty(isMine).propertyType == SerializedPropertyType.ObjectReference)
						if(property.GetProperty(isMine).objectReferenceValue == source)
							properties.Add(property);
				}
			}
		}
		if(parent != null)
			parent.FindRefs(source, isMine, properties);
		foreach(ObjectHelper helper in children)
			helper.FindRefs(source, isMine, properties);
	}
	/// <summary>
	/// Get the spouse (counterpart) of an object within this tree.  If the spouse is missing, copy the object and return the copy
	/// </summary>
	/// <param name="obj">The object we're looking for</param>
	/// <param name="isMine">Whether the object came from Mine (left)</param>
	/// <returns></returns>
	public GameObject GetObjectSpouse(GameObject obj, bool isMine) {
		if(obj == GetObject(isMine)) {
			if(GetObject(!isMine)) {
				return GetObject(!isMine);
			} else {
				Copy(!isMine);
				if(ObjectMerge.log && GetObject(isMine))
					Debug.Log("Copied " + GetObject(!isMine) + " in order to transfer reference");
			}
			return GetObject(!isMine);
		} else {
			foreach(ObjectHelper child in children)
				if(child.GetObjectSpouse(obj, isMine))
					return child.GetObjectSpouse(obj, isMine);
		}
		return null;
	}
	public void UnsetFlagRecursive() {
		findAndSetFlag = false;
		foreach(ObjectHelper helper in children)
			helper.UnsetFlagRecursive();
	}

	private void DrawObject(bool isMine, float width) {
		bool foldoutState = foldout;
		GUILayout.BeginVertical(GUILayout.Width(ObjectMerge.colWidth));
		UniMerge.Util.Indent(width, delegate() {
			GUILayout.BeginHorizontal();
			if(GetObject(isMine)) {
				if(GetObject(isMine).transform.childCount > 0)
					foldout = EditorGUILayout.Foldout(foldout, GetObject(isMine).name);
				else
					GUILayout.Label(GetObject(isMine).name);
				GUILayout.FlexibleSpace();
				if(GUILayout.Button("Ping"))
					EditorGUIUtility.PingObject(GetObject(isMine));
			} else GUILayout.Label("");
			GUILayout.EndHorizontal();
		});
		GUILayout.EndVertical();
		if(Event.current.alt && foldout != foldoutState)
			SetFoldoutRecur(foldout);
	}
	void SetFoldoutRecur(bool state) {
		foldout = state;
		foreach(ObjectHelper obj in children)
			obj.SetFoldoutRecur(state);
	}
	public bool ExpandDiffs() {
		foldout = !same;
		foreach(ObjectHelper obj in children) {
			if(obj.ExpandDiffs())
				foldout = true;
		}
		return foldout;
	}
	//Big ??? here.  What do we count as the same needing merge and what do we count as totally different?
	bool SameObject(GameObject mine, GameObject theirs) {
		return mine.name == theirs.name;
	}
	bool CompareProperties(GameObject mine, GameObject theirs) {
		return false;
	}
	GameObject GetObject(bool isMine) {
		return isMine ? mine : theirs;
	}
}
public class ComponentHelper {
	public bool show, same;
	public ObjectHelper parent;
	public Component mine, theirs;
	public System.Type type;
	SerializedObject mySO, theirSO;
	public List<PropertyHelper> properties = new List<PropertyHelper>();
	public Component GetComponent(bool isMine) {
		return isMine ? mine : theirs;
	}
	public ComponentHelper(ObjectHelper parent, Component mine, Component theirs) {
		this.parent = parent;
		this.mine = mine;
		this.theirs = theirs;
		if(mine != null)
			type = mine.GetType();
		else if(theirs != null)
			type = theirs.GetType();
	}
	public IEnumerable Refresh() {
		if(ObjectMerge.refreshWatch.Elapsed.TotalSeconds > ObjectMerge.maxFrameTime) {
			ObjectMerge.refreshWatch.Reset();
			yield return null;
			ObjectMerge.refreshWatch.Start();
		}
		properties.Clear();
		List<SerializedProperty> myProps = new List<SerializedProperty>();
		List<SerializedProperty> theirProps = new List<SerializedProperty>();
		if(GetComponent(true)) {
			mySO = new SerializedObject(GetComponent(true));
			GetProperties(myProps, mySO);
		}
		if(GetComponent(false)) {
			theirSO = new SerializedObject(GetComponent(false));
			GetProperties(theirProps, theirSO);
		}
		same = myProps.Count == theirProps.Count;
		if(mine && theirs && !same)
			Debug.LogWarning("not same number of props... wtf?");
		int count = Mathf.Max(myProps.Count, theirProps.Count);
		for(int i = 0; i < count; i++) {
			SerializedProperty myProp = null;
			SerializedProperty theirProp = null;
			if(i < myProps.Count)
				myProp = myProps[i];
			if(i < theirProps.Count)
				theirProp = theirProps[i];
			PropertyHelper ph = new PropertyHelper(this, myProp, theirProp);
			foreach(IEnumerable e in ph.CheckSame())
				yield return e;
			properties.Add(ph);
			if(!ph.same)
				same = false;
		}
		yield break;
	}

	private void GetProperties(List<SerializedProperty> myProps, SerializedObject obj) {
		SerializedProperty iterator = obj.GetIterator();
		bool enterChildren = true;
		while(iterator.NextVisible(enterChildren)) {
			myProps.Add(iterator.Copy());
			enterChildren = false;
		}
	}
	public void Draw(float width) {
		if(mine == null && theirs == null)		//TODO: figure out why blank componentHelpers are being created
			return;
		string style = ObjectMerge.alt ? ObjectMergeConfig.LIST_ALT_STYLE_NAME : ObjectMergeConfig.LIST_STYLE_NAME;
		if(!same)
			style += "Conflict";
		EditorGUILayout.BeginHorizontal(style);
		DrawComponent(true, width);
		//Swap buttons
		if(parent.mine && parent.theirs) {
			ObjectMerge.DrawMidButtons(mine, theirs, delegate() {					//Copy theirs to mine
				if(mine) EditorUtility.CopySerialized(theirs, mine);
				else EditorUtility.CopySerialized(theirs, parent.mine.AddComponent(theirs.GetType()));
				ObjectMerge.refresh = parent.DoRefresh();
			}, delegate() {															//Copy mine to theirs
				if(mine) EditorUtility.CopySerialized(theirs, mine);
				else EditorUtility.CopySerialized(theirs, parent.mine.AddComponent(theirs.GetType()));
				ObjectMerge.refresh = parent.DoRefresh();
			}, delegate() {															//Delete mine
				if(mine.GetType() == typeof(Camera)) {
					parent.DestroyAndClearRefs(mine.GetComponent<AudioListener>(), true);
					parent.DestroyAndClearRefs(mine.GetComponent<GUILayer>(), true);
					parent.DestroyAndClearRefs(mine.GetComponent("FlareLayer"), true);
				}
				parent.DestroyAndClearRefs(mine, true);
				ObjectMerge.refresh = parent.DoRefresh();
			}, delegate() {															//Delete theirs
				if(theirs.GetType() == typeof(Camera)) {
					parent.DestroyAndClearRefs(theirs.GetComponent<AudioListener>(), false);
					parent.DestroyAndClearRefs(theirs.GetComponent<GUILayer>(), false);
					parent.DestroyAndClearRefs(theirs.GetComponent("FlareLayer"), false);
				}
				parent.DestroyAndClearRefs(theirs, false);
				ObjectMerge.refresh = parent.DoRefresh();
			});
		} else GUILayout.Space(ObjectMergeConfig.midWidth * 2);
		//Display theirs
		DrawComponent(false, width);
		GUILayout.FlexibleSpace();
		if(GUILayout.Button(new GUIContent("R", "Refresh this object"), GUILayout.Width(25))) {
			ObjectMerge.refresh = parent.DoRefresh(true);
		}
		EditorGUILayout.EndHorizontal();
		ObjectMerge.alt = !ObjectMerge.alt;
		if(show) {
			List<PropertyHelper> tmp = new List<PropertyHelper>(properties);
			foreach(PropertyHelper property in tmp) {
				property.Draw(width);
			}
		}
		if(mySO != null && mySO.targetObject != null)
			if(mySO.ApplyModifiedProperties())
				ObjectMerge.refresh = parent.DoRefresh(false);
		if(theirSO != null && theirSO.targetObject != null)
			if(theirSO.ApplyModifiedProperties())
				ObjectMerge.refresh = parent.DoRefresh(false);
	}

	private void DrawComponent(bool isMine, float width) {
		GUILayout.BeginVertical(GUILayout.Width(ObjectMerge.colWidth));
		UniMerge.Util.Indent(width, delegate() {
			if(GetComponent(isMine)) {
				show = EditorGUILayout.Foldout(show, GetComponent(isMine).GetType().Name);
			} else GUILayout.Label("");
		});
		GUILayout.EndVertical();
	}
}
public class PropertyHelper {
	public bool show, same;
	public SerializedProperty mine, theirs;
	ComponentHelper parent;
	public SerializedProperty GetProperty(bool isMine) {
		return isMine ? mine : theirs;
	}
	public PropertyHelper(ComponentHelper parent, SerializedProperty mine, SerializedProperty theirs) {
		this.parent = parent;
		this.mine = mine;
		this.theirs = theirs;
	}
	public IEnumerable CheckSame() {
		UniMerge.Util.PropSame = true;
		foreach(IEnumerable e in UniMerge.Util.PropEqual(mine, theirs, ObjectMerge.mine, ObjectMerge.theirs))
			yield return e;
		same = UniMerge.Util.PropSame;
	}

	public void Draw(float width) {
		string style = ObjectMerge.alt ? ObjectMergeConfig.LIST_ALT_STYLE_NAME : ObjectMergeConfig.LIST_STYLE_NAME;
		if(!same)
			style += "Conflict";
		EditorGUILayout.BeginHorizontal(style);
		//Display mine
		DrawProperty(true, width);
		//Swap buttons
		if(mine != null && theirs != null) {
			ObjectMerge.DrawMidButtons(delegate() {										//Copy theirs to mine
				if(mine.propertyType == SerializedPropertyType.ObjectReference) {
					if(theirs.objectReferenceValue != null) {
						System.Type t = theirs.objectReferenceValue.GetType();
						if(t == typeof(GameObject)) {
							GameObject g = ObjectMerge.root.GetObjectSpouse((GameObject)theirs.objectReferenceValue, false);
							if(g) mine.objectReferenceValue = g;
							else mine.objectReferenceValue = theirs.objectReferenceValue;
						} else if(t.IsSubclassOf(typeof(Component))) {
							GameObject g = ObjectMerge.root.GetObjectSpouse(((Component)theirs.objectReferenceValue).gameObject, false);
							if(g){
								Component c = g.GetComponent(t);
								if(c == null) 	mine.objectReferenceValue = g.AddComponent(t);
								else 			mine.objectReferenceValue = g.GetComponent(t);
							}else 				mine.objectReferenceValue = theirs.objectReferenceValue;
						} else {
							mine.objectReferenceValue = theirs.objectReferenceValue;
						}
						
						if(mine.serializedObject.targetObject != null)
							mine.serializedObject.ApplyModifiedProperties();
						ObjectMerge.refresh = parent.parent.DoRefresh(false);
						GUIUtility.ExitGUI();
						return;
					}
				}
#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
				UniMerge.Util.SetProperty(theirs, mine);
#else
				mine.serializedObject.CopyFromSerializedProperty(theirs);
#endif
				if(mine.serializedObject.targetObject != null)
					mine.serializedObject.ApplyModifiedProperties();
				same = true;
				ObjectMerge.refresh = parent.parent.DoRefresh(false);
			}, delegate() {																//Copy mine to theirs
				if(theirs.propertyType == SerializedPropertyType.ObjectReference) {
					if(mine.objectReferenceValue != null) {
						System.Type t = mine.objectReferenceValue.GetType();
						if(t == typeof(GameObject)) {
							GameObject g = ObjectMerge.root.GetObjectSpouse((GameObject)mine.objectReferenceValue, true);
							if(g) theirs.objectReferenceValue = g;
							else theirs.objectReferenceValue = mine.objectReferenceValue;
						} else if(t.IsSubclassOf(typeof(Component))) {
							GameObject g = ObjectMerge.root.GetObjectSpouse(((Component)mine.objectReferenceValue).gameObject, true);
							if(g){
								Component c = g.GetComponent(t);
								if(c == null) 	theirs.objectReferenceValue = g.AddComponent(t);
								else 			theirs.objectReferenceValue = g.GetComponent(t);
							}else 				theirs.objectReferenceValue = mine.objectReferenceValue;
						} else
							theirs.objectReferenceValue = mine.objectReferenceValue;
						if(theirs.serializedObject.targetObject != null)
							theirs.serializedObject.ApplyModifiedProperties();
						ObjectMerge.refresh = parent.parent.DoRefresh(false);
						GUIUtility.ExitGUI();
						return;
					}
				}
#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
				UniMerge.Util.SetProperty(mine, theirs);
#else
				theirs.serializedObject.CopyFromSerializedProperty(mine);
#endif
				if(theirs.serializedObject.targetObject != null)
					theirs.serializedObject.ApplyModifiedProperties();
				same = true;
				ObjectMerge.refresh = parent.parent.DoRefresh(false);
			});
		} else GUILayout.Space(ObjectMergeConfig.midWidth * 2);
		//Display theirs
		DrawProperty(false, width);
		//Create space to offset for no reset button
		GUILayout.Space(ObjectMerge.RightColWidth);
		EditorGUILayout.EndHorizontal();
		ObjectMerge.alt = !ObjectMerge.alt;
	}
	private void DrawProperty(bool isMine, float width) {
		GUILayout.BeginVertical(GUILayout.Width(ObjectMerge.colWidth));
		if(GetProperty(isMine) != null) {
			UniMerge.Util.Indent(width + UniMerge.Util.TAB_SIZE * 3, delegate() {
#if UNITY_3_0 || UNITY_3_0_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
				EditorGUILayout.PropertyField(GetProperty(isMine));
#else
				EditorGUILayout.PropertyField(GetProperty(isMine), true);
#endif
			});
		} else GUILayout.Label("");
		GUILayout.Space(10);
		GUILayout.EndVertical();
	}
}