using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using PowerTools;

namespace PowerTools.Quest
{

#region Class: Component
//
// The component on the inventory item gui in scene. Only used for data
//
[SelectionBase]
public class InventoryComponent : MonoBehaviour
{
	[SerializeField] Inventory m_data = new Inventory();

	public Inventory GetData() { return m_data; }
	public void SetData(Inventory data) { m_data = data; }
}

#endregion 
#region Class: Script

// Script class to inherit from for gui scripts
[System.Serializable]
public class InventoryScript<T> : QuestScript  where T : QuestScript
{
	/// Used internally to access one script from another
	public static T Script { get { return E.GetScript<T>(); } } 
}


#endregion 
#region Class: Inventory (Data)

//
// Inventory item Data and functions. Persistant between scenes, as opposed to InventoryItemComponent which just holds default data and isn't ever instantiated
//
[System.Serializable] 
public partial class Inventory : IQuestClickable, IQuestScriptable, IInventory
{

	//
	// Default values set in inspector
	//
	[TextArea]
	[SerializeField] string m_description = "New Item";
	[Tooltip("Sprite animation for inventory in GUI")]
	[SerializeField] string m_animGui = string.Empty;
	[Tooltip("Sprite animation for inventory cursor")]
	[SerializeField] string m_animCursor = string.Empty;
	[Tooltip("Sprite animation for inventory cursor when not hovering over clickable")]
	[SerializeField] string m_animCursorInactive = string.Empty;
	[Tooltip("When picking up multiple, do you get multiple in your inventory, or do they just stack up")]
	[SerializeField] bool m_stack = false;
	[ReadOnly][SerializeField] string m_scriptName = "InventoryNew";
	[ReadOnly][SerializeField] string m_scriptClass = "InventoryNew";

	//
	// Private variables
	//
	QuestScript m_script = null;
	GameObject m_prefab = null;
	bool m_everCollected = false;
	//InventoryComponent m_instance = null;

	//
	//  Properties
	//
	public eQuestClickableType ClickableType { get{ return eQuestClickableType.Inventory; } }
	public string Description { get{ return m_description; } set{ m_description = value; } }
	public string AnimGui { get{ return m_animGui; } set{m_animGui = value;} }
	public string AnimCursor { get{ return m_animCursor; } set{m_animCursor = value;} }
	public string AnimCursorInactive { get{ return m_animCursorInactive; } set{m_animCursorInactive = value;} }
	public bool Stack { get{ return m_stack; } set{ m_stack = value; } }
	public string ScriptName { get{ return m_scriptName;} }
	public Inventory Data { get { return this; } }

	//
	// Implementing IQuestClickable- Mostly n/a
	//	
	public Vector2 WalkToPoint { get{return Vector2.zero;} set{} }
	public Vector2 LookAtPoint  { get{return Vector2.zero;} set{} }
	public float Baseline  { get{ return 0;} set{} }
	public bool Clickable { get{return true;} set{} }
	public string Cursor { get{return null;} set{} } // NB: This is the cursor used when mouse is hovered over item in inventory panel
	public Vector2 Position { get{return Vector2.zero;} }
	public void OnInteraction( eQuestVerb verb ){}
	public void OnCancelInteraction( eQuestVerb verb ){}
	public MonoBehaviour Instance { get{ return null; } }

	//
	// Getters/Setters - These are used by the engine. Scripts mainly use the properties
	//

	public string GetAnimGui() { return m_animGui; }
	public string GetAnimCursor() { return m_animCursor; }

	public QuestScript GetScript() { return m_script; }
	public IQuestScriptable GetScriptable() { return this; }
	public T GetScript<T>() where T : InventoryScript<T> {  return ( m_script != null ) ? m_script as T : null; }
	public string GetScriptName(){ return m_scriptName; }
	public string GetScriptClassName() { return m_scriptClass; }
	public void HotLoadScript(Assembly assembly) { QuestUtils.HotSwapScript( ref m_script, m_scriptClass, assembly ); }
	public GameObject GetPrefab() { return m_prefab; }

	/* NB: this is maybe not used? /
	public InventoryComponent GetInstance() { return m_instance; }
	public void SetInstance(InventoryComponent instance) 
	{ 
		m_instance = instance; 
		m_instance.SetData(this);
	}
	/* */

	//
	// Public Functions
	//

	/// Gives the inventory item to the current player. Same as C.Player.AddInventory(item)
	public void Add( int quantity = 1 ) { PowerQuest.Get.GetPlayer().AddInventory(this, quantity); }
	/// Gives the inventory item to the current player and set's it as active inventory. Same as C.Player.AddInventory(item)
	public void AddAsActive( int quantity = 1 ) { Add(quantity); Active = true; }
	/// Removes the item from the current player. Same as C.Player.RemoveInventory(item)
	public void Remove( int quantity = 1 ) { PowerQuest.Get.GetPlayer().RemoveInventory(this,quantity); }
	/// Sets this item as the active item for the current player (ie: selected item to use on stuff)
	public void SetActive() { PowerQuest.Get.GetPlayer().ActiveInventory = this; }
	/// Gets or sets this item as teh active item for the current player
	public bool Active 
	{ 
		get 
		{ 
			return PowerQuest.Get.GetPlayer().ActiveInventory == this; 
		} 
		set 
		{ 
			if ( (PowerQuest.Get.GetPlayer().ActiveInventory == this) != value)
				PowerQuest.Get.GetPlayer().ActiveInventory = value ? this : null; 
		}
	}

	// Called from player addInventory so items can record when they've been collected
	public void OnCollected() { m_everCollected = true; }

	/// Whether the current player has the item in their inventory
	public bool Owned 
	{ 
		get { return PowerQuest.Get.GetPlayer().HasInventory(this); } 
		set
		{
			if ( value == true && Owned == false )
				Add();
			else if ( value == false && Owned == true )
				Remove( Mathf.RoundToInt(PowerQuest.Get.GetPlayer().GetInventoryItemCount()) );
		}
	}

	/// Whether the item  has ever been collected
	public bool EverCollected { get { return m_everCollected; } }

	//
	// Internal Functions
	//

	public static Inventory FromInterface(IInventory inv){ return inv as Inventory; }

	//
	// Initialisation
	//

	public void EditorInitialise( string name )
	{
		m_scriptName = name;
		m_scriptClass = "Inventory"+name;
		m_description = name;
		m_animGui = name;
		m_animCursor = name;
		m_animCursorInactive = name;
	}
	public void EditorRename(string name)
	{
		// Could rename anims, but leave manual for now
		//if ( m_animGui == m_scriptName )
		//	m_animGui = name;
		//if ( m_animCursor == m_scriptName )
		//	m_animCursor = name;
		//if ( m_animCursor == m_scriptName )
		//	m_animCursor = name;
		m_scriptName = name;
		m_scriptClass = "Inventory"+name;
	}


	public void OnPostRestore( int version, GameObject prefab )
	{
		m_prefab = prefab;
		if ( m_script == null ) // script could be null if it didn't exist in old save game, but does now.
			m_script = QuestUtils.ConstructByName<QuestScript>(m_scriptClass);
	}

	public void Initialise( GameObject prefab )
	{
		m_prefab = prefab;
		// Construct the script
		m_script = QuestUtils.ConstructByName<QuestScript>(m_scriptClass);
	}

	// Handles setting up defaults incase items have been added or removed since last loading a save file
	[System.Runtime.Serialization.OnDeserializing]
	void CopyDefaults( System.Runtime.Serialization.StreamingContext sc )
	{
		QuestUtils.InitWithDefaults(this);
	}
}

#endregion

}
