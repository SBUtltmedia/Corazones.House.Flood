using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PowerTools;

namespace PowerTools.Quest
{

public class GuiInventoryPanel : MonoBehaviour
{
	class GuiInventoryPanelItem 
	{
		public int m_inventoryItem = -1;
		public GameObject m_obj = null;
		public SpriteAnim m_sprite = null;
		public Button m_button = null;
		public Inventory m_itemData = null;
	};
	class GuiInventoryPanelItems : List<GuiInventoryPanelItem> {}

	//[SerializeField] Vector2 m_itemSize = Vector2.one;
	[SerializeField] GameObject m_itemPrefab = null;
	[SerializeField] Transform m_grid = null;
	[Tooltip("Name of character to show inventory of. If empty will use the current player")]
	[SerializeField] string m_targetCharacter = null;

	[Tooltip("How many items to show at a time")]
	[SerializeField] int m_maxVisibleItems = 64;
	[Tooltip("How many items to scroll when pressing left/right or up/down buttons")]
	[SerializeField] int m_scrollDistance = 1;
	
	// Scroll back (or up) button
	[SerializeField] GameObject m_btnScrollBack = null;
	// Scroll forward (or down) button
	[SerializeField] GameObject m_btnScrollForward = null;

	GuiInventoryPanelItems m_items = new GuiInventoryPanelItems();

	// holds the last collected item to check if the inventory needs to be scrolled.
	static Character.CollectedItem m_lastCollectedItem = null;

	static int m_itemOffset = 0; // Todo - allow scrolling of items obviously.	

	public void ScrollReset()
	{
		m_itemOffset = 0;
	}

	public void ScrollForward()
	{
		if ( PowerQuest.Get.GetBlocked() )
			return;
		if ( m_itemOffset+m_maxVisibleItems < GetCharacter().GetInventoryItemCount()  )
			m_itemOffset += m_scrollDistance;
	}

	public void ScrollBack() 
	{
		if ( PowerQuest.Get.GetBlocked() )
			return;
		m_itemOffset = Mathf.Max(0,m_itemOffset - m_scrollDistance);
	}

	Character GetCharacter() { return string.IsNullOrEmpty(m_targetCharacter)  ? PowerQuest.Get.GetPlayer() : PowerQuest.Get.GetCharacter(m_targetCharacter); }

	void UpdateButtons()
	{
		Character character = GetCharacter();
		List<Character.CollectedItem> inventory = character.GetInventory();

		if (inventory.Count > 0 && inventory[inventory.Count-1] != m_lastCollectedItem)
		{
			if (GetCharacter().GetInventoryItemCount() > m_maxVisibleItems)
				m_itemOffset = ((int)GetCharacter().GetInventoryItemCount() / m_maxVisibleItems) * m_scrollDistance;
			// take note of the last collected item
			m_lastCollectedItem = inventory[inventory.Count-1];
		}

		// add to end if not enough
		while ( m_items.Count < inventory.Count-m_itemOffset && m_items.Count < m_maxVisibleItems )
		{
			GuiInventoryPanelItem item = new GuiInventoryPanelItem();
			item.m_obj = GameObject.Instantiate(m_itemPrefab) as GameObject;
			if ( item.m_obj != null )
			{
				item.m_obj.transform.SetParent(m_grid,false);
				item.m_sprite = item.m_obj.GetComponentInChildren<SpriteAnim>();

				item.m_obj.AddComponent<InventoryComponent>();

				GuiComponent component = item.m_obj.GetComponent<GuiComponent>();
				component.GetData().Visible = true;
				component.GetData().Clickable = true;
			}
			m_items.Add(item);
		}

		// remove from end if too many
		while ( m_items.Count > 0 && m_items.Count > inventory.Count-m_itemOffset )
		{
			GameObject obj = m_items[m_items.Count-1].m_obj;
			if ( obj != null )
				GameObject.Destroy(obj);
			m_items.RemoveAt(m_items.Count-1);
		}

		// Check if it's playing the correct anim, and if not, play it.
		for ( int i = 0; i < m_items.Count; ++i )
		{
			GuiInventoryPanelItem guiItem = m_items[i];
			Inventory inventoryItem = PowerQuest.Get.GetInventory(inventory[i+m_itemOffset].m_name);
			if ( guiItem.m_itemData != inventoryItem )
			{
				guiItem.m_itemData = inventoryItem;				
				guiItem.m_obj.GetComponent<InventoryComponent>().SetData(inventoryItem);
			}
			if ( guiItem != null && guiItem.m_sprite != null && guiItem.m_sprite.ClipName != inventoryItem.AnimGui )
			{
				AnimationClip anim =  PowerQuest.Get.GetInventoryAnimation( inventoryItem.AnimGui );
				if ( anim == null )
				{
				    Debug.LogWarning("Couldn't find inventory anim "+inventoryItem.AnimGui);
					inventoryItem.AnimGui = guiItem.m_sprite.ClipName; // so don't keep getting repeat warnings
				}
				guiItem.m_sprite.Play(anim);
			}
			
			// possibly not necessary any more if we use the inventory component for this job
			guiItem.m_obj.GetComponent<GuiComponent>().GetData().Description = inventoryItem.Description;
		}

		if ( m_btnScrollBack != null )
			m_btnScrollBack.gameObject.SetActive( m_itemOffset > 0);

		if ( m_btnScrollForward != null )
			m_btnScrollForward.gameObject.SetActive( character.GetInventoryItemCount() > m_itemOffset+m_maxVisibleItems );

	}

	// Update is called once per frame
	void Start()
	{
		UpdateButtons();
	}

	void Update() 
	{
		UpdateButtons();
	}

	// Sent via message from each item- TODO: use new event system for this as god intended
	void MsgOnItemClick(PointerEventData eventData)
	{
		if ( PowerQuest.Get.GetBlocked() )
			return;

		Character player = string.IsNullOrEmpty(m_targetCharacter)  ? PowerQuest.Get.GetPlayer() : PowerQuest.Get.GetCharacter(m_targetCharacter);

		List<Character.CollectedItem> inventory = player.GetInventory();
		int invIndex = m_items.FindIndex(item=>item.m_obj == eventData.pointerPress);
		if ( inventory.IsIndexValid(invIndex) )
		{
			bool shouldUnpause = PowerQuest.Get.OnInventoryClick(inventory[invIndex].m_name, eventData.button);
			if ( shouldUnpause )
			{
				// Game needs to run a sequence, so hide the gui
				transform.parent.GetComponent<GuiDropDownBar>()?.Hide();
			}
		}

		//Debug.Log("ItemClick- "+eventData.pointerPress+" button: "+eventData.button.ToString());
		 
	}
}
}