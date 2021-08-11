using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PowerTools
{

[RequireComponent(typeof(SpriteRenderer))]
public class PowerSprite : MonoBehaviour 
{
	#region Definitions
	public static readonly Color COLOR_DISABLED = new Color(1,1,1,0);
	static readonly string STR_SHADER = "Sprites/PowerSprite";
	static readonly string STR_SHADER_OUTLINE= "Sprites/PowerSpriteOutline";
	static readonly string STR_SPROP_TINT = "_Tint";
	static readonly string STR_SPROP_OUTLINE = "_Outline";
	static readonly string STR_SPROP_OFFSET = "_Offset";

	#endregion
	#region vars: Editor

	[SerializeField] Color m_tint = COLOR_DISABLED;
	[SerializeField] Color m_outline = COLOR_DISABLED;
	[SerializeField] Vector2 m_offset = Vector2.zero;
	[SerializeField] Shader m_shaderOverride = null;

	#endregion
	#region vars: Private

	static Shader s_shader = null;
	static Shader s_shaderOutline = null;
	Material m_materialCached = null;
	bool m_outlineEnabled = false;	
	SpriteAnimNodes m_nodes = null;

	#endregion
	#region Funcs: Public

	public Color Tint 
	{ 
		get { return m_tint; } 
		set 
		{
			if ( m_tint != value )
			{
				m_tint = value;
				UpdateOutline();
				UpdateTint();
			}
		}
	}


	public Color Outline 
	{ 
		get { return m_outline; } 
		set 
		{
			if ( m_outline != value )
			{
				m_outline = value;
				UpdateOutline();
				UpdateTint();
			}
		}
	}

	public Vector2 Offset
	{ 
		get { return m_offset; } 
		set 
		{
			if ( m_offset != value )
			{
				m_offset = value;
				UpdateOffset();
			}
		}
	}


	public void AlignLeft()
	{
		CheckMaterial();
		SpriteRenderer renderer = GetComponent<SpriteRenderer>();
		m_offset.x = renderer.sprite.bounds.size.x*0.5f;
		UpdateOffset();
	}
	public void AlignRight()
	{
		CheckMaterial();
		SpriteRenderer renderer = GetComponent<SpriteRenderer>();
		m_offset.x = -renderer.sprite.bounds.size.x*0.5f;
		UpdateOffset();
	}
	public void AlignTop()
	{
		CheckMaterial();
		SpriteRenderer renderer = GetComponent<SpriteRenderer>();
		m_offset.y = -renderer.sprite.bounds.size.y*0.5f;
		UpdateOffset();
	}
	public void AlignBottom()
	{
		CheckMaterial();
		SpriteRenderer renderer = GetComponent<SpriteRenderer>();
		m_offset.y = renderer.sprite.bounds.size.y*0.5f;

		UpdateOffset();
	}

	public void AlignCenter()
	{
		CheckMaterial();
		//SpriteRenderer renderer = GetComponent<SpriteRenderer>();
		m_offset.x = 0;

		UpdateOffset();
	}

	public void AlignMiddle()
	{
		CheckMaterial();
		//SpriteRenderer renderer = GetComponent<SpriteRenderer>();
		m_offset.y = 0;
		UpdateOffset();
	}


	public void Snap()
	{
		CheckMaterial();
		SpriteRenderer renderer = GetComponent<SpriteRenderer>();
		m_offset = Snap(m_offset,1.0f/renderer.sprite.pixelsPerUnit);
		UpdateOffset();
	}

	// Gets the world position of a sprite anim node, with the sprite offset included.
	public Vector2 GetNodePosition(int node)
	{

		// Lazy get the nodes component
		if ( m_nodes == null )
			m_nodes = GetComponent<SpriteAnimNodes>();
			
		Vector2 spriteOffset = Offset;
		spriteOffset.Scale(transform.localScale);
		Vector2 result = spriteOffset;

		if ( m_nodes == null )
		{
			// Just return position with sprite offset
			result += (Vector2)transform.position;
		}
		else 
		{
			result += (Vector2)m_nodes.GetPosition(node);
		}
		return result;
	}

	#endregion
	#region Funcs: Private/Internal

	void Reset()
	{
		CheckMaterial();
	}

	void OnValidate()
	{
		// DL TODO: Apparently OnValidate can cause slowdowns on saving in the editor? 
		{		
			CheckMaterial(true);
			UpdateTint();
			UpdateOutline();
			UpdateOffset();
		}
	}


	// Use this for initialization
	void Start() 
	{
		UpdateTint();
		UpdateOutline();
		UpdateOffset();
	}

	bool CheckMaterial(bool onValidate = false)
	{

		// Apply outline if it hadn't been previously enabled, but is set
		bool shouldApplyOutline = ( m_outlineEnabled == false && m_outline.a > 0 );

		if ( s_shader == null )
			s_shader = Shader.Find( STR_SHADER );		
		if ( s_shaderOutline == null )
			s_shaderOutline = Shader.Find( STR_SHADER_OUTLINE );
		
		if ( s_shader == null )
			return false;
		
		if ( m_materialCached != null && shouldApplyOutline == false )
			return true;

		Shader shader = m_shaderOverride != null ? m_shaderOverride : shouldApplyOutline ? s_shaderOutline : s_shader;

		SpriteRenderer renderer = GetComponent<SpriteRenderer>();
		if ( renderer != null && shader != null)
		{			
			if ( Application.isPlaying == false || (Application.isEditor && onValidate))
				m_materialCached = renderer.sharedMaterial;
			else 
				m_materialCached = renderer.material;
			
			if ( m_materialCached == null || m_materialCached.shader != shader )
			{
				m_materialCached = new Material(shader);
				if ( Application.isPlaying == false )
					renderer.sharedMaterial = m_materialCached;
				else 
					renderer.material = m_materialCached;
			}
			
		}

		if ( shouldApplyOutline && m_materialCached != null)
			m_outlineEnabled = true;
		return m_materialCached != null;
	}

	void UpdateTint()
	{
		if ( CheckMaterial() == false )
			return;
		m_materialCached.SetColor(STR_SPROP_TINT, m_tint);	
	}

	void UpdateOutline()
	{
		if ( CheckMaterial() == false )
			return;
		m_materialCached.SetColor(STR_SPROP_OUTLINE, m_outline);	
	}

	void UpdateOffset()
	{
		if ( CheckMaterial() == false )
			return;
		m_materialCached.SetVector(STR_SPROP_OFFSET, m_offset);	

	}

	#endregion
	#region Util methods

	static Vector2 Snap( Vector2 pos, float snapTo )
	{	
		return new Vector2( Snap(pos.x, snapTo), Snap(pos.y, snapTo) );
	}

	static float Snap( float pos, float snapTo = 1)
	{		
		if ( snapTo < 0.001f )
			return pos;
		return Mathf.Round(pos / snapTo) * snapTo;
	}

	#endregion

}

}