using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using PowerTools;

namespace PowerTools.Quest
{



#region Class: Quest Cursor 
[System.Serializable] 
public partial class QuestCursor : ICursor
{
	
	//
	// Default values set in inspector
	//

	#endregion
	#region QuestCursor Editor variables
	[SerializeField] bool m_visible = true;
	[SerializeField] string m_animationClickable = "Active";
	[SerializeField] string m_animationNonClickable = "Idle";
	[SerializeField] string m_animationUseInv = "UseInv";
	[SerializeField] string m_animationOverGui = "Idle";
	[SerializeField] string m_animationWait = "Wait";
	[SerializeField] Color m_inventoryOutlineColor = new Color(1,1,1,0);
	[Tooltip("If true, the cursor will be hidden when the game isn't interactive")]
	[SerializeField] bool m_hideWhenBlocking = true;


	#endregion
	#region QuestCursor private variables

	GameObject m_prefab = null;
	QuestCursorComponent m_instance = null;

	bool m_hasPositionOverride = false;
	Vector2 m_positionOverride = Vector2.zero;


	#endregion
	#region QuestCursor Public functions/properties

	public MonoBehaviour Instance { get{ return m_instance; } }
	public bool Visible 
	{ 
		get{ return m_visible;} 
		set
		{ 
			m_visible = value;
			if ( m_instance ) m_instance.SetVisible(m_visible && (m_hideWhenBlocking == false || PowerQuest.Get.GetBlocked() == false ));
		}
	}


	public string AnimationClickable 
	{ 
		get{ return m_animationClickable; } 
		set{ m_animationClickable = value; }
	}

	public string AnimationNonClickable 
	{ 
		get{ return m_animationNonClickable; } 
		set{ m_animationNonClickable = value; }
	}

	public string AnimationUseInv
	{ 
		get{ return m_animationUseInv; } 
		set{ m_animationUseInv = value; }
	}

	public string AnimationOverGui 
	{ 
		get{ return m_animationOverGui; } 
		set{ m_animationOverGui = value; }
	}
	public string AnimationWait
	{ 
		get{ return m_animationWait; } 
		set{ m_animationWait = value; }
	}

	public bool HideWhenBlocking { get {return m_hideWhenBlocking; } set{m_hideWhenBlocking = value;} }

	public Color InventoryOutlineColor
	{
		get{ return m_inventoryOutlineColor; }
		set{ m_inventoryOutlineColor = value; }
	}


	public GameObject GetPrefab() { return m_prefab; }
	public QuestCursorComponent GetInstance() { return m_instance; }
	public void SetInstance(QuestCursorComponent instance) 
	{ 
		m_instance = instance; 
		m_instance.SetData(this);
	}

	public bool NoneCursorActive { get { return m_instance.GetNoneCursorActive(); } }
	public bool InventoryCursorOverridden { get { return m_instance.GetInventoryCursorOverridden(); } }

	public Vector2 PositionOverride { get { return m_hasPositionOverride ? m_positionOverride : Vector2.zero; } set { SetPositionOverride(value); } }
	public bool HasPositionOverride { get { return m_hasPositionOverride; } }
	public void SetPositionOverride(Vector2 position) { m_hasPositionOverride = true; m_positionOverride = position; }
	public void ClearPositionOverride() { m_hasPositionOverride = false; }


	#endregion
	#region QuestCursor Initialisation functions

	public void EditorInitialise( string name ) {}
	public void EditorRename( string name ) {}

	public void OnPostRestore( int version, GameObject prefab )
	{
		m_prefab = prefab;
	}
	public void Initialise( GameObject prefab )
	{
		m_prefab = prefab;
	}



}

#endregion
#region Cursor Monobehaviour

public class QuestCursorComponent : MonoBehaviour 
{

	#endregion
	#region Component Editor variables

	[SerializeField] QuestCursor m_data = new QuestCursor();
	[Tooltip("List of animations that cursor will use even if an inv item is selected")]
	[SerializeField] List<string> m_inventoryOverrideAnims = new List<string>();
	[SerializeField, ReadOnly] List<AnimationClip> m_animations =  new List<AnimationClip>();

	#endregion
	#region Component Private Variables

	SpriteRenderer m_sprite = null;
	SpriteAnim m_spriteAnimator = null;
	bool m_noneCursor = false; // Flag that's set when "none" cursor is used
	bool m_inventoryOverride = false; // Flag that's set when inventoryOverride anim active
	PowerSprite m_powerSprite = null;

	#endregion
	#region Component Public functions

	public bool GetNoneCursorActive() { return m_noneCursor; }
	public bool GetInventoryCursorOverridden() { return m_inventoryOverride; }

	//bool m_overGUI = false;
	public QuestCursor GetData() { return m_data; }
	public void SetData(QuestCursor data) { m_data = data; }

	public void SetVisible(bool visible )
	{
		GetComponent<Renderer>().enabled = visible;
	}

	public List<AnimationClip> GetAnimations() { return m_animations; }

	// Finds the clip and outline colour the cursor should have for the specified clickable
	public void CalcCursorVisuals(IQuestClickable clickable, out string newAnim, out Color outlineColor)
	{
		newAnim = null;
		outlineColor = new Color(1,1,1,0);

		if ( PowerQuest.Get.GetBlocked() && PowerQuest.Get.GetCurrentDialog() == null )
		{
			newAnim = m_data.AnimationWait;
			return;
		}

		// If dialog is up, only show animation over-gui (should use the "modal" check for this really)
		if ( PowerQuest.Get.GetModalGuiActive() )
			newAnim = m_data.AnimationOverGui;
		// Work out which cursor to be showing

		Character player = PowerQuest.Get.GetPlayer();

		// Clickable
		if ( string.IsNullOrEmpty(newAnim) && clickable != null )
		{
			bool overInventoryOverrideCursor = m_inventoryOverrideAnims.Contains(clickable.Cursor); 

			// If over gui
			if ( clickable.ClickableType == eQuestClickableType.Gui )
			{
				Gui guiData = clickable as Gui;
				if ( guiData != null && guiData.AllowInventoryCursor && player.HasActiveInventory )
				{	
					// If gui allows invetnory cursor, use that			
					if ( string.IsNullOrEmpty(clickable.Description) && string.IsNullOrEmpty(player.ActiveInventory.AnimCursorInactive) == false )
						newAnim = player.ActiveInventory.AnimCursorInactive;
					else 
						newAnim = player.ActiveInventory.AnimCursor;					
				}

				// if there's a cursor set on the gui, use that
				if ( string.IsNullOrEmpty(newAnim) )
					newAnim = guiData.Cursor;

				if ( string.IsNullOrEmpty(newAnim) )
					newAnim = m_data.AnimationOverGui;
			}

			// If there's an inventory item selected use that cursor, otherwise the pointer
			if ( string.IsNullOrEmpty(newAnim) && player.HasActiveInventory && overInventoryOverrideCursor == false )
			{		
				if ( clickable.Cursor != "None" )
					outlineColor = m_data.InventoryOutlineColor;
				if ( string.IsNullOrEmpty(player.ActiveInventory.AnimCursor) == false )
					newAnim = player.ActiveInventory.AnimCursor;
				else  
					newAnim = m_data.AnimationUseInv;
			}

			// Find function override on clickable
			if ( string.IsNullOrEmpty(newAnim) )
			{
				string cursorFunc = PowerQuest.SCRIPT_FUNCTION_GETCURSOR;
				if ( clickable.ClickableType != eQuestClickableType.Character )
					cursorFunc += clickable.ClickableType.ToString() + clickable.ScriptName;
				string overrideFromFunction = GetCursorScriptOverride(clickable.GetScript(),  cursorFunc );
				if ( string.IsNullOrEmpty(overrideFromFunction) == false )
					newAnim = overrideFromFunction;
			}

			// If cursor anim is overriden
			if ( string.IsNullOrEmpty(newAnim) && string.IsNullOrEmpty(clickable.Cursor) == false )	
			{				
				if ( player.HasActiveInventory == false || overInventoryOverrideCursor )
					newAnim = clickable.Cursor;
			}

			// Clickable
			if ( string.IsNullOrEmpty(newAnim) )
			{
				newAnim = m_data.AnimationClickable;
			}


		}

		// Not clickable -  If ther'es an inventory item selected show that
		if ( string.IsNullOrEmpty(newAnim) && player.HasActiveInventory 
			&& PowerQuest.Get.Paused == false  ) // Temporary Hack for dialog system) 
		{
			// Fall back to pointer anim if there's no inventory one, as a default
			if ( string.IsNullOrEmpty(player.ActiveInventory.AnimCursorInactive) == false )
				newAnim = player.ActiveInventory.AnimCursorInactive;
			else if ( string.IsNullOrEmpty(player.ActiveInventory.AnimCursor) == false )
				newAnim = player.ActiveInventory.AnimCursor;
			else
				newAnim = m_data.AnimationUseInv;				
		}

		// not clickable, and no inventory
		if ( string.IsNullOrEmpty(newAnim) ) 
		{
			newAnim = m_data.AnimationNonClickable;
		}


		bool noneCursor = newAnim == "None" || (clickable != null && clickable.Cursor == "None");
		if ( noneCursor )
			outlineColor = new Color(1,1,1,0);

	}

	#endregion
	#region Component Private funcs

	void Awake () 
	{
		m_sprite = GetComponent<SpriteRenderer>();
		m_spriteAnimator = m_sprite.GetComponent<SpriteAnim>();	 

		m_powerSprite = m_sprite.GetComponent<PowerSprite>();
		//m_renderer = GetComponent<Renderer>();
	}

	// Use this for initialization
	void Start () 
	{		 
		m_spriteAnimator.Play( GetAnimations().Find( item=>string.Equals(m_data.AnimationClickable, item.name, System.StringComparison.OrdinalIgnoreCase) ) );
	}

	public AnimationClip FindCursorAnimation(string name)
	{
		AnimationClip clip = GetAnimations().Find( item=>string.Equals(name, item.name, System.StringComparison.OrdinalIgnoreCase) );

		if ( clip == null )
		{
			// HACK- try in the inventory animation library. Not sure how best to handle things being in different librarys. hmm. Could create map of every animation to it's collection like Inventory.Spanner, or CharacterJon.Walk
			clip = PowerQuest.Get.GetInventoryAnimation( name );
		}
		return clip;
	}

	void Update()
	{
		if ( m_data.HideWhenBlocking )
		{
			SetVisible((!PowerQuest.Get.GetBlocked()) && m_data.Visible);
		}

		transform.position = PowerQuest.Get.GetMousePosition();

		Camera cam = PowerQuest.Get.GetCameraGui();
		if ( cam != null )
		{				
			transform.position = cam.ScreenToWorldPoint( Input.mousePosition ).WithZ(0);
		}

		IQuestClickable clickable = PowerQuest.Get.GetMouseOverClickable();

		// Get the visuals
		string newAnim = null;
		Color outlineColor = new Color(1,1,1,0);
		CalcCursorVisuals(clickable, out newAnim, out outlineColor);

		// Play the anim if it's changed
		string currAnim = m_spriteAnimator.ClipName;
		if ( currAnim != newAnim )
		{
			AnimationClip clip = FindCursorAnimation(newAnim);
			m_spriteAnimator.Play(clip);
		}

		m_noneCursor = currAnim == "None" || (clickable != null && clickable.Cursor == "None");
		if ( m_noneCursor )
			outlineColor = new Color(1,1,1,0);
		m_inventoryOverride = m_inventoryOverrideAnims.Contains(currAnim);

		if ( m_powerSprite != null )
			m_powerSprite.Outline = outlineColor;
	}

	string GetCursorScriptOverride( QuestScript scriptClass, string methodName )
	{
		string result = null;
		if ( scriptClass != null )
		{
			System.Reflection.MethodInfo method = scriptClass.GetType().GetMethod( methodName );
			if ( method != null )
			{
				// Start sequence
				result = method.Invoke(scriptClass,null) as string;
			}
		}
		return result;
	}
	#endregion
}

}