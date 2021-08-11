using Microsoft.CSharp;
using System.CodeDom.Compiler;
using System.Reflection;
using System.Text;
using UnityEngine;
using System.Collections;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using PowerScript;
using PowerTools.Quest;
using PowerTools;
using UnityEditorInternal;
using System.Text.RegularExpressions;
#if UNITY_2018_3_OR_NEWER
using UnityEditor.Experimental.SceneManagement;
#endif

namespace PowerTools.Quest
{

#region Class: QuestEditorUtils
public static class QuestEditorExtentionUtils
{
	// Extention for adding generic menu items
	public static GenericMenu AddItem(this GenericMenu self, string name, bool enabled, GenericMenu.MenuFunction function)
	{
		if ( enabled )
			self.AddItem(new GUIContent(name), false, function);
		else 
			self.AddDisabledItem(new GUIContent(name));
		return self;
	}
	// Extention for adding generic menu items
	public static GenericMenu AddItemToggle(this GenericMenu self, string name, bool on, GenericMenu.MenuFunction function)
	{
		self.AddItem(new GUIContent(name), on, function);
		return self;
	}
}

public class QuestEditorUtils
{

	static readonly string TEMPLATE_FUNCTION = "\n\tvoid #FUNC#(#PARAM#)\n\t{\n\t}\n";
	static readonly string TEMPLATE_COROUTINE = "\n\tIEnumerator #FUNC#(#PARAM#)\n\t{\n\n\t\tyield return E.Break;\n\t}\n";
	static readonly string TEMPLATE_NEW_FUNCTION = "\n\tpublic #FUNC#\n\t{\n\t}\n";
	static readonly string TEMPLATE_NEW_COROUTINE = "\n\tpublic #FUNC#\n\t{\n\n\t\tyield return E.Break;\n\t}\n";

	#endregion
	#region Gui Layout utils
	
	public static void LayoutQuestObjectContextMenu( eQuestObjectType questObjectType, ReorderableList list, string scriptName,  GameObject prefab, Rect rect, int index,bool onRightClick, System.Action<GenericMenu,GameObject> addItemsCallback=null )
	{			
		if ( onRightClick== false || (Event.current.isMouse && Event.current.button == 1 && rect.Contains(Event.current.mousePosition) ) )
		{
			list.index = index;
			GenericMenu menu = new GenericMenu();
			menu.AddDisabledItem(new GUIContent(questObjectType.ToString() + ' ' + scriptName),false);
			
			if ( addItemsCallback != null )
				addItemsCallback(menu,prefab);

			menu.AddSeparator("");
			
			if ( questObjectType < eQuestObjectType.Gui )
				menu.AddItemToggle("Highlight", PowerQuestEditor.IsHighlighted(prefab),()=>PowerQuestEditor.ToggleHighlight(prefab) );
			menu.AddItem("Rename", !Application.isPlaying, ()=>{
				ScriptableObject.CreateInstance< RenameQuestObjectWindow >().ShowQuestWindow(
					prefab, questObjectType, scriptName, PowerQuestEditor.OpenPowerQuestEditor().RenameQuestObject );});		
			menu.AddItem("Delete",  !Application.isPlaying, ()=>list.onRemoveCallback(list));
			menu.AddSeparator(string.Empty);			
			menu.AddItem("Add New "+questObjectType,  !Application.isPlaying, ()=>list.onAddCallback(list)	);
			
			/*
			if ( PowerQuestEditor.GetActionEnabled(eQuestVerb.Look) )
			{
				menu.AddItem(new GUIContent("On Look"), false, ()=>{
					QuestScriptEditor.Open( PowerQuestEditor.Get.GetSelectedRoom(), QuestScriptEditor.eType.Hotspot,
							PowerQuest.SCRIPT_FUNCTION_LOOKAT_HOTSPOT+ scriptName,
							PowerQuestEditor.SCRIPT_PARAMS_LOOKAT_HOTSPOT);});
			}	
			*/

			menu.ShowAsContext();			
			Event.current.Use();	
		}

	}
	#endregion
	#region Misc Utils

	public static void CreateImporter( string path, string packingTag )
	{
		PowerSpriteImport importer = PowerSpriteImportEditor.CreateImporter(path);
		importer.m_packingTag = packingTag;
	}
	 

	// Set col to null to hide again
	public static void HidePolygonEditor()
	{
		UnityEditorInternal.EditMode.ChangeEditMode(UnityEditorInternal.EditMode.SceneViewEditMode.None, new Bounds(), null);						
		#if UNITY_2019_3_OR_NEWER
		UnityEditor.EditorTools.ToolManager.SetActiveTool((UnityEditor.EditorTools.EditorTool)null);
		#endif
	}
    public static void ShowPolygonEditor( Collider2D col )
	{
		if ( !col )		
		{
			HidePolygonEditor();
			return;
		}
			
		System.Type colliderEditorBase = System.Type.GetType("UnityEditor.ColliderEditorBase,UnityEditor.dll");
		Editor[] colliderEditors = Resources.FindObjectsOfTypeAll(colliderEditorBase) as Editor[];

		if (colliderEditors == null || colliderEditors.Length <= 0)
			return;

		UnityEditorInternal.EditMode.ChangeEditMode(UnityEditorInternal.EditMode.SceneViewEditMode.Collider, col.bounds, colliderEditors[0]);
				
		//Debug.Log("EditMode: " + UnityEditorInternal.EditMode.editMode);

		#if UNITY_2019_3_OR_NEWER
		// Need to test the rest of this in newer unity before committing to changing it
		try
		{
			Selection.activeGameObject = col.gameObject;
		var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		foreach (var assembly in assemblies) 
		{
			if (assembly.GetType("UnityEditor.PolygonCollider2DTool") != null) 
			{
						// This fails when the selection was changed this frame
				UnityEditor.EditorTools.ToolManager.SetActiveTool(assembly.GetType("UnityEditor.PolygonCollider2DTool"));
			}
		}
		}
		catch
		{}
		#endif
	}


	#endregion
	#region Functions: Script File IO

	public static string FindCurrentPath()
	{
		string path = AssetDatabase.GetAssetPath(Selection.activeObject);
		if (path == "")
		{
			path = "Assets";
		}
		else if (Path.GetExtension(path) != "")
		{
			path = path.Replace(Path.GetFileName (AssetDatabase.GetAssetPath (Selection.activeObject)), "");
		}
		return path;
	}
	
	
	public static string GetPrefabPath(GameObject gameObject)
	{
		
		#if UNITY_2018_3_OR_NEWER		

			// Get root gameobject
			GameObject prefabObject = gameObject == null ? null : gameObject.transform.root.gameObject;
		
			// If staged, return staged path			
			PrefabStage prefabStage = PrefabStageUtility.GetPrefabStage(prefabObject);			
			if ( prefabStage != null )
			{
				#if UNITY_2020_1_OR_NEWER
					return prefabStage.assetPath;	
				#else
					return prefabStage.prefabAssetPath;
				#endif														
			}

			// Otherwise, find the prefab if exists
			if ( PrefabUtility.GetPrefabInstanceStatus(prefabObject) == PrefabInstanceStatus.Connected || PrefabUtility.GetPrefabInstanceStatus(prefabObject) == PrefabInstanceStatus.Disconnected )			
				prefabObject = PrefabUtility.GetCorrespondingObjectFromSource(prefabObject);

		#else

			// Get root gameobject
			Object prefabObject = gameObject == null ? null : gameObject.transform.root.gameObject;

			// The nice old easy pre-2018.3 way
			if ( PrefabUtility.GetPrefabType(prefabObject) == PrefabType.PrefabInstance )
				prefabObject = PrefabUtility.GetPrefabParent(prefabObject);
				
		#endif

		if ( prefabObject != null )
			return AssetDatabase.GetAssetPath(prefabObject);
		return null;
			
	}

	public static void ReplacePrefab( GameObject gameObj )
	{	
		#if UNITY_2018_3_OR_NEWER	
		PrefabUtility.SaveAsPrefabAssetAndConnect( PrefabUtility.GetOutermostPrefabInstanceRoot(gameObj), QuestEditorUtils.GetPrefabPath(gameObj), InteractionMode.AutomatedAction);		
		#else
		PrefabUtility.ReplacePrefab( gameObj, PrefabUtility.GetPrefabParent(gameObj), ReplacePrefabOptions.ConnectToPrefab );
		#endif
	}

	// Returns the prefab for an instance. NB: DOESN'T WORK IN PLAY MODE FOR 2018.3+
	public static GameObject GetPrefabParent( GameObject instance, bool suppressWarning = false )
	{
		if ( instance == null )
			return null;
		#if UNITY_2018_3_OR_NEWER	
			if ( PrefabUtility.GetPrefabAssetType(instance) != PrefabAssetType.NotAPrefab && PrefabUtility.GetPrefabInstanceStatus(instance) == PrefabInstanceStatus.NotAPrefab  )
			{
				// Christ this API is confusing. But this means it's a prefab in the project view
				return instance;
			}
			PrefabStage prefabStage = PrefabStageUtility.GetPrefabStage(instance);	
			if ( prefabStage != null )
				return instance; // if staged then it's a prefab already I guess?
			if ( Application.isPlaying && suppressWarning == false )
				Debug.LogWarning("Attemped to use GetPrefabParent on insance while game is running. Returning null");
			return PrefabUtility.GetCorrespondingObjectFromSource(instance); //NB: DOESN'T work when game is in play mode :(
		#else
			if ( PrefabUtility.GetPrefabType(instance) == PrefabType.Prefab )
				return instance;
			return PrefabUtility.GetPrefabParent(instance) as GameObject;
		#endif
	}

	#if UNITY_2018_3_OR_NEWER	
	/* // Added for reference (might need it later when getting confused about new prefabs)

	public static void RemoveColliders (IEnumerable <GameObject> selection)//
	{
		var prefabAssets = new HashSet <string> ();
		GameObject go;
		foreach (var gameObject in selection)
		{
			if (!PrefabUtility.IsPartOfAnyPrefab (gameObject))
			{
				var components = gameObject.GetComponentsInChildren <Collider2D> (true);
				foreach (var component in components)
					Object.DestroyImmediate (component);
				continue;
			}

			if (IsSceneInstance (gameObject))
			{
				var isImmutablePrefab = PrefabUtility.IsPartOfImmutablePrefab (gameObject);
				var isVariantPrefab = PrefabUtility.IsPartOfVariantPrefab (gameObject);
				if (isImmutablePrefab || isVariantPrefab)
					continue;
				go = PrefabUtility.GetCorrespondingObjectFromSource (gameObject);
				if (go != null)
					prefabAssets.Add (AssetDatabase.GetAssetPath (go));
				foreach (var asset in prefabAssets)
				{
					var rootGameObject = PrefabUtility.LoadPrefabContents (asset);
					var components = rootGameObject.GetComponentsInChildren <Collider2D> (true);
					foreach (var component in components)
						Object.DestroyImmediate (component);
					PrefabUtility.SaveAsPrefabAsset (rootGameObject, asset);
					PrefabUtility.UnloadPrefabContents (rootGameObject);
				}
			}
			else
			{
				var isImmutablePrefab = PrefabUtility.IsPartOfImmutablePrefab (gameObject);
				var isVariantPrefab = PrefabUtility.IsPartOfVariantPrefab (gameObject);
				if (isImmutablePrefab || isVariantPrefab)
					continue;
				prefabAssets.Add (AssetDatabase.GetAssetPath (gameObject));
				foreach (var asset in prefabAssets)
				{
					var rootGameObject = PrefabUtility.LoadPrefabContents (asset);
					var components = rootGameObject.GetComponentsInChildren <Collider2D> (true);
					foreach (var component in components)
						Object.DestroyImmediate (component);
					PrefabUtility.SaveAsPrefabAsset (rootGameObject, asset);
					PrefabUtility.UnloadPrefabContents (rootGameObject);
				}
			}
		}
	}

	public static bool IsSceneInstance (GameObject gameObject)
	{
		var isInstance = PrefabUtility.IsPartOfPrefabInstance (gameObject);
		var isAsset = PrefabUtility.IsPartOfPrefabAsset (gameObject);
		var isSceneInstance = isInstance && !isAsset;
		return isSceneInstance;
	}
	*/
	#endif
	
	public static string GetScriptPath(GameObject gameObject, string fileName)
	{		
		string path = GetPrefabPath(gameObject);
		if ( path == null )
			return null;
		return Path.GetDirectoryName(path) + "/" + fileName;
	}

	public static bool InsertTextIntoFile( string path, string insertBeforeLineContaining, string toInsert )
	{
		try 
		{
			// read in contents of file	
			string allText = File.ReadAllText(path);
			// find text
			int index = allText.LastIndexOf( insertBeforeLineContaining ); 
			if ( index < 0 )
				throw new System.Exception($"Couldn't find comment with token '{insertBeforeLineContaining}' in QuestScriptAutos.cs. Did you edit the comments that say 'Do not edit'?");

			// find start of line (check for both windows and unix)
			index = Mathf.Max( allText.LastIndexOf( '\n', index ), allText.LastIndexOf( '\r', index ) );			 

			File.WriteAllText(path, allText.Insert(index, toInsert));
		}
		catch ( System.Exception e )
		{
			Debug.Log("Failed to Insert text into file: " + e.ToString() );
			return false;
		}
		return true;
	}

	// For removing objects from the global script
	public static bool RemoveLineFromFile( string path, string toRemoveType, string toRemoveObjName )
	{
		Regex regex = new Regex( $"{toRemoveType}\\s+{toRemoveObjName}\\s+", RegexOptions.Compiled); // Match: "Room Forest "
		try 
		{
			// read in contents of file	
			List<string> allText = new List<string>(File.ReadAllLines(path));			
			allText.RemoveAll( item => regex.IsMatch(item) );
			WriteAllLines(path,allText);
		}
		catch ( System.Exception e )
		{
			Debug.Log("Failed to remove text from file: " + e.ToString() );
			return false;
		}
		return true;
	}

	// Same as File.WriteAllLines, but does '\n' as line ending
	public static void WriteAllLines(string path, IEnumerable<string> lines)
	{
		using (var writer = new StreamWriter(path))
		{
			foreach (var line in lines)
			{
				writer.Write(line+'\n');
			}
		}
	}

	// Opens a script file if it exists. If it doesn't it'll create the file
	public static bool CreateScript(GameObject gameObject, string fileName, string fileTemplateText)
	{
		string path = GetScriptPath(gameObject,fileName);

		//Debug.Log("Prefab Path: "+path);
		bool foundFile = false;
		try 
		{		
			foundFile = File.Exists(path);				
		} 
		catch (System.Exception ex)
		{ 
			if ( ex == null ) {}
		}

		if ( foundFile == false )
		{
			// Create the file
			try 
			{
				File.WriteAllText(path, fileTemplateText);
				foundFile = true;
				PowerQuestEditor.GetPowerQuestEditor().RequestAssetRefresh(); //AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);				
			}
			catch (System.Exception ex)
			{ 
				Debug.Log("Failed to create file: " + ex.ToString() ); 
			}
		}
		return foundFile;
	}

	// Opens a script file if it exists. If it doesn't it'll create the file
	public static bool OpenScript(GameObject gameObject, string fileName, string fileTemplateText)
	{		
		string path = GetScriptPath(gameObject,fileName);
		if ( CreateScript(gameObject, fileName,fileTemplateText) )
		{
			Object scriptObj = AssetDatabase.LoadAssetAtPath<Object>(path);
			if ( scriptObj != null )
			{
				//Debug.Log("Found Script and line");	
				AssetDatabase.OpenAsset(scriptObj,0);
				return true;
			}
			else 
			{						
				Debug.Log("Couldn't open script at " + path);	
			}
		}

		return false;
	}

	// Creates a file and function in a script (if it didn't already exist, and returns the line (or -1 if not created)
	public static int CreateScriptFunction(string path, string fileTemplateText, string functionName, string parameters="", bool isCoroutine = true )
	{
		int foundLineNum = -1;

		//Debug.Log("Prefab Path: "+path);
		bool foundFile = false;

		try 
		{		
			int lineNum = 0;			
			foreach ( string line in File.ReadAllLines(path) )
			{
				if ( line.Contains(functionName+"(") )
				{
					foundLineNum = lineNum+2;
					break;
				}
				lineNum++;
			}
			foundFile = true;			
		} 
		catch 
		{ 
		}
		
		if ( foundFile == false )
		{
			// Create the file
			try 
			{
				File.WriteAllText(path, fileTemplateText);
				foundFile = true;
				PowerQuestEditor.GetPowerQuestEditor().RequestAssetRefresh(); //AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
			}
			catch (System.Exception ex)
			{ 
				Debug.Log("Failed to create file: " + ex.ToString() ); 
			}
		}

		if ( foundFile && foundLineNum == -1 )
		{
			// Create the function in the file
			try 
			{

				string allText = File.ReadAllText(path);
				int index = allText.LastIndexOf( '}' );
				File.WriteAllText(path, allText.Insert(index, (isCoroutine ? TEMPLATE_COROUTINE : TEMPLATE_FUNCTION).Replace("#FUNC#", functionName).Replace("#PARAM#", parameters)));

				// find line we wrote to

				foundLineNum = 0;
				for ( int i = 0; i < index; ++i )
				{
					if ( allText[i] == '\n' )
						foundLineNum++;
				}

			}
			catch (System.Exception ex)
			{ 
				Debug.Log("Failed to add function to script: " + ex.ToString() ); 
			}
		}
		return foundLineNum;
	}



	// Creates a function in a script (if it didn't already exist, and returns the line (or -1 if not created). File will NOT be created
	public static int CreateScriptFunction(string path, string functionName, string parameters="", bool isCoroutine = true )
	{
		int foundLineNum = -1;

		//Debug.Log("Prefab Path: "+path);
		bool foundFile = false;

		try 
		{		
			int lineNum = 0;			
			foreach ( string line in File.ReadAllLines(path) )
			{
				if ( line.Contains(functionName+"(") )
				{
					foundLineNum = lineNum+2;
					break;
				}
				lineNum++;
			}
			foundFile = true;			
		} 
		catch 
		{ 
		}

		if ( foundFile && foundLineNum == -1 )
		{
			// Create the function in the file
			try 
			{

				string allText = File.ReadAllText(path);
				int index = allText.LastIndexOf( '}' );
				File.WriteAllText(path, allText.Insert(index, (isCoroutine ? TEMPLATE_COROUTINE : TEMPLATE_FUNCTION).Replace("#FUNC#", functionName).Replace("#PARAM#", parameters)));

				// find line we wrote to

				foundLineNum = 0;
				for ( int i = 0; i < index; ++i )
				{
					if ( allText[i] == '\n' )
						foundLineNum++;
				}

			}
			catch (System.Exception ex)
			{ 
				Debug.Log("Failed to add function to script: " + ex.ToString() ); 
			}
		}
		return foundLineNum;
	}

	
	// Creates a function in a script (if it didn't already exist, and returns the line (or -1 if not created). File will NOT be created
	// this one is used for the "Add function" option in the script editor. 
	public static int CreateScriptFunction(string path, string fullFuncitonDeclaration, bool isCoroutine )
	{
		int foundLineNum = -1;

		//Debug.Log("Prefab Path: "+path);
		bool foundFile = false;

		try 
		{		
			int lineNum = 0;			
			foreach ( string line in File.ReadAllLines(path) )
			{
				if ( line.Contains(fullFuncitonDeclaration) )
				{
					foundLineNum = lineNum+2;
					break;
				}
				lineNum++;
			}
			foundFile = true;			
		} 
		catch 
		{ 
		}

		if ( foundFile && foundLineNum == -1 )
		{
			// Create the function in the file
			try 
			{

				string allText = File.ReadAllText(path);
				int index = allText.LastIndexOf( '}' );
				File.WriteAllText(path, allText.Insert(index, (isCoroutine ? TEMPLATE_NEW_COROUTINE : TEMPLATE_NEW_FUNCTION).Replace("#FUNC#", fullFuncitonDeclaration)));

				// find line we wrote to

				foundLineNum = 0;
				for ( int i = 0; i < index; ++i )
				{
					if ( allText[i] == '\n' )
						foundLineNum++;
				}

			}
			catch (System.Exception ex)
			{ 
				Debug.Log("Failed to add function to script: " + ex.ToString() ); 
			}
		}
		return foundLineNum;
	}

	public static bool OpenScriptFunction(GameObject gameObject, string fileName, string fileTemplateText, string functionName, string parameters="", bool isCoroutine = true )
	{
		string path = GetScriptPath(gameObject, fileName);
		int line = CreateScriptFunction(path, fileTemplateText,functionName,parameters, isCoroutine);
		if ( line >= 0 )
		{
			Object scriptObj = AssetDatabase.LoadAssetAtPath<Object>(path);
			if ( scriptObj != null )
			{
				//Debug.Log("Found Script and line");	
				AssetDatabase.OpenAsset(scriptObj,line);
				return true;
			}
			else 
			{						
				Debug.Log("Couldn't open script at " + path);	
			}		
		}
		return false;
	}	

	public static GUIStyle GetMiniButtonStyle(int index, int total)
	{
		if ( total <= 1 )
			return EditorStyles.miniButton;
		if ( index <= 0 )
			return EditorStyles.miniButtonLeft;
		if ( index >= total-1 )
			return EditorStyles.miniButtonRight;
		return EditorStyles.miniButtonMid;
	}

	public static string GetFullPath(GameObject prefabObject, string fileName )
	{
		string path = AssetDatabase.GetAssetPath(prefabObject);
		string directory = Path.GetDirectoryName(path);
		return directory + "/" + fileName;
	}


	#endregion
	#region Functions: Hotloading/Compiling

	static readonly Regex REGEX_GLOBALS =  new Regex(@"(?<!\w)Globals\.", RegexOptions.Compiled); // zero width negative look behind fun. quicker than capturing group
	static readonly string REGEX_GLOBALS_REPLACE =  @"GlobalScript.Script."; // Globals.blah -> GlobalScript.Script.blah

	// Compile passed files
	public static Assembly CompileFiles(string[] paths)
	{
		var provider = new CSharpCodeProvider();
		var param = new CompilerParameters();

		
		EditorUtility.DisplayProgressBar("Compiling","Loading files",0.2f);

		// string[] ignoreAssemblies = {"ARModule","ClothModule","AIModule","Terrain","Web","VRModule","Vehicles","Wind","XR","Google",".VR",".OSX","Linux"}; - don't think this makes a difference

		// Add ALL of the assembly references (except dynamic ones, they crash)
		foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
		{
			try 
			{
				//bool contains = System.Array.Exists(ignoreAssemblies, item=>assembly.FullName.Contains(item)); // don't think this optimisastion makes any difference
				//if ( contains == false )					
				param.ReferencedAssemblies.Add(assembly.Location);
				
			}
			catch (System.NotSupportedException)
			{
				// Some assemblies don't let you use assembly.Location. These should be ignored
			}			
		}
		 
		// Ignore all warnings when compiling while testing
		param.TreatWarningsAsErrors = false;
		param.WarningLevel = 0;
		param.IncludeDebugInformation = true; // Not sure whether to unclude this or not tbh

		// Generate a dll in memory
		param.GenerateExecutable = false;
		param.GenerateInMemory = true;
		
		// Load all files into strings, so we can do some find/replace on them to fix some stuff - Takes about 0 sec
		string[] sources = new string[paths.Length];
		for (int i = 0; i < paths.Length; ++i)
		{
			try
			{				
				sources[i] = File.ReadAllText(paths[i]);
			}
			catch
			{
				Debug.LogError("Failed to read source file at: "+paths[i]);				
			}
		}
		
		// Now replace "Globals." with "GlobalScript.Script.", since otherwise the "GlobalScript" referenced will come from the wrong assembly
		for ( int i = 0; i < sources.Length; ++i )
		{
			sources[i] = REGEX_GLOBALS.Replace(sources[i],REGEX_GLOBALS_REPLACE);
		}
		

		// Compile the source		
		EditorUtility.DisplayProgressBar("Compiling","Compiling Files",0.4f); // Takes about 3 sec (of 6 total)
		//var result = provider.CompileAssemblyFromFile(param,paths);
		var result = provider.CompileAssemblyFromSource(param,sources);

		if (result.Errors.Count > 0)
		{
			var msg = new StringBuilder();
			foreach (CompilerError error in result.Errors) 
			{
				// Log error on unity log
				MethodBase mUnityLog = typeof(UnityEngine.Debug).GetMethod("LogPlayerBuildError", BindingFlags.NonPublic | BindingFlags.Static);
				mUnityLog.Invoke(null, new object[] { string.Format("{0}({1},{2}): error {3}: {4}", error.FileName, error.Line, error.Column,
error.ErrorNumber, error.ErrorText), error.FileName,  error.Line, error.Column });

				// Also pass error in exception
				msg.AppendFormat("{0}({1},{2}): error {3}: {4}\n",					
					error.FileName, error.Line, error.Column,
					error.ErrorNumber, error.ErrorText);
			}
			throw new System.Exception(msg.ToString());
		}

		// Return the assembly
		return result.CompiledAssembly;
	}

	// Compile a single file
	public static Assembly CompileFile(string path)
	{
		var provider = new CSharpCodeProvider();
		var param = new CompilerParameters();

		// Add ALL of the assembly references
		foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
		{
			param.ReferencedAssemblies.Add(assembly.Location);
		}

		param.TreatWarningsAsErrors = false;
		param.WarningLevel = 0;
		//param.CompilerOptions = "-nowarn:436";
		
		// Generate a dll in memory
		param.GenerateExecutable = false;
		param.GenerateInMemory = true;
		param.IncludeDebugInformation = true;

		// Compile the source
		var result = provider.CompileAssemblyFromFile(param, path);

		if (result.Errors.Count > 0) {
			var msg = new StringBuilder();
			foreach (CompilerError error in result.Errors) {
				msg.AppendFormat("Error ({0}): {1}\n",
					error.ErrorNumber, error.ErrorText);
			}
			throw new System.Exception(msg.ToString());
		}

		// Return the assembly
		return result.CompiledAssembly;
	}




}

#endregion
#region Class: QuestClickableEditorUtils
public class QuestClickableEditorUtils
{

	static public void OnSceneGUI( MonoBehaviour component, IQuestClickable clickable )
	{
		GUIStyle textStyle = new GUIStyle(EditorStyles.boldLabel);

		Transform transform = component.transform;
		{
			float oldY = transform.position.y + clickable.Baseline;
			Vector3 position = transform.position + new Vector3( -15, clickable.Baseline, 0);

			Handles.color = Color.cyan;
			GUI.color = Color.cyan;
			textStyle.normal.textColor = GUI.color;

			EditorGUI.BeginChangeCheck();
			position = Handles.FreeMoveHandle( position, Quaternion.identity,4.0f,new Vector3(0,1,0),Handles.DotHandleCap);

			Handles.Label(position + new Vector3(5,0,0), "Baseline", textStyle);
			Handles.color = Color.cyan.WithAlpha(0.5f);
			Handles.DrawLine( position + (Vector3.left * 500), position + (Vector3.right * 500) );

			if ( EditorGUI.EndChangeCheck() ) 
			{
				Undo.RecordObject(component,"Changed Baseline");
				clickable.Baseline = Utils.Snap(position.y - transform.position.y,PowerQuestEditor.SnapAmount);				
			}			

			if ( transform.position.y + clickable.Baseline != oldY )
			{
				SpriteRenderer renderer = transform.GetComponentInChildren<SpriteRenderer>();
				if ( renderer != null )
				{
					renderer.sortingOrder = -Mathf.RoundToInt((transform.position.y + clickable.Baseline)*10.0f);
				}
			}
		}

		{
			Vector3 position = transform.position + clickable.WalkToPoint.WithZ(0);
			Handles.color = Color.green;
			GUI.color = Color.green;
			textStyle.normal.textColor = GUI.color;
					
			EditorGUI.BeginChangeCheck();
			position = Handles.FreeMoveHandle( position, Quaternion.identity,2.0f,new Vector3(0,1,0),Handles.DotHandleCap);
			Handles.Label(position + new Vector3(0,0,0), " Walk To", textStyle);			
			if ( EditorGUI.EndChangeCheck() ) 
			{
				Undo.RecordObject(component,"Changed Walk To Point");									
				clickable.WalkToPoint = Utils.Snap((Vector2)(position - transform.position),PowerQuestEditor.SnapAmount);
			}


		}

		{
			Vector3 position = transform.position + clickable.LookAtPoint.WithZ(0);
			Handles.color = Color.red;
			GUI.color = Color.red;
			textStyle.normal.textColor = GUI.color;
			
			EditorGUI.BeginChangeCheck();
			position = Handles.FreeMoveHandle( position, Quaternion.identity,2.0f,new Vector3(0,1,0),Handles.DotHandleCap);
			Handles.Label(position + new Vector3(0,0,0), " Look At", textStyle);
			if ( EditorGUI.EndChangeCheck() ) 
			{
				Undo.RecordObject(component,"Changed Look AT Point");									
				clickable.LookAtPoint = Utils.Snap((Vector2)(position - transform.position),PowerQuestEditor.SnapAmount);
			}
			
		}
	}


}

#endregion
#region Classes: Quest Object Windows (create/rename)

// New Object Window
class CreateQuestObjectWindow : EditorWindow 
{
	public delegate void DelegateCreateFunction(string path, string prefabName);

	eQuestObjectType m_type = eQuestObjectType.Character;
	string m_prefabName = "";
	string m_path = null;
	string m_typeDescription = "";
	bool m_createsChildOnly = true;
	string m_examples = null;
	DelegateCreateFunction m_createFunction = null;

	// Inits the window. If path is null it'll create default, if createsChildOnly is true, it won't give details about where prefab is created.
	public void ShowQuestWindow(eQuestObjectType type, string typeDescription, string examples, DelegateCreateFunction createFunction, string path = null, bool createsChildOnly = false)
	{
		m_type = type;
		m_typeDescription = typeDescription;
		m_createFunction = createFunction;
		m_createsChildOnly = createsChildOnly;
		m_examples = examples;
		if ( string.IsNullOrEmpty(path) == false)
			m_path = path;
		titleContent.text = "Create "+typeDescription;
		ShowUtility(); // EditorWindow function - shows the window without a toolbar
	}

	public void SetPath( string path )
	{
	}

	void OnGUI() 
	{
		titleContent.text = "Create "+m_typeDescription;

		if (  string.IsNullOrEmpty(m_path) )
			m_path = QuestEditorUtils.FindCurrentPath();

		m_prefabName = EditorGUILayout.TextField("Choose Name", m_prefabName).Trim();
		EditorGUILayout.HelpBox("Short and Unique for scripts- eg: "+m_examples,MessageType.None);
		if ( m_createsChildOnly == false )
			EditorGUILayout.LabelField("Creates Prefab At", m_path+"/"+m_prefabName+"/"+m_typeDescription+m_prefabName+".prefab");

		GUILayout.BeginHorizontal();
		bool create = GUILayout.Button("Create");
		bool close  = GUILayout.Button("Cancel");
		GUILayout.EndHorizontal();
		
		if ( create && PowerQuestEditor.Get.SanitizeQuestObjectNameWithError(m_type, ref m_prefabName) == false )
			create = false;

		if (create) 
		{
			if ( m_createFunction == null )
			{
				Debug.LogError("Create function not set in CreateQuestObjectWindow");
				return;
			}
			m_createFunction.Invoke(m_path, m_prefabName);
			close = true;
		}

		if (close) 
		{
			//GUILayout.BeginVertical(); // <- wtf?
			Close();
		}
	}
	void OnDestroy()
	{
		m_path = null;
	}
}


// New room object window
class CreateRoomObjectWindow : EditorWindow 
{
	public delegate void DelegateCreateFunction(string name);
	eQuestObjectType m_type = eQuestObjectType.Hotspot;
	string m_prefabName = "";
	string m_path = null;
	string m_typeDescription = "";
	string m_examples = null;
	DelegateCreateFunction m_createFunction = null;

	// Inits the window. If path is null it'll create default, if createsChildOnly is true, it won't give details about where prefab is created.
	public void ShowQuestWindow(eQuestObjectType type, string typeDescription, string examples, DelegateCreateFunction createFunction )
	{
		m_type = type;
		m_typeDescription = typeDescription;
		m_createFunction = createFunction;
		m_examples = examples;
		titleContent.text = "Create "+m_typeDescription;
		ShowUtility(); // EditorWindow function - shows the window without a toolbar
	}

	void OnGUI() 
	{
		titleContent.text = "Create "+m_typeDescription;
			
		if (  string.IsNullOrEmpty(m_path) )
			m_path = QuestEditorUtils.FindCurrentPath();

		m_prefabName = EditorGUILayout.TextField("Choose Name", m_prefabName).Trim();
		EditorGUILayout.HelpBox("Short and Unique to this room- eg: "+m_examples,MessageType.None);

		GUILayout.BeginHorizontal();
		bool create = GUILayout.Button("Create");
		bool close  = GUILayout.Button("Cancel");
		GUILayout.EndHorizontal();
		
		if ( create && PowerQuestEditor.Get.SanitizeQuestObjectNameWithError(m_type, ref m_prefabName) == false )
			create = false;

		if (create) 
		{
			if ( m_createFunction == null )
			{
				Debug.LogError("Create function not set in CreateRoomObjectWindow");
				return;
			}
			m_createFunction.Invoke(m_prefabName);
			close = true;
		}

		if (close) 
		{
			GUILayout.BeginVertical();
			Close();
		}
	}
	void OnDestroy()
	{
		m_path = null;
	}
}

// New prop Window
class CreatePropWindow : EditorWindow 
{
	string m_prefabName = "";
	bool m_addCollider = false;
	string m_path = null;

	void OnGUI() 
	{
		if (  string.IsNullOrEmpty(m_path) )
			m_path = QuestEditorUtils.FindCurrentPath();

		m_prefabName = EditorGUILayout.TextField("Choose Name", m_prefabName).Trim();
		EditorGUILayout.HelpBox("Short and Unique to this room- eg: 'Trapdoor' or 'GoldenIdol'",MessageType.None);
		m_addCollider = EditorGUILayout.Toggle("Is Interactive", m_addCollider);

		GUILayout.BeginHorizontal();
		bool create = GUILayout.Button("Create");
		bool close  = GUILayout.Button("Cancel");
		GUILayout.EndHorizontal();
		
		if ( create && PowerQuestEditor.Get.SanitizeQuestObjectNameWithError(eQuestObjectType.Prop, ref m_prefabName) == false )
			create = false;

		if (create) 
		{
			PowerQuestEditor.CreateProp(m_prefabName, m_addCollider);
			close = true;
		}
		if (close) 
		{
			Close();
		}
	}
	void OnDestroy()
	{
		m_path = null;
	}
}


// New Object Window
class RenameQuestObjectWindow : EditorWindow 
{
	public delegate void DelegateRenameFunction(GameObject prefab, eQuestObjectType type, string name );

	eQuestObjectType m_type = eQuestObjectType.Character;
	string m_oldName = string.Empty;
	GameObject m_prefab = null;
	DelegateRenameFunction m_renameFunction = null;
	string m_name = string.Empty;

	// Inits the window. If path is null it'll create default, if createsChildOnly is true, it won't give details about where prefab is created.
	public void ShowQuestWindow(GameObject prefab, eQuestObjectType questType, string oldName, DelegateRenameFunction renameFunction)
	{

		titleContent.text = "Rename " + oldName;
		m_prefab = prefab;
		m_type = questType;
		m_oldName = oldName;
		m_name = oldName;
		m_renameFunction = renameFunction;
		ShowUtility(); // EditorWindow function - shows the window without a toolbar
	}

	void OnGUI() 
	{		
		m_name = EditorGUILayout.TextField("Rename", m_name);

		string error = string.Empty;		
		if  ( m_name.Equals(m_oldName) == false && PowerQuestEditor.Get.SanitizeQuestObjectName( m_type, ref m_name, out error ) == false )
			EditorGUILayout.HelpBox(error, MessageType.Info);		

		GUILayout.BeginHorizontal();
		bool rename = GUILayout.Button("Rename");
		bool close  = GUILayout.Button("Cancel");
		GUILayout.EndHorizontal();

		if ( rename )
		{
			if ( PowerQuestEditor.Get.SanitizeQuestObjectNameWithError(m_type, ref m_name) == false )
				rename = false;
		}

		if ( rename )
		{
			rename = EditorUtility.DisplayDialog("Really Rename?", "This will rename all the object's files, as well as all mentions of the object in the script. \nThis may cause errors, so make sure you've backed up first!\n\nAre you sure you want to rename?","Rename","Cancel");
		}

		if ( rename ) 
		{
			if ( m_renameFunction == null )
			{
				Debug.LogError("Rename function not set in RenameQuestObjectWindow");
				return;
			}
			m_renameFunction.Invoke(m_prefab, m_type, m_name);
			close = true;
		}

		if (close) 
		{
			GUILayout.BeginVertical();
			Close();
		}
	}
	void OnDestroy()
	{
	}
}


#endregion
#region Class: PostProcessor

public class PowerQuestAssetPostProcessor : AssetPostprocessor
{

	static string[] s_importedAssets, s_deletedAssets, s_movedAssets, s_movedFromAssetPaths;

	/// Returns true if paths of any changed assets match fragments of the path passed in. Case sensitive. Use Forward Slash eg Assets/PowerQuest/Blah
	public static bool HasPostProcessed(string toMatchInPath) 
	{
		bool result = false;
		if ( s_importedAssets == null )
			return false;
		if ( result == false )
			result = System.Array.Exists(s_importedAssets, item => item.Contains(toMatchInPath));
		if ( result == false )
			result = System.Array.Exists(s_deletedAssets, item => item.Contains(toMatchInPath));
		if ( result == false )
			result = System.Array.Exists(s_movedAssets, item => item.Contains(toMatchInPath));
		if ( result == false )
			result = System.Array.Exists(s_movedFromAssetPaths, item => item.Contains(toMatchInPath));

		//if ( result )
		//	Debug.Log("Has Post Processed: " + toMatchInPath);

		return result;		
	}

	//static System.Diagnostics.Stopwatch s_stopwatch = new System.Diagnostics.Stopwatch();
	static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths )
	{		
		if ( PowerQuestEditor.IsOpen() == false )
			return;
		if ( PowerQuestEditor.IsOpen() )
		{
			/*
			// This function gets called like 10 times sometimes, so don't want to do these expensive operations each time
			if ( s_stopwatch.IsRunning == false || s_stopwatch.ElapsedMilliseconds > 10000 ) 
			{
				s_stopwatch.Restart();
				StringBuilder builder = new StringBuilder();
				builder.Append("OnPostProcess: {");
				System.Array.ForEach(importedAssets, item=>builder.Append(item).Append(','));
				builder.Append("}, {");
				System.Array.ForEach(deletedAssets, item=>builder.Append(item).Append(','));
				builder.Append("}, {");
				System.Array.ForEach(movedAssets, item=>builder.Append(item).Append(','));
				builder.Append("}, {");
				System.Array.ForEach(movedFromAssetPaths, item=>builder.Append(item).Append(','));
				builder.Append("}");
				Debug.Log(builder.ToString());
			*/
				s_importedAssets = importedAssets;
				s_deletedAssets = deletedAssets;
				s_movedAssets = movedAssets;
				s_movedFromAssetPaths = movedFromAssetPaths;
				PowerQuestEditor.Get.OnPostprocessAllAssets(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths);
			/*
			}
			*/
		}
	}

}


#endregion
}
