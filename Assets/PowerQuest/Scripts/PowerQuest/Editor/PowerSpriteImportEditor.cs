// Created by Dave Lloyd (@duzzondrums) for Powerhoof - http://tools.powerhoof.com for updates

//#define SHOW_LOOP_TICKBOX

using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using PowerTools.SpriteImporter; // For EditorExtentionUtils
using AnimImportData = PowerTools.PowerSpriteImport.AnimImportData;

namespace PowerTools
{

[CustomEditor(typeof(PowerSpriteImport))]
public class PowerSpriteImportEditor : Editor
{
	#region Definitions

	static readonly float DEFAULT_SAMPLE_RATE = 100;
	static readonly float DEFAULT_FRAME_DURATION = 0.1f;
	static readonly float DEFAULT_SAMPLE_RATE_MS = 1000;

	static readonly string LIST_PROPERTY_NAME = "m_animations";
	static readonly string REGEX_PNG = @"(?<name>.*)_(?<id>\d+)\.png";
	static readonly string[] ASEPRITE_PATHS = { @"C:\Program Files (x86)\Steam\SteamApps\common\Aseprite\Aseprite.exe",  @"C:\Program Files\Aseprite\Aseprite.exe", @"C:\Program Files (x86)\Aseprite\Aseprite.exe" };


	// Data structure for importing aseprite json data
	[System.Serializable]
	class AsepriteJsonData
	{
		[System.Serializable]
		public class Frame
		{
				public int duration = 0;
		}
		[System.Serializable]
		public class MetaData
		{
			public Tag[] frameTags = null;
		}
		[System.Serializable]
		public class Tag
		{
			 public string name = null;
			 public int from = 0;
			 public int to = 0;
		}

		public Frame[] frames=null; // just to get framecount
		public MetaData meta = null;
	}

	#endregion
	#region Variables: Private

	[SerializeField] bool m_showSpriteImporterSettings = false;

	PowerSpriteImport m_component;
	List<AnimImportData> m_items;
	string m_asepritePath = "";

	ReorderableList m_list;

	#endregion
	#region Context menu items

	[MenuItem("Assets/Create/Power Sprite Importer")]
	public static void CreateImporter()
	{
		PowerSpriteImport asset = ScriptableObject.CreateInstance<PowerSpriteImport> ();

		string path = AssetDatabase.GetAssetPath (Selection.activeObject);
		if (path == "")
		{
			path = "Assets";
		}
		else if (Path.GetExtension (path) != "")
		{
			path = path.Replace (Path.GetFileName (AssetDatabase.GetAssetPath (Selection.activeObject)), "");
		}

		string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath (path + "/SpriteImporter.asset");

		AssetDatabase.CreateAsset (asset, assetPathAndName);

		AssetDatabase.SaveAssets ();
        AssetDatabase.Refresh();
		EditorUtility.FocusProjectWindow ();
		Selection.activeObject = asset;
	}

	public static PowerSpriteImport CreateImporter(string path)
	{
		PowerSpriteImport asset = ScriptableObject.CreateInstance<PowerSpriteImport>();

		AssetDatabase.CreateAsset(asset, path);
		AssetDatabase.SaveAssets();
	    AssetDatabase.Refresh();
		return asset;
	}

	[MenuItem("Assets/Import Sprites from Photoshop",true)]
	static bool ContextImportFromPhotoshopValidate(MenuCommand command) { return Selection.activeObject != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(Selection.activeObject)) == false; }

	[MenuItem("Assets/Import Sprites from Photoshop",false,30)]
	static void ContextImportFromPhotoshop(MenuCommand command)
	{
		string path = AssetDatabase.GetAssetPath(Selection.activeObject);
		if ( string.IsNullOrEmpty(Path.GetExtension(path)) == false )
			path = Path.GetDirectoryName(path); // if selected object path is to a file (not a directory) change it to the directory path.

		EditorUtility.DisplayProgressBar("Exporting from Photoshop", "Exporting from currently open photoshop file",0.8f);

		// Find vbs script path
		string[] guids = AssetDatabase.FindAssets("PSDExport");
		Debug.Assert(guids.Length > 0,"Couldn't find PSDExport.vbs");
		string exporterPath = Path.GetFullPath( AssetDatabase.GUIDToAssetPath(guids[0]) );

		// Runs script
		System.Diagnostics.Process process = new System.Diagnostics.Process();
		process.StartInfo.FileName = exporterPath;
		process.StartInfo.Arguments = "active" + " " + Path.GetFullPath(path);
		process.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Minimized;
		process.Start();
		// Wait up to 20 seconds for exit
		process.WaitForExit(20000);
	    AssetDatabase.Refresh();
		EditorUtility.ClearProgressBar();
	}


	[MenuItem("Assets/Create Anim From Sprites #%&a",true)]
	static bool ContextCreateAnimationFromSpritesValidate(MenuCommand command) { return Selection.activeObject != null && Selection.activeObject is Texture; }

	[MenuItem("Assets/Create Anim From Sprites #%&a",false,31)]
	static void ContextCreateAnimationFromSprites(MenuCommand command)
	{
		PowerSpriteImportEditor.CreateAnimationsFromSelected();
	}

	#endregion
	#region Functions: Init

	[System.NonSerialized]
    GUIContent m_openAnimIcon = null;

	void OnEnable()
	{

		m_component = (PowerSpriteImport)target;
		m_items = m_component.m_animations;

		if ( Directory.Exists(m_component.m_sourceDirectory) == false )
			m_component.m_sourceDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);

		RecalcLengths();

		m_list = new ReorderableList(serializedObject, serializedObject.FindProperty(LIST_PROPERTY_NAME),true, true, true, true);
		m_list.drawHeaderCallback = (Rect rect) => {
			EditorGUI.LabelField(new Rect(rect) {width = rect.width}, "Animation Name");
			#if SHOW_LOOP_TICKBOX
			EditorGUI.LabelField(new Rect(rect) {x = rect.width-107, width=107}, "1st Frame/Loop");
			#else
			EditorGUI.LabelField(new Rect(rect) {x = rect.width-90, width=90}, "First Frame");
			#endif
		};

		m_list.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
		{
			if ( Event.current.isMouse && Event.current.button == 1 && rect.Contains(Event.current.mousePosition) )
				LayoutContextMenu(rect, index);
							
			#if SHOW_LOOP_TICKBOX
				float[] widths = {5,35,3,15,2,25,0,25};
			#else			
				float[] widths = {5,35,2,25,0,25}; // Element widths, including spaces
			#endif
							
			int widthIndex = 0;
			System.Array.ForEach( widths, elementWidth=> rect.width -= elementWidth ); // Get width minus right-aligned stuff
			rect.height = EditorGUIUtility.singleLineHeight;
			rect.y += 2;

			// Name
			
			SerializedProperty listItem = m_list.serializedProperty.GetArrayElementAtIndex(index);
			EditorGUI.BeginChangeCheck();
			EditorGUI.PropertyField( rect, listItem.FindPropertyRelative("m_name"), GUIContent.none);
			if ( EditorGUI.EndChangeCheck() )
					m_list.index = index;
			
			rect.xMin += rect.width + widths[widthIndex++];
			rect.width = widths[widthIndex++];
			
			// First Frame

			EditorGUI.BeginChangeCheck();
			m_items[index].m_firstFrame = EditorGUI.DelayedIntField( rect, m_items[index].m_firstFrame);
			if ( EditorGUI.EndChangeCheck() )
			{
				// Recalc previous length since it will have changed
				if ( index > 0 )
					m_items[index-1].m_length = Mathf.Max(1, m_items[index].m_firstFrame - m_items[index-1].m_firstFrame);
				// Recalc first frames
				RecalcFirstFrames();
			}

			if ( string.IsNullOrEmpty(m_items[index].m_name ) )
				GUI.enabled = false;
			
			rect.y -= 2;			
			rect.xMin += rect.width + widths[widthIndex++];
			rect.width = widths[widthIndex++];
				
			#if SHOW_LOOP_TICKBOX	
			
			// Loop

			m_items[index].m_loop =
				EditorGUI.Toggle(rect, m_items[index].m_loop);

			rect.xMin += rect.width + widths[widthIndex++];
			rect.width = widths[widthIndex++];	
			#endif			

			// Play

			if ( m_openAnimIcon == null )
				m_openAnimIcon = EditorGUIUtility.IconContent("PlayButton");
			if ( GUI.Button(rect, m_openAnimIcon, EditorStyles.miniButtonLeft) )
			{
				LocateAnim(m_items[index]);
				OpenAnim(m_items[index]);
			}

			rect.xMin += rect.width + widths[widthIndex++];
			rect.width = widths[widthIndex++];
			
			// '...'
			if ( GUI.Button(rect, "...", EditorStyles.miniButtonRight) )
			{
				LayoutContextMenu(rect, index);
			}

			GUI.enabled = true;

		};
		m_list.onAddCallback += (list) =>
		{
			if ( m_list.index < 0 || m_list.index >= m_list.count-1 )
			{
				m_items.Add(new AnimImportData());
				m_list.index = m_list.index+1;
			}
			else
			{
				m_items.Insert( m_list.index+1, new AnimImportData() );
				m_list.index = m_list.index+1;
			}
			RecalcFirstFrames();

		};
		m_list.onRemoveCallback += (list) =>
		{
			int index = m_list.index;
			m_items.RemoveAt(m_list.index);
			RecalcFirstFrames();
			index--;
			if ( index >= 0 )
				m_list.index = index;

		};
		m_list.onReorderCallback += (list) =>
		{
			RecalcFirstFrames();
		};
	}


	#endregion
	#region Functions: GUI Layout

	override public void OnInspectorGUI()
	{
		if ( m_list == null )
			return;

		GUILayout.Space(10);

		//
		// PSD/Asprites source file
		//
		EditorGUILayout.LabelField("Source Image File", EditorStyles.boldLabel);
		EditorGUILayout.BeginHorizontal();

		EditorGUI.BeginChangeCheck();
		m_component.m_sourcePSD = EditorGUILayout.TextField(m_component.m_sourcePSD);
		if (EditorGUI.EndChangeCheck() )
		{
			m_component.m_isAseprite = ( string.IsNullOrEmpty( m_component.m_sourcePSD ) == false && (m_component.m_sourcePSD.EndsWith(".ase") || m_component.m_sourcePSD.EndsWith(".aseprite")));
		}

		if ( GUILayout.Button("...", EditorStyles.miniButton, GUILayout.Width(25)) )
		{
			string result = EditorUtility.OpenFilePanel("Select source PSD",m_component.m_sourcePSD,"");
			if ( string.IsNullOrEmpty(result) == false ) m_component.m_sourcePSD = result;
			EditorGUIUtility.keyboardControl = -1;
			m_component.m_isAseprite = ( string.IsNullOrEmpty( m_component.m_sourcePSD ) == false && (m_component.m_sourcePSD.EndsWith(".ase") || m_component.m_sourcePSD.EndsWith(".aseprite")));
		}
		if ( string.IsNullOrEmpty(m_component.m_sourcePSD) == false )
		{
			m_component.m_sourcePSD = MakeRelative(m_component.m_sourcePSD, Application.dataPath);
		}
		if ( string.IsNullOrEmpty(m_component.m_sourcePSD) == false )
		{
			if ( GUILayout.Button("Open in Editor", EditorStyles.miniButton ) )
			{
				if ( string.IsNullOrEmpty(m_component.m_sourcePSD) == false )
				{
					if ( File.Exists(Application.dataPath+"/../"+m_component.m_sourcePSD) )
					{
						Application.OpenURL(Application.dataPath+"/../"+m_component.m_sourcePSD);
					}
					else
					{
						EditorUtility.DisplayDialog("Couldn't find file","The file isn't there anymore. Make sure the relative path from project directory is the same as when you set it.","OK");
					}
				}
				else
				{
					if ( EditorUtility.DisplayDialog("Path not set","You need to set the path first!","Sure", "Maybe later") )
					{
						string result = EditorUtility.OpenFilePanel("Select source PSD",m_component.m_sourcePSD,"");
						if ( string.IsNullOrEmpty(result) == false ) m_component.m_sourcePSD = result;
					}
				}
			}
		}
		EditorGUILayout.EndHorizontal();


		EditorGUILayout.Space();

		//
		// Source Directory/Import
		//
		/* Uncomment if you want to import from a folder instead of directly from photoshop/aseprite. Files should be in this format: <name>_1.png, <name>_2.png... /
		if ( string.IsNullOrEmpty(m_component.m_sourcePSD) )
		{
			EditorGUILayout.LabelField("Source Image Folder", EditorStyles.boldLabel);
			EditorGUILayout.BeginHorizontal();
			m_component.m_sourceDirectory = EditorGUILayout.TextField( "Source PNG Directory", m_component.m_sourceDirectory );
			if ( GUILayout.Button("...",  GUILayout.Width(25)) )
			{
				string result = EditorUtility.OpenFolderPanel("Select Directory with Source PNGs",m_component.m_sourceDirectory,"");
				if ( string.IsNullOrEmpty(result) == false ) m_component.m_sourceDirectory = result;
				EditorGUIUtility.keyboardControl = -1;

			}
			EditorGUILayout.EndHorizontal();
			m_component.m_deleteImportedPngs = EditorGUILayout.Toggle("Delete source PNGs", m_component.m_deleteImportedPngs);
		}

		GUILayout.Space(10);
		/**/

		//
		// List
		//

		bool importTags = false;

		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.LabelField("Animation Specification", EditorStyles.boldLabel);
		if ( m_component.m_isAseprite )
			importTags |= GUILayout.Button( "Import Aseprite Tags", EditorStyles.miniButton );
		EditorGUILayout.EndHorizontal();

		serializedObject.Update();
		m_list.DoLayoutList();

		EditorGUILayout.BeginHorizontal();
		bool importPNGs = GUILayout.Button( "Import Sprites" );
		bool createAnimations = GUILayout.Button( "Create Animations" );
		EditorGUILayout.EndHorizontal();

		//	EditorGUILayout.HelpBox("NB: If animation is shortened, the extra sprites won't be deleted automatically. Delete them manually.", MessageType.None);

		EditorGUILayout.Space();
		m_showSpriteImporterSettings = EditorGUILayout.Foldout(m_showSpriteImporterSettings, "Advanced Settings");
		if ( m_showSpriteImporterSettings )
		{
			importTags |= GUILayout.Button( "Import Aseprite Tags" );
			m_component.m_spriteDirectory = EditorGUILayout.TextField("Sprite Folder", m_component.m_spriteDirectory );
			m_component.m_gui = EditorGUILayout.Toggle("GUI Sprite", m_component.m_gui);
			m_component.m_packingTag = EditorGUILayout.TextField("Packing Tag", m_component.m_packingTag);
			m_component.m_pixelsPerUnit = EditorGUILayout.FloatField("Pixels Per Unit", m_component.m_pixelsPerUnit);
			m_component.m_filterMode = (FilterMode)EditorGUILayout.EnumPopup("Filter Mode", (System.Enum)m_component.m_filterMode);
			m_component.m_compression = (PowerSpriteImport.eTextureCompression)EditorGUILayout.EnumPopup("Compression", (System.Enum)m_component.m_compression);
			m_component.m_crunchedCompression = EditorGUILayout.Toggle("Crunched Compression",m_component.m_crunchedCompression);

			m_asepritePath = EditorGUILayout.TextField("Aseprite Path", m_asepritePath);
		}

		GUILayout.Space(20);

		EditorGUILayout.HelpBox(@"TO USE:
     1) Duplicate this asset into the target folder.
     2) Choose your source PSD/Aseprite file.
     3) Add the names you want imported anim(s) to have.
     4) Import away!
     5) Leave the importer where it is to easily re-import later.",
			MessageType.None);


		EditorGUILayout.HelpBox(@"OR JUST IGNORE THIS PREFAB AND:
     1) Open photoshop file you want to import sprites from.
     2) Right click target folder -> Import Sprites from Photoshop
     3) Select sprites, right click -> Create -> Anim from Sprites",
			MessageType.None);

		GUILayout.Space(20);

		serializedObject.ApplyModifiedProperties();


		if ( importPNGs )
		{
			ImportPNGs();
			importPNGs = false;
		}
		else if ( createAnimations )
		{
			CreateAnimations();
			createAnimations = false;
		}
		else if (importTags)
		{
			ImportJsonTags();
			importTags = false;
		}
		else if (GUI.changed)
		{
			EditorUtility.SetDirty(target);
		}

		// This needs to be done this way or you get errors. Took a while to find a way to make it work heheh
		if ( m_showRenamePrompt != null && Event.current.type == EventType.Repaint)
		{
			RenamePrompt prompt = m_showRenamePrompt;
			m_showRenamePrompt = null;
			PopupWindow.Show(prompt.m_rect, prompt);
		}

	}

	RenamePrompt m_showRenamePrompt = null;

	void LayoutContextMenu( Rect rect, int index )
	{
		ReorderableList list = m_list;
		{
			list.index = index;

			AnimImportData data = m_items[index];

			if ( string.IsNullOrEmpty(data.m_name) )
				return;

			string spritePath = GetSubdirectory(m_component.m_spriteDirectory);
			string componentPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(m_component));
			bool spriteFound = File.Exists(spritePath + data.m_name + "_0.png");
			bool animFound = File.Exists(componentPath+"/"+ data.m_name+".anim");
			if ( animFound )
			{
				AnimationClip animClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(componentPath+"/"+ data.m_name+".anim");
				data.m_loop = animClip.isLooping;
			}

			GenericMenu menu = new GenericMenu();
			menu.AddDisabledItem(new GUIContent(data.m_name),false);
			
			menu.AddItemToggle("Loop",data.m_loop,()=>ToggleLooping(data));
			menu.AddSeparator("");

			menu.AddItem("Open", animFound, ()=> OpenAnim(data) );
			menu.AddSeparator("");
			
			menu.AddItem("Locate Sprite", spriteFound, ()=> LocateSprite(data) );
			menu.AddItem("Locate Anim", animFound, ()=> LocateAnim(data) );
			menu.AddSeparator("");
			
			if ( spriteFound == false )
				menu.AddItem("Import and Build", true, ()=>{ImportSprites(index);BuildAnimation(index);} );
			/*else 
				menu.AddItem("Reimport and Rebuild", true, ()=>{ImportSprites(index);BuildAnimation(index);} );			
			*/
			if ( spriteFound == false )
				menu.AddItem("Import Sprites", true, ()=>ImportSprites(index) );
			else
				menu.AddItem("Reimport Sprites", true, ()=>ImportSprites(index) );
			if ( animFound == false )
				menu.AddItem("Build Animation", true, ()=>BuildAnimation(index) );
			else
				menu.AddItem("Rebuild Animation", true, ()=>BuildAnimation(index) );
			menu.AddSeparator("");

			
			// Check if we can add options to add props (only if this importer is for the selected room, prop doesn't exist, etc
			bool createProp = Quest.PowerQuestEditor.IsOpen();
			createProp &= Quest.PowerQuestEditor.Get.GetSelectedRoom() != null;
			if ( createProp )
			{	
				// Check that the importer is a room one, and the one for the current room
				string importerPath=Path.GetDirectoryName(AssetDatabase.GetAssetPath(target));
				createProp = ( importerPath.StartsWith(@"Assets\Game\Room"));
				createProp = createProp && importerPath == Path.GetDirectoryName(Quest.QuestEditorUtils.GetPrefabPath(Quest.PowerQuestEditor.Get.GetSelectedRoom().gameObject));
			}
			// Check prop doesn't already exist
			createProp &= Quest.PowerQuestEditor.Get.GetSelectedRoom().GetData().GetProps().Exists(prop=>string.Equals(prop.ScriptName, data.m_name, System.StringComparison.OrdinalIgnoreCase) ) == false;

			if ( createProp )
			{					
				string propName=data.m_name;
				menu.AddItem("Create Prop", true, () => 
				{
					if ( PowerTools.Quest.PowerQuestEditor.Get.SanitizeQuestObjectNameWithError(PowerTools.Quest.eQuestObjectType.Prop, ref propName) )
						Quest.PowerQuestEditor.CreateProp(propName,false); 
					Selection.activeObject=target;
				} );
				menu.AddItem("Create Clickable Prop", true, ()=> 
				{
					if ( PowerTools.Quest.PowerQuestEditor.Get.SanitizeQuestObjectNameWithError(PowerTools.Quest.eQuestObjectType.Prop, ref propName) )
						Quest.PowerQuestEditor.CreateProp(propName,true); 						
					Selection.activeObject=target;
				} );
				menu.AddSeparator("");
			}

			// New/remove/rename options
			menu.AddItem("Add New", true, ()=>list.onAddCallback(list));
			if ( spriteFound == false )
			{
				menu.AddItem("Remove", true, ()=>list.onRemoveCallback(list));
			}
			else 
			{				
				menu.AddItem("Rename Anim and Sprites", true, ()=> m_showRenamePrompt = new RenamePrompt(rect, data.m_name, (newName)=>Rename(data,index,newName)));
				menu.AddItem("Delete Anim and Sprites", true, ()=>Delete(data, index));
			}
			menu.AddSeparator(string.Empty);

			menu.ShowAsContext();
			Event.current.Use();
		}

	}
	#endregion
	#region Functions: Helpers
	void ToggleLooping(AnimImportData data)
	{
		data.m_loop=!data.m_loop;
		
		string componentPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(m_component));
		AnimationClip animClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(componentPath+"/"+ data.m_name+".anim");
		if ( animClip != null )
		{		
			AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(animClip);
			settings.loopTime = data.m_loop;
			AnimationUtility.SetAnimationClipSettings(animClip, settings);
			EditorUtility.SetDirty(animClip);
		}

	}
	void OpenAnim(AnimImportData data)
	{
		string componentPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(m_component));
		AnimationClip animClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(componentPath+"/"+ data.m_name+".anim");
		if ( animClip != null )
			PowerTools.SpriteAnimator.Show(animClip);
	}
	void LocateAnim(AnimImportData data)
	{
		string componentPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(m_component));
		AnimationClip animClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(componentPath+"/"+ data.m_name+".anim");
		if ( animClip != null )
			EditorGUIUtility.PingObject(animClip);
	}
	void LocateSprite(AnimImportData data)
	{
		string componentPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(m_component));
		string spritePath = componentPath+'\\'+m_component.m_spriteDirectory+'\\'+ data.m_name + "_0.png";
		Texture2D sprite = AssetDatabase.LoadAssetAtPath<Texture2D>(spritePath);
		if ( sprite != null )
			EditorGUIUtility.PingObject(sprite);
	}

	void Delete(AnimImportData data, int index)
	{				
		
		if (  EditorUtility.DisplayDialog("Really delete?",$"Are you sure you want to delete {data.m_name}.anim and all its sprites?","Delete","Cancel") == false )
			return;		

		// Find animation, delete it		

		string componentPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(m_component));
		string spritePath = GetSubdirectory(m_component.m_spriteDirectory);
		string spritePathLocal = componentPath+"/"+m_component.m_spriteDirectory+"/";
		
		string resultString = data.m_name+".anim\n";

		// Find animation, delete it
		AssetDatabase.DeleteAsset(componentPath+"/"+ data.m_name+".anim");
		
		// Find all sprites, delete them too
		bool found = true;
		int frameId = 0;
		while ( found )
		{
			found = false;
			string fileName = data.m_name + "_"+frameId.ToString()+".png";
			string filePath = spritePath + fileName;
			string assetPath = spritePathLocal+fileName;
			if ( File.Exists(filePath))
			{
				found = true;
				AssetDatabase.DeleteAsset(assetPath);
				resultString += $"{fileName}\n";
			}

			++frameId;
		}
				
		// Don't actually delete the item in the list, might want to reimport or something.
		// m_list.onRemoveCallback(m_list);

		AssetDatabase.SaveAssets();
		// Refresh asset database
		AssetDatabase.Refresh();

		EditorUtility.DisplayDialog("Deleted...",resultString,"OK");
	}

	void Rename(AnimImportData data, int index, string newName)
	{
		// Show rename prompt
		if ( string.IsNullOrEmpty(newName) )
			return;
		
		string resultString = null;

		string componentPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(m_component));
		string spritePath = GetSubdirectory(m_component.m_spriteDirectory);
		string spritePathLocal = componentPath+"/"+m_component.m_spriteDirectory+"/";


		// Find animation, rename it
		AssetDatabase.RenameAsset(componentPath+"/"+ data.m_name+".anim",newName);
		resultString += $"{data.m_name}.anim to {newName}.anim\n";

		// Find sprites, rename them too
		
		bool found = true;
		int frameId = 0;
		while ( found )
		{
			found = false;
			string fileName = data.m_name + "_"+frameId.ToString()+".png";
			string filePath = spritePath + fileName;
			string assetPath = spritePathLocal+fileName;
			string newSpriteName = newName + "_"+frameId.ToString()+".png";
			if ( File.Exists(filePath))
			{
				found = true;
				AssetDatabase.RenameAsset(assetPath,newSpriteName);
				resultString += $"{fileName} to {newSpriteName}\n";
			}

			++frameId;
		}
				
		// Change name of asset too
		data.m_name = newName;

		AssetDatabase.SaveAssets();
		// Refresh asset database
		AssetDatabase.Refresh();
		
		EditorUtility.DisplayDialog("Renamed...",resultString,"OK");

	}

	void ImportSprites(int index)
	{
		ImportPNGs(index,true);
	}
	void BuildAnimation(int index)
	{
		string componentPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(m_component));
		string spritePath = GetSubdirectory(m_component.m_spriteDirectory);
		CreateAnimation(spritePath, componentPath, index, true);
		AssetDatabase.SaveAssets();
		// Refresh asset database
		AssetDatabase.Refresh();
		OpenAnim(m_items[index]);
	}

	void ImportJsonTags()
	{
		Undo.RecordObject(m_component, "Import tags from Aseprite file");
		string asepritePath = SetupAsepritePath();
		if ( string.IsNullOrEmpty(asepritePath) )
			return;

		System.Diagnostics.Process process = new System.Diagnostics.Process();
		string jsonFile = $"{m_component.m_sourceDirectory}\\AseTagExport.json";
		process.StartInfo.FileName = asepritePath;
		// nb: includes "ignore-layer" argument, any layers named "guide" or "ignore" aren't exported
		process.StartInfo.Arguments = $"-b \"{Path.GetFullPath(m_component.m_sourcePSD)}\" --data \"{Path.GetFullPath(jsonFile)}\" --list-tags --format json-array";
		process.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Minimized;
		process.Start();

		// Wait up to 20 seconds for exit
		process.WaitForExit(20000);

		// import the json
		string jsonString = File.ReadAllText($"{jsonFile}");
		AsepriteJsonData tagData = UnityEngine.JsonUtility.FromJson<AsepriteJsonData>(jsonString);
		if ( tagData != null && tagData.meta != null && tagData.meta.frameTags != null && tagData.meta.frameTags.Length > 0 && tagData.frames!= null)
		{
			AsepriteJsonData.Tag tagdat = tagData.meta.frameTags[0];
			// Clear list of anims
			m_items.Clear();
			int last = 0;
			foreach( AsepriteJsonData.Tag tag in tagData.meta.frameTags )
			{
				if ( last > 0 && last < tag.from )
				{
					// Add empty tag where necessary
					m_items.Add( new AnimImportData(){m_name = null, m_firstFrame = last+1} );
				}
				m_items.Add( new AnimImportData(){m_name = tag.name, m_firstFrame = tag.from+1} );
				last = tag.to+1;
			}
			// find frame count, and ignore last frame if it's not tagged
			int numFrames = tagData.frames.Length;
			if ( last < numFrames )
				m_items.Add( new AnimImportData(){m_name = null, m_firstFrame = last+1} );

			RecalcLengths();

			// Set last frame length from metadata
			m_items[m_items.Count-1].m_length = tagData.frames.Length-(m_items[m_items.Count-1].m_firstFrame-1);

			// Read frame timings
			foreach ( AnimImportData data in m_items )
			{
				int[] frames = new int[data.m_length];
				int max = tagData.frames.Length - data.m_firstFrame + 1;
				for ( int i = 0; i < data.m_length && i < max; ++i)
				{
					frames[i]=tagData.frames[i+data.m_firstFrame-1].duration;
				}
				data.m_frameDurations = frames;
			}

		}
		else
		{
			Debug.LogError("Failed to import tag data from aseprite");
		}

		// Delete sprites
		Directory.Delete(m_component.m_sourceDirectory,true);
		if ( File.Exists(m_component.m_sourceDirectory+".meta") )
			File.Delete(m_component.m_sourceDirectory+".meta");
	}

	// Set some stuff up for importing from aseprite. Used when importing pngs, and json data
	string SetupAsepritePath()
	{
		if ( string.IsNullOrEmpty( m_component.m_sourcePSD ) == false && (m_component.m_sourcePSD.EndsWith(".ase") ||m_component.m_sourcePSD.EndsWith(".aseprite")) )
		{
			m_component.m_sourceDirectory = Path.GetDirectoryName(m_component.m_sourcePSD)+@"/Export";

			Directory.CreateDirectory(m_component.m_sourceDirectory);

			// Find aseprite path

			string asepritePath = EditorPrefs.GetString("AsePath");
			if ( File.Exists(asepritePath) == false )
			{
				if ( File.Exists(m_asepritePath) )
				{
					asepritePath = m_asepritePath;
					EditorPrefs.SetString("AsePath", asepritePath);
				}
				else
				{
					foreach ( string tmpasePath in ASEPRITE_PATHS )
					{
						if (File.Exists(tmpasePath) )
						{
							asepritePath = tmpasePath;
							m_asepritePath = asepritePath;
							EditorPrefs.SetString("AsePath", asepritePath);
							break;
						}
					}
				}
			}
			if ( File.Exists(asepritePath) == false )
			{
				EditorUtility.DisplayDialog("Error","Couldn't find path to Aseprite, set it in Advanced Settings first!","OK");
				return null;
			}

			return asepritePath;
		}
		return null;

	}

	void ImportPNGs(int itemId = -1, bool deleteRemoved = false)
	{
		if ( string.IsNullOrEmpty( m_component.m_sourcePSD ) == false && (m_component.m_sourcePSD.EndsWith(".ase") || m_component.m_sourcePSD.EndsWith(".aseprite")) )
		{
			string asepritePath = SetupAsepritePath();
			if ( string.IsNullOrEmpty(asepritePath) )
				return;

			System.Diagnostics.Process process = new System.Diagnostics.Process();
			process.StartInfo.FileName = asepritePath;
			// nb: includes "ignore-layer" argument, any layers named "guide" or "ignore" aren't exported
			string jsonFile = $"{m_component.m_sourceDirectory}\\AseTagExport.json";
			process.StartInfo.Arguments = string.Format("-b --ignore-layer \"Guide\" --ignore-layer \"Ignore\" --ignore-layer \"ignore\" \"{0}\" --save-as \"{1}\\{2}_1.png\" --data \"{3}\" --list-tags --format json-array", Path.GetFullPath(m_component.m_sourcePSD), Path.GetFullPath(m_component.m_sourceDirectory), Path.GetFileNameWithoutExtension(m_component.m_sourcePSD),Path.GetFullPath(jsonFile) );
			process.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Minimized;
			process.Start();

			// Wait up to 20 seconds for exit
			process.WaitForExit(20000);

			// Import metadata, and get frame timings

			// import the json
			string jsonString = File.ReadAllText($"{jsonFile}");
			AsepriteJsonData tagData = UnityEngine.JsonUtility.FromJson<AsepriteJsonData>(jsonString);
			if ( tagData != null && tagData.meta != null && tagData.meta.frameTags != null && tagData.meta.frameTags.Length > 0 && tagData.frames != null)
			{
				AsepriteJsonData.Tag tagdat = tagData.meta.frameTags[0];
				// Set last frame length from metadata
				m_items[m_items.Count-1].m_length = tagData.frames.Length-(m_items[m_items.Count-1].m_firstFrame-1);

				// Read frame timings
				foreach ( AnimImportData data in m_items )
				{
					if ( data.m_length <= 0)
						continue;
					int[] frames = new int[data.m_length];
					int max = tagData.frames.Length - data.m_firstFrame + 1;

					for ( int i = 0; i < data.m_length && i < max; ++i)
					{
						frames[i]=tagData.frames[i+data.m_firstFrame-1].duration;
					}
					data.m_frameDurations = frames;

				}
			}

			// import the pngs
			ImportPNGsFromFolder(itemId,deleteRemoved);
			// Delete sprites
			Directory.Delete(m_component.m_sourceDirectory,true);
			if ( File.Exists(m_component.m_sourceDirectory+".meta") )
				File.Delete(m_component.m_sourceDirectory+".meta");

		}
		else if ( string.IsNullOrEmpty( m_component.m_sourcePSD ) == false && m_component.m_sourcePSD.EndsWith(".psd") )
		{

			EditorUtility.DisplayProgressBar("Exporting from Photoshop", "Exporting from "+m_component.m_sourcePSD,0.8f);


			// Find vbs script path
			string[] guids = AssetDatabase.FindAssets("PSDExport");
			Debug.Assert(guids.Length > 0,"Couldn't find PSDExport.vbs");
			string exporterPath = Path.GetFullPath( AssetDatabase.GUIDToAssetPath(guids[0]) );

			// Runs script
			m_component.m_sourceDirectory = Path.GetDirectoryName(m_component.m_sourcePSD)+@"/Export";
			System.Diagnostics.Process process = new System.Diagnostics.Process();
			process.StartInfo.FileName = exporterPath;
			process.StartInfo.Arguments = Path.GetFullPath(m_component.m_sourcePSD) + " " + Path.GetFullPath(m_component.m_sourceDirectory);
			process.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Minimized;
			process.Start();
			// Wait up to 20 seconds for exit
			process.WaitForExit(20000);
			EditorUtility.ClearProgressBar();
			// import the pngs
			ImportPNGsFromFolder(itemId,deleteRemoved);
			try
			{
				// Delete exportfolder + metadata
				Directory.Delete(m_component.m_sourceDirectory,true);
				if ( File.Exists(m_component.m_sourceDirectory+".meta") )
					File.Delete(m_component.m_sourceDirectory+".meta");
			}
			catch
			{
			}
		}
		else
		{
			ImportPNGsFromFolder(itemId,deleteRemoved);
		}
	}

	void ImportPNGsFromFolder(int itemId, bool deleteRemoved)
	{

		if ( string.IsNullOrEmpty(m_component.m_sourceDirectory) )
		{
			EditorUtility.DisplayDialog("Error","Failed to import, set source directory first!","OK");
			return;
		}

		if ( m_items.Count  == 0 )
		{
			EditorUtility.DisplayDialog("Error","Failed to import, set some anims up first!","OK");
			return;
		}

		string ourPath = GetSubdirectory(m_component.m_spriteDirectory);

		string[] sourceFileNames = Directory.GetFiles(m_component.m_sourceDirectory,"*_*.png");
		int[] sourceFileIds = new int[sourceFileNames.Length];
		string resultString = "";

		Regex pngRegex = new Regex(REGEX_PNG, RegexOptions.IgnoreCase | RegexOptions.Compiled );
		for ( int i = 0; i < sourceFileNames.Length; ++i )
		{
			int id = -1;
			Match match = pngRegex.Match( sourceFileNames[i] );
			id = int.Parse( match.Groups["id"].Value );
			sourceFileIds[i] = id;
		}

		for ( int i = 0; i < m_items.Count; ++i )
		{
			if ( itemId >= 0 && i != itemId )
				continue;
			int firstFrame = m_items[i].m_firstFrame;
			int lastFrame = ( i+1 < m_items.Count) ? m_items[i+1].m_firstFrame : 1000;
			//if ( lastFrame < firstFrame )
				//lastFrame = 10000;
			string failString = null;
			int copiedCount = 0;


			for ( int spriteId = firstFrame; spriteId < lastFrame; ++spriteId )
			{
				// Find path that matches prefix
				string sourcePath = null;

				int sourceIndex = System.Array.FindIndex(sourceFileIds, sourceId=>sourceId==spriteId);
				if ( sourceIndex >= 0 && sourceIndex < sourceFileNames.Length )
				{
					sourcePath = sourceFileNames[sourceIndex];
				}

				if ( string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(m_items[i].m_name) )
				{
					//failString = "Failed to copy from empty Path";
					break;
				}
				else
				{

					string fileName = m_items[i].m_name + "_"+(spriteId-firstFrame).ToString()+".png";
					string targetPath = ourPath + fileName;
					//Debug.Log("source: "+sourcePath+", targetPath = " + targetPath);

					// Copy the file
					try
					{
						bool alreadyExisted = File.Exists(targetPath);
						File.Copy(sourcePath, targetPath, true);
						if ( alreadyExisted == false )
						{
							string relativeTargetPath = MakeRelative(targetPath, Application.dataPath);
							TextureImporter importer = TextureImporter.GetAtPath(relativeTargetPath) as TextureImporter;
							if ( importer == null )
								AssetDatabase.ImportAsset(relativeTargetPath, ImportAssetOptions.ForceUpdate); // If importer didn't already exist import the asset first before changing settings

							importer = TextureImporter.GetAtPath(relativeTargetPath) as TextureImporter;
							importer.spritePixelsPerUnit = m_component.m_pixelsPerUnit;
							switch ( m_component.m_compression )
							{
								case PowerSpriteImport.eTextureCompression.None:  importer.textureCompression = TextureImporterCompression.Uncompressed; break;
								case PowerSpriteImport.eTextureCompression.Low:  importer.textureCompression = TextureImporterCompression.CompressedLQ; break;
								case PowerSpriteImport.eTextureCompression.Normal:  importer.textureCompression = TextureImporterCompression.Compressed; break;
								case PowerSpriteImport.eTextureCompression.High:  importer.textureCompression = TextureImporterCompression.CompressedHQ; break;
							}
							importer.crunchedCompression = m_component.m_crunchedCompression;
							importer.filterMode = m_component.m_filterMode;
							importer.mipmapEnabled = false;

							//importer.textureFormat = TextureImporterFormat.AutomaticTruecolor;
							if ( m_component.m_gui && string.IsNullOrEmpty(m_component.m_packingTag) == false)
								importer.spritePackingTag = "[RECT]"+m_component.m_packingTag;
							else if ( string.IsNullOrEmpty(m_component.m_packingTag) == false )
								importer.spritePackingTag = m_component.m_packingTag;

							#if UNITY_2017_1_OR_NEWER
							// This isn't necessary in 5.6, but seems to be in 2018, not sure about 2017
							AssetDatabase.ImportAsset(relativeTargetPath, ImportAssetOptions.ForceUpdate);
							#endif
						}
						copiedCount++;
						//resultString += "Imported "+m_items[i].m_name + "_"+(spriteId-firstFrame).ToString()+".png"+"\n";
					}
					catch(System.Exception e )
					{
						if (e!= null){};
						if ( string.IsNullOrEmpty(failString) )
							failString = "Failed to copy from "+sourcePath+" to "+targetPath;
					}

					// Delete the copied file
					if ( m_component.m_deleteImportedPngs )
					{
						try
						{
							File.Delete(sourcePath);
						}
						catch(System.Exception e )
						{
							if (e!= null){};
							if ( string.IsNullOrEmpty(failString) )
								failString = "Failed to delete "+sourcePath;
						}
					}

				}
			}

			// Remove sprites that shouldn't be there
			if ( deleteRemoved )
			{
				bool found = true;
				int frameId = lastFrame-firstFrame;
				while ( found )
				{
					found = false;
					string fileName = m_items[i].m_name + "_"+frameId.ToString()+".png";
					string targetPath = ourPath + fileName;
					if ( File.Exists(targetPath))
					{
						found = true;
						try
						{
							File.Delete(targetPath);
							File.Delete(targetPath+".meta");
							resultString += $"Deleted {fileName}\n";
						}
						catch
						{}
					}

					++frameId;
				}
			}


			if ( string.IsNullOrEmpty(failString) )
			{
				if ( copiedCount <= 0 )
				{
					if ( string.IsNullOrEmpty(m_items[i].m_name) == false )
						resultString += "Failed to import "+m_items[i].m_name + ": No frames found\n";
				}
				else if ( copiedCount == 1 )
				{
					// Imported Idle (1 frame)
					resultString += "Imported "+m_items[i].m_name + " (1 frame)\n";
				}
				else
				{
					// Imported Idle (5 frames)
					resultString += "Imported "+m_items[i].m_name + " ("+copiedCount.ToString()+" frames)\n";
				}
			}
			else
			{
				resultString += "Failed to import "+ m_items[i].m_name +" "+ failString+"\n";
			}
		}

		AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

		if ( string.IsNullOrEmpty(resultString) == false )
		{
			EditorUtility.DisplayDialog("Import Result",resultString,"OK");
		}
		else
		{

			EditorUtility.DisplayDialog("Import Result","No sprites were found in "+m_component.m_sourceDirectory,"OK");
		}

		AssetDatabase.Refresh();

	}

	void CreateAnimations()
	{
		string spritePath = GetSubdirectory(m_component.m_spriteDirectory);

		string componentPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(m_component));

		// Add new stuff/make edits
		for ( int i = 0; i < m_items.Count; ++i )
		{
			CreateAnimation(spritePath, componentPath, i, false);
		}

		AssetDatabase.SaveAssets();

		// Refresh asset database
		AssetDatabase.Refresh();
	}

	void CreateAnimation(string spritePath, string componentPath, int index, bool overwrite )
	{
		AnimImportData data = m_items[index];

		// Skip anims with no name
		if ( string.IsNullOrEmpty(data.m_name) )
			return;

		int firstFrame = data.m_firstFrame;
		int lastFrame = firstFrame+1;
		if ( index+1 < m_items.Count)
		{
			lastFrame = m_items[index+1].m_firstFrame;
		}
		else
		{
			for ( int spriteId = firstFrame; spriteId < 1000; ++spriteId )
			{
				lastFrame = spriteId;
				string fileName = data.m_name + "_"+(spriteId-firstFrame).ToString()+".png";

				if ( File.Exists(spritePath + fileName) == false )
				{
					break;
				}
			}
		}

		bool isNew = false;
		string animFileName = componentPath+"/"+ data.m_name+".anim";
		AnimationClip animClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animFileName);
		if ( animClip == null )
		{
			animClip = new AnimationClip();
			isNew = true;
		}

		// For now don't override existing animations
		if ( isNew == false && overwrite == false)
			return;

		int numFrames = lastFrame - firstFrame;

		EditorCurveBinding curveBinding = new EditorCurveBinding();

		ObjectReferenceKeyframe[] keyframes = null;
		if ( isNew )
		{
			animClip.name = data.m_name;

			{
				AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(animClip);
				settings.loopTime = data.m_loop;
				AnimationUtility.SetAnimationClipSettings(animClip, settings);
			}

			// I want to change the sprites of the sprite renderer, so I put the typeof(SpriteRenderer) as the binding type.
			curveBinding.type = m_component.m_gui ? typeof(UnityEngine.UI.Image) : typeof(SpriteRenderer);
			// This is the property name to change the sprite of a sprite renderer
			curveBinding.propertyName = "m_Sprite";
			// Regular path to the gameobject that will be changed (empty string means root)
			curveBinding.path = "";

		}
		else
		{
			curveBinding = System.Array.Find( AnimationUtility.GetObjectReferenceCurveBindings(animClip), item=>item.propertyName == "m_Sprite" );
			//keyframes = AnimationUtility.GetObjectReferenceCurve(m_clip, m_curveBinding ); // For now we're blatting all keyframes. Not ideal
		}

		animClip.frameRate = DEFAULT_SAMPLE_RATE;
		keyframes = new ObjectReferenceKeyframe[numFrames+1]; // NB: duplicating last frame so can have higher sample rate than frame rate

		float cumulativeTime = 0;
		for ( int frameIndex = 0; frameIndex < numFrames; ++frameIndex )
		{
			keyframes[frameIndex] = new ObjectReferenceKeyframe();
			keyframes[frameIndex].time = cumulativeTime;
			if ( data.m_frameDurations != null && frameIndex < data.m_frameDurations.Length )
				cumulativeTime += (float)data.m_frameDurations[frameIndex]/DEFAULT_SAMPLE_RATE_MS;
			else
				cumulativeTime += DEFAULT_FRAME_DURATION;


			string fileName = data.m_name + "_"+(frameIndex).ToString()+".png";
			string relativeTargetPath = MakeRelative( spritePath + fileName, Application.dataPath);
			#if UNITY_5_0_0
			Sprite sprite = AssetDatabase.LoadAssetAtPath(relativeTargetPath, typeof(Sprite)) as Sprite;
			#else
			Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(relativeTargetPath);
			#endif
			keyframes[frameIndex].value = sprite;
		}
		// Add duplicate frame to end
		if ( numFrames > 0 )
		{
			keyframes[numFrames] = new ObjectReferenceKeyframe() {
				time = cumulativeTime-(1.0f/animClip.frameRate),
				value = keyframes[numFrames-1].value };
		}

		AnimationUtility.SetObjectReferenceCurve(animClip,curveBinding,keyframes);

		if ( isNew )
		{
			// Save the animation
			AssetDatabase.CreateAsset(animClip, animFileName);
		}
		else
		{
			EditorUtility.SetDirty(animClip);
		}

	}

	static void CreateAnimationsFromSelected()
	{

		// Put textures in list and sort
		List<Texture2D> textures = new List<Texture2D>(Selection.objects.Length);
		foreach( Object obj in Selection.objects )
		{
			if ( obj != null && obj as Texture2D != null )
				textures.Add(obj as Texture2D);
		}
		if ( textures.Count <= 0 )
			return;

		using ( PowerSpriteImportEditor.NaturalComparer comparer = new PowerSpriteImportEditor.NaturalComparer() )
		{
			textures.Sort( (a, b) => comparer.Compare(a.name,b.name) );
		}

		string spritePath =  Path.GetDirectoryName(AssetDatabase.GetAssetPath(textures[0]));
		string animName = textures[0].name;
		int lastUnderscoreIndex = animName.LastIndexOf('_');
		if ( lastUnderscoreIndex > 0 )
			animName = animName.Substring(0,lastUnderscoreIndex);

		// Create list of sprites
		List<Sprite> sprites = new List<Sprite>();
		foreach( Texture2D tex in textures )
		{
			// Grab all sprites associated with a texture, add to list
			string path = AssetDatabase.GetAssetPath(tex);
			Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
			foreach (Object subAsset in assets)
			{
				if (subAsset is Sprite)
				{
					sprites.Add((Sprite)subAsset);
				}
			}
		}


		//
		// Create anims
		//
		bool isNew = false;
		string animFileName = spritePath+"/"+ animName+".anim";
		AnimationClip animClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animFileName);
		if ( animClip == null )
		{
			animClip = new AnimationClip();
			isNew = true;
		}

		animClip.name = animName;

		EditorCurveBinding curveBinding = new EditorCurveBinding();
		// I want to change the sprites of the sprite renderer, so I put the typeof(SpriteRenderer) as the binding type.
		curveBinding.type = typeof(SpriteRenderer);
		// This is the property name to change the sprite of a sprite renderer
		curveBinding.propertyName = "m_Sprite";
		// Regular path to the gameobject that will be changed (empty string means root)
		curveBinding.path = "";

		if ( isNew )
			animClip.frameRate = DEFAULT_SAMPLE_RATE;

		ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Count+1]; // NB: duplicating last frame so can have higher sample rate than frame rate

		for ( int frameIndex = 0; frameIndex < keyframes.Length-1; ++frameIndex )
		{
			keyframes[frameIndex] = new ObjectReferenceKeyframe();
			keyframes[frameIndex].time = 0.1f * frameIndex;
			keyframes[frameIndex].value = sprites[frameIndex];
		}
		// Add duplicate frame to end
		if ( sprites.Count > 0 )
		{
			keyframes[sprites.Count] = new ObjectReferenceKeyframe() {
				time = (0.1f*sprites.Count)-(1.0f/animClip.frameRate),
				value = keyframes[sprites.Count-1].value };
		}

		AnimationUtility.SetObjectReferenceCurve(animClip,curveBinding,keyframes);

		if ( isNew )
		{
			// Save the animation
			AssetDatabase.CreateAsset(animClip, animFileName);
		}

		AssetDatabase.SaveAssets();

		// Refresh asset database
		AssetDatabase.Refresh();
	}


	public static string MakeRelative(string filePath, string referencePath)
	{
		try
		{
			var fileUri = new System.Uri(filePath);
			var referenceUri = new System.Uri(referencePath);
			if ( referenceUri.IsAbsoluteUri)
				return referenceUri.MakeRelativeUri(fileUri).ToString();
		}
		catch (System.Exception e)
		{
			if ( e != null ){}
		}
		return filePath;
	}

	// Gets a subdirectory of current game object directory, creating it if necessary
	string GetSubdirectory(string subDirectoryName )
	{
		string result = Path.GetDirectoryName(AssetDatabase.GetAssetPath(m_component));
		if ( string.IsNullOrEmpty(subDirectoryName) == false )
		{
			result += "/" + subDirectoryName;
			Directory.CreateDirectory( result );
		}
		result = Path.GetFullPath(result);

		result = result.Replace("\\", "/");

		if (result.EndsWith("/") == false )
			result += "/";
		return result;
	}


	void RecalcLengths()
	{
		if ( m_items == null )
			return;
		// Recalculate lengths of all frames except last
		for ( int i = 0; i < m_items.Count-1; ++i )
		{
			m_items[i].m_length = Mathf.Max(1, m_items[i+1].m_firstFrame - m_items[i].m_firstFrame);
		}

	}

	void RecalcFirstFrames()
	{
		if ( m_items == null )
			return;
		// Recalculate first frames based on order of frames, and calculated lengths.
		for ( int i = 1; i < m_items.Count; ++i )
		{
			m_items[i].m_firstFrame = m_items[i-1].m_firstFrame + m_items[i-1].m_length;
		}
	}

	/// For sorting strings by natural order (so, for example walk_9.png is sorted before walk_10.png)
	public class NaturalComparer: Comparer<string>, System.IDisposable
	{
		// NaturalComparer function courtesy of Justin Jones http://www.codeproject.com/Articles/22517/Natural-Sort-Comparer

		Dictionary<string, string[]> m_table = null;

		public NaturalComparer()
		{
			m_table = new Dictionary<string, string[]>();
		}

		public void Dispose()
		{
			m_table.Clear();
			m_table = null;
		}

		public override int Compare(string x, string y)
		{
			if (x == y)
				return 0;

			string[] x1, y1;
			if (!m_table.TryGetValue(x, out x1))
			{
				x1 = Regex.Split(x.Replace(" ", ""), "([0-9]+)");
				m_table.Add(x, x1);
			}
			if (!m_table.TryGetValue(y, out y1))
			{
				y1 = Regex.Split(y.Replace(" ", ""), "([0-9]+)");
				m_table.Add(y, y1);
			}

			for (int i = 0; i < x1.Length && i < y1.Length; i++)
			{
				if (x1[i] != y1[i])
				{
					return PartCompare(x1[i], y1[i]);
				}
			}

			if (y1.Length > x1.Length)
			{
				return 1;
			}
			else if (x1.Length > y1.Length)
			{
				return -1;
			}

			return 0;
		}


		static int PartCompare(string left, string right)
		{
			int x, y;
			if (!int.TryParse(left, out x))
			{
				return left.CompareTo(right);
			}

			if (!int.TryParse(right, out y))
			{
				return left.CompareTo(right);
			}

			return x.CompareTo(y);
		}

	}

}

#endregion
#region RenamePrompt Window

// Window for creating a new function
public class RenamePrompt : PopupWindowContent 
{

	System.Action<string> OnRename = null;

	public string m_result = null;
	public Rect m_rect = new Rect();
	
	public RenamePrompt(){}
	public RenamePrompt(Rect rect, string currName, System.Action<string> onRename)
	{
		m_rect = rect;
		m_result = currName; 
		OnRename += onRename; 
	}
	
	override public void OnGUI(Rect rect) 
	{		
		//titleContent.text = "New Function";
		editorWindow.minSize = new Vector2(0,0);
		editorWindow.maxSize = new Vector2(500,EditorGUIUtility.singleLineHeight + 5 );		
		
		GUILayout.BeginHorizontal();
				
		m_result = EditorGUILayout.TextField(m_result).Trim();

		bool rename = GUILayout.Button("Rename");
		GUILayout.EndHorizontal();

		if ( rename )
		{
			if ( OnRename != null)
				OnRename.Invoke(m_result);		
			editorWindow.Close();
		}
	}

}


}
#endregion


namespace PowerTools.SpriteImporter
{
	#region Class: SpriteImporterEditorExtentionUtils
	public static class SpriteImporterEditorExtentionUtils
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
#endregion
}
