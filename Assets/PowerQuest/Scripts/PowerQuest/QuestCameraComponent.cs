using UnityEngine;
using System.Collections;
using PowerTools;

namespace PowerTools.Quest
{



//
// Actual Camera component in the scene
//
public partial class QuestCameraComponent : MonoBehaviour 
{
	[SerializeField] QuestCamera m_data = null;
	[SerializeField] float m_smoothingFactor = 10.0f;
	[SerializeField] float m_smoothingMinSpeed = 10.0f;
	[Tooltip("How far player has to move before scrolling starts")]
	[SerializeField] Vector2 m_distFromPlayerBeforeScroll = new Vector2(25,10);
	[SerializeField] float m_characterFacingOffset = 0.0f;

	[Header("Screenshake global multipliers")]
	[SerializeField] float m_shakeIntensityMult = 1.0f;
	[SerializeField] float m_shakeFalloffMult = 1.0f;	
	[SerializeField] float m_shakeSpeed = 40.0f;

	[Header("Prefab References")]
	[SerializeField] GameObject m_prefabPixelCam = null;
	
	
	Vector2 m_playerDragged = Vector2.zero;
	Vector2 m_playerPosCached = Vector2.zero;
	eFace m_playerFaceLast = eFace.Down;

	// Hack to ensure can check if snapped last update
	bool m_snappedSinceUpdate = true;
	bool m_snappedLastUpdate = true;

	float m_shakeIntensity = 0;	
	float m_shakeFalloff = 1;
	float m_shakeDurationTimer = 0;
	Vector2 m_screenShakeOffset = Vector2.zero;

	float m_overrideTransitionTimer = 0;
	float m_overrideTransitionTime = 0;

	float m_zoomTransitionTimer = 0;
	float m_zoomTransitionTime = 0;

	// The current amount we're zoomed from the default set in PowerQuest
	float m_zoomMultiplier = 1;

	bool m_targetPositionChanged = false;
	Vector2 m_cachedTargetNoPixelSnap = Vector2.zero;

	public QuestCamera GetData() { return m_data; }
	public void SetData(QuestCamera data) { m_data = data; }

	Camera m_camera = null;
	GameObject m_pixelCam = null;
		

	public void OnEnterRoom()
	{
		Snap();
	}

	public void Snap()
	{
		UpdatePos(true);
	}

	public bool GetSnappedLastUpdate() { return m_snappedLastUpdate; }

	public void OnOverridePosition(float transitionTime)
	{
		m_overrideTransitionTime = transitionTime;
		m_overrideTransitionTimer = transitionTime;

		// Update target position before we transition back (by passing true to snap values)
		if ( m_data.GetHasPositionOverride() == false )
			GetCameraFollowTargetPosition(true);
		

		// Snap if there's no transition
		if ( transitionTime <= 0 )
			Snap();
	}

	public bool GetHasPositionOverrideOrTransitioning()
	{
		return m_data.GetHasPositionOverride() || m_overrideTransitionTimer > 0.0f;
	}

	public void OnZoom(float transitionTime)
	{
		m_zoomTransitionTime = transitionTime;
		m_zoomTransitionTimer = transitionTime;

		// Update target position before we transition back (by passing true to snap values)
		if ( m_data.GetHasZoom() == false )
			GetCameraFollowTargetPosition(true);

		// Snap if there's no transition
		if ( transitionTime <= 0 )
			Snap();
	}
	public bool GetHasZoomOrTransitioning()
	{
		return m_data.GetHasZoom() || m_zoomTransitionTimer > 0.0f;
	}


	Vector2 GetHalfCamSize()
	{
		return new Vector2(m_camera.orthographicSize * m_camera.aspect,m_camera.orthographicSize);
	}

	// Returns the distance the camera can move before showing screen edges
	public RectCentered CalcOffsetLimits()
	{			
		Vector2 halfCamSize = GetHalfCamSize();
		RectCentered result = PowerQuest.Get.GetCurrentRoom().Bounds;
		result.Min = result.Min + halfCamSize;
		result.Max = result.Max - halfCamSize;
		if ( result.Width < 0 )
		{
			result.Width = 0;
			//result.Center = result.Center.WithX(PowerQuest.Get.GetCurrentRoom().Size.Center.x);
		}
		if ( result.Height < 0 )
		{
			result.Height = 0;
			//result.Center = result.Center.WithY(PowerQuest.Get.GetCurrentRoom().Size.Center.y);
		}
		return result;
	}

	/*
	// Returns the distance the camera can move before showing screen edges
	public Vector2 CalcMaxOffset()
	{
		
		//	Bounds are where world -halfwidth >= viewport.x 0 && world halfwidth <= viewport.x 1

		Vector2 halfRoomSize = PowerQuest.Get.GetCurrentRoom().Size.size * 0.5f;
		Vector2 halfCamSize = new Vector2(m_camera.orthographicSize * m_camera.aspect,m_camera.orthographicSize);
		Vector2 maxOffset = halfRoomSize-halfCamSize;
		if ( maxOffset.x < 0 )
			maxOffset.x = 0;
		if ( maxOffset.y < 0 )
			maxOffset.y = 0;
		return maxOffset;

	}*/

	// Returns a position where camera won't be outside room bounds
	public Vector2 ClampPositionToRoomBounds( Vector2 position )
	{
		if ( m_data.IgnoreBounds )
			return position;
		RectCentered maxOffset = CalcOffsetLimits();
		position.x = Mathf.Clamp( position.x, maxOffset.Min.x, maxOffset.Max.x );
		position.y = Mathf.Clamp( position.y, maxOffset.Min.y, maxOffset.Max.y );
		return position;		
	}

	// Calcuates the target camera position it's following the player (even if it's not currently). 
	// Quest Scripts can use this to find where the camera would be when overrideing the position.
	public Vector2 GetCameraFollowTargetPosition( bool snap = true, bool disablePixelSnap = false )
	{
		if ( PowerQuest.Get == null || m_data == null )
			return Vector2.zero;
		
		Vector2 position = m_data.GetPosition();

		ICharacter character = m_data.GetCharacterToFollow();
		if ( character != null || PowerQuest.Get.GetCurrentRoom() != character.Room )
		{
			// When character is facing left/right, add/change offset from the character so the camera leads in the direction they're facing

			if ( m_playerFaceLast != character.Facing )
			{					
				if ( character.Facing == eFace.Left || character.Facing == eFace.Right )
				{
					m_playerFaceLast = character.Facing;
				}
			}

			Vector2 characterPos = character.Position + m_data.OffsetFromCharacter;

			// When facing left/right, offset so that camera leads infront of player
			if ( character.Walking )
			{
				if ( m_playerFaceLast == eFace.Left )
				{
					characterPos.x = characterPos.x - (m_characterFacingOffset / m_zoomMultiplier);
				}
				else if ( m_playerFaceLast == eFace.Right )
				{
					characterPos.x = characterPos.x + (m_characterFacingOffset / m_zoomMultiplier);
				}
			}

			// When player moves back/forth quickly, don't scroll the room
			if ( snap )
			{
				m_playerPosCached = characterPos;
				m_playerDragged = characterPos;
			}
			else
			{				
				Vector2 distFromPlayerBeforeScroll = m_distFromPlayerBeforeScroll / m_zoomMultiplier;

				if ( characterPos.x > m_playerPosCached.x )
				{
					if ( characterPos.x > m_playerDragged.x + distFromPlayerBeforeScroll.x )
						m_playerDragged.x = characterPos.x - distFromPlayerBeforeScroll.x;
				}
				else 
				{
					if ( characterPos.x < m_playerDragged.x - distFromPlayerBeforeScroll.x )
						m_playerDragged.x = characterPos.x + distFromPlayerBeforeScroll.x;			
				}

				if ( characterPos.y > m_playerPosCached.y )
				{
					if ( characterPos.y > m_playerDragged.y + distFromPlayerBeforeScroll.y )
						m_playerDragged.y = characterPos.y - distFromPlayerBeforeScroll.y;
				}
				else 
				{
					if ( characterPos.y < m_playerDragged.y - distFromPlayerBeforeScroll.y )
						m_playerDragged.y = characterPos.y + distFromPlayerBeforeScroll.y;			
				}
				m_playerPosCached = characterPos;	
			}

			if ( m_camera != null )
			{
				{					
					RectCentered scrollSize = PowerQuest.Get.GetCurrentRoom().ScrollBounds;
					RectCentered offsetLimits = CalcOffsetLimits();

					if ( scrollSize.Width <= 0.0f )
					{
						position.x = m_playerDragged.x;
					}
					else 
					{
						if ( GetHasZoomOrTransitioning() ) // When Zoom is applied, scale the scroll limits, otherwise character will probably be 
						{
							scrollSize.MinX = offsetLimits.MinX + ((scrollSize.MinX - offsetLimits.MinX) / m_data.GetZoom());
							scrollSize.MaxX = offsetLimits.MaxX + ((scrollSize.MaxX - offsetLimits.MaxX) / m_data.GetZoom());
						}
						position.x = Mathf.Lerp( offsetLimits.Min.x, offsetLimits.Max.x, Mathf.InverseLerp(scrollSize.Min.x, scrollSize.Max.x, m_playerDragged.x) );
					}

					if ( scrollSize.Height <= 0.0f )
					{
						position.y = m_playerDragged.y;
					}
					else 
					{
						if ( GetHasZoomOrTransitioning() ) // When Zoom is applied, scale the scroll limits, otherwise character will probably be 
						{
							scrollSize.MinY = offsetLimits.MinY + ((scrollSize.MinY - offsetLimits.MinY) / m_data.GetZoom());
							scrollSize.MaxY = offsetLimits.MaxY + ((scrollSize.MaxY - offsetLimits.MaxY) / m_data.GetZoom());
						}
						position.y = Mathf.Lerp( offsetLimits.Min.y, offsetLimits.Max.y, Mathf.InverseLerp(scrollSize.Min.y, scrollSize.Max.y, m_playerDragged.y) );
					}
				}
			}				

		}

		// Snap target position
		if ( disablePixelSnap == false )
			position = Utils.Snap(position, PowerQuest.Get.SnapAmount);

		//
		// Clamp to room bounds
		//
		position = ClampPositionToRoomBounds(position);

		return position;
	}

	public bool GetTargetChangedLastUpdate()  { return m_targetPositionChanged; } 

	

	// Use this for initialization
	void Awake() 
	{
		m_camera = GetComponent<Camera>();
	}

	void Start() 
	{
		if ( PowerQuest.Get.GetPixelCamEnabled() && m_prefabPixelCam != null )
		{
			// Set up pixel camera
			m_pixelCam = GameObject.Instantiate(m_prefabPixelCam) as GameObject;
			// Set pixel cam render layer (camera layers without the "HighRes" one)
			int layerHighRes = LayerMask.NameToLayer("HighRes");			
			m_pixelCam.GetComponent<Camera>().cullingMask = Utils.MaskUnsetAt(m_camera.cullingMask,layerHighRes);
			m_pixelCam.transform.GetChild(0).gameObject.layer = layerHighRes;
			// Set this camera to only render HighRes stuff
			m_camera.cullingMask = 1<<layerHighRes;
		}
	}

	// Update is called once per frame
	void Update() 
	{	
		// Hack to ensure can check if snapped last update
		if ( m_snappedSinceUpdate == false )
			m_snappedLastUpdate = false;
		m_snappedSinceUpdate = false;

		UpdatePos(PowerQuest.Get.GetSkippingCutscene());
	}

	void LateUpdate()
	{
		// Update pixel cam position if it exists
		if ( m_pixelCam != null )			
			m_pixelCam.transform.position = Utils.Snap(transform.position).WithZ(m_pixelCam.transform.position.z);
	}

	void UpdatePos(bool snap)
	{
		if ( snap )
		{
			m_snappedSinceUpdate = true;
			m_snappedLastUpdate = true;
		}

		if ( m_data.Enabled == false )
			return;

		//
		// Apply Zoom
		//
		float orthoSize = PowerQuest.Get.VerticalResolution;

		if ( GetHasZoomOrTransitioning() )
		{
			if ( snap )
				m_zoomTransitionTimer = 0;
			if ( m_zoomTransitionTimer > 0 )
				m_zoomTransitionTimer -= Time.deltaTime;
			float ratio = m_zoomTransitionTime <= 0 ? 0 : (m_zoomTransitionTimer/m_zoomTransitionTime);
			if ( m_data.GetHasZoom() ) // If false, then we're transitioning back to original position
				ratio = 1.0f - ratio;	// Reverse transition			
			orthoSize /= Mathf.Lerp( m_data.GetZoomPrev(), m_data.GetZoom(), ratio );
		}
		m_camera.orthographicSize = orthoSize * 0.5f;

		// Calc zoom multiplier, and adjust smooth and shake amount to account for it, as well as "player leading" and stuff
		m_zoomMultiplier = PowerQuest.Get.DefaultVerticalResolution / orthoSize;

		Vector2 position = m_data.GetPosition();
		Vector2 oldPosition = position;
		Vector2 targetPosition = position;
		Vector2 targetPositionNoPixelSnap = position;

		if ( GetHasPositionOverrideOrTransitioning() )
		{
			if ( snap )
				m_overrideTransitionTimer = 0;
			if ( m_overrideTransitionTimer > 0 )
				m_overrideTransitionTimer -= Time.deltaTime;
			float ratio = m_overrideTransitionTime <= 0 ? 0 : (m_overrideTransitionTimer/m_overrideTransitionTime);
			if ( m_data.GetHasPositionOverride() ) // If false, then we're transitioning back to original position
				ratio = 1.0f - ratio;
			targetPosition = ClampPositionToRoomBounds(m_data.GetPositionOverride());
			// Snap target position at source
			//position = Utils.Snap(position, PowerQuest.Get.SnapAmount);
			Vector2 prevPosition = m_data.GetPositionOverridePrev() == new Vector2(float.MaxValue,float.MaxValue) ? GetCameraFollowTargetPosition(snap) : ClampPositionToRoomBounds(m_data.GetPositionOverridePrev());
			position = Vector2.Lerp( prevPosition, targetPosition, ratio );

			targetPositionNoPixelSnap = targetPosition;
		}
		else 
		{
			// Following player
			position = GetCameraFollowTargetPosition(snap);
			targetPosition = position;
			targetPositionNoPixelSnap = GetCameraFollowTargetPosition(snap, true);

		}

		// Update whether targetposition changed. this ignores snapping, otherwise gives wrong results when checking if it changed last update.
		m_targetPositionChanged = ( targetPositionNoPixelSnap != m_cachedTargetNoPixelSnap );
		m_cachedTargetNoPixelSnap = targetPositionNoPixelSnap;

		//
		// Smooth camera movement
		//
		if ( snap == false )
		{
			Vector2 diff = position - oldPosition;
			float dist = diff.magnitude;
			float smoothDist = Mathf.Max(m_smoothingMinSpeed, m_smoothingFactor * dist) * Time.deltaTime * m_zoomMultiplier;
			if ( dist > smoothDist )
				position = oldPosition + (smoothDist*diff.normalized);
		}

		//
		// Screenshake
		//
		m_screenShakeOffset = Vector2.zero;				
		{	
			if ( m_shakeIntensity > 0 )
			{
				m_screenShakeOffset = (((new Vector2( Mathf.PerlinNoise(m_shakeSpeed * Time.time, 0), Mathf.PerlinNoise(1, m_shakeSpeed * Time.time) )) * 2) - Vector2.one) * m_shakeIntensity * m_shakeIntensityMult * m_zoomMultiplier;			
				
				if ( m_shakeDurationTimer > 0 )
				{
					m_shakeDurationTimer -= Time.deltaTime;
					if ( m_shakeDurationTimer <= 0 )
					if ( m_shakeFalloff > 0 )
					{
						m_shakeIntensity -= (-m_shakeDurationTimer) / m_shakeFalloff; // If we overshot the end time, apply the amount we overshot to the falloff
					}
					else 
					{
						m_shakeIntensity = 0;
					}
				}
				else if ( m_shakeFalloff > 0 )
				{			
					m_shakeIntensity -= Time.deltaTime / m_shakeFalloff;
				}
				else 
				{
					m_shakeIntensity = 0;
				}
			}
		}

		//
		// Apply position
		//	
		m_data.SetPosition(position); // Store in data
		m_data.SetTargetPosition(targetPosition);
		transform.position = (m_screenShakeOffset +position).WithZ(transform.position.z); 
	}

	// The current shake intensity. Can be used to add your own
	public float ShakeIntensity => m_shakeIntensity;

	public void Shake( CameraShakeData shakeData )  { Shake(shakeData.m_intensity, shakeData.m_duration, shakeData.m_falloff); }

	public void Shake( float intensity = 1.0f, float duration = 0.1f, float falloff = 0.15f ) 
	{
		m_shakeDurationTimer = duration;
		m_shakeIntensity = intensity;
		m_shakeFalloff = m_shakeIntensity <= 0 ? 0 : ( falloff * m_shakeFalloffMult / m_shakeIntensity);		
	}

	void MsgShake( float intensity, float duration, float falloff ) { Shake(intensity, duration,  falloff); }
	void MsgShake( float intensity, float duration ) { Shake(intensity, duration); }
	void MsgShake( float intensity ) { Shake(intensity); }


}

// CameraShakeData lets you have a camera shake set up in the inspector as a single variable rather than passing individual vars to the screenshake
[System.Serializable]
public class CameraShakeData
{
	public float m_intensity = 1;
	public float m_duration = 0.1f;
	public float m_falloff = 0.15f;
}


//
// Camera Data and functions. Persistant between scenes, as opposed to CameraComponent which lives on a GameObject in a scene.
//
[System.Serializable] 
public partial class QuestCamera : ICamera
{
	[Tooltip("Offset from the target character that the camera tries to center on")]
	[SerializeField] Vector2 m_offsetFromCharacter = new Vector2(0,30);
	//
	// Private variables
	//
	QuestCameraComponent m_instance = null;
	string m_characterToFollow = null; // String so it will be saved
	bool m_hasPositionOverride = false; // Flag is necessary to be reversable mid-transition
	Vector2 m_positionOverride = new Vector2(float.MaxValue,float.MaxValue);
	Vector2 m_positionOverridePrev = new Vector2(float.MaxValue,float.MaxValue);
	float m_zoom = 1.0f;
	float m_zoomPrev = 1.0f;
	bool m_hasZoom = false; // Flag is necessary to be reversable mid-transition
	Vector2 m_position = Vector2.zero;
	Vector2 m_targetPosition = Vector2.zero;
	bool m_enabled = true;
	bool m_ignoreBounds = false;

	//
	//  Properties
	//

	/// Sets whether PowerQuest controls the camera, set to false if you want to control the camera yourself (eg. animate it)
	public bool Enabled 
	{ 
		get { return m_enabled; }
		set 
		{ 
			bool didEnable = m_enabled == false && value == true;
			m_enabled = value;
			if ( m_instance != null )
			{
				m_instance.enabled = value;
				if ( didEnable)
					m_instance.Snap();
			}
		}
	}


	/// Sets whether overrides to camera position ignore room bounds. Useful for snapping camera to stuff off to the side of the room in cutscenes
	public bool IgnoreBounds 
	{ 
		get { return m_ignoreBounds; }
		set 
		{ 
			m_ignoreBounds = value; 
			if ( m_instance != null )
				m_instance.Snap();
		}
	}

	public Vector2 OffsetFromCharacter { 
		get { return m_offsetFromCharacter; } 
		set { m_offsetFromCharacter = value; } }
		
	/// Returns the current shake intensity. \sa Shake
	public float ShakeIntensity => (m_instance?.ShakeIntensity ?? 0);

	//
	// Getters/Setters - These are used by the engine. Scripts mainly use the properties
	//

	public QuestCameraComponent GetInstance() { return m_instance; }
	public void SetInstance(QuestCameraComponent instance) 
	{ 
		m_instance = instance; 
		m_instance.SetData(this);
	}
	public ICharacter GetCharacterToFollow() { return PowerQuest.Get.GetCharacter(m_characterToFollow); }
	public bool GetHasPositionOverride() { return m_hasPositionOverride; }

	public Vector2 GetPositionOverride() { return m_positionOverride; }
	public Vector2 GetPositionOverridePrev() { return m_positionOverridePrev; }

	public bool GetHasPositionOverrideOrTransition() 
	{ 
		if ( m_instance != null )
			return m_instance.GetHasPositionOverrideOrTransitioning(); 
		return m_hasPositionOverride; 
	}

	public void SetCharacterToFollow(ICharacter character, float overTime = 0) 
	{ 
		m_characterToFollow = character.ScriptName;
		if ( overTime > 0 ) 
		{
			SetPositionOverride(m_position,0); 
			ResetPositionOverride(0.6f); 
		}
		else if ( m_instance != null ) 
		{
			m_instance.Snap(); 
		}
	}
	public void SetPositionOverride	(float x, float y = 0, float transitionTime = 0 ) 
	{ 
		if ( m_hasPositionOverride )
			m_positionOverridePrev = m_positionOverride;
		m_hasPositionOverride = true; 
		m_positionOverride = new Vector2(x,y);  
		if ( m_instance != null )
			m_instance.OnOverridePosition(transitionTime);
	}
	public void SetPositionOverride	(Vector2 positionOverride, float transitionTime = 0 ) { SetPositionOverride(positionOverride.x, positionOverride.y, transitionTime); }
	public void ResetPositionOverride(float transitionTime = 0)
	{ 
		m_positionOverridePrev = new Vector2(float.MaxValue,float.MaxValue);
		m_hasPositionOverride = false;  
		if ( m_instance != null) 
			m_instance.OnOverridePosition(transitionTime); 
	}	

	/// Gets the current camera zoom (mulitplier on default/room vertical height)
	public float GetZoom() { return m_zoom > 0 ? m_zoom : 1; }
	/// Returns true if the camera has a zoom override
	public bool GetHasZoom() { return m_hasZoom; }
	public float GetZoomPrev() { return m_zoomPrev > 0 ? m_zoomPrev : 1; }
	/// Returns true if the camera's zoom is overriden, or if it's still transitioning back to default
	public bool GetHasZoomOrTransition() { return ( m_instance != null ) ? m_instance.GetHasZoomOrTransitioning() : m_hasZoom; }
	/// Sets a camera zoom (mulitplier on default/room vertical height)
	public void SetZoom(float zoom, float transitionTime = 0) // TODO: add transitions again
	{		
		if ( m_hasZoom )
			m_zoomPrev = m_zoom;
		m_hasZoom = true;
		m_zoom = zoom;
		if ( m_instance != null ) 
			m_instance.OnZoom(transitionTime); // TODO: add transitions again
	}
	/// Removes any zoom override, returning to the default/room vertical height
	public void ResetZoom(float transitionTime = 0)
	{ 
		m_zoomPrev = 1;
		m_hasZoom = false;
		if ( m_instance != null ) 
			m_instance.OnZoom(transitionTime);

	}

	public void Snap() { m_instance.Snap(); }

	// Returns the actual position of the camera
	public Vector2 GetPosition() { return m_position; }

	// Set position of camera object, usually you'd use SetPositionOverride to stop tracking a player.
	public void SetPosition(Vector2 position) { m_position = position; }

	// Returns the target position of the camera
	public Vector2 GetTargetPosition() { return m_targetPosition; }

	// Set position of camera object, usually you'd use SetPositionOverride to stop tracking a player.
	public void SetTargetPosition(Vector2 position) { m_targetPosition = position; }

	public bool GetSnappedLastUpdate() { return m_instance == null ? true : m_instance.GetSnappedLastUpdate(); }
	public bool GetTargetPosChangedLastUpdate() { return m_instance == null ? true : m_instance.GetTargetChangedLastUpdate(); }
		

	public void Shake(float intensity, float duration = 0.1f, float falloff = 0.15f)
	{
		m_instance.Shake(intensity, duration, falloff);
	}
	public void Shake(CameraShakeData data)
	{
		m_instance.Shake(data.m_intensity, data.m_duration, data.m_falloff);
	}


	//
	// Internal Functions
	//
}


}
