using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using PowerTools;

namespace PowerTools.Quest
{

//
// Room Data and functions. Persistant between scenes, as opposed to RoomComponent which lives on a GameObject in a scene.
//
[System.Serializable]
public partial class Room : IQuestScriptable, IRoom
{
	
	#region Definitions

	[System.Serializable]
	public class RoomPoint
	{
		public string m_name = "Point";
		public Vector2 m_position = Vector2.zero;

		public static implicit operator Vector2(RoomPoint self) { return self.m_position; }
	}
    
	#endregion
	#region Vars: Editor defaults

	[Tooltip("Currently not used")]
	[HideInInspector,SerializeField] string m_description = "New Room";		// Name displayed in descriptions
	[Tooltip("When false, the player is hidden and can't walk")]
	[SerializeField] bool m_playerVisible = true;
	[Tooltip("The vertical resolution of this room. Set Non-zero to override the default set in PowerQuest. (How many pixels high the camera view should be)")]
	[SerializeField] float m_verticalResolution = 0;
	[Tooltip("The walkable area that's currently enabled")]
	[SerializeField] int m_activeWalkableArea = 0;	
	[Tooltip("Defines the bounds of the room, the camera will not go outside these bounds")]
	[SerializeField] RectCentered m_bounds = new RectCentered(0,0,0,0);
	[Tooltip("Defines the area in which the camera will track the player (0 to disable)")]
	[SerializeField] RectCentered m_scrollBounds = new RectCentered(0,0,0,0);
	[ReadOnly][SerializeField] string m_shortName = "New";			// Name used in scripts
	[ReadOnly][SerializeField] string m_scriptClass = "RoomNew";	// Name of script class
	[ReadOnly][SerializeField] string m_sceneName = "SceneNew";		// Name of scene
	[ReadOnly][SerializeField] List<RoomPoint> m_points = new List<RoomPoint>(); // 2d Points accessable by script

	#endregion
	#region Vars: private

	List<Hotspot> m_hotspots = new List<Hotspot>();
	List<Prop> m_props = new List<Prop>();
	List<Region> m_regions = new List<Region>();

	RoomComponent m_instance = null;
	QuestScript m_script = null;
	GameObject m_prefab = null;
	int m_timesVisited = 0;	// How many times the room has been visited

	//
	//  Properties
	//
	#endregion
	#region Properties

	public RoomComponent Instance { get{ return m_instance; } }
	public string Description { get { return m_description; } }
	public string ScriptName { get{ return m_shortName;} }
	public void EnterBG() { PowerQuest.Get.ChangeRoomBG(this); }
	public Coroutine Enter() { return PowerQuest.Get.ChangeRoom(this); }
	public bool Active 
	{ 
		get
		{
			return PowerQuest.Get.GetCurrentRoom() == this;
		}
		set
		{
			if ( value )
				PowerQuest.Get.ChangeRoomBG(this);
			else 
				Debug.LogError("Can't set Room.Active to false, move to another room instead");
		}
	}
	public bool Current { get { return Active; } set { Active = value; } }
	public bool Visited { get{ return m_timesVisited > 0; } }
	public bool FirstTimeVisited { get{ return m_timesVisited == 1; } }
	public int TimesVisited { get{ return m_timesVisited; } }
	public RectCentered Bounds { get { return m_bounds; } set { m_bounds=value; } }
	public RectCentered ScrollBounds { get { return m_scrollBounds; } set {m_scrollBounds=value; } }
	/// Set the Visited property, for debugging.
	public void DebugSetVisited(int times) {m_timesVisited=times;}
    public int ActiveWalkableArea 
    { 
        get { return m_activeWalkableArea; } 
        set 
        { 
            if ( m_activeWalkableArea != value )
            {
                m_activeWalkableArea = value; 
                if ( m_instance != null)
                {                
                    m_instance.SetActiveWalkableArea(m_activeWalkableArea);
                }
            }
        } 
    }
    public bool PlayerVisible
	{
        get { return m_playerVisible; } 
        set 
        { 
			if ( m_playerVisible != value )
            {
				m_playerVisible = value; 
				// TODO: Update visibility of player
            }
        } 
    }
	public float VerticalResolution
	{
		get { return m_verticalResolution; }
		set { m_verticalResolution = value; }
	}
	public float Zoom
	{
		get { return ( m_verticalResolution > 0 ) ? PowerQuest.Get.DefaultVerticalResolution/m_verticalResolution : 1; }
		set { m_verticalResolution = PowerQuest.Get.DefaultVerticalResolution * value; }			
	}

	//
	// Getters/Setters - These are used by the engine. Scripts mainly use the properties
	//

	#endregion
	#region Funcs: Public

	public GameObject GetPrefab() { return m_prefab; }
	public string GetScriptName(){ return m_shortName; }
	public string GetScriptClassName(){ return m_scriptClass; }
	public QuestScript GetScript() { return m_script; }
	public IQuestScriptable GetScriptable() { return this; }
	public T GetScript<T>() where T : RoomScript<T> {  return ( m_script != null ) ? m_script as T : null; }
	public void HotLoadScript(Assembly assembly) { QuestUtils.HotSwapScript( ref m_script, m_scriptClass, assembly ); }

	public RoomComponent GetInstance() { return m_instance; }
	public void SetInstance(RoomComponent roomInstance)
	{ 
		m_instance = roomInstance; 
		m_instance.SetData(this);
		m_timesVisited++;

		//
		// Set the instances for child props/hotspots -  Find the data that belongs to each prop/hotspot instance in the room instance, and link it up.
		//
		HotspotComponent[] hotspotInstances = m_instance.GetComponentsInChildren<HotspotComponent>(true);
		foreach ( HotspotComponent hotspotInstance in hotspotInstances )
		{
			Hotspot hotspotData = m_hotspots.Find(item=>item.ScriptName == hotspotInstance.GetData().ScriptName);
			hotspotData.SetInstance(hotspotInstance);	// Set the instance in the current (non-prefab/non-default) data
		}

		PropComponent[] propInstances = m_instance.GetComponentsInChildren<PropComponent>(true);
		foreach ( PropComponent propInstance in propInstances )
		{
			Prop propData = m_props.Find(item=>item.ScriptName == propInstance.GetData().ScriptName);
			propData.SetInstance(propInstance);	// Set the instance in the current (non-prefab/non-default) data
		}

		RegionComponent[] regionInstances = m_instance.GetComponentsInChildren<RegionComponent>(true);
		foreach ( RegionComponent regionInstance in regionInstances )
		{
			Region regionData = m_regions.Find(item=>item.ScriptName == regionInstance.GetData().ScriptName);
			regionData.SetInstance(regionInstance);	// Set the instance in the current (non-prefab/non-default) data
		}

        // init walkable area
        m_instance.SetActiveWalkableArea(m_activeWalkableArea);
        
	}
	public Room Data { get {return this; } }
	public Hotspot GetHotspot(string name) 
	{ 
		
		Hotspot result = m_hotspots.Find(hotspot=>string.Equals(hotspot.ScriptName, name, System.StringComparison.OrdinalIgnoreCase ) );
		if ( result == null )
			Debug.LogError("Hotspot '"+name+"' doesn't exist in " +ScriptName);
		return result;
	} 

	public Prop GetProp(string name) 
	{ 
		Prop result = m_props.Find(prop=>string.Equals(prop.ScriptName, name, System.StringComparison.OrdinalIgnoreCase ));
		if ( result == null )
			Debug.LogError("Prop '"+name+"' doesn't exist in " +ScriptName);
		return result;
	} 
	public Region GetRegion(string name) 
	{ 

		Region result = m_regions.Find(region=>string.Equals(region.ScriptName, name, System.StringComparison.OrdinalIgnoreCase ) );
		if ( result == null )
			Debug.LogError("Region '"+name+"' doesn't exist in " +ScriptName);
		return result;
	} 
	public Vector2 GetPoint(string name) 
	{ 
		RoomPoint result = FindPoint(name);
		return result.m_position;
	}
	public void SetPoint(string name, Vector2 position) 
	{
		RoomPoint result = FindPoint(name);
		result.m_position = position;
	}
	/// Moves a named room position to the location of another named position
	public void SetPoint(string name, string fromPoint)
	{
		RoomPoint result = FindPoint(name);
		result.m_position = GetPoint(fromPoint);		
	}

	public void SetSize( RectCentered size ) { m_bounds = size; }
	public void SetScrollSize( RectCentered size ) { m_scrollBounds = size; }

	public string GetSceneName() { return m_sceneName; }

	public List<Hotspot> GetHotspots() { return m_hotspots; }
	public List<Prop> GetProps() { return m_props; }
	public List<RoomPoint> GetPoints() 
	{ 
		if ( m_points == null ) m_points = new List<RoomPoint>();
			return m_points; 
	}
	public List<Region> GetRegions() { return m_regions; }

	public static implicit operator string(Room room) { return room.m_shortName; }


	//
	// Initialisation
	//
	#endregion 
	#region Funcs: Initialisation

	public void EditorInitialise( string name  )
	{
		m_shortName = name;
		m_description = name;
		m_scriptClass = "Room"+name;
		m_sceneName = "SceneRoom"+name;
	}

	public void EditorRename(string name)
	{
		m_shortName = name;
		m_scriptClass = "Room"+name;
		m_sceneName = "SceneRoom"+name;		
	}

	public void OnPostRestore( int version, GameObject prefab )
	{
		m_prefab = prefab;
		if ( m_script == null ) // script could be null if it didn't exist in old save game, but does now.
			m_script = QuestUtils.ConstructByName<QuestScript>(m_scriptClass);

		//
		// We've read in hotspots and instances, but there might have been more added, or some removed, so grab the list from prefab then copy data across by name
		//

		// Hotspots - Find the loaded data by name - if it matches, copy data from the list of loaded hotspots, otherwise copy data from the prefab
		List<Hotspot> loadedHotspots = m_hotspots;
		m_hotspots = new List<Hotspot>();
		HotspotComponent[] hotspotPrefabs = prefab.GetComponentsInChildren<HotspotComponent>(true);
		foreach ( HotspotComponent prefabComponent in hotspotPrefabs )
		{
			
			Hotspot restoredData = loadedHotspots.Find(item=>item.ScriptName == prefabComponent.GetData().ScriptName);
			Hotspot data = new Hotspot();
			QuestUtils.CopyFields(data, restoredData != null ? restoredData : prefabComponent.GetData() );
			m_hotspots.Add(data);	
		}

		// Props - Find the loaded data by name - if it matches, copy data from the list of loaded hotspots, otherwise copy data from the prefab
		List<Prop> loadedProps = m_props;
		m_props = new List<Prop>();
		PropComponent[] propPrefabs = prefab.GetComponentsInChildren<PropComponent>(true);
		foreach ( PropComponent prefabComponent in propPrefabs )
		{
			
			Prop restoredData = loadedProps.Find(item=>item.ScriptName == prefabComponent.GetData().ScriptName);
			Prop data = new Prop();
			QuestUtils.CopyFields(data, restoredData != null ? restoredData : prefabComponent.GetData() );
			m_props.Add(data);	
		}

		// Regions - Find the loaded data by name - if it matches, copy data from the list of loaded regions, otherwise copy data from the prefab
		List<Region> loadedRegions = m_regions;
		m_regions = new List<Region>();
		RegionComponent[] regionPrefabs = prefab.GetComponentsInChildren<RegionComponent>(true);
		foreach ( RegionComponent prefabComponent in regionPrefabs )
		{

			Region restoredData = loadedRegions.Find(item=>item.ScriptName == prefabComponent.GetData().ScriptName);
			Region data = new Region();
			QuestUtils.CopyFields(data, restoredData != null ? restoredData : prefabComponent.GetData() );
			m_regions.Add(data);	
		}
	}

	//
	// Initialise Data classes ( rooms, characters, etc.
	//		- Copies the default data from their prefabs into permanent data that's kept between scenes.
	//		- Instantiates "Scripts" classes
	//
	public void Initialise( GameObject prefab )
	{
		m_prefab = prefab;

		// Construct the script
		m_script = QuestUtils.ConstructByName<QuestScript>(m_scriptClass);

		//
		// Copy data from hotspots
		//
		m_hotspots.Clear();
		HotspotComponent[] hotspotPrefabs = prefab.GetComponentsInChildren<HotspotComponent>(true);
		foreach ( HotspotComponent hotspotPrefab in hotspotPrefabs )
		{
			Hotspot data = new Hotspot();
			QuestUtils.CopyFields(data, hotspotPrefab.GetData());
			m_hotspots.Add(data);	
		}

		//
		// Copy data from props
		//
		m_props.Clear();
		PropComponent[] propPrefabs = prefab.GetComponentsInChildren<PropComponent>(true);
		foreach ( PropComponent propPrefab in propPrefabs)
		{
			Prop data = new Prop();
			QuestUtils.CopyFields(data, propPrefab.GetData());
			m_props.Add(data);
			data.Position = propPrefab.transform.position;
		}	

		//
		// Copy data from regions
		//
		m_regions.Clear();
		RegionComponent[] regionPrefabs = prefab.GetComponentsInChildren<RegionComponent>(true);
		foreach ( RegionComponent regionPrefab in regionPrefabs )
		{
			Region data = new Region();
			QuestUtils.CopyFields(data, regionPrefab.GetData());
			m_regions.Add(data);	
		}
	}

	#endregion
	#region Funcs: Internal

	RoomPoint FindPoint( string name )
	{
		RoomPoint result = m_points.Find(pos=>string.Equals(pos.m_name, name, System.StringComparison.OrdinalIgnoreCase ) );
		if ( result == null )
		{
			Debug.LogError("Position '"+name+"' doesn't exist in " +ScriptName);
			return new RoomPoint();
		}
		return result;
	}

	// Handles setting up defaults incase rooms have been added or removed since last loading a save file
	[System.Runtime.Serialization.OnDeserializing]
	void CopyDefaults( System.Runtime.Serialization.StreamingContext sc )
	{
		QuestUtils.InitWithDefaults(this);
	}

	#endregion

}

}
