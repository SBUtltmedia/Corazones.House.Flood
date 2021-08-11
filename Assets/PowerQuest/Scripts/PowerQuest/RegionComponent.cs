using UnityEngine;
using UnityEngine.Serialization;
using System.Collections;
using System.Collections.Generic;
using PowerTools;

namespace PowerTools.Quest
{

//
// Region Data and functions. Persistant between scenes, as opposed to RegionComponent which lives on a GameObject in a scene.
//
[System.Serializable] 
public partial class Region : IRegion, IQuestScriptable
{

	#region Region: Editor data
	//
	// Default values set in inspector
	//
	[Tooltip("Whether walking on region triggers events/tints characters, etc")]
	[FormerlySerializedAs("m_triggerEnabled")]
	[SerializeField] bool m_enabled = true;
	[Tooltip("Whether character can walk over region, if false they'll path around it")]
	[SerializeField] bool m_walkable = true;
	[Tooltip("Colour to tint the player when in this area")]
	[SerializeField] Color m_tint = new Color(1,1,1,0); // TODO: Player tint
	[Tooltip("Amount to scale the player while in the region (at the top)")]
	[SerializeField] float m_scaleTop = 1;
	[Tooltip("Amount to scale the player while in the region (at the bottom)")]
	[SerializeField] float m_scaleBottom = 1;
	[ReadOnly][SerializeField] string m_scriptName = "RegionNew";

	#endregion
	#region Region: Vars: private

	//
	// Private variables
	//
	RegionComponent m_instance = null;
	
	BitArray m_characterOnRegionMask = new BitArray(64);
	BitArray m_characterOnRegionMaskOld = new BitArray(64);

	#endregion
	#region Region: properties
	//
	//  Properties
	//
	public string ScriptName { get{ return m_scriptName;} }
	public MonoBehaviour Instance { get{ return m_instance; } }
	public Region Data {get {return this;} }
	public bool Enabled { get{ return m_enabled;} set{m_enabled = value;} }
	public bool Walkable 
	{ 
		get { return m_walkable; } 
		set 
		{
			m_walkable = value; 
			if ( m_instance )
			{
				m_instance.OnSetWalkable(m_walkable);
			}
		}
	}
	public Color Tint { get{ return m_tint;} set{m_tint = value;} }
	public float ScaleTop { get{ return m_scaleTop;}  set{ m_scaleTop = value; }  }
	public float ScaleBottom { get{ return m_scaleBottom;} set{ m_scaleBottom = value;}  }
	public bool GetCharacterOnRegion(ICharacter character) { return m_instance == null ? false : m_instance.GetCharacterOnRegion(character.Data); }
	public RegionComponent GetInstance() { return m_instance; }
	public void SetInstance(RegionComponent instance) 
	{ 
		m_instance = instance; 
		instance.SetData(this);
		instance.OnSetWalkable(m_walkable);
	}
	// Return room's script
	public QuestScript GetScript() { return (PowerQuest.Get.GetCurrentRoom() == null) ? null : PowerQuest.Get.GetCurrentRoom().GetScript(); } 

	//
	// Public Functions
	//
	#endregion
	#region Region: Functions: Public 

	public void EditorInitialise( string name )
	{
		m_scriptName = name;
	}
	public void EditorRename(string name)
	{
		m_scriptName = name;
	}

	public BitArray GetCharacterOnRegionMask() { return m_characterOnRegionMask; }
	public BitArray GetCharacterOnRegionMaskOld() { return m_characterOnRegionMaskOld; }

	#endregion
	#region Region Functions: Implementing IQuestScriptable

	// Doesn't use all functions
	public string GetScriptName() { return m_scriptName; }
	public string GetScriptClassName() { return "Region"+m_scriptName; }
	public void HotLoadScript(System.Reflection.Assembly assembly) { /*No-op*/ }
	
	#endregion
	#region Region: Functions: Private 

	//
	// Internal Functions
	//


	// Handles setting up defaults incase items have been added or removed since last loading a save file
	[System.Runtime.Serialization.OnDeserializing]
	void CopyDefaults( System.Runtime.Serialization.StreamingContext sc )
	{
		QuestUtils.InitWithDefaults(this);
	}
	#endregion
}

//
// The component on the region in scene
//
public class RegionComponent : MonoBehaviour 
{
	public enum eTriggerResult { None,Enter,Exit,Stay };

	[SerializeField] Region m_data = new Region();

	PolygonCollider2D m_polygonCollider = null;

	float m_minColliderY = 0;
	float m_maxColliderY = 0;

	public Region GetData() { return m_data; }
	public void SetData(Region data) { m_data = data; }

	// Add or remove obstacle from polynav
	public void OnSetWalkable( bool walkable )
	{
		if ( m_polygonCollider == null )
			m_polygonCollider = GetComponent<PolygonCollider2D>();
		if ( walkable )
		{
			PowerQuest.Get.Pathfinder.RemoveObstacle(m_polygonCollider);
		}
		else 
		{							
			PowerQuest.Get.Pathfinder.AddObstacle(m_polygonCollider);
		}
	}

	public PolygonCollider2D GetPolygonCollider() { return m_polygonCollider; }

	public bool GetCharacterOnRegion(Character character)
	{
		return m_data.GetCharacterOnRegionMask().Get( PowerQuest.Get.GetCharacterId(character) );
	}

	// checks if character is in zone. Doesn't check if character is in room, so check that before calling. Returns true if standing on region
	public bool UpdateCharactersOnRegion( int index, Vector2 characterPos )
	{
		if ( m_data.Enabled == false )	
			return false;

		//bool wasInside = m_characterOnRegionMask.Get(index);
		bool inside = ( m_polygonCollider != null && m_polygonCollider.OverlapPoint(characterPos) );
		m_data.GetCharacterOnRegionMask().Set(index,inside);

		return inside;
	}

	public float GetScaleAt(Vector2 position)
	{
		if ( GetData().ScaleTop == 1 && GetData().ScaleBottom == 1 )
			return 1; // Scaling off
		return Mathf.Lerp( GetData().ScaleBottom, GetData().ScaleTop, Mathf.InverseLerp(m_minColliderY, m_maxColliderY, position.y ));
	}

	// Updates and returns whether the character entered/exited/stayed in the region. this is used in non-blocking situations
	public eTriggerResult UpdateCharacterOnRegionState( int index )
	{
		eTriggerResult result = eTriggerResult.None;

		bool inside = m_data.GetCharacterOnRegionMask().Get(index);
		bool wasInside = m_data.GetCharacterOnRegionMaskOld().Get(index);

		if ( wasInside )
		{
			result = inside ? eTriggerResult.Stay : eTriggerResult.Exit;
		}
		else if ( inside )
		{
			result = eTriggerResult.Enter;
		}		 

		// update cached mask
		m_data.GetCharacterOnRegionMaskOld().Set(index, m_data.GetCharacterOnRegionMask().Get(index));
		return result;
	}

	public void OnRoomLoaded()
	{
		// There's no copy value kinda thing, so i'm doing this to make a copy efficiently
		m_data.GetCharacterOnRegionMaskOld().SetAll(true);
		m_data.GetCharacterOnRegionMaskOld().And( m_data.GetCharacterOnRegionMask() );
	}


	// Use this for initialization
	void Start() 
	{
		if ( m_polygonCollider == null )
			m_polygonCollider = GetComponent<PolygonCollider2D>();
		m_data.GetCharacterOnRegionMask().Length = PowerQuest.Get.GetCharacters().Count; // kind of hacky way to get this info :/
		m_data.GetCharacterOnRegionMaskOld().Length = m_data.GetCharacterOnRegionMask().Length;

		// Find min and max points of collider
		if ( m_polygonCollider != null )
		{
			m_minColliderY = float.MaxValue;
			m_maxColliderY = float.MinValue;
			System.Array.ForEach( m_polygonCollider.points, item=>
				{
					if ( item.y < m_minColliderY )
						m_minColliderY = item.y;
					if ( item.y > m_maxColliderY )
						m_maxColliderY = item.y;					
				} );
		}
	}

	// Called once room and everything in it has been created and PowerQuest has initialised references. After Start, Before OnEnterRoom.
	public void OnLoadComplete()
	{
		OnSetWalkable(GetData().Walkable);

	}
	/*
	// Update is called once per frame
	void Update () 
	{

	}
	*/
}

}
