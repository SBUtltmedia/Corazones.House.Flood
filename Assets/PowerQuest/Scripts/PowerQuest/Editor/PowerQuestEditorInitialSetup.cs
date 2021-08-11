using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using System.IO;
using System.Text.RegularExpressions;
using PowerTools.Quest;
using PowerTools;

namespace PowerTools.Quest
{


// For Initial Setup gui stuff
public partial class PowerQuestEditor
{
	#region Variables: Static definitions

	static readonly string[] GAME_TEMPLATE_NAMES = {"Default","9-Verb"};		
	static readonly string[] GAME_TEMPLATE_PATHS = 
	{
		"Assets/PowerQuest/Templates/DefaultGameTemplate.unitypackage",
		"Assets/PowerQuest/Templates/9VerbGameTemplate.unitypackage",
	};
	static readonly string[] GAME_TEMPLATE_DESCRIPTIONS = 
	{
		"The Default Template is a modern 1 or 2 click interface, with drop-down inventory (Similar to most Wadjet Eye games, or Beneath a Steel Sky).",
		"The 9-Verb interface is the classic Lucasarts style. It's clunky by modern standards, but fun for that retro style. Just be prepared to write lots of responses for interactions! \n\nIt's a bit more complicated than the default, so read \"Assets\\Game\\9-Verb-ReadMe.txt\" to get started.",
	};

	#endregion
	#region Variables: Serialized

	[SerializeField] bool m_initialSetupImportInProgress = false;

	#endregion
	#region Gui Layout: Initial Setup Required

	enum eGameTemplate {Default, NineVerb};
	eGameTemplate m_gameTemplate = eGameTemplate.Default;

	void OnGuiInitialSetup()
	{
		GUILayout.Space(20);
		//GUILayout.BeginHorizontal();
		GUILayout.Label("Initial set up required!", EditorStyles.boldLabel );

		EditorGUILayout.HelpBox("This will create template game files to get you started."+"\n\n"+GAME_TEMPLATE_DESCRIPTIONS[(int)m_gameTemplate],MessageType.Info);

		
		m_gameTemplate = (eGameTemplate)EditorGUILayout.Popup("Game Template:",(int)m_gameTemplate,GAME_TEMPLATE_NAMES);
		string templateText = string.Empty;

		if ( GUILayout.Button("Set it up!") )
		{
			m_initialSetupImportInProgress = true;
			AssetDatabase.ImportPackage( GAME_TEMPLATE_PATHS[(int)m_gameTemplate], false );
		}
		
		EditorGUILayout.Separator();
		LayoutManual();
	}


    #endregion
	#region Functions: Private


	// Checks setup of powerquest
	void UpdateCheckInitialSetup()
	{

		if ( m_powerQuest == null )
		{
			GameObject obj = AssetDatabase.LoadAssetAtPath(m_powerQuestPath, typeof(GameObject)) as GameObject;
			if ( obj != null ) // we check for layout event type so we don't change the gui while between layout and refresh
				m_powerQuest = obj.GetComponent<PowerQuest>();
			if ( m_powerQuest != null )
				OnFoundPowerQuest();
		}

		// Find systemText in same folder as PowerQuest
		if ( m_systemText == null)
		{
			string systemPath = Path.GetDirectoryName(m_powerQuestPath)+"/SystemText.prefab";
			GameObject obj = AssetDatabase.LoadAssetAtPath(systemPath, typeof(GameObject)) as GameObject;
			if ( obj != null )
			{
				m_systemText = obj.GetComponent<SystemText>();
			}
		}

		// Find systemAudio in same folder as PowerQuest
		if ( m_systemAudio == null)
		{
			string systemPath = Path.GetDirectoryName(m_systemAudioPath)+"/SystemAudio.prefab";
			GameObject obj = AssetDatabase.LoadAssetAtPath(systemPath, typeof(GameObject)) as GameObject;
			if ( obj != null )
			{
				m_systemAudio = obj.GetComponent<SystemAudio>();
			}
		}
	}

	void OnFoundPowerQuest()
	{
		CreateMainGuiLists();

		if ( m_initialSetupImportInProgress )
		{
			m_initialSetupImportInProgress = false;
			OnInitialSetupComplete();
		}
	}

	// Called when first imported game data
	void OnInitialSetupComplete()
	{
		AssetDatabase.Refresh(); // In case there were changes while play mode.

		// Add scenes to editor build settings
		foreach ( RoomComponent roomPrefab in GetPowerQuest().GetRoomPrefabs() )
		{	
			string path	= Path.GetDirectoryName( AssetDatabase.GetAssetPath(roomPrefab) ) + "/" + roomPrefab.GetData().GetSceneName()+".unity";
			AddSceneToBuildSettings(path);
		}

		// Load the first scene
		if ( GetPowerQuest().GetRoomPrefabs().Count > 0 )
		{
			LoadRoomScene(GetPowerQuest().GetRoomPrefabs()[0]);
		}
		
		// NB: This file exists for upgrades, but must be removed
		AssetDatabase.DeleteAsset(@"Assets\Plugins\PowerSpriteImport\Editor\PowerSpriteImportEditor.cs");		

		Repaint();

	}

	#endregion
	#region Version Upgradeing

	TextAsset m_versionFile = null;

	// Checks if version has changed, and if it has, upgrades
	void CheckVersion()
	{
		if ( m_powerQuest == null )
			return;

		// load version file 
		if ( m_versionFile == null )
			m_versionFile =  AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/PowerQuest/Version.txt");				
		int newVersion = Version(m_versionFile.text);

		// Check version, upgrade stuff if necessary		
		int version = m_powerQuest.EditorGetVersion();
		if ( version < newVersion )
		{
			if ( UpgradeVersion(version, newVersion) )
			{
				m_powerQuest.EditorSetVersion(newVersion);
				EditorUtility.SetDirty(m_powerQuest);
			}
		}
	}


	bool UpgradeVersion(int oldVersion, int newVersion)
	{
		try 
		{

			// Version 0.4.3 changed what GlobalScript inherits from (slightly)
			if ( oldVersion < Version(0,4,3) )
			{
				// Need to update GlobalScript to inherit from GlobalScript	

				string globalSource = File.ReadAllText(	PowerQuestEditor.PATH_GLOBAL_SCRIPT );
				globalSource = Regex.Replace(globalSource, "class GlobalScript.*","class GlobalScript : GlobalScriptBase<GlobalScript>", RegexOptions.Multiline);
				File.WriteAllText( PowerQuestEditor.PATH_GLOBAL_SCRIPT, globalSource );
				AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
			}

			// 0.5.0: Changes to DialogText, AudioCues, QuestCamera
			if ( oldVersion < Version(0,5,0) )
			{		
				// Dialog text set to Gui layer with -10 sort order
				m_powerQuest.GetDialogTextPrefab().SortingLayer = "Gui";
				m_powerQuest.GetDialogTextPrefab().OrderInLayer = -10;
				m_powerQuest.GetDialogTextPrefab().gameObject.layer = LayerMask.NameToLayer("UI");
				EditorUtility.SetDirty(m_powerQuest.GetDialogTextPrefab());

				// AudioCues that have a clip with "loop" should set "loop" in base
				foreach( AudioCue cue in m_systemAudio.EditorGetAudioCues() )
				{
					if ( cue.GetClipCount() > 0 && cue.GetClipData(0).m_loop )
					{
						cue.m_loop = true;
						EditorUtility.SetDirty(cue.gameObject);
					}					
				}
				/* Actually... leave it as it is
				// QuestCamera layer should be set to NoPause
				GameObject questCam = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Camera/QuestCamera.prefab");
				if ( questCam != null )
				{
					questCam.layer = LayerMask.NameToLayer("NoPause");
					EditorUtility.SetDirty(questCam);
				}*/

			}

			if ( oldVersion < Version(0,5,2) )
			{
				// Add speechBox package
				AssetDatabase.ImportPackage( "Assets/PowerQuest/Templates/SpeechBox.unitypackage", false );	

			}

			if ( oldVersion < Version(0,8,1) )
			{
				// Add PowerQuestExtensions
				AssetDatabase.ImportPackage( "Assets/PowerQuest/Templates/Update-0-8.unitypackage", false );

				// Also really should add "OnMouseClick" to global script here... but too fiddly, so people are on there own with that one :O

				// Also, gui toolbar curves changed, copy them from the inventory one
				GuiComponent guiToolbar = m_powerQuest.GetGuiPrefabs().Find(item=>item.GetData().ScriptName == "Toolbar");
				GuiComponent guiInventory = m_powerQuest.GetGuiPrefabs().Find(item=>item.GetData().ScriptName == "Inventory");
				if ( guiToolbar != null && guiInventory != null && guiToolbar.GetComponent<GuiDropDownBar>() != null && guiInventory.GetComponent<GuiDropDownBar>() != null)
				{
					SerializedObject objToolbar = new SerializedObject(guiToolbar.GetComponent<GuiDropDownBar>());
					SerializedObject invToolbar = new SerializedObject(guiInventory.GetComponent<GuiDropDownBar>());
					if ( Mathf.Approximately( objToolbar.FindProperty("m_dropDownDistance").floatValue,1.0f) )
					{
						objToolbar.FindProperty("m_dropDownDistance").floatValue = invToolbar.FindProperty("m_dropDownDistance").floatValue;
						objToolbar.FindProperty("m_curveIn").animationCurveValue = invToolbar.FindProperty("m_curveIn").animationCurveValue;
						objToolbar.FindProperty("m_curveOut").animationCurveValue = invToolbar.FindProperty("m_curveOut").animationCurveValue;

						objToolbar.ApplyModifiedProperties();
						EditorUtility.SetDirty(guiToolbar);
					}
				}


				// Also remove PlayAmbientSound call from globalscript
				string globalSource = File.ReadAllText(	PowerQuestEditor.PATH_GLOBAL_SCRIPT );
				globalSource = Regex.Replace(globalSource, @"// Restart ambient sound","", RegexOptions.Multiline);
				globalSource = Regex.Replace(globalSource, @"PlayAmbientSound\(m_ambientSoundName\);","", RegexOptions.Multiline);
				File.WriteAllText( PowerQuestEditor.PATH_GLOBAL_SCRIPT, globalSource );
				AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

				// And delete the QuestGuiButton which has been removed
				AssetDatabase.DeleteAsset("Assets/PowerQuest/Scripts/Gui/QuestGuiButton.cs");
			}

			if ( oldVersion < Version(0,9,0) )
			{
				// update dialog text offset (down to 2, now it takes text  size into account)
				GuiComponent guiDialog = m_powerQuest.GetGuiPrefabs().Find(item=>item.GetData().ScriptName == "DialogTree");
				if ( guiDialog != null && guiDialog.GetComponent<GuiDialogTreeComponent>() != null )
				{
					SerializedObject serializedObj = new SerializedObject(guiDialog.GetComponent<GuiDialogTreeComponent>());
					if ( serializedObj.FindProperty("m_itemSpacing").floatValue > 5 )
					{
						
						serializedObj.FindProperty("m_itemSpacing").floatValue = 2;
						serializedObj.ApplyModifiedProperties();
						EditorUtility.SetDirty(guiDialog);
					}
				}

				// Update "Enable" and "Show" functions to remove the first boolean (if it's false, should error). Show(false) will give wierd errors), 

				// Actually- just show a prompt
				EditorUtility.DisplayDialog(
					"PowerQuest Updated!",
					"Important note! The following functions changed-\n\n   Character::Show(bool visible, bool clickable)\n   Prop::Enable(bool visible, bool clickable)\n\nChanged to \n\n   Character::Show(bool clickable)\n   Prop::Enable(bool clickable)\n\nSo where you had C.Player.Show(true,true); Change it to 'C.Player.Show();'\n\nWhere you had 'C.Player.Show(false,true);'. Change it to 'C.Player.Visible = true;'\n\n---\n\nLook at the online documentation for a full list of changes!\n",
					"Got it!");

			}

			if ( oldVersion < Version(0,11,0) )
			{
				// Set default inventory click mode to zero when updating				
				SerializedObject serializedObj = new SerializedObject(m_powerQuest);
				if ( serializedObj.FindProperty("m_inventoryClickStyle") != null)
				{
					// Found inv click property					
					serializedObj.FindProperty("m_inventoryClickStyle").intValue = 0;
					serializedObj.ApplyModifiedProperties();
					EditorUtility.SetDirty(m_powerQuest);
				}
			}

			if ( oldVersion < Version(0,12,5) )
			{
				// update project settings to set webgl setting
				PowerQuestProjectSetupUtil.SetProjectSettings();
			}
			if ( oldVersion < Version(0,13,6) )
			{
			
				// Actually- just show a prompt
				EditorUtility.DisplayDialog(
					"PowerQuest Updated!",
					"Important note! The character Show() and Enable() functions have changed subtly. They no longer override the 'Clickable' property you have set. \n\nEg. If you've specifically set 'Clickable = false;' then call 'Show();' the character will stay non-clickable.\n\n---\n\nLook at the online documentation for more info and a full list of changes!\n",
					"Got it!");
			}

			if (oldVersion < Version(0,14,5))
			{
				// Delete old version of PowerSpriteImportEditor				
				AssetDatabase.DeleteAsset(@"Assets\Plugins\PowerSpriteImport\Editor\PowerSpriteImportEditor.cs");				
			}

		}
		catch ( System.Exception e )
		{
			Debug.LogError("Failed to upgrade PowerQuest, close unity and try again\n\n"+e.Message);
			return false;
		}

		return true;
	}

}

#endregion
#region PowerQuest Layer Installer

//  class that just adds necessary layers to the project
[InitializeOnLoad]
public class PowerQuestProjectSetupUtil
{
	static readonly string[] LAYERS_REQUIRED = {"NoPause","HighRes"};
	static readonly string[] SORTINGLAYERS_NAME = {"Gui","GameText"};
	static readonly long[] SORTINGLAYERS_HASH = {3130941793,1935310555}; // hack but it was breaking existing stuff without it.

	/*
	static readonly string[] SHADER_PATHS_REQUIRED = 
		{
			"Assets/PowerQuest/Scripts/Shaders/PowerSprite.shader",
			"Assets/PowerQuest/Scripts/Shaders/PowerSpriteOutline.shader",
			"Assets/PowerQuest/Scripts/Shaders/FontPixel.shader",
			"Assets/PowerQuest/Scripts/Shaders/FontPixelSmooth.shader",
			"Assets/PowerQuest/Scripts/Shaders/FontSharp.shader"
		};
	*/

	static PowerQuestProjectSetupUtil()
	{
		if ( HasLayers() == false )
		{
			AddLayers();
			SetProjectSettings();
		}
	}

	/*
	static void AddShaders()
	{
		return;
		SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath ("ProjectSettings/GraphicsSettings.asset")[0]);

		SerializedProperty allLayers = tagManager.FindProperty ("m_AlwaysIncludedShaders");
		if (allLayers == null || !allLayers.isArray)
			return;	

		List<string> shadersToAdd = new List<string>(SHADER_PATHS_REQUIRED);

		for (int i = 0; i < allLayers.arraySize; ++i )
		{
			string name = AssetDatabase.GetAssetPath(allLayers.GetArrayElementAtIndex(i).objectReferenceValue);
			shadersToAdd.RemoveAll(item=>item == name);
			Debug.Log("Found Shader "+name);
		}

		foreach( string shaderPath in shadersToAdd )
		{
			// Find the shader
			Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
			if ( shader != null )
			{
				allLayers.InsertArrayElementAtIndex( allLayers.arraySize );
				allLayers.GetArrayElementAtIndex(allLayers.arraySize-1).objectReferenceValue = shader;
				Debug.Log("Added Shader "+shaderPath);
			}
			else 
				Debug.Log("Failed to add shader "+shaderPath);
			
		}		
	}*/

	static bool HasLayers()
	{	
		return System.Array.Exists(LAYERS_REQUIRED, item=>LayerMask.NameToLayer(item) <= 0 ) == false
			&& System.Array.Exists(SORTINGLAYERS_NAME, item=>SortingLayer.GetLayerValueFromName(item) <= 0 ) == false;
	}

	// Adapted From http://forum.unity3d.com/threads/adding-layer-by-script.41970/ via AC ;)
	static void AddLayers()
	{
		if ( HasLayers() )
			return;

		// Doesn't exist, so create it
		SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath ("ProjectSettings/TagManager.asset")[0]);

		SerializedProperty allLayers = tagManager.FindProperty ("layers");
		if (allLayers == null || !allLayers.isArray)
			return;	

		foreach ( string layerName in LAYERS_REQUIRED )
		{
			if ( LayerMask.NameToLayer(layerName) >= 0 )
				continue;
			
			// Create layer
			SerializedProperty slot = null;
			for (int i = 8; i <= 31; i++)
			{			
				SerializedProperty sp = allLayers.GetArrayElementAtIndex (i);
				if (sp != null && string.IsNullOrEmpty (sp.stringValue))
				{
					slot = sp;
					break;
				}
			}

			if (slot != null)
			{
				slot.stringValue = layerName;
			}
			else
			{
				Debug.LogError("Failed to install PowerQuest- Could not find an open Layer Slot for: " + layerName);
				return;
			}

			tagManager.ApplyModifiedProperties ();

			Debug.Log("Created layer: " + layerName);
		}

		SerializedProperty allSortingLayers = tagManager.FindProperty ("m_SortingLayers");
		if (allSortingLayers == null || !allSortingLayers.isArray)
			return;	
		for ( int i = 0; i < SORTINGLAYERS_NAME.Length; ++i )
		{
			string layerName = SORTINGLAYERS_NAME[i];
			long layerHash = SORTINGLAYERS_HASH[i];

			//Debug.Log("layer: " + layerName + "Id: "+SortingLayer.GetLayerValueFromName(layerName));

			if ( SortingLayer.GetLayerValueFromName(layerName) > 0 ) 
				continue;
			
			// Create layer
			allSortingLayers.InsertArrayElementAtIndex(allSortingLayers.arraySize);
			SerializedProperty sp = allSortingLayers.GetArrayElementAtIndex(allSortingLayers.arraySize-1);
			sp.FindPropertyRelative("name").stringValue = layerName;
			sp.FindPropertyRelative("uniqueID").longValue = layerHash;

			tagManager.ApplyModifiedProperties ();

			Debug.Log("Created sorting layer: " + layerName); 
		}
	}

	public static void SetProjectSettings()
	{
		SerializedObject editorSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath ("ProjectSettings/EditorSettings.asset")[0]);
		if (  editorSettings != null )
		{
			// Sprite packer setting
			SerializedProperty prop = editorSettings.FindProperty ("m_SpritePackerMode");
			if ( prop != null )
			{
				#if UNITY_2020_1_OR_NEWER
					Debug.LogWarning("PowerQuest setup - NB: If using unity 2020+, sprite atlases are not created automatically (yet). If your game runs poorly, google how to set up sprite atlases ;)"); 
				#else				
					prop.intValue = (int)SpritePackerMode.BuildTimeOnly;
					Debug.Log("PowerQuest setup - Set sprite packer to legacy mode"); 
				#endif
			}

			// Line endings settings
			prop = editorSettings.FindProperty ("m_LineEndingsForNewScripts");
			if ( prop != null )
			{
				prop.intValue = (int)LineEndingsMode.Unix;
				Debug.Log("PowerQuest setup - Set line endings to unix"); 
			}
			editorSettings.ApplyModifiedProperties();
		}

		SerializedObject projectSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath ("ProjectSettings/ProjectSettings.asset")[0]);
		if (  projectSettings != null )
		{
			// WebGL exception settings
			// webGLExceptionSupport: 2
			SerializedProperty prop = projectSettings.FindProperty("webGLExceptionSupport");
			if ( prop != null )
			{
				prop.intValue = (int)WebGLExceptionSupport.FullWithoutStacktrace;				
				Debug.Log("PowerQuest setup - WebGL Exception Support to full"); 
			}
			projectSettings.ApplyModifiedProperties();
		}
	}
	
	public static void SetLineEndingsSetting()
	{
		SerializedObject editorSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath ("ProjectSettings/EditorSettings.asset")[0]);
		if (  editorSettings == null )
			return;
		SerializedProperty prop = editorSettings.FindProperty ("m_SpritePackerMode");
		if ( prop != null )
		{
			prop.intValue = (int)SpritePackerMode.BuildTimeOnly;
		}

		prop = editorSettings.FindProperty ("m_LineEndingsForNewScripts");
		if ( prop != null )
		{
			prop.intValue = (int)LineEndingsMode.Unix;
		}
		editorSettings.ApplyModifiedProperties();

		//webGLExceptionSupport: 2

		Debug.Log("PowerQuest setup - Set sprite packer to legacy mode"); 
	}

}

#endregion

}
