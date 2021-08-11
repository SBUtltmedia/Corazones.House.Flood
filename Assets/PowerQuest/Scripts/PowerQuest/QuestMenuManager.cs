using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using PowerTools;

namespace PowerTools.Quest
{


/** Quest Menu Manager- This is where menu stuff was originally going to be managed. 
	At the moment it only does a couple of things:
	- Fading in and out (and not particularly well)
	- Using GUI baselines to control their sort order
	
	I imagine this will be refactored when I get to doing better gui/menu management
*/
[System.Serializable]
public class QuestMenuManager
{
	public static readonly string DEFAULT_FADE_SOURCE = "";

	[SerializeField] SpriteRenderer m_prefabEffectMenuFadeOut = null;
	[SerializeField] Color m_fadeColour = Color.black;
	[Tooltip("If using custom fade, poll GetFadeRatio() and do your own fading")]
	[SerializeField] bool m_customFade = false;

	// Callback for implementing custom fades
	public System.Action CallbackOnUpdateFade = null;

	// list of things causing a fade in or out. Call fadeIn/FadeOut with a different source if you don't want code somewhere else to conflict with your fading
	SourceList m_fadeSources = new SourceList();

	float m_fadeInTime = 0;
	float m_fadeOutTime = 0;
	float m_fadeAlpha = 0;
	SpriteRenderer m_sprite = null;
	Color m_fadeColourPrev = Color.black;

	public Color FadeColor { get { return m_fadeColour;} set{m_fadeColourPrev = m_fadeColour; m_fadeColour = value; UpdateFadeSprite(); } }
	public void FadeColorRestore() { m_fadeColour = m_fadeColourPrev; /* UpdateFadeSprite();*/ } 

	public bool GetFading() 
	{
		return (m_fadeSources.Empty() == false && m_fadeAlpha < 1) || (m_fadeSources.Empty()  && m_fadeAlpha > 0);
	}
	public float GetFadeRatio()	{ return m_fadeAlpha; }
	public Color GetFadeColor()	{ return m_fadeColour; }
	public bool GetCustomFade() { return m_customFade; }

	// Resets all fade data (game will be faded in). Used when restoring a game, etc.
	public void ResetFade()
	{
		m_fadeInTime = 0;
		m_fadeOutTime = 0;
		m_fadeAlpha = 0;
		m_fadeSources.Clear();
		// Coroutines should be stopped along with this function being called.
	}
	

	// Fade out (for main menu)
	//////////////////////////////////////////////////////////////////////

	public void FadeOut(float time) { FadeOut(time,DEFAULT_FADE_SOURCE); }
	public void FadeOut(float time, string source)
	{
		//Debug.Log("FadeOut: " + source+": "+time);
		m_fadeOutTime = time;
		m_fadeSources.Add(source);
		if ( m_fadeOutTime == 0 )
		{
			m_fadeAlpha = 1;
			UpdateFadeSprite();
		}
	}
	public void FadeSkip()
	{
		//Debug.Log("FadeSkip");
		m_fadeAlpha = ( m_fadeSources.Empty() ) ? 0 : 1;
		UpdateFadeSprite();
	}
	public void FadeIn(float time) { FadeIn(time,DEFAULT_FADE_SOURCE); }
	public void FadeIn(float time, string source)
	{
		//Debug.Log("FadeIn: " + source+": "+time);
		m_fadeInTime = time;
		m_fadeSources.Remove(source);
		if ( m_fadeInTime == 0 )
		{
			m_fadeAlpha = 0;
			UpdateFadeSprite();
		}
	}

		
	// Update is called once per frame
	public void Update() 
	{
		// Fade Out
		if ( m_fadeSources.Empty() == false && m_fadeAlpha < 1.0f )
		{
			m_fadeAlpha += Time.deltaTime / m_fadeOutTime;
			if ( m_fadeAlpha > 1.0f )
				m_fadeAlpha = 1.0f;
			UpdateFadeSprite();
		}

		// fade in
		if( m_fadeSources.Empty() && m_fadeAlpha > 0.0f )
		{
			m_fadeAlpha -= Time.deltaTime / m_fadeInTime;
			if ( m_fadeAlpha < 0.0f )
				m_fadeAlpha = 0.0f;
			UpdateFadeSprite();
		}


		// Update Gui sort order based on "baseline"
		List<Gui> guis = new List<Gui>(PowerQuest.Get.GetGuis());
		// Sort guis by baseline
		guis.Sort( (a,b)=>
			{ 
				int result = a.Baseline.CompareTo(b.Baseline);
				if ( result == 0 && a.Instance != null && b.Instance != null )
					result = a.Instance.GetInstanceID().CompareTo(b.Instance.GetInstanceID());
				return result;
			});
		int guiIndex = 0;
		foreach ( Gui gui in guis )
		{
			if ( gui.Instance != null )
				gui.Instance.transform.SetSiblingIndex(guiIndex++);			
		}

	}

	void UpdateFadeSprite()
	{
		if ( m_prefabEffectMenuFadeOut != null && m_sprite == null && m_customFade == false )
		{
			GameObject obj = GameObject.Instantiate(m_prefabEffectMenuFadeOut.gameObject);
			if ( obj != null )
			{
				m_sprite = obj.GetComponent<SpriteRenderer>();
				// If pixel cam's enabled, set to the sub-pixel layer so it renders on top of everything
				if ( PowerQuest.Get.GetPixelCamEnabled() )
					obj.layer = LayerMask.NameToLayer("HighRes");
			}
		}

		if ( m_sprite != null && m_customFade == false )
		{
			m_sprite.color = m_fadeColour.WithAlpha(m_fadeAlpha);
			m_sprite.gameObject.SetActive(m_fadeAlpha > 0.0f);
		}

		if ( m_customFade && CallbackOnUpdateFade != null )
			CallbackOnUpdateFade.Invoke();
			
	}



}

}
