using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

namespace PowerTools.Quest
{


public class GuiInventoryItem : MonoBehaviour, IPointerClickHandler
{
	public void OnPointerClick(PointerEventData eventData)
	{		
		//Debug.Log("So this isn't happening anymore?"+eventData.button.ToString());
		SendMessageUpwards("MsgOnItemClick",eventData);
	}

	

}

}
