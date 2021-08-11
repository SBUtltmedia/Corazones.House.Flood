#define SET_ALL_FONTS
#define FONT_SNAPPING 

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using PowerTools;
using PowerTools.QuestText;
using System.Text.RegularExpressions;

namespace PowerTools.Quest
{


[System.Serializable]
public class TextOutline
{
	public enum eDirection
	{
		Top = 1<<0,
		Bottom = 1<<1,
		Left = 1<<2,
		Right = 1<<3,
		TopLeft = 1<<4,
		TopRight = 1<<5,
		BottomLeft = 1<<6,
		BottomRight = 1<<7
	};

	[BitMask(typeof(eDirection))]
	public int m_directions = 0;
	public float m_width = 1.0f;
	public Color m_color = Color.black;	
}

[ExecuteInEditMode]
[RequireComponent(typeof(TextMesh))]
public class QuestText : MonoBehaviour 
{
	#if FONT_SNAPPING
	static readonly string STR_SHADER_PIXEL = "Powerhoof/Pixel Text Shader";
	#else
	static readonly string STR_SHADER_PIXEL = "Powerhoof/Sharp Text Shader";
	#endif
	static readonly string STR_SHADER = "GUI/Text Shader";
	static readonly Vector2 SCREEN_PADDING = new Vector2(8,8);

	static readonly Vector3[] SHADOW_OFFSETS = new Vector3[]	
	{
		Vector3.up, 
		Vector3.down, 
		Vector3.left, 
		Vector3.right,
		Vector3.up + Vector3.left,
		Vector3.up + Vector3.right,
		Vector3.down + Vector3.left,
		Vector3.down + Vector3.right,
	};



	//[System.Serializable]
	//public class PlatformSpriteData
	//{
	//	public Sprite m_sprite = null;
	//	[BitMask(typeof(ePlatform))]
	//	public ePlatform m_platforms = (ePlatform)~0;	
	//	public bool m_includePCKeyboard = true;	
	//}

	[System.Serializable]
	public class TextSpriteData
	{ 
		public string m_tag = "";
		public float m_offsetY = 0;
		public Sprite m_sprite = null;
		//public PlatformSpriteData[] m_sprite = { new PlatformSpriteData() };

		public override string ToString(){ return m_tag; }
	}

	[Multiline]
	[SerializeField] string m_text = "";
	[SerializeField] bool m_localize = false;
	[Header("Sorting")]
	[SerializeField] string m_sortingLayer = "Default";
	[SerializeField] int m_orderInLayer = 0;
	[Header("Wrap/Truncate Settings")]
	[Tooltip("Width in game units to wrap text (0 = disabled)")]
	[SerializeField, Delayed] float m_wrapWidth = 0.0f;
	[Tooltip("Whether to cut the text (adding ellipsis) instead of wrapping it")]
	[SerializeField] bool m_truncate = false;
	[Tooltip("Whether to use more expensive word wrap that tries to keep uniform line width")]
	[SerializeField] bool m_wrapUniformLineWidth = false;
	[Tooltip("Min line width when uniform line width is used, or Keep On Screen is set and the width is shortened. (0 = disabled)")]
	[SerializeField, Delayed] float m_wrapWidthMin = 0.0f;
	[Tooltip("Ensures the dialog is on screen when created (eg. offscreen character dialog)")]
	[SerializeField] bool m_keepOnScreen = false;
	[Header("Appearance")]
	[SerializeField] Shader m_shaderOverride = null;
	[SerializeField] TextOutline m_outline = null;	

	static Shader s_shader = null;
	bool m_materialSet = false;

	TextMesh m_mesh = null;
	TextWrapper m_textWrapper = null;
	MeshRenderer m_meshRenderer = null;

	Vector2 m_attachWorldPos = Vector2.zero; // Used to attach to a world pos but show on a gui camera
	Vector2 m_attachOffset = Vector2.zero;

	bool m_editorRefresh = false; // Required, since can't delete outline text in OnValidate. Maybe better not to delete, just to hide (unless not in editor)

	// Use the saved unlocalized text to update the text when translation changes
	[SerializeField, HideInInspector] string m_unlocalizedText = null;

	[SerializeField, HideInInspector] List<TextMesh> m_outlineMeshes = null;

	

	public string text { get { return m_text; } set { SetText(value); } }
	public Color color { get { return CheckTextMesh() ? m_mesh.color : Color.white; } set { if ( CheckTextMesh() ) { m_mesh.color = value; } } }

	public float WrapWidth { get { return m_wrapWidth; } set { m_wrapWidth = value; } }
	public bool Truncate { get { return m_truncate; } set { m_truncate = value; } }
	public bool WrapUniformLineWidth { get { return m_wrapUniformLineWidth; } set { m_wrapUniformLineWidth = value; } }

	public string SortingLayer { get{ return m_sortingLayer; } set{ m_sortingLayer = value; } }
	public int OrderInLayer { get{ return m_orderInLayer; } set
	{ 
		m_orderInLayer = value; 
		// Update any background text too!
		TextOutline outline = m_outline;
		SetOutline(null);
		SetOutline(outline);

	} }
	public bool GetShouldLocalize() { return m_localize; }
	public float GetWrapWidth() { return m_wrapWidth; }
	public void SetWrapWidth(float width)
	{ 
		m_wrapWidth = width; 		
		SetText(m_text);
	}

	public TextOutline GetOutline() { return m_outline; }
	public void SetOutline( TextOutline outline ) 
	{ 
		if ( m_outline != null && (outline == null || m_outline.m_directions != outline.m_directions) && m_outlineMeshes != null )
		{
			// remove existing outline meshes
			for ( int i = 0; i < m_outlineMeshes.Count; ++i ) 
			{
				if ( m_outlineMeshes[i] != null )
					GameObject.Destroy(	m_outlineMeshes[i].gameObject );
			}
			m_outlineMeshes.Clear();
		}
		m_outline = outline;
		SetText(m_text);
	}

	public string GetUnlocalizedText() { return string.IsNullOrEmpty(m_unlocalizedText) ? m_text : m_unlocalizedText; }


	// Use this for initialization
	void Start () 
	{
		if ( CheckTextMesh() )
		{
			CheckMaterial();
			string text = m_text;
			SetText(text);
		}
	}


	public void SetText(string text)
	{
		// Don't bother if text is the same
		if (text == null) 
			text = string.Empty;
		m_unlocalizedText = text;
		text = SystemText.Localize( text );		
		text = ParseImages(text);
		m_text = text;

		if ( CheckMaterial() )
		{
			// Set sorting layer
			m_meshRenderer.sortingOrder = m_orderInLayer;
			int id = UnityEngine.SortingLayer.NameToID(m_sortingLayer);
			if ( UnityEngine.SortingLayer.IsValid(id) )
				m_meshRenderer.sortingLayerID = id;
			
			if ( m_wrapWidth > 0.0f )
			{
				if ( m_textWrapper == null  )
					m_textWrapper = new TextWrapper(m_mesh);
				
				if ( m_keepOnScreen == false )
				{
					if ( m_truncate )
						text = m_textWrapper.Truncate(m_text, m_wrapWidth);
					else if ( m_wrapUniformLineWidth )
						text = m_textWrapper.WrapTextMinimiseWidth(m_text, m_wrapWidth, m_wrapWidthMin);
					else 
						text = m_textWrapper.WrapText(m_text, m_wrapWidth);
				}
				else
				{
					// Work out how to best keep the text on screen

					float finalWidth = m_wrapWidth;
					Rect bounds = new Rect( (Vector2)(transform.position), Vector2.zero );
					Rect cameraBounds = new Rect();

					if ( Application.isPlaying )
					{
						// First, adjust wrap width so that it's on screen if possible
						Camera camera = PowerQuest.Get.GetCameraGui();
						bounds.width = m_wrapWidth;
						if ( m_mesh.alignment == TextAlignment.Center )
							bounds.center = bounds.position;
						else if ( m_mesh.alignment == TextAlignment.Right )
							bounds.x += m_wrapWidth;

						cameraBounds = new Rect(camera.transform.position.x, camera.transform.position.y, camera.orthographicSize * 2.0f * camera.aspect - SCREEN_PADDING.x, camera.orthographicSize * 2.0f - SCREEN_PADDING.y);
						cameraBounds.x -= cameraBounds.width*0.5f;
						cameraBounds.y -= cameraBounds.height*0.5f;


						// If wrap width is adjustable, shrink it 
						if ( m_wrapWidthMin > 0 )
						{
							if ( bounds.xMin < cameraBounds.xMin )
								finalWidth -= cameraBounds.xMin -  bounds.xMin;
							if ( bounds.xMax > cameraBounds.xMax )
								finalWidth -= bounds.xMax - cameraBounds.xMax;
							finalWidth = Mathf.Max(m_wrapWidthMin+1, finalWidth);
						}
					}

					//
					// Now offset bounds so that they're on screen
					//
					if ( m_truncate )
						text = m_textWrapper.Truncate(m_text, finalWidth);
					else if ( m_wrapUniformLineWidth )
						text = m_textWrapper.WrapTextMinimiseWidth(m_text, finalWidth, m_wrapWidthMin);
					else 
						text = m_textWrapper.WrapText(m_text, finalWidth);
					
					m_mesh.text = text;

					if ( Application.isPlaying )
					{
						bounds.Set(m_textWrapper.Bounds.min.x, m_textWrapper.Bounds.min.y, m_textWrapper.Bounds.size.x, m_textWrapper.Bounds.size.y);

						// Get the text height from the textwrapper
						Vector2 offset = Vector2.zero;
						if ( bounds.xMin < cameraBounds.xMin )
							offset.x += cameraBounds.xMin - bounds.xMin;
						if ( bounds.xMax > cameraBounds.xMax )
							offset.x += cameraBounds.xMax - bounds.xMax;
						if ( bounds.yMin < cameraBounds.yMin )
							offset.y += cameraBounds.yMin - bounds.yMin;
						if ( bounds.yMax > cameraBounds.yMax )
							offset.y += cameraBounds.yMax - bounds.yMax;

						if ( m_attachWorldPos != Vector2.zero )
						{
							#if FONT_SNAPPING
							m_attachOffset = Utils.Snap(offset, PowerQuest.Get.SnapAmount);
							#else
							m_attachOffset = offset;
							#endif
						}
						else 
						{
							transform.Translate(offset.WithZ(0));				
						}
					}

				}
			}

			// Update outline
			if ( m_outline != null && m_outline.m_directions != 0 && gameObject.scene.IsValid() )
			{

				// Meshes may have been deleted, so remove them if they have
				for ( int i = m_outlineMeshes.Count - 1; i >= 0; i-- ) 
				{
					if ( m_outlineMeshes[i] == null )
						m_outlineMeshes.RemoveAt(i);
				}

				// loop through all potential outline angles
				int meshId = 0;
				for ( int i = 0; i < 8; ++i ) 
				{
					if ( PowerTools.QuestText.BitMask.IsSet(m_outline.m_directions, i)  )
					{
						if ( m_outlineMeshes == null )
							m_outlineMeshes = new List<TextMesh>();			
						

						if ( meshId >= m_outlineMeshes.Count  )
						{
							GameObject obj = new GameObject(((TextOutline.eDirection)(1<<i)).ToString(), m_mesh.GetType() ) as GameObject;
							obj.hideFlags = HideFlags.HideAndDontSave;
							obj.transform.parent = transform;
							obj.transform.localScale = Vector3.one;
							obj.transform.localPosition = SHADOW_OFFSETS[i] * m_outline.m_width;
							obj.layer = gameObject.layer;

							MeshRenderer newShadowRenderer = obj.GetComponent<MeshRenderer>();
							newShadowRenderer.sharedMaterial = m_meshRenderer.sharedMaterial;//new Material(m_meshRenderer.material);
							newShadowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
							newShadowRenderer.receiveShadows = false;
							newShadowRenderer.sortingLayerID = m_meshRenderer.sortingLayerID;
							newShadowRenderer.sortingLayerName = m_meshRenderer.sortingLayerName;
							newShadowRenderer.sortingOrder = m_meshRenderer.sortingOrder - 1;

							TextMesh newShadowMesh = obj.GetComponent<TextMesh>();
							newShadowMesh.alignment = m_mesh.alignment;
							newShadowMesh.anchor = m_mesh.anchor;
							newShadowMesh.characterSize = m_mesh.characterSize;
							newShadowMesh.font = m_mesh.font;
							newShadowMesh.fontSize = m_mesh.fontSize;
							newShadowMesh.fontStyle = m_mesh.fontStyle;
							newShadowMesh.richText = m_mesh.richText;
							newShadowMesh.lineSpacing = m_mesh.lineSpacing;
							newShadowMesh.tabSize= m_mesh.tabSize;
							newShadowMesh.offsetZ = m_mesh.offsetZ + 0.1f;


							m_outlineMeshes.Add(newShadowMesh);
						}
						TextMesh shadowMesh = m_outlineMeshes[meshId];
						shadowMesh.text = text;
						shadowMesh.color = m_outline.m_color;

						meshId++;
					}
				}
			}

			m_mesh.text = text;
		}

	}

	public void AttachTo(Vector2 worldPosition)
	{
		m_attachOffset = Vector2.zero;
		m_attachWorldPos = worldPosition;
		LateUpdate();
	}

	void OnValidate()
	{
		// TODO: this is touched on unity save, can potentially cause slow downs
		if ( gameObject.activeInHierarchy )
		{
			m_editorRefresh = true;
		}
	}

	void Update()
	{
		if ( gameObject.activeInHierarchy && Application.isEditor && Application.isPlaying == false && m_editorRefresh )
		{
			if ( m_outline != null )
			{
				// remove existing outline meshes
				for ( int i = 0; i < m_outlineMeshes.Count; ++i ) 
				{
					if ( m_outlineMeshes[i] != null )
						GameObject.DestroyImmediate( m_outlineMeshes[i].gameObject );
				}
				m_outlineMeshes.Clear();
			}
			SetText(m_text);
			m_editorRefresh = false;
		}
	}

	void LateUpdate()
	{
		if ( m_attachWorldPos != Vector2.zero )
		{
			if ( PowerQuest.GetValid() == false || PowerQuest.Get.GetCamera() == null || PowerQuest.Get.GetCameraGui() == null )
				return;
			QuestCamera questCam = PowerQuest.Get.GetCamera();
			Camera cam = questCam.GetInstance().GetComponent<Camera>();
			Camera guiCam = PowerQuest.Get.GetCameraGui();

			// Need to add the difference between camera offset and actual position to account for the amount the camera has snapped.
			Vector2 camSnapOffset = ((Vector2)cam.transform.position - questCam.GetPosition());
			Vector2 guiSpacePosition = guiCam.ViewportToWorldPoint( cam.WorldToViewportPoint(m_attachWorldPos) ) + (Vector3)m_attachOffset + (Vector3)camSnapOffset;

			#if FONT_SNAPPING

			if ( PowerQuest.Get.GetSnapToPixel() )
			{
				// finally we snap so that the text verts don't jiggle around
				// The snap amount is modified by the current camera zoom to account for resolution differences in the game camera and the gui camera (stops pixel text jiggling)
				float snapAmount = PowerQuest.Get.SnapAmount * Mathf.Max(questCam.GetZoom(),1);
				transform.position = Utils.Snap(guiSpacePosition, snapAmount )+new Vector2(0.001f,0.001f);
			
				// Offset by amount camera has moved so vertexes snap internally, but text still scrolls smoothly
				Vector2 offset = guiSpacePosition-(Vector2)transform.position;
				m_meshRenderer.material.SetVector("_Offset", offset);			
			}
			else 
			{
				transform.position = guiSpacePosition;
			}

			#else
			transform.position = guiSpacePosition;
			#endif
			
		}
	}

	bool CheckTextMesh() 
	{
		if ( m_mesh == null )
			m_mesh = GetComponent<TextMesh>();		
		return m_mesh != null;		    
	}

	bool CheckMaterial()
	{		
		if ( m_materialSet )
			return true;

		if ( m_mesh == null )
			m_mesh = GetComponent<TextMesh>();
		
		if ( m_meshRenderer == null )
			m_meshRenderer = GetComponent<MeshRenderer>();

		if ( m_mesh == null || m_meshRenderer == null )
			return false;

		if ( Application.isPlaying == false )
			return true;

		if ( s_shader == null )
			s_shader = Shader.Find(PowerQuest.Get.GetSnapToPixel() ? STR_SHADER_PIXEL : STR_SHADER);		
		
		if ( s_shader == null )
			return false;

		if ( m_shaderOverride != null && m_meshRenderer.material != null && m_meshRenderer.material.shader != m_shaderOverride )
		{
			Material mat = m_meshRenderer.material;
			if ( mat.shader != m_shaderOverride )
			{
				mat = new Material(mat);
				mat.shader = m_shaderOverride;
				mat.mainTexture.filterMode = PowerQuest.Get.GetSnapToPixel() ? FilterMode.Point : FilterMode.Bilinear;
				mat.mainTexture. anisoLevel = PowerQuest.Get.GetSnapToPixel() ? 0 : 1;
				m_meshRenderer.material = mat;

			}
			m_materialSet = mat != null;
			return m_materialSet;
		}
		else 
		{
			
			#if SET_ALL_FONTS
				// Set all fonts to use the shader
				if ( m_mesh.font.material.shader != s_shader )
				{
					m_mesh.font.material.shader = s_shader;
					m_mesh.font.material.mainTexture.filterMode = PowerQuest.Get.GetSnapToPixel() ? FilterMode.Point : FilterMode.Bilinear;
					m_mesh.font.material.mainTexture.anisoLevel = PowerQuest.Get.GetSnapToPixel() ? 0 : 1;
				}
			m_materialSet = true;
			return true;
			#else			
				Material mat = m_meshRenderer.sharedMaterial;
				if ( mat.shader != s_shader )
				{
					mat = new Material(mat);
					mat.shader = s_shader;
					mat.mainTexture.filterMode = PowerQuest.Get.GetSnapToPixel() ? FilterMode.Point : FilterMode.Bilinear;
					mat.mainTexture. anisoLevel = PowerQuest.Get.GetSnapToPixel() ? 0 : 1;
					m_meshRenderer.sharedMaterial = mat;
					
				}
				m_materialSet = mat != null;
				return m_materialSet;
			#endif
		}
	}


	static readonly string REGEX_TAG = @"\[(\w*)\]";

	string ParseImages(string text)
	{
		if ( Application.isPlaying == false )
			return text;
		// Remove other materials first. Ideally this wouldn' tbe necessary, but I'd need to put more work in to get it working without clearing materials first
		MeshRenderer renderer = GetComponent<MeshRenderer>();
		if ( renderer.materials.Length > 1 )
		{
			for ( int i = 1; i < renderer.materials.Length; ++i )
			{
				Destroy(renderer.materials[i]);
			}
			renderer.materials = new Material[] { renderer.materials[0] };
		}

		// Use regex to find cases of #Whatever# so we can add the image
		return Regex.Replace(text,REGEX_TAG, EvaluateImageTagMatch );
	}

	static readonly string TAG_QUAD = @"<quad material={0} size={1} x={2} y={3} width={4} height={5} />";

	string EvaluateImageTagMatch( Match match )
	{	
		string result = string.Empty;	
		if ( match.Groups == null || match.Groups.Count < 2)
			return result;

		string tag = match.Groups[1].Value;
		// TODO: Append platform tag
		tag += "PS";
		TextSpriteData data = System.Array.Find( PowerQuest.Get.m_textSprites, item=>string.Equals(item.m_tag, tag,System.StringComparison.OrdinalIgnoreCase) );
		// if not found with platform tag, try without
		if ( data == null )
		{
			tag = match.Groups[1].Value;
			data = System.Array.Find( PowerQuest.Get.m_textSprites, item=>string.Equals(item.m_tag, tag,System.StringComparison.OrdinalIgnoreCase) );
		}

		if ( data == null || data.m_sprite == null )
			return result;
		Sprite sprite = data.m_sprite;

		Material material = null;
		// Check if material exists in guitext already

		MeshRenderer renderer = GetComponent<MeshRenderer>();
		int matIndex = System.Array.FindIndex(renderer.materials, item=>item.mainTexture == sprite.texture);
		if ( matIndex < 0 )
		{
			matIndex = renderer.materials.Length;
			Material[] materials = new Material[matIndex+1];
			System.Array.Copy(renderer.materials, materials, matIndex);
			material = Instantiate(PowerQuest.Get.m_textSpriteMaterial);
			material.mainTexture = sprite.texture;
			materials[matIndex] = material;
			renderer.materials = materials;
		}
		material = renderer.materials[matIndex];
		material.SetVector("_Offset",new Vector2(0,data.m_offsetY));
		result = string.Format(TAG_QUAD, matIndex, sprite.textureRect.height, sprite.textureRect.x/sprite.texture.width, sprite.textureRect.y/sprite.texture.height, sprite.textureRect.width/sprite.texture.width, sprite.textureRect.height/sprite.texture.height);
		//result = string.Format(TAG_QUAD, matIndex, m_mesh.fontSize*2, sprite.textureRect.x/sprite.texture.width, sprite.textureRect.y/sprite.texture.height, sprite.textureRect.width/sprite.texture.width, sprite.textureRect.height/sprite.texture.height);

		return result;

	}
		

}
}

namespace PowerTools.QuestText
{

	public struct BitMask
	{	
		public BitMask( int mask )
		{
			m_value = mask;
		}


		public BitMask( params int[] bitsSet )
		{
			m_value = 0;
			for( int i = 0; i< bitsSet.Length; ++i )
			{
				m_value |= 1 <<  bitsSet[i];
			}
		}

		public static implicit operator int(BitMask m) 
		{
			return m.m_value;	
		}

		public int Value { get{ return m_value; } set { m_value = value; } }
		public void SetAt(int index) { m_value |= 1 << index; }
		public void SetAt<T>(T index) { m_value |= 1 << (int)(object)index; }
		public void UnsetAt(int index) { m_value &= ~(1 << index); }
		public void UnsetAt<T>(T index) { m_value &= ~(1 << (int)(object)index); }
		public bool IsSet(int index) { return (m_value & 1 << index) != 0; }
		public bool IsSet<T>(T index) { return (m_value & 1 << (int)(object)index) != 0; }
		public void Clear() { m_value = 0; }

		// And some static functions if you don't wanna construt the bitmask  and just wanna pass in/out an int
		public static int SetAt(int mask, int index) { return mask | 1 << index; }
		public static int UnsetAt(int mask, int index)  { return mask & ~(1 << index); }
		public static bool IsSet(int mask, int index) { return (mask & 1 << index) != 0; }


		public static uint GetNumberOfSetBits(uint i)
		{
			// From http://stackoverflow.com/questions/109023/how-to-count-the-number-of-set-bits-in-a-32-bit-integer
			i = i - ((i >> 1) & 0x55555555);
			i = (i & 0x33333333) + ((i >> 2) & 0x33333333);
			return (((i + (i >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24;
		}

		int m_value;


	}


	public class BitMaskAttribute : PropertyAttribute
	{
		public System.Type propType;
		public BitMaskAttribute(System.Type aType)
		{
			propType = aType;
		}
	}

}
