using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using PowerTools.Quest;
using PowerTools;

namespace PowerTools.Quest
{

public partial class PowerQuestEditor
{

	#region Variables: Static definitions

	#endregion
	#region Variables: Serialized

	[SerializeField]  Editor m_walkableAreaEditor = null;
	[SerializeField]  int m_walkableAreaEditingId = -1;
	[SerializeField] int m_selectedRoomPoint = -1;
	[SerializeField] bool m_showRoomCharacters = false;

	#endregion
	#region Variables: Private


	ReorderableList m_listHotspots = null;
	ReorderableList m_listProps = null;
	ReorderableList m_listRegions = null;
	ReorderableList m_listPoints = null;
	ReorderableList m_listWalkableAreas = null; 
	ReorderableList m_listRoomCharacters = null; 

	// list of functions you can start he game with
	List<string> m_playFromFuncs = new List<string>();

	#endregion
	#region Functions: Quest Room


	void SelectRoom(ReorderableList list)
	{
		PowerQuestEditor powerQuestEditor = OpenPowerQuestEditor();
		if ( powerQuestEditor == null )
			return;

		if ( m_listRoomPrefabs.IsIndexValid(list.index))
		{
			// Find if there's an instance in the scene, if so select that.
			RoomComponent component = m_listRoomPrefabs[list.index];
			if ( component != null )
			{
				// If there's an instance inthe scene, select that, otherwise select the prefab
				GameObject instance = GameObject.Find( "Room"+component.GetData().ScriptName );
				Selection.activeObject = component.gameObject;
				if ( instance != null && instance.GetComponent<RoomComponent>() != null )
				{
					powerQuestEditor.Repaint();
					Selection.activeObject = instance;


					// Also ping the prefab
					EditorGUIUtility.PingObject(component.gameObject);
				}
				
				// focus the project window This confusing statement checks that the it's not an instance of a prefab (therefore is found in the project)
				if ( PrefabUtility.GetPrefabInstanceStatus(component) == PrefabInstanceStatus.NotAPrefab )
					EditorUtility.FocusProjectWindow();

				//Selection.activeObject = (instance != null && instance.GetComponent<RoomComponent>() != null ) ? instance : component.gameObject;
				
			}
		}
	}


	public static void CreateRoom( string path, string name )
	{
		// Make sure we can find powerQuest
		PowerQuestEditor powerQuestEditor = OpenPowerQuestEditor();
		if ( powerQuestEditor == null )
			return;

		// Check quest camera + guicamera are set, or if not, try grabbing them from the current scene
		if ( powerQuestEditor.m_questCamera == null )
		{
			QuestCameraComponent instance = GameObject.FindObjectOfType<QuestCameraComponent>();
			if ( instance != null )
				powerQuestEditor.m_questCamera = QuestEditorUtils.GetPrefabParent(instance.gameObject) as GameObject;
		}
		if ( powerQuestEditor.m_questGuiCamera == null )
		{
			#if UNITY_2017_1_OR_NEWER
			Canvas instance = GameObject.FindObjectOfType<Canvas>();
			#else
			GUILayer instance = GameObject.FindObjectOfType<GUILayer>();
			#endif
			if ( instance != null )
				powerQuestEditor.m_questGuiCamera = QuestEditorUtils.GetPrefabParent(instance.gameObject) as GameObject;
		}
		if ( powerQuestEditor.m_questCamera == null || powerQuestEditor.m_questGuiCamera == null )
		{
			Debug.LogError("Add a QuestCamera and QuestGuiCamera to the scene first");
			return;		
		}

		// Give user opportunity to save scene, and cancel if they hit cancel
		if ( EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false )
			return;

		// create directory
		path += "/" + name;
		if ( Directory.Exists(path) == false )
		{
			Directory.CreateDirectory(path);
			// This isn't necessary
			//AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
		}

		// Create new scene
		Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);

		// Add Camera and SystemMain
		PrefabUtility.InstantiatePrefab(powerQuestEditor.m_powerQuest);
		PrefabUtility.InstantiatePrefab(powerQuestEditor.m_questCamera);
		PrefabUtility.InstantiatePrefab(powerQuestEditor.m_questGuiCamera);

		// Create SpriteCollection
		if ( Directory.Exists(path+"/Sprites") == false )
		{
			Directory.CreateDirectory(path+"/Sprites");
			// This isn't necessary
			//AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
		}
		QuestEditorUtils.CreateImporter(path+"/_Import"+name+".asset", string.Empty);//"Room"+name);

		// Create game object
		GameObject gameObject = new GameObject("Room"+name, typeof(RoomComponent)) as GameObject; 

		RoomComponent room = gameObject.GetComponent<RoomComponent>();
		room.GetData().EditorInitialise(name);

		GameObject walkableAreaObj = new GameObject("WalkableArea",typeof(WalkableComponent)) as GameObject;
		walkableAreaObj.transform.parent = gameObject.transform;
		walkableAreaObj.name = "WalkableArea";
		walkableAreaObj.GetComponent<PolygonCollider2D>().points = DEFAULT_COLLIDER_POINTS;
		walkableAreaObj.GetComponent<PolygonCollider2D>().isTrigger = true;

		// turn game object into prefab
		#if UNITY_2018_3_OR_NEWER
		Object prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(gameObject, path + "/Room"+name+".prefab", InteractionMode.AutomatedAction);		
		#else
		Object prefab = PrefabUtility.CreatePrefab(path + "/Room"+name+".prefab", gameObject, ReplacePrefabOptions.ConnectToPrefab);
		#endif

		// Select the prefab for editing
		Selection.activeObject = prefab;

		// Add item to list in PowerQuest and repaint the quest editor
		powerQuestEditor.m_powerQuest.GetRoomPrefabs().Add(((GameObject)prefab).GetComponent<RoomComponent>());
		EditorUtility.SetDirty(powerQuestEditor.m_powerQuest);
		powerQuestEditor.Repaint();

		// Add line to GameGlobals.cs for easy scripting
		// public static Room Village { get { return E.GetRoom("RoomVillage"); } }
		QuestEditorUtils.InsertTextIntoFile(PATH_GAME_GLOBALS, "#R", "\n\t\tpublic static IRoom "+name.PadRight(14)+" { get { return PowerQuest.Get.GetRoom(\""+name+"\"); } }");

		// Save scene
		string scenePath = path+"/SceneRoom"+name+".unity";
		EditorSceneManager.SaveScene(newScene,scenePath);

		// Add scene to editor build settings
		PowerQuestEditor.AddSceneToBuildSettings(scenePath);
		
		powerQuestEditor.RequestAssetRefresh();

	}


	#endregion
	#region Functions: Quest Hotspot


	static public void CreateHotspot(string name = "New")
	{
		PowerQuestEditor powerQuestEditor = OpenPowerQuestEditor();
		if ( powerQuestEditor == null || powerQuestEditor.m_selectedRoom == null)
			return;

		// Create game object
		GameObject gameObject = new GameObject("Hotspot"+name, typeof(HotspotComponent), typeof(PolygonCollider2D)) as GameObject; 

		//CharacterComponent character = gameObject.GetComponent<CharacterComponent>();
		//character.GetData().EditorInitialise(name);

		PolygonCollider2D collider = gameObject.GetComponent<PolygonCollider2D>();
		collider.isTrigger = true;		 
		collider.points = PowerQuestEditor.DEFAULT_COLLIDER_POINTS;


		HotspotComponent hotspotComponent = gameObject.GetComponent<HotspotComponent>();
		hotspotComponent.GetData().EditorInitialise(name);

		// Add to the selected room 
		gameObject.transform.parent = powerQuestEditor.m_selectedRoom.transform;

		Selection.activeObject = gameObject;		

		powerQuestEditor.m_selectedRoom.EditorUpdateChildComponents();
		powerQuestEditor.UpdateRoomObjectOrder(false);
		QuestEditorUtils.ReplacePrefab(powerQuestEditor.m_selectedRoom.gameObject);
		powerQuestEditor.Repaint();

	}

	void DeleteHotspot(int index = -1) 
	{
		// if index is -1, deletes the end
		List<HotspotComponent> components = m_selectedRoom.GetHotspotComponents();
		HotspotComponent component = null;
		if (components.Count <= 0)
			return;

		if ( index == -1)
			index = components.Count - 1;
		if ( components.IsIndexValid(index) )
			component = components[index];
		
		if ( EditorUtility.DisplayDialog("Really Delete?", "Dude, Sure you wanna delete "+component.GetData().ScriptName+"?", "Yeah Man", "Hmm, Nah") )
		{
			if ( component != null )
			{
				#if UNITY_2018_3_OR_NEWER

					string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(m_selectedRoom.gameObject);
					GameObject instancedObject = PrefabUtility.LoadPrefabContents(assetPath);
					RoomComponent instancedRoom = instancedObject.GetComponent<RoomComponent>();
					HotspotComponent instancedComponent = instancedRoom.GetHotspotComponents()[index];
					instancedRoom.GetHotspotComponents().Remove(instancedComponent);
					GameObject.DestroyImmediate(instancedComponent.gameObject);
					PrefabUtility.SaveAsPrefabAsset(instancedObject, assetPath);
					PrefabUtility.UnloadPrefabContents(instancedObject);

				#else
					components.Remove(component);
					GameObject.DestroyImmediate(component.gameObject);
				#endif
			}	
		}

		m_selectedRoom.EditorUpdateChildComponents();
		#if !UNITY_2018_3_OR_NEWER
			PrefabUtility.ReplacePrefab( m_selectedRoom.gameObject, PrefabUtility.GetPrefabParent(m_selectedRoom.gameObject), ReplacePrefabOptions.ConnectToPrefab );
		#endif
		PowerQuestEditor powerQuestEditor = EditorWindow.GetWindow<PowerQuestEditor>();
		if ( powerQuestEditor != null ) powerQuestEditor.Repaint();

	}


	#endregion
	#region Functions: Quest Prop

	public static void CreateProp( string name = "New", bool addCollider = true )
	{

		PowerQuestEditor powerQuestEditor = OpenPowerQuestEditor();
		if ( powerQuestEditor == null || powerQuestEditor.m_selectedRoom == null)
			return;

		// Create game object
		GameObject gameObject = new GameObject("Prop"+name, typeof(PropComponent) ) as GameObject; 


		PropComponent propComponent = gameObject.GetComponent<PropComponent>();
		propComponent.GetData().EditorInitialise(name);

		propComponent.GetData().Clickable = addCollider;
		if ( addCollider )
		{
			PolygonCollider2D collider = gameObject.AddComponent<PolygonCollider2D>();
			collider.isTrigger = true;
			collider.points = DEFAULT_COLLIDER_POINTS;
		}
		gameObject.AddComponent<SpriteAnim>();

		gameObject.GetComponent<SpriteRenderer>().sortingOrder = powerQuestEditor.m_selectedRoom.GetPropComponents().Count;

		// Set sprite if one exists already
		UpdateDefaultSprite( propComponent, propComponent.GetData().Animation, powerQuestEditor.m_selectedRoom.GetAnimations() );

		Selection.activeObject = gameObject;

		// Add to the selected room
		gameObject.transform.parent = powerQuestEditor.m_selectedRoom.transform;
		powerQuestEditor.m_selectedRoom.EditorUpdateChildComponents();
		powerQuestEditor.UpdateRoomObjectOrder(false);
		QuestEditorUtils.ReplacePrefab(powerQuestEditor.m_selectedRoom.gameObject);
		powerQuestEditor.Repaint(); 

	}

	void DeleteProp(int index = -1) 
	{
		// if index is -1, deletes the end
		List<PropComponent> components = m_selectedRoom.GetPropComponents();
		PropComponent component = null;
		if (components.Count <= 0)
			return;

		if (index == -1)
			index = components.Count - 1;
		if ( components.IsIndexValid(index) )
			component = components[index];
		
		if ( EditorUtility.DisplayDialog("Really Delete?", "Dude, Sure you wanna delete "+component.GetData().ScriptName+"?", "Yeah Man", "Hmm, Nah") )
		{
			if ( component != null )
			{
				#if UNITY_2018_3_OR_NEWER

					string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(m_selectedRoom.gameObject);
					GameObject instancedObject = PrefabUtility.LoadPrefabContents(assetPath);
					RoomComponent instancedRoom = instancedObject.GetComponent<RoomComponent>();
					PropComponent instancedComponent = instancedRoom.GetPropComponents()[index];
					instancedRoom.GetPropComponents().Remove(instancedComponent);
					GameObject.DestroyImmediate(instancedComponent.gameObject);
					PrefabUtility.SaveAsPrefabAsset(instancedObject, assetPath);
					PrefabUtility.UnloadPrefabContents(instancedObject);

				#else
					components.Remove(component);
					GameObject.DestroyImmediate(component.gameObject);
				#endif
			}	
		}	

		m_selectedRoom.EditorUpdateChildComponents();
		#if !UNITY_2018_3_OR_NEWER
			PrefabUtility.ReplacePrefab( m_selectedRoom.gameObject, PrefabUtility.GetPrefabParent(m_selectedRoom.gameObject), ReplacePrefabOptions.ConnectToPrefab );
		#endif
			PowerQuestEditor powerQuestEditor = EditorWindow.GetWindow<PowerQuestEditor>();
		if ( powerQuestEditor != null ) powerQuestEditor.Repaint();
	}

	#endregion
	#region Functions: Quest Region

	static public void CreateRegion(string name = "New")
	{
		PowerQuestEditor powerQuestEditor = OpenPowerQuestEditor();
		if ( powerQuestEditor == null || powerQuestEditor.m_selectedRoom == null)
			return;

		// Create game object
		GameObject gameObject = new GameObject("Region"+name, typeof(RegionComponent), typeof(PolygonCollider2D)) as GameObject; 

		//CharacterComponent character = gameObject.GetComponent<CharacterComponent>();
		//character.GetData().EditorInitialise(name);

		PolygonCollider2D collider = gameObject.GetComponent<PolygonCollider2D>();
		collider.isTrigger = true;
		collider.points = DEFAULT_COLLIDER_POINTS;

		RegionComponent regionComponent = gameObject.GetComponent<RegionComponent>();
		regionComponent.GetData().EditorInitialise(name);

		// Add to the selected room 
		gameObject.transform.parent = powerQuestEditor.m_selectedRoom.transform;

		Selection.activeObject = gameObject;

		powerQuestEditor.m_selectedRoom.EditorUpdateChildComponents();
		powerQuestEditor.UpdateRoomObjectOrder(false);
		QuestEditorUtils.ReplacePrefab(powerQuestEditor.m_selectedRoom.gameObject);
		powerQuestEditor.Repaint();

	}
	void DeleteRegion(int index = -1) 
	{
		// if index is -1, deletes the end
		List<RegionComponent> components = m_selectedRoom.GetRegionComponents();
		RegionComponent component = null;
		if (components.Count <= 0)
			return;

		if (index == -1)
			index = components.Count - 1;
		if ( components.IsIndexValid(index) )
			component = components[index];
		
		if ( EditorUtility.DisplayDialog("Really Delete?", "Dude, Sure you wanna delete "+component.GetData().ScriptName+"?", "Yeah Man", "Hmm, Nah") )
		{
			if ( component != null )
			{
				#if UNITY_2018_3_OR_NEWER
					string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(m_selectedRoom.gameObject);
					GameObject instancedObject = PrefabUtility.LoadPrefabContents(assetPath);
					RoomComponent instancedRoom = instancedObject.GetComponent<RoomComponent>();
					RegionComponent instancedComponent = instancedRoom.GetRegionComponents()[index];
					instancedRoom.GetRegionComponents().Remove(instancedComponent);
					GameObject.DestroyImmediate(instancedComponent.gameObject);
					PrefabUtility.SaveAsPrefabAsset(instancedObject, assetPath);
					PrefabUtility.UnloadPrefabContents(instancedObject);

				#else
					components.Remove(component);
					GameObject.DestroyImmediate(component.gameObject);
				#endif
			}
		}

		m_selectedRoom.EditorUpdateChildComponents();
		#if !UNITY_2018_3_OR_NEWER
			PrefabUtility.ReplacePrefab( m_selectedRoom.gameObject, PrefabUtility.GetPrefabParent(m_selectedRoom.gameObject), ReplacePrefabOptions.ConnectToPrefab );
		#endif
		PowerQuestEditor powerQuestEditor = EditorWindow.GetWindow<PowerQuestEditor>();
		if ( powerQuestEditor != null ) powerQuestEditor.Repaint();

	}


	#endregion
	#region Functions: Quest Walkable Area
	
	public static void CreateWalkableArea()
	{

		PowerQuestEditor powerQuestEditor = OpenPowerQuestEditor();
		if ( powerQuestEditor == null || powerQuestEditor.m_selectedRoom == null)
			return;


		GameObject walkableAreaObj = new GameObject("WalkableArea",typeof(WalkableComponent)) as GameObject;
		walkableAreaObj.transform.parent = powerQuestEditor.m_selectedRoom.transform;
		walkableAreaObj.name = "WalkableArea";
		walkableAreaObj.GetComponent<PolygonCollider2D>().points = DEFAULT_COLLIDER_POINTS;
		walkableAreaObj.GetComponent<PolygonCollider2D>().isTrigger = true;

		Selection.activeObject = walkableAreaObj;

		// Add to the selected room
		powerQuestEditor.m_selectedRoom.EditorUpdateChildComponents();
		powerQuestEditor.UpdateRoomObjectOrder(false);
		QuestEditorUtils.ReplacePrefab(powerQuestEditor.m_selectedRoom.gameObject);
		powerQuestEditor.Repaint();

	}

	void DeleteWalkableArea(int index = -1) 
	{
		// if index is -1, deletes the end
		List<WalkableComponent> components = m_selectedRoom.GetWalkableAreas();
		WalkableComponent component = null;
		if ( components.Count <= 0 )
			return;
		
		// Remove gameobject
		if ( index == -1 )
			index = components.Count-1;
		if ( components.IsIndexValid(index) )
			component = components[index];
		
		if ( EditorUtility.DisplayDialog("Really Delete?", "Dude, Sure you wanna delete Walkable Area "+index.ToString()+" ?", "Yeah Man", "Hmm, Nah") )
		{
			if ( component != null )
			{                
				#if UNITY_2018_3_OR_NEWER
					string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(m_selectedRoom.gameObject);
					GameObject instancedObject = PrefabUtility.LoadPrefabContents(assetPath);
					RoomComponent instancedRoom = instancedObject.GetComponent<RoomComponent>();
					WalkableComponent instancedComponent = instancedRoom.GetWalkableAreas()[index];
					instancedRoom.GetWalkableAreas().Remove(instancedComponent);
					GameObject.DestroyImmediate(instancedComponent.gameObject);
					PrefabUtility.SaveAsPrefabAsset(instancedObject, assetPath);
					PrefabUtility.UnloadPrefabContents(instancedObject);
				#else
					components.Remove(component);
					GameObject.DestroyImmediate(component.gameObject);
				#endif

			}
		}

		if ( m_walkableAreaEditor != null ) 
			Editor.DestroyImmediate(m_walkableAreaEditor);
		m_walkableAreaEditingId = -1;

		m_selectedRoom.EditorUpdateChildComponents();
		#if !UNITY_2018_3_OR_NEWER
			PrefabUtility.ReplacePrefab( m_selectedRoom.gameObject, PrefabUtility.GetPrefabParent(m_selectedRoom.gameObject), ReplacePrefabOptions.ConnectToPrefab );
		#endif
		PowerQuestEditor powerQuestEditor = EditorWindow.GetWindow<PowerQuestEditor>();
		if ( powerQuestEditor != null ) powerQuestEditor.Repaint();

	}

	#endregion
	#region Functions: Create GUI Lists

	void UpdateRoomSelection( RoomComponent newRoom, bool repaint = false )
	{
		
		if ( m_selectedRoom == null )
			m_selectedRoom = null; // Fix for wierd bug: In case of missing reference, clear the room reference so that it will never match the new room. I didn't know that could happen!

		RoomComponent oldRoom = m_selectedRoom;
		if ( newRoom == null )
			m_selectedRoom = null;

		if ( (newRoom != null && oldRoom != newRoom)
			|| (newRoom == null) != (m_listProps == null && m_listHotspots == null && m_listRegions == null) ) // if changed, or lists are obviously out of date
		{
			m_selectedRoom = newRoom;
			m_listHotspots = null;
			m_listProps = null;
			m_listRegions = null;
			m_listPoints = null;
			m_listWalkableAreas = null;
			m_listRoomCharacters = null;
			m_selectedRoomPoint = -1;

			if ( m_selectedRoom != null )
			{
				m_selectedRoom.EditorUpdateChildComponents();

				if ( m_walkableAreaEditor != null ) 
					Editor.DestroyImmediate(m_walkableAreaEditor);
				m_walkableAreaEditingId = -1;

				// The selected room will be an instance unless the game is running
				#if UNITY_2018_3_OR_NEWER	
				bool isInstance = PrefabUtility.GetPrefabInstanceStatus(m_selectedRoom.gameObject) == PrefabInstanceStatus.Connected;
				#else 
				bool isInstance = PrefabUtility.GetPrefabType(m_selectedRoom.gameObject) == PrefabType.PrefabInstance;
				#endif

				if ( isInstance )
				{
					// If it's the room instance that's being edited, allow to add/remove hotspots
					m_listHotspots = new ReorderableList(m_selectedRoom.GetHotspotComponents(),typeof(HotspotComponent)) 
					{ 
						drawHeaderCallback = 	(Rect rect) => { EditorGUI.LabelField(rect, "Hotspots"); },
						drawElementCallback = 	LayoutHotspotGUI,
						onSelectCallback = 		SelectGameObjectFromList,
						onReorderCallback =     (ReorderableList list)=>UpdateRoomObjectOrder(),
						onAddCallback = 		(ReorderableList list) => 	
						{ 
							ScriptableObject.CreateInstance< CreateRoomObjectWindow >().ShowQuestWindow( eQuestObjectType.Hotspot, "Hotspot", "'Shrubbery' or 'DistantCity'",  PowerQuestEditor.CreateHotspot);
						},
						onRemoveCallback = 		(ReorderableList list) => { DeleteHotspot(list.index); }
					};

					m_listProps = new ReorderableList(m_selectedRoom.GetPropComponents(),typeof(PropComponent)) 
					{ 
						drawHeaderCallback = 	(Rect rect) => { EditorGUI.LabelField(rect, "Props"); },
						drawElementCallback = 	LayoutPropGUI,
						onSelectCallback = 		SelectGameObjectFromList,
						onReorderCallback =     (ReorderableList list)=>UpdateRoomObjectOrder(),
						onAddCallback = 		(ReorderableList list) => 
						{ 
							ScriptableObject.CreateInstance< CreatePropWindow >().ShowUtility();
						},
						onRemoveCallback = 		(ReorderableList list) => { DeleteProp(list.index); }
					};

					// If it's the room instance that's being edited, allow to add/remove regions
					m_listRegions = new ReorderableList(m_selectedRoom.GetRegionComponents(),typeof(RegionComponent)) 
					{ 
						drawHeaderCallback = 	(Rect rect) => { EditorGUI.LabelField(rect, "Regions"); },
						drawElementCallback = 	LayoutRegionGUI,
						onSelectCallback = 		SelectGameObjectFromList,
						onReorderCallback =     (ReorderableList list)=>UpdateRoomObjectOrder(),
						onAddCallback = 		(ReorderableList list) => 	
						{ 
							ScriptableObject.CreateInstance< CreateRoomObjectWindow >().ShowQuestWindow( eQuestObjectType.Region, "Region", "'Puddle' or 'Quicksand'",  PowerQuestEditor.CreateRegion);
						},
						onRemoveCallback = 		(ReorderableList list) => { DeleteRegion(list.index); }
					};

					m_listPoints = new ReorderableList(m_selectedRoom.GetData().GetPoints(),typeof(Room.RoomPoint)) 
					{ 
						drawHeaderCallback = 	(Rect rect) => { EditorGUI.LabelField(rect, "Points"); },
						drawElementCallback = 	LayoutRoomPointGUI,
						onSelectCallback = 		(ReorderableList list) => 
						{ 
							Selection.activeObject = null; 
							UnselectSceneTools(); 
							m_selectedRoomPoint = list.index; 
							SceneView.RepaintAll(); 
						},
						onAddCallback = 		(ReorderableList list) => 
						{ 
							Undo.RecordObject(m_selectedRoom, "Added Point");
							m_selectedRoom.GetData().GetPoints().Add(new Room.RoomPoint() { m_name = "Point"+m_selectedRoom.GetData().GetPoints().Count } ); 
							EditorUtility.SetDirty(m_selectedRoom);
						},
						//onRemoveCallback = 	(ReorderableList list) => { DeletePosition(list.index); }
					};

					m_listWalkableAreas = new ReorderableList(m_selectedRoom.GetWalkableAreas(),typeof(WalkableComponent)) 
					{ 
						drawHeaderCallback =    (Rect rect) => { EditorGUI.LabelField(rect, "Walkable Areas"); },
						drawElementCallback =   LayoutWalkableAreaGUI,
						onSelectCallback =      SelectGameObjectFromList,
						onReorderCallback =     (ReorderableList list)=>UpdateRoomObjectOrder(),
						onAddCallback =         (ReorderableList list) =>   { CreateWalkableArea(); },
						onRemoveCallback =      (ReorderableList list) => { DeleteWalkableArea(list.index); }
					};
				}
				else 
				{
					// This should only happen when the game is running now.
					// If it's the room prefab that's being edited, DON'T allow to add/remove hotspots. Couldn't find a way to add/remove children of the prefab without errors
					m_listHotspots = new ReorderableList(m_selectedRoom.GetHotspotComponents(),typeof(HotspotComponent), true, true, false, false) 
					{ 
						drawHeaderCallback = 	(Rect rect) => { EditorGUI.LabelField(rect, "Hotspots"); },
						drawElementCallback = 	LayoutHotspotGUI,
						onSelectCallback = 		SelectGameObjectFromList
					};

					m_listProps = new ReorderableList(m_selectedRoom.GetPropComponents(),typeof(PropComponent), true, true, false, false) 
					{ 
						drawHeaderCallback = 	(Rect rect) => { EditorGUI.LabelField(rect, "Props"); },
						drawElementCallback = 	LayoutPropGUI,
						onSelectCallback = 		SelectGameObjectFromList
					};

					m_listRegions = new ReorderableList(m_selectedRoom.GetRegionComponents(),typeof(RegionComponent), true, true, false, false) 
					{ 
						drawHeaderCallback = 	(Rect rect) => { EditorGUI.LabelField(rect, "Regions"); },
						drawElementCallback = 	LayoutRegionGUI,
						onSelectCallback = 		SelectGameObjectFromList
					};

					m_listPoints = new ReorderableList(m_selectedRoom.GetData().GetPoints(),typeof(Room.RoomPoint), true, true, false, false) 
					{ 
						drawHeaderCallback = 	(Rect rect) => { EditorGUI.LabelField(rect, "Points"); },
						drawElementCallback = 	LayoutRoomPointGUI,
						onSelectCallback = 		(ReorderableList list) => { m_selectedRoomPoint = list.index; },
					};

					m_listWalkableAreas = new ReorderableList(m_selectedRoom.GetWalkableAreas(),typeof(WalkableComponent), true, true, false, false) 
					{ 
						drawHeaderCallback =    (Rect rect) => { EditorGUI.LabelField(rect, "Walkable Areas"); },
						drawElementCallback =   LayoutWalkableAreaGUI,
						onSelectCallback =      SelectGameObjectFromList
					};
				}
			}


			m_listRoomCharacters = new ReorderableList( m_powerQuest.GetCharacterPrefabs(), typeof(CharacterComponent),false,true,false,false) 
			{ 			
				drawHeaderCallback = 	(Rect rect) => {  m_showRoomCharacters = EditorGUI.Foldout(rect, m_showRoomCharacters,"Characters (Room Specific Interactions)", true); },
				drawElementCallback = 	LayoutRoomCharacterGUI,
				onSelectCallback = 		SelectGameObjectFromList,
			};

			UpdatePlayFromFuncs();	

			if ( repaint ) 
				Repaint();
		}
	}

	void UpdateRoomObjectOrder(bool applyPrefab = true)
	{
		
		PowerQuestEditor powerQuestEditor = OpenPowerQuestEditor();
		if ( powerQuestEditor == null || powerQuestEditor.m_selectedRoom == null)
			return;

		int index = 0;					
		powerQuestEditor.m_selectedRoom.GetWalkableAreas().ForEach(item=> item.transform.SetSiblingIndex(index++));
		powerQuestEditor.m_selectedRoom.GetHotspotComponents().ForEach(item=> item.transform.SetSiblingIndex(index++));
		powerQuestEditor.m_selectedRoom.GetPropComponents().ForEach(item=> item.transform.SetSiblingIndex(index++));
		powerQuestEditor.m_selectedRoom.GetRegionComponents().ForEach(item=> item.transform.SetSiblingIndex(index++));
	
		if ( applyPrefab )
		{
			QuestEditorUtils.ReplacePrefab(powerQuestEditor.m_selectedRoom.gameObject);
			powerQuestEditor.Repaint(); 

		}	
	}

	#endregion
	#region Gui Layout: Room and contents

	void OnGuiRoom( bool tabChanged )
	{	    
		//
		// Layout room contents
		//

		if ( m_selectedRoom == null )
		{
			GUILayout.Label("Select a room's scene in the Main Panel", EditorStyles.centeredGreyMiniLabel);			
			return;		 
		}

		//GUILayout.Space(2);
					
		#if UNITY_2018_3_OR_NEWER	
		bool isPrefab = PrefabUtility.GetPrefabInstanceStatus(m_selectedRoom.gameObject) != PrefabInstanceStatus.Connected;
		#else 
		bool isPrefab =  PrefabUtility.GetPrefabType(m_selectedRoom.gameObject) == PrefabType.Prefab;
		#endif
		GUILayout.Label( m_selectedRoom.GetData().ScriptName + ( isPrefab ? " (Prefab)" : "" ), new GUIStyle(EditorStyles.largeLabel){alignment=TextAnchor.MiddleCenter});
		//GUILayout.Space(2);
				
		GUILayout.BeginHorizontal();

		if ( GUILayout.Button( "Select", EditorStyles.miniButtonLeft ) ) 
		{ 
			Selection.activeObject = m_selectedRoom.gameObject; 
			GameObject room = QuestEditorUtils.GetPrefabParent(m_selectedRoom.gameObject, true);
			if ( room == null && Application.isPlaying ) // in play mode, GetPrefabParent doesn't work :'(
			{
				RoomComponent roomC = GetPowerQuestEditor().GetRoom(m_selectedRoom.GetData().ScriptName);
				room = roomC != null ? roomC.gameObject : null;				
			}
			EditorGUIUtility.PingObject( room );
		}


		if ( GUILayout.Button( "Script", EditorStyles.miniButtonRight ) )
		{ 
			// Open the script
			QuestScriptEditor.Open( m_selectedRoom );	
		}
		GUILayout.EndHorizontal();
		GUILayout.Space(8);
		
		m_scrollPosition = EditorGUILayout.BeginScrollView(m_scrollPosition);
		
		if ( m_playFromFuncs.Count > 1 )
		{
			int debugFuncId = m_playFromFuncs.FindIndex(item=>string.Compare(item,m_selectedRoom.m_debugStartFunction,true) == 0);
			if ( debugFuncId <= 0) debugFuncId = 0;		
			debugFuncId = EditorGUILayout.Popup("Play-from function: ", debugFuncId, m_playFromFuncs.ToArray(), new GUIStyle(EditorStyles.toolbarPopup) );
			if ( debugFuncId <= 0)
				m_selectedRoom.m_debugStartFunction = null;
			else
				m_selectedRoom.m_debugStartFunction = m_playFromFuncs[debugFuncId];
				
			GUILayout.Space(8);
		}


		if ( m_listHotspots != null ) m_listHotspots.DoLayoutList();

		GUILayout.Space(5);

		if ( m_listProps != null ) m_listProps.DoLayoutList();

		GUILayout.Space(5);

		if ( m_listRegions != null ) m_listRegions.DoLayoutList();

		GUILayout.Space(5);

		if ( m_listPoints != null ) m_listPoints.DoLayoutList();

		GUILayout.Space(5);

		if ( m_listWalkableAreas != null ) m_listWalkableAreas.DoLayoutList();

		/* Now toggling in  place. But need to test in 2019.3+
		if ( m_walkableAreaEditor != null && m_walkableAreaEditor.target != null )
		{			
			GUILayout.Label("Editing Walkable Area "+m_walkableAreaEditingId.ToString()+":");
			m_walkableAreaEditor.OnInspectorGUI();
		} */

		GUILayout.Space(5);

		// Layout characters
		if ( m_showRoomCharacters && m_listRoomCharacters != null) 
			m_listRoomCharacters.DoLayoutList();
		else  
			m_showRoomCharacters = EditorGUILayout.Foldout(m_showRoomCharacters,"Characters (Room Specific Interactions)", true); 

		EditorGUILayout.EndScrollView();

		GUILayout.Label($"Mouse Pos (Ctrl+M to copy): {Mathf.RoundToInt(m_mousePos.x)}, {Mathf.RoundToInt(m_mousePos.y)}".PadRight(38,' '), EditorStyles.centeredGreyMiniLabel);
		GUILayout.Space(3);
	}


	void LayoutHotspotGUI(Rect rect, int index, bool isActive, bool isFocused)
	{
		if ( m_selectedRoom != null && m_selectedRoom.GetHotspotComponents().IsIndexValid(index))
		{

			HotspotComponent itemComponent = m_selectedRoom.GetHotspotComponents()[index];
			if ( itemComponent != null && itemComponent.GetData() != null )
			{		
				QuestEditorUtils.LayoutQuestObjectContextMenu( eQuestObjectType.Hotspot, m_listHotspots, itemComponent.GetData().GetScriptName(), itemComponent.gameObject, rect, index, true );

				int actionCount = (PowerQuestEditor.GetActionEnabled(eQuestVerb.Look)?1:0)
					+ (PowerQuestEditor.GetActionEnabled(eQuestVerb.Use)?1:0)
					+ (PowerQuestEditor.GetActionEnabled(eQuestVerb.Inventory)?1:0);
				float totalFixedWidth = (34 *actionCount);//+22;
				//actionCount++;
				float offset = rect.x;
				float fixedWidth = 34;
				EditorGUI.LabelField(new Rect(rect.x, rect.y+2, rect.width-totalFixedWidth, EditorGUIUtility.singleLineHeight), itemComponent.GetData().ScriptName );

				offset += rect.width - totalFixedWidth;
				int actionNum = 0;
				if ( PowerQuestEditor.GetActionEnabled(eQuestVerb.Look) )
				{
					if ( GUI.Button(new Rect(offset, rect.y, fixedWidth, EditorGUIUtility.singleLineHeight), "Look", QuestEditorUtils.GetMiniButtonStyle(actionNum++,actionCount) ) )
					{
						// Lookat
						QuestScriptEditor.Open( m_selectedRoom, QuestScriptEditor.eType.Hotspot,
							PowerQuest.SCRIPT_FUNCTION_LOOKAT_HOTSPOT+ itemComponent.GetData().ScriptName,
							PowerQuestEditor.SCRIPT_PARAMS_LOOKAT_HOTSPOT);
					}
					offset += fixedWidth;
				}
				if ( PowerQuestEditor.GetActionEnabled(eQuestVerb.Use) )
				{
					if ( GUI.Button(new Rect(offset, rect.y, fixedWidth, EditorGUIUtility.singleLineHeight), "Use", QuestEditorUtils.GetMiniButtonStyle(actionNum++,actionCount) ) )
					{
						// Interact
						QuestScriptEditor.Open( m_selectedRoom, QuestScriptEditor.eType.Hotspot,
							PowerQuest.SCRIPT_FUNCTION_INTERACT_HOTSPOT+ itemComponent.GetData().ScriptName,
							PowerQuestEditor.SCRIPT_PARAMS_INTERACT_HOTSPOT);
					}
					offset += fixedWidth;
				}

				if ( PowerQuestEditor.GetActionEnabled(eQuestVerb.Inventory) )
				{
					if ( GUI.Button(new Rect(offset, rect.y, fixedWidth, EditorGUIUtility.singleLineHeight), "Inv", QuestEditorUtils.GetMiniButtonStyle(actionNum++,actionCount) ) )
					{
						// UseItem
						QuestScriptEditor.Open( m_selectedRoom, QuestScriptEditor.eType.Hotspot,
							PowerQuest.SCRIPT_FUNCTION_USEINV_HOTSPOT+ itemComponent.GetData().ScriptName,
							PowerQuestEditor.SCRIPT_PARAMS_USEINV_HOTSPOT);
					}
					offset += fixedWidth;
				}
				
				/* Not sure if want this for hotspots/props yet
				fixedWidth = 22;
				if ( GUI.Button(new Rect(offset, rect.y, fixedWidth, EditorGUIUtility.singleLineHeight), "...", EditorStyles.miniButtonRight ) )
					QuestEditorUtils.LayoutQuestObjectContextMenu( eQuestObjectType.Hotspot, m_listHotspots, itemComponent.GetData().GetScriptName(), itemComponent.gameObject, rect, index,false );
				offset += fixedWidth;
				*/


			}
		}
	}

	void LayoutPropGUI(Rect rect, int index, bool isActive, bool isFocused)
	{
		if ( m_selectedRoom != null && m_selectedRoom.GetPropComponents().IsIndexValid(index))
		{             

			PropComponent itemComponent = m_selectedRoom.GetPropComponents()[index];
			bool hasCollider = itemComponent.GetComponent<Collider2D>() != null;
			if ( itemComponent != null && itemComponent.GetData() != null )
			{         
				QuestEditorUtils.LayoutQuestObjectContextMenu( eQuestObjectType.Prop, m_listProps, itemComponent.GetData().GetScriptName(), itemComponent.gameObject, rect, index, true );

				int actionCount = (PowerQuestEditor.GetActionEnabled(eQuestVerb.Look)?1:0)
					+ (PowerQuestEditor.GetActionEnabled(eQuestVerb.Use)?1:0)
					+ (PowerQuestEditor.GetActionEnabled(eQuestVerb.Inventory)?1:0);
				float totalFixedWidth = (34 *actionCount);
				float offset = rect.x;
				float fixedWidth = 34;
				int actionNum = 0;
				EditorGUI.LabelField(new Rect(rect.x, rect.y+2, rect.width-totalFixedWidth, EditorGUIUtility.singleLineHeight), itemComponent.GetData().ScriptName);

				if ( hasCollider )
				{
					offset += rect.width - totalFixedWidth;
					if ( PowerQuestEditor.GetActionEnabled(eQuestVerb.Look) )
					{
						if ( GUI.Button(new Rect(offset, rect.y, fixedWidth, EditorGUIUtility.singleLineHeight), "Look", QuestEditorUtils.GetMiniButtonStyle(actionNum++,actionCount) ) )
						{
							// Lookat
							QuestScriptEditor.Open( 
								m_selectedRoom, QuestScriptEditor.eType.Prop,
								PowerQuest.SCRIPT_FUNCTION_LOOKAT_PROP+ itemComponent.GetData().ScriptName,
								PowerQuestEditor.SCRIPT_PARAMS_LOOKAT_PROP);
						}
					
						offset += fixedWidth;
					}
					if ( PowerQuestEditor.GetActionEnabled(eQuestVerb.Use) )
					{
						if ( GUI.Button(new Rect(offset, rect.y, fixedWidth, EditorGUIUtility.singleLineHeight), "Use", QuestEditorUtils.GetMiniButtonStyle(actionNum++,actionCount) ) )
						{
							// Interact
							QuestScriptEditor.Open( 
								m_selectedRoom, QuestScriptEditor.eType.Prop,
								PowerQuest.SCRIPT_FUNCTION_INTERACT_PROP+ itemComponent.GetData().ScriptName,
								PowerQuestEditor.SCRIPT_PARAMS_INTERACT_PROP);
						}
						offset += fixedWidth;
					}
					if ( PowerQuestEditor.GetActionEnabled(eQuestVerb.Inventory) )
					{
						if ( GUI.Button(new Rect(offset, rect.y, fixedWidth, EditorGUIUtility.singleLineHeight), "Inv", QuestEditorUtils.GetMiniButtonStyle(actionNum++,actionCount) ) )
						{
							// UseItem
							QuestScriptEditor.Open( 
								m_selectedRoom, QuestScriptEditor.eType.Prop,
								PowerQuest.SCRIPT_FUNCTION_USEINV_PROP+ itemComponent.GetData().ScriptName,
								PowerQuestEditor.SCRIPT_PARAMS_USEINV_PROP);
						}
						offset += fixedWidth;
					}
				}
			}
		}
	}

	void LayoutRegionGUI(Rect rect, int index, bool isActive, bool isFocused)
	{
		if ( m_selectedRoom != null && m_selectedRoom.GetRegionComponents().IsIndexValid(index))
		{   
			RegionComponent itemComponent = m_selectedRoom.GetRegionComponents()[index];
			
			if ( itemComponent != null && itemComponent.GetData() != null )
			{
				QuestEditorUtils.LayoutQuestObjectContextMenu( eQuestObjectType.Hotspot, m_listHotspots, itemComponent.GetData().GetScriptName(), itemComponent.gameObject, rect, index, true );

				int actionCount = 2;
				float fixedWidth = 45;
				float totalFixedWidth = (fixedWidth *actionCount);
				float offset = rect.x;
				EditorGUI.LabelField(new Rect(rect.x, rect.y+2, rect.width-totalFixedWidth, EditorGUIUtility.singleLineHeight), itemComponent.GetData().ScriptName);

				offset += rect.width - totalFixedWidth;
				int actionNum = 0;

				if ( GUI.Button(new Rect(offset, rect.y, fixedWidth, EditorGUIUtility.singleLineHeight), "Enter", QuestEditorUtils.GetMiniButtonStyle(actionNum++,actionCount) ) )
				{
					// Lookat
					QuestScriptEditor.Open( m_selectedRoom, QuestScriptEditor.eType.Region,
						PowerQuest.SCRIPT_FUNCTION_ENTER_REGION+ itemComponent.GetData().ScriptName,
						PowerQuestEditor.SCRIPT_PARAMS_ENTER_REGION);
				}
				offset += fixedWidth;


				if ( GUI.Button(new Rect(offset, rect.y, fixedWidth, EditorGUIUtility.singleLineHeight), "Exit", QuestEditorUtils.GetMiniButtonStyle(actionNum++,actionCount) ) )
				{
					// Interact
					QuestScriptEditor.Open( m_selectedRoom, QuestScriptEditor.eType.Region,
						PowerQuest.SCRIPT_FUNCTION_EXIT_REGION+ itemComponent.GetData().ScriptName,
						PowerQuestEditor.SCRIPT_PARAMS_EXIT_REGION);
				}
				offset += fixedWidth;


			}
		}
	}

	void LayoutRoomPointGUI(Rect rect, int index, bool isActive, bool isFocused)
	{
		if ( m_selectedRoom != null && m_selectedRoom.GetData().GetPoints().IsIndexValid(index))
		{
			Room.RoomPoint point = m_selectedRoom.GetData().GetPoints()[index];
			if ( point != null )
			{
				Undo.RecordObject(m_selectedRoom,"Point Changed");
				EditorGUI.BeginChangeCheck();
				float totalFixedWidth = 50+50;
				float offset = rect.x;
				if ( index == m_selectedRoomPoint )
					point.m_name = EditorGUI.TextField(new Rect(rect.x, rect.y, rect.width-totalFixedWidth, EditorGUIUtility.singleLineHeight), point.m_name);
				else 
					EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width-totalFixedWidth, EditorGUIUtility.singleLineHeight), point.m_name);
				
				float x = point.m_position.x;
				float y = point.m_position.y;
				offset += rect.width - totalFixedWidth;

				//position.m_name = EditorGUI.DelayedTextField(new Rect(rect.x, rect.y, rect.width-totalFixedWidth, EditorGUIUtility.singleLineHeight), position.m_name);
				float[] xy = new float[] {x,y};
				GUIContent[] xyLbl = new GUIContent[] {new GUIContent("x"),new GUIContent("y")};
				offset += rect.width - totalFixedWidth;

				EditorGUI.MultiFloatField(new Rect(rect.x+rect.width-totalFixedWidth, rect.y, totalFixedWidth, EditorGUIUtility.singleLineHeight),xyLbl,xy);
				if ( EditorGUI.EndChangeCheck() )
				{
					point.m_position = Utils.Snap(new Vector2(xy[0],xy[1]),PowerQuestEditor.SnapAmount);
					EditorUtility.SetDirty(m_selectedRoom);
					SceneView.RepaintAll();
				}
			}
		}
	}

	//public static readonly GUIStyle TOOLBAR_TOGGLE = new GUIStyle(EditorStyles.toggle) { font = EditorStyles.miniLabel.font, fontSize = EditorStyles.miniLabel.fontSize, padding = new RectOffset(15,0,3,0) };
	void LayoutWalkableAreaGUI(Rect rect, int index, bool isActive, bool isFocused)
	{
		if ( m_selectedRoom != null && m_selectedRoom.GetWalkableAreas().IsIndexValid(index))
		{

			PolygonCollider2D itemComponent = m_selectedRoom.GetWalkableAreas()[index].PolygonCollider;
			if ( itemComponent != null )
			{               
				float fixedWidth = 130;
				float totalFixedWidth = fixedWidth*1;
				float offset = rect.x;
				EditorGUI.LabelField(new Rect(rect.x, rect.y+2, rect.width-totalFixedWidth, EditorGUIUtility.singleLineHeight), "Id: "+index.ToString());

				offset += rect.width - totalFixedWidth;
				bool wasEditingPoly = m_walkableAreaEditingId == index && m_walkableAreaEditor != null;

				#if UNITY_2019_3_OR_NEWER
					if ( Selection.activeGameObject != itemComponent.gameObject )
						return;
				#endif

				bool isEditingPoly = GUI.Toggle(new Rect(offset, rect.y, fixedWidth, EditorGUIUtility.singleLineHeight), wasEditingPoly, "Edit Polygon", EditorStyles.miniButton);
				
				if ( wasEditingPoly == true && isEditingPoly == false )
				{
					//if ( GUI.Button(new Rect(offset, rect.y, fixedWidth, EditorGUIUtility.singleLineHeight), "Hide Polygon Editor", EditorStyles.miniButton) )
					//if ( GUI.Toggle(new Rect(offset, rect.y, fixedWidth, EditorGUIUtility.singleLineHeight), true, "Edit Polygon", EditorStyles.miniButton) == false )					
					/*QuestEditorUtils.HidePolygonEditor();
					if ( m_walkableAreaEditor != null ) 
						Editor.DestroyImmediate(m_walkableAreaEditor);
					m_walkableAreaEditingId = -1;	*/
					UnselectSceneTools();
				}
				else if ( wasEditingPoly == false && isEditingPoly == true)
				{
					Selection.activeGameObject = null;
					UnselectSceneTools();
					
					//m_selectedRoom.SetActiveWalkableArea(index); // NB: This doesn't change the active walkable area in the prefab,  just the active polygon in the pathfinder. But not sure why I do this tbh
					m_listWalkableAreas.index = index;

					m_walkableAreaEditor = Editor.CreateEditor(itemComponent);
					m_walkableAreaEditingId = index;

					QuestEditorUtils.ShowPolygonEditor(itemComponent);

					/* Removed, now we're auto-editing polys
					// Scroll down to show editor
					m_scrollPosition = new Vector2(0,100000);
					*/

					EditorUtility.SetDirty(m_powerQuest);
				}

			}
		}
	}

	// Character on room panel
	void LayoutRoomCharacterGUI(Rect rect, int index, bool isActive, bool isFocused)
	{
		if ( m_powerQuest != null && m_powerQuest.GetCharacterPrefabs().IsIndexValid(index))
		{
			CharacterComponent itemComponent = m_powerQuest.GetCharacterPrefabs()[index];
			if ( itemComponent != null && itemComponent.GetData() != null )
			{
				int actionCount = (PowerQuestEditor.GetActionEnabled(eQuestVerb.Look)?1:0)
					+ (PowerQuestEditor.GetActionEnabled(eQuestVerb.Use)?1:0)
					+ (PowerQuestEditor.GetActionEnabled(eQuestVerb.Inventory)?1:0);
				float totalFixedWidth = /*60+*/(34 *actionCount);
				float offset = rect.x;
				EditorGUI.LabelField(new Rect(rect.x, rect.y+2, rect.width-totalFixedWidth, EditorGUIUtility.singleLineHeight), itemComponent.GetData().GetScriptName(), (IsHighlighted(itemComponent)?EditorStyles.whiteLabel:EditorStyles.label) );
				offset += rect.width - totalFixedWidth;
				float fixedWidth = 60;
				// Don't show script button
				/*if ( GUI.Button(new Rect(offset, rect.y, fixedWidth, EditorGUIUtility.singleLineHeight), "Script", EditorStyles.miniButtonLeft ) )
				{
					// Open the script
					QuestScriptEditor.Open( itemComponent );		
				}
				offset += fixedWidth;
				*/
				fixedWidth = 34;
				//GUIStyle nextStyle = EditorStyles.miniButtonLeft;
				int actionNum = 0; // start at one since there's already a left item
				if ( PowerQuestEditor.GetActionEnabled(eQuestVerb.Look) )
				{
					if ( GUI.Button(new Rect(offset, rect.y, fixedWidth, EditorGUIUtility.singleLineHeight), "Look", QuestEditorUtils.GetMiniButtonStyle(actionNum++,actionCount+1) ) )
					{
						// Lookat
						QuestScriptEditor.Open( m_selectedRoom, PowerQuest.SCRIPT_FUNCTION_LOOKAT_CHARACTER+ itemComponent.GetData().ScriptName, PowerQuestEditor.SCRIPT_PARAMS_LOOKAT_ROOM_CHARACTER);
					}
					offset += fixedWidth;
					actionNum++;
				}
				if ( PowerQuestEditor.GetActionEnabled(eQuestVerb.Use) )
				{
					if ( GUI.Button(new Rect(offset, rect.y, fixedWidth, EditorGUIUtility.singleLineHeight), "Use", QuestEditorUtils.GetMiniButtonStyle(actionNum++,actionCount+1) ) )
					{
						// Interact
						QuestScriptEditor.Open( m_selectedRoom, PowerQuest.SCRIPT_FUNCTION_INTERACT_CHARACTER+ itemComponent.GetData().ScriptName, PowerQuestEditor.SCRIPT_PARAMS_INTERACT_ROOM_CHARACTER);
					}
					offset += fixedWidth;
					actionNum++;
				}
				if ( PowerQuestEditor.GetActionEnabled(eQuestVerb.Inventory) )
				{
					if ( PowerQuestEditor.GetActionEnabled(eQuestVerb.Inventory) && GUI.Button(new Rect(offset, rect.y, fixedWidth, EditorGUIUtility.singleLineHeight), "Inv", QuestEditorUtils.GetMiniButtonStyle(actionNum++,actionCount+1) ) )
					{
						// UseItem
						QuestScriptEditor.Open( m_selectedRoom, PowerQuest.SCRIPT_FUNCTION_USEINV_CHARACTER+ itemComponent.GetData().ScriptName, PowerQuestEditor.SCRIPT_PARAMS_USEINV_ROOM_CHARACTER);
					}
					offset += fixedWidth;
					actionNum++;
				}
			}
		}
	}

	#endregion 
	#region Funcs: Layout Scene

	bool m_editingPointMouseDown = false;

	void OnSceneRoom(SceneView sceneView)
	{
		// Repaint if mouse moved in scene view
		if ( Event.current != null && Event.current.isMouse )
		{
			if ( m_selectedTab == 1 )
				PowerQuestEditor.Get.Repaint();
		}

		// Update walkable area editor
		if ( m_walkableAreaEditor != null && m_walkableAreaEditor.target != null )
		{
			// if ( m_listWalkableAreas != null && m_selectedRoom.GetWalkableAreas().IsIndexValid(m_listWalkableAreas.index)) // could move this so visible whenevern walkable areas are selected
			{
				
				Handles.color = Color.yellow;
				Vector2[] walkablePoints = m_selectedRoom.GetWalkableAreas()[m_listWalkableAreas.index].Points;
				Vector2 offset = m_selectedRoom.GetWalkableAreas()[m_listWalkableAreas.index].PolygonCollider.offset;
				Handles.DrawAAPolyLine(4f,System.Array.ConvertAll<Vector2,Vector3>(walkablePoints, item=>item+offset));	
				if (  walkablePoints.Length > 2 )
					Handles.DrawAAPolyLine(4f,walkablePoints[walkablePoints.Length-1]+offset, walkablePoints[0]+offset);	
			}
			
			
			//GUILayout.Label("Walkable Area "+m_walkableAreaEditingId.ToString()+":", EditorStyles.boldLabel);
			MethodInfo methodInfo = m_walkableAreaEditor.GetType().GetMethod("OnSceneGUI"); //.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
			if ( methodInfo != null )
			{
				methodInfo.Invoke(m_walkableAreaEditor,null);

				
				if ( UnityEditorInternal.EditMode.editMode != EditMode.SceneViewEditMode.Collider )
					QuestEditorUtils.ShowPolygonEditor(m_walkableAreaEditor.target as PolygonCollider2D);

			}
		}

		// Update Room Points
		if ( m_selectedRoom != null && m_selectedTab == 1 )
		{
			for ( int i = 0; i < m_selectedRoom.GetData().GetPoints().Count; ++i )
			{
				Room.RoomPoint point = m_selectedRoom.GetData().GetPoints()[i];

				Vector3 position = point.m_position.WithZ(0);
				if ( m_selectedRoomPoint == i )
				{						
					Vector2 newPos = Utils.Snap(Handles.PositionHandle( position, Quaternion.identity),PowerQuestEditor.SnapAmount );//,2.0f,new Vector3(0,1,0),Handles.DotHandleCap));										
					if ( point.m_position != newPos )
					{
						Undo.RecordObject(m_selectedRoom,"Point moved");
						point.m_position = newPos;
						Repaint();
					}

					if ( Event.current != null && Event.current.type == EventType.MouseDown && Event.current.button == 0  )
					{
						// Unselect when used
						Event.current.Use();
						m_selectedRoomPoint = -1;
					}
					else if ( m_editingPointMouseDown )
					{
						Selection.activeObject = null;
						if ( Event.current != null && Event.current.type == EventType.MouseLeaveWindow )
							m_editingPointMouseDown = false;						
					}
					if ( Selection.activeObject != null )
						m_selectedRoomPoint = -1;
						

				}
				else 
				{
					Handles.color = Color.yellow;
					Handles.DrawLine( position + (Vector3.left * 2), position + (Vector3.right * 2) );
					Handles.DrawLine( position + (Vector3.up * 2), position + (Vector3.down * 2) );

					if ( Event.current != null && Event.current.type == EventType.MouseDown && Event.current.button == 0 )
					{
						if ( (m_mousePos - (Vector2)position).sqrMagnitude < 6 )
						{
							m_editingPointMouseDown = true;
							Selection.activeObject = null;
							UnselectSceneTools();
							Event.current.Use();
							m_selectedRoomPoint = i;
						}
						//Event.current.Use();
					}
				}
				GUI.color = Color.yellow;

				Handles.Label(position + new Vector3(1,0,0), point.m_name, new GUIStyle(EditorStyles.boldLabel) {normal = { textColor = Color.yellow } } );

			}
		}
	}

	#endregion	
	#region Funcs: Util

	void UpdatePlayFromFuncs()
	{
		// Generate debug funcs list from attribtues in script
		if ( m_selectedRoom == null )
			return;
		m_playFromFuncs.Clear();
		m_playFromFuncs.Add("None");
		System.Type type = System.Type.GetType( string.Format("{0}, {1}", m_selectedRoom.GetData().GetScriptClassName(),  typeof(PowerQuest).Assembly.FullName ));
		if (type == null)
			return;
		foreach( System.Reflection.MethodInfo method in type.GetMethods( BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) )
		{						
			if ( method.GetCustomAttributes(typeof(QuestPlayFromFunctionAttribute),false).Length > 0 )
			{
				// Debug.Log( method.Name );		
				m_playFromFuncs.Add(method.Name);
			}
		}
	}

	#endregion
	
}

}
