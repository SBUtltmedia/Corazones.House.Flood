using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PowerTools.Quest;

public class GuiSimpleButton : MonoBehaviour
{
	[SerializeField] Sprite m_sprite = null;
	[SerializeField] Sprite m_spriteHover = null;
	[QuestLocalize]
	[SerializeField] string m_description = string.Empty;
	[Tooltip("When true, the button will still 'hover' even during a blocking script")]
	[SerializeField] bool m_ignoreScriptBlocking = false;

	[SerializeField] Color m_defaultColor = Color.white;
	[SerializeField] Color m_hoverColor = Color.white;


	RectTransform m_rectTrans = null;
	SpriteRenderer m_spriteRenderer = null;

	bool m_mouseOver = false;
	

	public string Description {get{return m_description;} set{m_description = value;}}   

	public bool IsMouseOver() { return m_mouseOver; }
	public void SetSprites(Sprite normalSprite, Sprite hoverSprite)
	{
		m_sprite = normalSprite;
		m_spriteHover = hoverSprite;
		Update();
	}

	void OnEnable()
	{
		Update();
	}


	// Update is called once per frame
	void Update()
	{
		if ( m_spriteRenderer == null )
			m_spriteRenderer = GetComponentInChildren<SpriteRenderer>();
		if ( m_rectTrans == null )
			m_rectTrans = GetComponent<RectTransform>();    
		if ( m_spriteRenderer == null || m_rectTrans == null || PowerQuest.Get == null )
			return; 
		if ( PowerQuest.Get.GetCameraGui() == null )
			return;
		Vector2 mousePos = PowerQuest.Get.GetCameraGui().ScreenToWorldPoint( Input.mousePosition.WithZ(0) );
		// NB: This requires buttons to be anchored bottom left!!
		Rect rect = new Rect(m_rectTrans.rect){ x = m_rectTrans.position.x, y = m_rectTrans.position.y };
		m_mouseOver = rect.Contains(mousePos) && (m_ignoreScriptBlocking || PowerQuest.Get.GetBlocked() == false);
		if ( m_spriteHover != null && m_sprite != null )
			m_spriteRenderer.sprite = m_mouseOver ? m_spriteHover : m_sprite;
		m_spriteRenderer.color = m_mouseOver ? m_hoverColor : m_defaultColor;
	}
}
