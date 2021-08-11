using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using System.Reflection;
using PowerTools;

namespace PowerTools.Quest
{

/// The main system where all the juicy goodness happens
public partial class PowerQuest : Singleton<PowerQuest>, ISerializationCallbackReceiver, IQuestScriptable, IPowerQuest
{	
	#region internal classes/definitions
	
	static readonly System.Type TYPE_COMPILERGENERATED = typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute);

	// Class for timers
	[System.Serializable] class Timer { public string n; public float t; }	

	// Variables in this class are all saved/restored automatically
	class SavedVarCollection
	{
		public SourceList m_occurances = new SourceList();
		public List<string> m_tempDisabledProps = new List<string>();
		public List<string> m_tempDisabledHotspots = new List<string>();
		public List<string> m_tempCursorNoneProps = new List<string>();
		public List<string> m_tempCursorNoneHotspots = new List<string>();
		public List<string> m_tempCursorNoneCursor = new List<string>();

		public List<string> m_currentInteractionOccurrances = new List<string>();
		public List<string> m_captureInputSources = new List<string>();
		public List<Timer> m_timers = new List<Timer>();
	}

	enum eInventoryClickStyle { SelectInventory, UseInventory, OnMouseClick }

	#endregion
	#region Variables: Inspector

	[Header("Settings")]

	[Tooltip("Defaults for settings that can be changed in game")]
	[SerializeField] QuestSettings m_settings = new QuestSettings();

	[Header("Project Setup")]

	[Tooltip("The default vertical resolution of the game. How many pixels high the camera view will be.")]
	[SerializeField] float m_verticalResolution = 180;
	[Tooltip("Whether camera and other things snap to pixel. For pixel art games")]
	[SerializeField] bool m_snapToPixel = true;
	[Tooltip("Whether to set up a pixel camera that renderes sprites at pixel resolution. For pixel art games")]
	[SerializeField] bool m_pixelCamEnabled = false;
	[Tooltip("Default pixels per unit that sprites are imported at")]
	[SerializeField] float m_defaultPixelsPerUnit = 1;
	[Tooltip("Whether character dialog text requires a click to advance, or dismisses after dialog's spoken/after time")]
	[SerializeField] bool m_sayTextAutoAdvance = true;
	[Tooltip("Whether display requires a click to advance, or dismisses after dialog's spoken/after time")]
	[SerializeField] bool m_displayTextAutoAdvance = false;
	[Tooltip("When clicking to skip text, ignore clicks until text has been shown for this time")]
	[SerializeField] float m_textNoSkipTime = 0.25f;	
	[Tooltip("Whether charaters stop walking automatically when they start talking")]
	[SerializeField] bool m_stopWalkingToTalk = true;

	[Header("What editor actions are available")]

	[SerializeField] bool m_enableUse = true;
	[SerializeField] bool m_enableLook = true;
	[SerializeField] bool m_enableInventory = true;
	[Tooltip("Whether clicking inventory results in 'Selecting' it Broken Sword style, or 'Using' it Lucasarts style, or specified in GlobalScript's OnMouseClick")]
	[SerializeField] eInventoryClickStyle m_inventoryClickStyle = eInventoryClickStyle.OnMouseClick;		

	[Header("Other Defaults")]

	[Tooltip("Increase when the data you're saving changes")]
	[SerializeField] int m_saveVersion = 0;
	[Tooltip("Increase when you can no longer save games of a specific version. After launch you should avoid increasing this if possible or player's save files get invalidated!")]
	[SerializeField] int m_saveVersionRequired = 0;
	[SerializeField] QuestMenuManager m_menuManager = null;
	[SerializeField] QuestCursorComponent m_cursorPrefab = null;
	[SerializeField] QuestCameraComponent m_cameraPrefab = null;
	[Tooltip("Prefab for displayed above character")]
	[SerializeField] QuestText m_dialogTextPrefab = null;
	[Tooltip("Global offset of dialog text (above character sprite)")]
	[SerializeField] Vector2 m_dialogTextOffset = Vector2.zero;

	[Header("Spawnables")]

	[SerializeField] List<GameObject> m_spawnablePrefabs = new List<GameObject>();

	[Header("Text Sprite setup")]

	public Material m_textSpriteMaterial = null;
	[ReorderableArrayAttribute]
	public QuestText.TextSpriteData[] m_textSprites = null;

	[Header("Other systems to create")]
	[SerializeField] List<Component> m_systems = null;
		
	[Header("Prefab Lists (read only, enable debug inspector to edit)")]

	[SerializeField, ReadOnly] List<RoomComponent> m_roomPrefabs = new List<RoomComponent>();
	[SerializeField, ReadOnly] List<CharacterComponent> m_characterPrefabs = new List<CharacterComponent>();
	[SerializeField, ReadOnly] List<GuiComponent> m_guiPrefabs = new List<GuiComponent>();
	[SerializeField, ReadOnly] List<DialogTreeComponent> m_dialogTreePrefabs = new List<DialogTreeComponent>();
	[SerializeField, ReadOnly] List<InventoryComponent> m_inventoryPrefabs = new List<InventoryComponent>();
	//[Header("Inventory Animations (read only)")]
	[SerializeField, ReadOnly] List<AnimationClip> m_inventoryAnimations = new List<AnimationClip>();

	[Header("Pre-loaded Shaders")]

	#pragma warning disable 414
	[SerializeField] Shader[] m_preloadedShaders = null;
	#pragma warning restore 414

	// Used when upgrading powerquest
	[SerializeField, HideInInspector] int m_version = -1;

	// Version checking vars
	[SerializeField, HideInInspector] int m_newVersion = -1;

	//
	// Private variables
	//
	#endregion
	#region Variables: Private

	QuestScript m_globalScript = null;
	QuestCamera m_cameraData = new QuestCamera();
	UnityEngine.Camera m_cameraGui = null;
	Canvas m_canvas = null;
	QuestCursor m_cursor = null;
	Character m_player = null;
	List<Character> m_characters = new List<Character>();
	List<Inventory> m_inventoryItems = new List<Inventory>();
	List<DialogTree> m_dialogTrees = new List<DialogTree>();
	List<Gui> m_guis = new List<Gui>();
	List<Room> m_rooms = new List<Room>();

	Room m_currentRoom = null;
	#pragma warning disable 414
	DialogOption m_dialogOptionSelected = null; // Will probably use this later
	#pragma warning restore 414
	DialogTree m_currentDialog = null;
	DialogTree m_previousDialog = null;
		
	bool m_skipCutscene = false;		// If true, coroutines will skip any waiting and get to the end
	bool m_skipDialog = false;			// If true talk/wait coroutines will skip
	bool m_blocking = false;			// True when other interactions cannot be performed
	bool m_transitioning = false;		// True when transitioning between rooms
	bool m_roomLoopStarted = false;		// True while mainloop is running (after transition and OnRoomEnterAfterFadein)
	bool m_cutscene = false;			// True when in a cutscene
	bool m_initialised = false; 	    // Flag set true after first ever awake has finished and all questobjects have been created 
	bool m_walkClickDown = false;       // Flag set when clicked to walk and reset when mouse button released
	bool m_guiConsumedClick = false;	 // Flags "game" mouse clicks to be ignored, resets after every update. NB: No longer necessary (AFAIK)
	bool m_sequenceIsCancelable = false; // If true, the current sequence will be stopped on click
	bool m_allowEnableCancel = false; 	// Used to determine if rest of sequence can be safely skipped
	bool m_serializeComplete = false;
	bool m_restoring = false;
	bool m_displayActive = false;		// True when Display dialog is visible or audio is playing (triggered by Display or DisplayBG)
	bool m_skipCutsceneButtonConsumed = false; // true when skip cutscene button is pressed, and waiting for it to be released (so don't process "held down esc" events)
	bool m_waitingForBGDialogSkip = false; // Used for "WaitForDialogSkip", which turns any background dialog into foreground dialog. Experimental feature.
	bool m_restartOnUpdate = false;	// Flag used to restart at start of next update
	bool m_debugRestartCurrentRoom = false; // Flag used to restart in current room at start of next update

	// Extra snapping tweaks
	bool m_useFancyParallaxSnapping = true;
	public bool UseFancyParalaxSnapping { get { return m_useFancyParallaxSnapping; } set { m_useFancyParallaxSnapping = value; } }

	// These mouse clicks are for the blocking update, which 
	//bool m_leftClick = false;		
	//bool m_rightClick = false;
	bool m_leftClickPrev = false;
	bool m_rightClickPrev = false;
	bool m_overrideMousePos = false;
	Vector2 m_mousePos = Vector2.zero;
	float m_textSpeedMultiplier = 1;
	float m_timeLastTextShown = 0;

	Coroutine m_currentSequence = null;	// Main active sequence
	List<Coroutine> m_queuedScriptInteractions = new List<Coroutine>();
	Coroutine m_coroutineMainLoop = null;	// Stored so that when changing scenes, the main loop coroutine can be stopped, and then restarted in next room.
	IEnumerator m_coroutineSay = null;
	bool m_levelLoadedCalled = false; 	// So that OnLevelLoad code can be only handled once
	bool m_overrideMouseOverClickable = false;
	IQuestClickable m_mouseOverClickable = null; // What the mouse is hovering over
	IQuestClickable m_lastClickable = null; // Used for accessing "WalkTo" and "LookAt" of current interaction
	string m_mouseOverDescriptionOverride = null;

	AudioHandle m_dialogAudioSource = null;
	SavedVarCollection m_savedVars = new SavedVarCollection();
	SavedVarCollection SV {get {return m_savedVars;}} // Shortcut to saved variables collection

	//
	// Variables for canceling current sequences
	//
	Coroutine m_backgroundSequence = null;	// Cancelable sequence
	List<Coroutine> m_currentSequences = new List<Coroutine>(); // List of current sequence coroutines. Used for canceling sequences
	List<Coroutine> m_backgroundSequences = new List<Coroutine>(); // ListOf current cancelable coroutines
	// Cached current interaction/verb for rolling back, incase edited before first (cancelable) WalkTo
	List<IQuestClickable>m_currentInteractionClickables = new List<IQuestClickable>();
	List<eQuestVerb> m_currentInteractionVerbs = new List<eQuestVerb>();


	// Hot-loading stuff
	static Assembly m_hotLoadAssembly = null; // static because I don't want it to reset when resetting game for debugging
	// List<IQuestScriptable> m_hotLoadedScriptables = null; // for now just recompiling all scriptables

	// Stuff for auto-loading scripts based on current interaction running
	static readonly string STR_UNHANDLED = "Unhandled";
	IQuestScriptable m_autoLoadScriptable = null;
	string m_autoLoadFunction = string.Empty;
	IQuestScriptable m_autoLoadUnhandledScriptable = null;
	string m_autoLoadUnhandledFunction = string.Empty;

	Coroutine m_consumedInteraction = null;

	#endregion
	#region Functions: Implementing IPowerQuest

	/// Breaks instantly, rather than yielding for a frame
	public YieldInstruction Break { get { return EMPTY_YIELD_INSTRUCTION; } } 
	/// Breaks instantly, and but registers as having "done something", so player won't say "I can't do that"
	public YieldInstruction ConsumeEvent { get { return CONSUME_YIELD_INSTRUCTION; } } 

	/// Convenient shortcut to the game camera
	public ICamera Camera	{ get{ return GetCamera(); } }
	/// Convenient shortcut to the Cursor
	public ICursor Cursor { get{ return GetCursor(); } }

	/// Restart the game from the first scene
	public void Restart()
	{
		m_restartOnUpdate = true;
		// Destroy everything, including powerquest, and load the game scene again
		StopAllCoroutines();
	}

	// Wait for time (or default 0.5 sec)
	public Coroutine Wait(float time = 0.5f)	{	return StartQuestCoroutine(CoroutineWaitForTime(time, false)); }
	// Wait for time (or default 0.5 sec). Pressing button will skip the waiting
	public Coroutine WaitSkip(float time = 0.5f)	{	return StartQuestCoroutine(CoroutineWaitForTime(time, true)); }

	/// Invokes the specified function after the specified time has elapsed (non-blocking). EG: `E.DelayedInvoke(1, ()=/>{ C.Player.FaceLeft(); } );`
	public void DelayedInvoke( float time, System.Action functionToInvoke ) { StartQuestCoroutine(CoroutineDelayedInvoke(time, functionToInvoke)); }

	/// <summary>
	/// Use this when you want to yield to another function that returns an IEnumerator
	/// Usage: yield return E.WaitFor( SimpleExampleFunction ); or yield return E.WaitFor( ()=>ExampleFunctionWithParams(C.Dave, "lol") );
	/// </summary>
	/// 
	/// <param name="functionToWaitFor">A function that returns IEnumerator. Eg: "SimpleExampleFunction" or, "()=/>ExampleFunctionWithParams(C.Dave, 69)" if it has params</param>
	public Coroutine WaitFor( DelegateWaitForFunction functionToWaitFor ) 
	{ 
		// update the script to auto-load (ignored when not in editor)
		if ( Application.isEditor && functionToWaitFor != null && functionToWaitFor.Target != null )
		{			
			// Search through all scriptables and find one with matching classname
			List<IQuestScriptable> scriptables = PowerQuest.Get.GetAllScriptables();
			string classname = functionToWaitFor.Target.GetType().Name;
			IQuestScriptable scriptable = scriptables.Find(item=>item.GetScriptClassName() == classname);
			// Set the auto-laod script. TODO: Once the coroutine has finished, return to the previous script.
			if ( scriptable != null )
			{
				// If function is called like this: WaitFor(()=>MyFunction(34); The function name will be dynamic, eg. "<OnInteractDoor>b__20_0" don't think we can auto-load it.				
				//if ( functionToWaitFor.Method.IsSpecialName == false && functionToWaitFor.Method.Name[0] == '<') // check it's not dynamic lambda expression method. Can't auto-load these.
				if ( System.Attribute.IsDefined(functionToWaitFor.Method, TYPE_COMPILERGENERATED) == false )
					SetAutoLoadScript( scriptable, functionToWaitFor.Method.Name, true );
			}	
		}		
		return StartQuestCoroutine(functionToWaitFor()); 
	}
	public Coroutine WaitWhile( System.Func<bool> condition, bool skippable = false ) { return StartQuestCoroutine(CoroutineWaitWhile(condition, skippable)); }
	public Coroutine WaitUntil( System.Func<bool> condition, bool skippable = false ) { return StartQuestCoroutine(CoroutineWaitUntil(condition, skippable)); }
	public Coroutine WaitForDialog() {	return StartQuestCoroutine(CoroutineWaitForDialog()); }

	//
	// Narrator 
	//

	public Coroutine Display( string dialog, int id = -1 ) 
	{ 
		if ( m_coroutineSay != null )
		{
			StopCoroutine(m_coroutineSay);
			EndDisplay();
		}
		m_coroutineSay = CoroutineDisplay(dialog,id);
		return StartCoroutine(m_coroutineSay); 
	}
	public Coroutine DisplayBG( string dialog, int id = -1 ) 
	{ 
		if ( m_coroutineSay != null )
		{
			StopCoroutine(m_coroutineSay);
			EndDisplay();
		}
		m_coroutineSay = CoroutineDisplayBG(dialog,id);
		StartCoroutine(m_coroutineSay); 
		return StartCoroutine(CoroutineEmpty()); // Start a coroutine so can wait a frame
	}

	//
	// Cutscenes
	//

	public void StartCutscene() { m_cutscene = true; }
	public void EndCutscene() { OnEndCutscene(); }

	//
	// Screen transitions (fading to/from a color)
	//

	public Coroutine FadeIn( float time = 0.2f, bool skippable = true )	{	return StartCoroutine(CoroutineFadeIn(DEFAULT_FADE_SOURCE, time, skippable)); }
	public Coroutine FadeOut( float time = 0.2f, bool skippable = true )	{	return StartCoroutine(CoroutineFadeOut(DEFAULT_FADE_SOURCE, time, skippable)); }
	public Coroutine FadeIn( string source, float time = 0.2f, bool skippable = true )	{	return StartCoroutine(CoroutineFadeIn(source, time, skippable)); }
	public Coroutine FadeOut( string source, float time = 0.2f, bool skippable = true )	{	return StartCoroutine(CoroutineFadeOut(source, time, skippable)); }

	public void FadeInBG( float time = 0.2f ) {	m_menuManager.FadeIn(time, DEFAULT_FADE_SOURCE);}
	public void FadeOutBG( float time = 0.2f ){	m_menuManager.FadeOut(time, DEFAULT_FADE_SOURCE);}
	public void FadeInBG( float time, string source ) {	m_menuManager.FadeIn(time, source);}
	public void FadeOutBG( float time, string source ){	m_menuManager.FadeOut(time, source);}

	public bool GetFading() { return m_menuManager.GetFading(); }
	public Color FadeColor { get{return m_menuManager.FadeColor;} set{ m_menuManager.FadeColor = value;} }
	public void FadeColorRestore() { m_menuManager.FadeColorRestore(); }

	public float GetFadeRatio() {return m_menuManager.GetFadeRatio();}
	public Color GetFadeColor() {return m_menuManager.GetFadeColor();}

	public QuestMenuManager GetMenuManager() { return m_menuManager; }

	//
	// Pause/Unpause the game
	//

	public bool Paused 
	{ 
		get { return SystemTime.Paused; } 

		set 
		{ 
			if ( value ) 
				Pause(); 
			else 
				UnPause(); 
		} 
	}

	public void Pause(string source = null)
	{
		if ( SystemTime.HasInstance() )
			SystemTime.Get.PauseGame(source);
	}

	public void UnPause(string source = null)
	{
		if ( SystemTime.HasInstance() )
			SystemTime.Get.UnPauseGame(source);
	}

	//
	// Start/Stop timers
	//

	/// Starts timer with a *name*, counting down from `time` in seconds. Set to zero to remove timer
	public void SetTimer(string name, float time)
	{
		Timer timer = SV.m_timers.Find(item=> item.n.Equals(name, System.StringComparison.OrdinalIgnoreCase  ) );

		// Setting to zero removes timer
		if ( time <= 0 )
		{
			if ( timer != null )
				SV.m_timers.Remove(timer);
			return;
		}

		if ( timer == null)
		{
			timer = new Timer(){n = name, t = time};
			SV.m_timers.Add(timer);
		}		
		
		timer.t = time;		
	}
	/// Checks whether the timer with specified `name` has expired. If the timeout set with SetTimer has elapsed, returns *true*. Otherwise, returns *false*.
	public bool GetTimerExpired(string name)
	{
		Timer timer = SV.m_timers.Find(item=> item.n.Equals(name, System.StringComparison.OrdinalIgnoreCase  ) );
		if ( timer != null && timer.t <= 0 )
		{
			SV.m_timers.Remove(timer);
			return true;
		}
		return false;
	}
	
	///	Returns timer value 	
	public float GetTimer(string name)
	{
		Timer timer = SV.m_timers.Find(item=> item.n.Equals(name, System.StringComparison.OrdinalIgnoreCase  ) );		
		return (timer != null && timer.t > 0) ? timer.t : 0;
	}

	//
	// Change room
	//


	/// Change the current room. Alternative to C.Player.Room = room;
	public void ChangeRoomBG( IRoom room ) { GetPlayer().Room = room; }

	/// Change the current room. Can be yielded too, and blocks until after OnEnterAfterFade of the new room finishes. NB: This does NOT currently work in Unity 2019 and later.
	public Coroutine ChangeRoom( IRoom room ) { return StartCoroutine( CoroutineChangeRoom(room) ); }


	//
	// Access to Quest Objects (rooms, characters, inventory, dialog, guis)
	//

	/// Returns the quest script matching the object type passed in. Eg: E.GetScript<RoomKitchen>().m_drawerOpen = true;
	public T GetScript<T>() where T : QuestScript
	{ 
		string scriptName = typeof(T).ToString();
		IQuestScriptable scriptable = null;
		System.Type type = typeof(T);
		if ( type.IsSubclassOf(typeof(RoomScript<T>)) )
		{
			scriptName = scriptName.Substring(4);
			scriptable = GetRoom(scriptName);
		}
		else if ( type.IsSubclassOf(typeof(CharacterScript<T>)) )
		{
			scriptName = scriptName.Substring(9);
			scriptable = GetCharacter(scriptName);
		}
		else if ( type.IsSubclassOf(typeof(DialogTreeScript<T>)) )
		{
			scriptName = scriptName.Substring(6);
			scriptable = GetDialogTree(scriptName);
		}
		else if ( type.IsSubclassOf(typeof(InventoryScript<T>)) )
		{
			scriptName = scriptName.Substring(9);
			scriptable = GetInventory(scriptName);
		}
		else if ( type.ToString() == GLOBAL_SCRIPT_NAME ) // type.IsSubclassOf(typeof(GlobalScriptBase<T>)) )
		{
			scriptName = GLOBAL_SCRIPT_NAME;
			scriptable = this;
		}
		if ( scriptable != null && scriptable.GetScript() != null )
			return scriptable.GetScript() as T;

		return null;
	}

	public Room GetCurrentRoom() { return m_currentRoom; }
	public Room GetRoom(string scriptName) 
	{ 
		Room result = m_rooms.Find(item=>string.Equals(item.ScriptName, scriptName, System.StringComparison.OrdinalIgnoreCase )); 
		if ( result == null && string.IsNullOrEmpty(scriptName) == false )
			Debug.LogError("Room doesn't exist: "+scriptName+". Check for typos and that it's added to PowerQuest");
		return result;
	}

	public ICharacter Player { get { return m_player; } set { SetPlayer(value); } }
	public Character GetPlayer() { return m_player; }
	public void SetPlayer(ICharacter character) 
	{ 
		Character newPlayer = GetCharacter(character.ScriptName);
		m_player = newPlayer;
		GetCamera().SetCharacterToFollow(newPlayer);
		ChangeRoomBG(m_player.Room);
	}
	public Character GetCharacter(string scriptName) { return m_characters.Find(item=>string.Equals(item.ScriptName, scriptName, System.StringComparison.OrdinalIgnoreCase )); }
	/// Shortcut to the current player's active inventory  
	public IInventory ActiveInventory { get { return m_player.ActiveInventory; } set { m_player.ActiveInventory = value; } }
	public Inventory GetInventory(string scriptName) { return m_inventoryItems.Find(item=>string.Equals(item.ScriptName, scriptName, System.StringComparison.OrdinalIgnoreCase )); }
	public DialogTree GetCurrentDialog() { return m_currentDialog; }	
	public DialogTree GetPreviousDialog() { return m_previousDialog; }
	public DialogTree GetDialogTree(string scriptName) { return m_dialogTrees.Find(item=>string.Equals(item.ScriptName, scriptName, System.StringComparison.OrdinalIgnoreCase)); }
	public Gui GetGui(string scriptName) { return m_guis.Find(item=>string.Equals(item.ScriptName, scriptName, System.StringComparison.OrdinalIgnoreCase)); }
	public GameObject GetSpawnablePrefab(string name)
	{
		return m_spawnablePrefabs.Find(item=>string.Equals(name, item.name, System.StringComparison.OrdinalIgnoreCase));
	}

	//
	// Access to useful system data
	//

	public UnityEngine.Camera GetCameraGui() { return m_cameraGui; }
	public Canvas GetCanvas() { return m_canvas; }
	public Vector2 GetMousePosition() { return m_mousePos; }
	public void SetMousePositionOverride(Vector2 mousePos) { m_overrideMousePos = true; m_mousePos = mousePos; }
	public void ResetMousePositionOverride() { m_overrideMousePos = false; } 
	public IQuestClickable GetMouseOverClickable() { return m_mouseOverClickable; }
	public eQuestClickableType GetMouseOverType() { return m_mouseOverClickable == null ? eQuestClickableType.None : m_mouseOverClickable.ClickableType; }
	public void SetMouseOverClickableOverride( IQuestClickable clickable ) { m_overrideMouseOverClickable = true; m_mouseOverClickable = clickable; }
	public void ResetMouseOverClickableOverride() { m_overrideMouseOverClickable = false; m_mouseOverClickable = null; } 
	public string GetMouseOverDescription() 
	{ 
		if ( m_mouseOverDescriptionOverride != null )
			return m_mouseOverDescriptionOverride;		
		return (m_mouseOverClickable != null) ? m_mouseOverClickable.Description : string.Empty; 
	}
	public Vector2 GetLastLookAt() { return (m_lastClickable == null || m_lastClickable.Instance == null) ? Vector2.zero : (m_lastClickable.LookAtPoint + (Vector2)m_lastClickable.Instance.transform.position); }
	public Vector2 GetLastWalkTo() { return (m_lastClickable == null || m_lastClickable.Instance == null) ? Vector2.zero : (m_lastClickable.WalkToPoint + (Vector2)m_lastClickable.Instance.transform.position); }

	/// Returns the current vertical resolution (using room override if one exists)
	public float VerticalResolution { get 
		{
			if ( m_currentRoom != null && m_currentRoom.VerticalResolution > 0 )
				return m_currentRoom.VerticalResolution;				
			return m_verticalResolution; 
		} 
	}
	public float DefaultVerticalResolution { get { return m_verticalResolution; } }

	//
	// Settings
	//

	// Shortcut to access settings
	public QuestSettings Settings { get { return m_settings; } }

	public void SetTextSpeedMultiplier(float multiplier) { m_textSpeedMultiplier = multiplier; }
	public float GetTextSpeedMultiplier() { return m_textSpeedMultiplier; }

	//
	// Functions for handling mouse clicks on things
	//

	/// Starts the specified action for the verb on whatever the mouse is over (whatever the current GetMouseOverClickable() happens to be ). 
	/**
	 * This would usually be called from the OnMouseClick function in your global script
	 */
	// Processes click on whatever the current mouse is over
	public bool ProcessClick( eQuestVerb verb ) { return ProcessClick(verb, m_mouseOverClickable, m_mousePos); }
	// Processes click on a specified clickable and/or mouse position
	public bool ProcessClick( eQuestVerb verb, IQuestClickable clickable, Vector2 mousePosition )
	{
		bool interactionFound = false;
		bool clickHandled = false;
		GameObject clickedObj = (clickable == null || clickable.Instance == null) ? null : (clickable.Instance.gameObject);

		// 
		// Call "OnAnyClick" first
		//
		if ( clickHandled == false )
		{
			// OnAnyClick is first checked in global script and in the current room. So can be used to interrupt what would normally cause an action
			if ( clickHandled == false )
				clickHandled = StartScriptInteraction(m_currentRoom, "OnAnyClick" );
			if ( clickHandled == false )
				clickHandled = StartScriptInteraction( this, "OnAnyClick" );

			if ( clickHandled )
			{
				m_queuedScriptInteractions.Add(m_currentSequence);
				interactionFound = true;
			}
		}

		if ( clickHandled == false && verb == eQuestVerb.None )
		{
			clickHandled = true;
		}

		if ( clickHandled == false && verb == eQuestVerb.Walk )
		{
			// Cancel any current interaction.
			OnInteraction(null,eQuestVerb.Walk);

			// Handle walk to clicked
			{
				// Check for "OnWalkTo" interrupts
				if ( clickHandled == false )
					clickHandled = StartScriptInteraction(m_currentRoom, "OnWalkTo" );
				if ( clickHandled == false )
					clickHandled = StartScriptInteraction( this, "OnWalkTo" );
				if ( clickHandled )
				{
					m_queuedScriptInteractions.Add(m_currentSequence); 
					interactionFound = true;
				}
			}

			if ( clickHandled == false )
			{
				// Walk to the current mouse position	
				m_player.WalkToBG(mousePosition);
				if ( mousePosition == m_mousePos ) // don't "hold down walk" if passed in position isn't just the mouse pos (otherwise it'll be overridden next update).
					m_walkClickDown = true; 
				clickHandled = true;
				interactionFound = true;
			}
		}

		//
		// Action the verb
		//
		

		// Click character
		if ( clickedObj != null && clickHandled == false )
		{							
			CharacterComponent component = clickedObj.GetComponent<CharacterComponent>();
			if ( component != null ) 
			{
				m_lastClickable = clickable;
				OnInteraction(m_lastClickable,verb);
				// First try in room
				{
					switch ( verb )
					{
						case eQuestVerb.Inventory: { clickHandled = StartScriptInteraction( m_currentRoom.GetScriptable(), SCRIPT_FUNCTION_USEINV_CHARACTER+m_lastClickable.ScriptName, new object[] {component.GetData(), m_player.ActiveInventory}, true ); } break;
						case eQuestVerb.Look: { clickHandled = StartScriptInteraction( m_currentRoom.GetScriptable(), SCRIPT_FUNCTION_LOOKAT_CHARACTER+m_lastClickable.ScriptName, new object[] {component.GetData()}, true ); } break;
						case eQuestVerb.Use: { clickHandled = StartScriptInteraction( m_currentRoom.GetScriptable(), SCRIPT_FUNCTION_INTERACT_CHARACTER+m_lastClickable.ScriptName, new object[] {component.GetData()}, true ); } break;
					}
				}
				if ( clickHandled == false )
				{
					// If didn't find override in room script, try in character script
					switch ( verb )
					{
					case eQuestVerb.Inventory: { clickHandled = StartScriptInteraction( m_lastClickable.GetScriptable(), SCRIPT_FUNCTION_USEINV, new object[] {m_player.ActiveInventory}, true ); } break;
					case eQuestVerb.Look: { clickHandled = StartScriptInteraction( m_lastClickable.GetScriptable(), SCRIPT_FUNCTION_LOOKAT, null, true ); } break;
					case eQuestVerb.Use: { clickHandled = StartScriptInteraction( m_lastClickable.GetScriptable(), SCRIPT_FUNCTION_INTERACT, null, true ); } break;
					}
				}
				if ( clickHandled )
				{
					m_queuedScriptInteractions.Add(m_currentSequence);
					interactionFound = true;
				}
			}
		}

		// Click hotspot
		if ( clickedObj != null && clickHandled == false )
		{
			// try hotspot
			HotspotComponent component = clickedObj.GetComponent<HotspotComponent>();
			if ( component != null ) 
			{
				m_lastClickable = clickable;
				OnInteraction(m_lastClickable,verb);
				if ( verb == eQuestVerb.Inventory && m_player.HasActiveInventory )
					clickHandled = StartScriptInteraction( m_lastClickable.GetScriptable(), PowerQuest.SCRIPT_FUNCTION_USEINV_HOTSPOT + m_lastClickable.ScriptName, new object[] {component.GetData(), m_player.ActiveInventory}, true );
				else 
					clickHandled = StartScriptInteraction( m_lastClickable.GetScriptable(), (verb != eQuestVerb.Look ? SCRIPT_FUNCTION_INTERACT_HOTSPOT : SCRIPT_FUNCTION_LOOKAT_HOTSPOT) + m_lastClickable.ScriptName, new object[] {component.GetData()}, true );
				if ( clickHandled )
				{
					m_queuedScriptInteractions.Add(m_currentSequence);
					interactionFound = true;
				}
			}
		}

		// Click prop
		if ( clickedObj != null && clickHandled == false )
		{
			// try prop
			PropComponent component = clickedObj.GetComponent<PropComponent>();
			if ( component != null )
			{
				m_lastClickable = clickable;
				OnInteraction(m_lastClickable,verb);
				if ( verb == eQuestVerb.Inventory && m_player.HasActiveInventory )
					clickHandled = StartScriptInteraction( m_lastClickable.GetScriptable(), PowerQuest.SCRIPT_FUNCTION_USEINV_PROP + m_lastClickable.ScriptName, new object[] {component.GetData(), m_player.ActiveInventory}, true );
				else 
					clickHandled = StartScriptInteraction( m_lastClickable.GetScriptable(), (verb != eQuestVerb.Look ? SCRIPT_FUNCTION_INTERACT_PROP : SCRIPT_FUNCTION_LOOKAT_PROP) + m_lastClickable.ScriptName, new object[] {component.GetData()}, true );
				if ( clickHandled )
				{
					m_queuedScriptInteractions.Add(m_currentSequence);
					interactionFound = true;
				}
			}
		}
		
		// Click inventory item - Only if inventory click style is "OnMouseClick"
		if ( clickHandled == false && m_inventoryClickStyle == eInventoryClickStyle.OnMouseClick)
		{				
			if ( clickable.ClickableType == eQuestClickableType.Inventory )
			{
				m_lastClickable = clickable;
				OnInteraction(m_lastClickable,verb);
				if ( verb == eQuestVerb.Inventory && m_player.HasActiveInventory )
				{
					// clicked one inventory item on another
					clickHandled = StartScriptInteraction( m_lastClickable.GetScriptable(), PowerQuest.SCRIPT_FUNCTION_USEINV_INVENTORY, new object[] {clickable as IInventory, m_player.ActiveInventory}, true );
					// If click wasn't handled, try swapping the items
					if ( clickHandled == false )
						clickHandled = StartScriptInteraction( m_player.ActiveInventory as IQuestScriptable, PowerQuest.SCRIPT_FUNCTION_USEINV_INVENTORY, new object[] {m_player.ActiveInventory, clickable as IInventory}, true );					
				}
				else 
				{
					clickHandled = StartScriptInteraction( m_lastClickable.GetScriptable(), (verb != eQuestVerb.Look ? SCRIPT_FUNCTION_INTERACT_INVENTORY : SCRIPT_FUNCTION_LOOKAT_INVENTORY), new object[] {clickable as IInventory}, true );
				}
				if ( clickHandled )
				{
					m_queuedScriptInteractions.Add(m_currentSequence);
					interactionFound = true;
				}
			}
		}
		
		
		//
		// Clicked something, but no sequence was started, so fallback to UnhandledEvent
		//
		if ( clickable != null && clickHandled == false )
		{
			// Unhandled event
			//StartScriptInteraction(m_gameScript, verb == eQuestVerb.Use ? "UnhandledInteract" : "UnhandledLookAt" );

			if ( m_globalScript != null )
			{								
				string methodName = "";
				object[] parameters = null;
				if ( verb == eQuestVerb.Inventory && m_player.HasActiveInventory )
				{
					if ( clickable.ClickableType == eQuestClickableType.Inventory ) // use items on eachother
					{
						methodName = "UnhandledUseInvInv";
						parameters = new object[]{ clickable, m_player.ActiveInventory };							
					}
					else  // use items on other things
					{
						methodName = "UnhandledUseInv";
						parameters = new object[]{ clickable, m_player.ActiveInventory };
					}
				}
				else // other clicks on things
				{
					methodName = verb != eQuestVerb.Look ? "UnhandledInteract" : "UnhandledLookAt";
					parameters = new object[]{ clickable };
				}

				if ( clickHandled == false )
					clickHandled = StartScriptInteraction( this, methodName, parameters, true );

				if ( clickHandled )
				{
					m_queuedScriptInteractions.Add(m_currentSequence);
					interactionFound = true;
				}
			}
		}
		if ( CallbackOnProcessClick != null )
			CallbackOnProcessClick.Invoke(interactionFound);
		return interactionFound;
	}

	//
	// Functions that let scripts call other scripts interaction functions
	//
	public Coroutine HandleInteract( IHotspot target )
	{			
		OnHandleInteraction(target.IClickable, eQuestVerb.Use);
		return StartScriptInteractionCoroutine( target.IClickable.GetScript(), SCRIPT_FUNCTION_INTERACT_HOTSPOT + target.ScriptName, new object[] {target}, true );
	}
	public Coroutine HandleLookAt( IHotspot target )
	{
		OnHandleInteraction(target.IClickable, eQuestVerb.Look);
		return StartScriptInteractionCoroutine( target.IClickable.GetScript(), SCRIPT_FUNCTION_LOOKAT_HOTSPOT + target.ScriptName, new object[] {target}, true );		
	}
	public Coroutine HandleInventory( IHotspot target, IInventory item )
	{
		OnHandleInteraction(target.IClickable, eQuestVerb.Inventory);
		return StartScriptInteractionCoroutine( target.IClickable.GetScript(), PowerQuest.SCRIPT_FUNCTION_USEINV_HOTSPOT + target.ScriptName, new object[] {target, item}, true );
	}
	public Coroutine HandleInteract( IProp target )
	{
		OnHandleInteraction(target.IClickable, eQuestVerb.Use);
		return StartScriptInteractionCoroutine( target.IClickable.GetScript(), SCRIPT_FUNCTION_INTERACT_PROP + target.ScriptName, new object[] {target}, true );
	}
	public Coroutine HandleLookAt( IProp target )
	{
		OnHandleInteraction(target.IClickable, eQuestVerb.Look);
		return StartScriptInteractionCoroutine( target.IClickable.GetScript(), SCRIPT_FUNCTION_LOOKAT_PROP + target.ScriptName, new object[] {target}, true );
	}
	public Coroutine HandleInventory( IProp target, IInventory item )	
	{
		OnHandleInteraction(target.IClickable, eQuestVerb.Inventory);
		return StartScriptInteractionCoroutine( target.IClickable.GetScript(), PowerQuest.SCRIPT_FUNCTION_USEINV_PROP + target.ScriptName, new object[] {target, item}, true );	
	}
	public Coroutine HandleInteract( ICharacter target )	
	{ 
		OnHandleInteraction(target.IClickable, eQuestVerb.Use);
		// first try in room script, then fall back to character script
		Coroutine result = StartScriptInteractionCoroutine( m_currentRoom.GetScriptable().GetScript(), PowerQuest. SCRIPT_FUNCTION_INTERACT_CHARACTER+target.ScriptName, new object[] {target}, true ); 
		if ( result != null )
			return result;
		return StartScriptInteractionCoroutine( target.IClickable.GetScript(), PowerQuest.SCRIPT_FUNCTION_INTERACT, null, true ); 
	}
	public Coroutine HandleLookAt( ICharacter target )	
	{ 
		OnHandleInteraction(target.IClickable, eQuestVerb.Look);
		// first try in room script, then fall back to character script
		Coroutine result = StartScriptInteractionCoroutine( m_currentRoom.GetScriptable().GetScript(), PowerQuest. SCRIPT_FUNCTION_LOOKAT_CHARACTER+target.ScriptName, new object[] {target}, true ); 
		if ( result != null )
			return result;
		return StartScriptInteractionCoroutine( target.IClickable.GetScript(), PowerQuest.SCRIPT_FUNCTION_LOOKAT, null, true ); 
	}
	public Coroutine HandleInventory( ICharacter target, IInventory item ) 
	{ 
		OnHandleInteraction(target.IClickable, eQuestVerb.Inventory);
		// first try in room script, then fall back to character script
		Coroutine result = StartScriptInteractionCoroutine( m_currentRoom.GetScriptable().GetScript(), PowerQuest. SCRIPT_FUNCTION_USEINV_CHARACTER+target.ScriptName, new object[] {target,item}, true ); 
		if ( result != null )
			return result;
		return StartScriptInteractionCoroutine( target.IClickable.GetScript(), PowerQuest.SCRIPT_FUNCTION_USEINV, new object[] {item}, true ); 
	}
	public Coroutine HandleInteract( IInventory target )	{ return StartScriptInteractionCoroutine( target.Data.GetScript(), PowerQuest.SCRIPT_FUNCTION_INTERACT_INVENTORY, new object[] {target}, true ); }
	public Coroutine HandleLookAt( IInventory target ) 		{ return StartScriptInteractionCoroutine( target.Data.GetScript(), PowerQuest.SCRIPT_FUNCTION_LOOKAT_INVENTORY, new object[] {target}, true ); }
	public Coroutine HandleInventory( IInventory target, IInventory item ) { return StartScriptInteractionCoroutine( target.Data.GetScript(), PowerQuest.SCRIPT_FUNCTION_USEINV_INVENTORY, new object[] {target, item}, true ); }

	//
	// Misc utilities
	//
	
	/// Get whether canceling the current sequence is enabled
	public bool GetCanCancel()
	{
		return m_sequenceIsCancelable;
	}

	/// Enables canceling in the current sequence. This would potentially cause lots of issues with half-run functions, so not recommended.
	public void EnableCancel() 
	{
		if ( m_sequenceIsCancelable == false && m_allowEnableCancel )
		{
			EnableCancelInternal();

			// Restart the main loop
			StopCoroutine(m_coroutineMainLoop);
			m_coroutineMainLoop = StartCoroutine( MainLoop() );
		}
	}

	/// Disables canceling during the current sequence
	public void DisableCancel() 
	{ 
		if ( m_sequenceIsCancelable && m_backgroundSequence != null )
		{
			// swap background sequenct to be the current sequence
			m_currentSequence = m_backgroundSequence;
			m_backgroundSequence = null;

			// Swap coroutines lists back
			m_currentSequences = m_backgroundSequences;
			m_backgroundSequences.Clear();

		}
		m_sequenceIsCancelable = false; 
		m_allowEnableCancel = false;
	}

	// Advanced function- allows you to cancel current sequence in progress. Use to interupt player interactions when something else happens (eg: on trigger or something in UpdateBlocking)
	public void CancelCurrentInteraction()
	{
		if ( m_sequenceIsCancelable && m_backgroundSequence != null )
		{
			// Rollback sequence "used" and "Clicked" and "Occurrances"
			for ( int i = 0; i < m_currentInteractionClickables.Count; ++i)
				m_currentInteractionClickables[i]?.OnCancelInteraction(m_currentInteractionVerbs[i]);
			m_currentInteractionVerbs.Clear();
			m_currentInteractionClickables.Clear();
			
			SV.m_currentInteractionOccurrances.ForEach(occurance=>SV.m_occurances.Remove(occurance));
			SV.m_currentInteractionOccurrances.Clear();	

			// Stop the background sequence as well as all other sequences started since "cancelable"
			StopCoroutine(m_backgroundSequence);
			m_backgroundSequence = null;
			foreach( Coroutine routine in m_backgroundSequences )
			{
				if ( routine != null )
					StopCoroutine(routine);			
			}
			m_backgroundSequences.Clear();

			// Clear the current sequence and sequences list
			m_currentSequence = null;
			m_currentSequences.Clear();
			m_sequenceIsCancelable = false;
		}	

	}

	// Returns true the first time something occurrs, increments each time
	public bool FirstOccurance(string uniqueString)
	{
		if ( m_allowEnableCancel )
			SV.m_currentInteractionOccurrances.Add(uniqueString);
		return SV.m_occurances.Add(uniqueString) <= 1;
	}
	// Checks how many times something has occurred without incrementing the occurance
	public int GetOccuranceCount(string thing)
	{
		return SV.m_occurances.Count(thing);
	}

	// Returns number of times something has occurred, and incrementing the number
	public int Occurrance(string thing)
	{
		if ( m_allowEnableCancel )
			SV.m_currentInteractionOccurrances.Add(thing);
		return SV.m_occurances.Add(thing) - 1;
	}

	/// Helper function that temporarily disables all clickables, except those specified (will probably move to PowerQuest system)
	public void DisableAllClickablesExcept(params string[] exceptions)
	{
		foreach( Prop prop in PowerQuest.Get.GetCurrentRoom().GetProps() )
		{	        
			if ( prop.Clickable && System.Array.Exists(exceptions, item=> item == prop.ScriptName) == false )
			{
				SV.m_tempDisabledProps.Add( prop.ScriptName);
				prop.Clickable = false;
			}
		}

		foreach( Hotspot hotspot in PowerQuest.Get.GetCurrentRoom().GetHotspots() )
		{
			if ( hotspot.Clickable && System.Array.Exists(exceptions, item=> item == hotspot.ScriptName) == false )
			{
				SV.m_tempDisabledHotspots.Add(hotspot.ScriptName);
				hotspot.Clickable = false;
			}
		}
	}

	/// Helper function that restores clickables disabled with the DisableAllClickablesExcept function (will probably move to PowerQuest system)
	public void RestoreAllClickables()
	{
		foreach( string name in SV.m_tempDisabledProps )
		{
			Prop prop = PowerQuest.Get.GetCurrentRoom().GetProp(name);
			if ( prop != null )
				prop.Clickable = true;
		}
		SV.m_tempDisabledProps.Clear();

		foreach( string name in SV.m_tempDisabledHotspots )
		{
			Hotspot hotspot = PowerQuest.Get.GetCurrentRoom().GetHotspot(name);
			if ( hotspot != null )
				hotspot.Clickable = true;
		}
		SV.m_tempDisabledHotspots.Clear();
	}


	/// Set all clickables to have a specific cursor temporarily, restore using RestoreAllClickableCursors() (will probably move to PowerQuest system)
	public void SetAllClickableCursors( string cursor, params string[] exceptions)
	{
		foreach( Prop prop in PowerQuest.Get.GetCurrentRoom().GetProps() )
		{	        
			if ( prop.Clickable && prop.Cursor != cursor && System.Array.Exists(exceptions, item=> item == prop.ScriptName) == false )
			{
				SV.m_tempCursorNoneCursor.Add(prop.Cursor);
				SV.m_tempCursorNoneProps.Add( prop.ScriptName);
				prop.Cursor = cursor;
			}
		}

		foreach( Hotspot hotspot in PowerQuest.Get.GetCurrentRoom().GetHotspots() )
		{
			if ( hotspot.Clickable && hotspot.Cursor != cursor && System.Array.Exists(exceptions, item=> item == hotspot.ScriptName) == false )
			{
				SV.m_tempCursorNoneCursor.Add(hotspot.Cursor);
				SV.m_tempCursorNoneHotspots.Add(hotspot.ScriptName);
				hotspot.Cursor = cursor;
			}
		}
	}

	/// Resets all clickable cursors (will probably move to PowerQuest system)
	public void RestoreAllClickableCursors()
	{
		int i = 0; 
		foreach( string name in SV.m_tempCursorNoneProps )
		{
			Prop prop = PowerQuest.Get.GetCurrentRoom().GetProp(name);
			if ( prop != null )
				prop.Cursor = SV.m_tempCursorNoneCursor[i];
			++i;
		}
		SV.m_tempCursorNoneProps.Clear();

		foreach( string name in SV.m_tempCursorNoneHotspots )
		{
			Hotspot hotspot = PowerQuest.Get.GetCurrentRoom().GetHotspot(name);
			if ( hotspot != null )
				hotspot.Cursor = SV.m_tempCursorNoneCursor[i];
			++i;
		}
		SV.m_tempCursorNoneHotspots.Clear();

		SV.m_tempCursorNoneCursor.Clear();
	}

	int m_inlineDialogResult = -1;
	DialogTree m_inlineDialogPrevDialog = null;
	public Coroutine WaitForInlineDialog(params string[] options)
	{
		if ( options != null && options.Length >  0 )
			return StartQuestCoroutine(CoroutineWaitForInlineDialog(options));
		else 
			return null;
	}

	IEnumerator CoroutineWaitForInlineDialog(string[] options)
	{
		bool wasCutscene = m_cutscene;
		if ( wasCutscene )
			EndCutscene();

		m_inlineDialogResult = -1;
		m_inlineDialogPrevDialog = m_currentDialog;
		DialogTree dialogTree = new DialogTree();
		int i =  0;
		foreach( string optionText in options )
		{
			DialogOption option = new DialogOption();
			option.InlineId = i;
			option.Text = optionText;
			dialogTree.Options.Add(option);
			i++;
		}

		m_currentDialog = dialogTree;
		GetGui(Settings.DialogTreeGui).Visible = true;
		bool hideCursor = GetCursor().HideWhenBlocking;
		GetCursor().HideWhenBlocking = false;
		GetCursor().Visible = true;
		yield return WaitWhile(()=> m_inlineDialogResult < 0 );
		GetCursor().HideWhenBlocking = hideCursor;

		m_currentDialog = m_inlineDialogPrevDialog;

		if (wasCutscene)
			StartCutscene();

		yield return Break;
	}

	public int InlineDialogResult {get { return m_inlineDialogResult; } }


	#endregion
	#region Functions: Implementing IQuestScriptable

	public string GetScriptName() { return "PowerQuest"; }
	public string GetScriptClassName() { return GLOBAL_SCRIPT_NAME; }
	public QuestScript GetScript() { return m_globalScript; }
	public IQuestScriptable GetScriptable() { return this; }
	public void HotLoadScript(Assembly assembly) { QuestUtils.HotSwapScript( ref m_globalScript, GLOBAL_SCRIPT_NAME, assembly ); }
	public void EditorRename(string name) {}



	#endregion
	#region Functions: ISerializationCallbackReceiver

	public bool GetSerializationComplete() { return m_serializeComplete; }

	public void OnBeforeSerialize()
	{
		m_serializeComplete = false;
	}
	public void OnAfterDeserialize()
	{
		m_serializeComplete = true;
	}

	//
	// Save/Load 
	//  Moved to "PowerQuestSave.cs"
	//

	#endregion
	#region Functions: Public System (advanced functions)

	/// Called when dialog is skipped
	public System.Action CallbackOnDialogSkipped = null;
	/// Called when a character collects an inventory item. EG:  OnInventoryCollected(ICharacter character, IInventory item);
	public System.Action<ICharacter, IInventory> CallbackOnInventoryCollected = null;
	// Callback when cutscene's skipped
	public System.Action CallbackOnEndCutscene = null;
	// Callback when cutscene's skipped, CallbackOnProcessClick(bool interactionFound)
	public System.Action<bool> CallbackOnProcessClick = null;

	//  Used by characters to check if should set autoavance flag on dialog
	public bool GetShouldSayTextAutoAdvance() { return m_sayTextAutoAdvance; }

	public bool GetStopWalkingToTalk() { return m_stopWalkingToTalk; }

	// Wait for current dialog 
	public Coroutine WaitForDialog(float time, AudioHandle audioSource, bool autoAdvance, bool skippable)	{	return StartCoroutine(CoroutineWaitForDialog(time, skippable, autoAdvance, audioSource)); }

	// Hides display box. Access provided incase want to cancel background dialog
	public void CancelDisplayBG()
	{
		EndDisplay();
	}

	public bool GetBlocked() 
	{
		return (m_blocking && m_sequenceIsCancelable == false) || m_transitioning;
	}

	// Used for custom/external controller skipping dialog
	public void SkipDialog(bool useNoSkipTime = true)
	{
		if ( useNoSkipTime == false || (Time.timeSinceLevelLoad-m_timeLastTextShown) > m_textNoSkipTime )
			m_skipDialog = true;		
	}

	// Returns true if a cutscene was skipped
	public bool SkipCutscene()
	{
		// Incase cutscene skipping takes multiple frames, check we're not already skipping
		if ( m_skipCutscene )
			return true;

		if ( m_cutscene )
		{
			SystemAudio.Play("SkipCutscene");
			FadeOutBG(0, "CUTSCENE");

			// Todo: Needs to skip background stuff, like if a walktobackground is halfway through completing, it needs to be completed.
			foreach ( Character character in m_characters )
			{
				if ( character.Instance != null ) (character.Instance as CharacterComponent).OnSkipCutscene();
			}

			m_skipCutscene = true;
		}

		return m_cutscene;
	}

	public bool GetSkippingCutscene() 
	{
		return m_skipCutscene;
	}

	public QuestCamera GetCamera() { return m_cameraData; }
	public QuestCursor GetCursor() { return m_cursor; }


	public Pathfinder Pathfinder { get{ return m_currentRoom != null ? m_currentRoom.GetInstance().GetPathfinder() : null; } }

	public List<Character> GetCharacters() { return m_characters; }
	public Character GetCharacter(int id) 
	{ 
		List<Character> list = m_characters;
		if ( id >= 0 && id < list.Count )
			return list[id];
		return null;
	}
	public int GetCharacterId(Character character) { return m_characters.FindIndex(ch=>ch==character); }

	public Inventory GetInventory(int id) 
	{
		List<Inventory> list = m_inventoryItems;
		if ( id >= 0 && id < list.Count )
			return list[id];
		return null;
	}


	public DialogTree GetDialogTree(int id) 
	{
		List<DialogTree> list = m_dialogTrees;
		if ( id >= 0 && id < list.Count )
			return list[id];
		return null;
	}

	public Gui GetGui(int id) 
	{
		List<Gui> list = m_guis;
		if ( id >= 0 && id < list.Count )
			return list[id];
		return null;
	}
	public QuestText GetDialogTextPrefab() { return m_dialogTextPrefab; }
	public Vector2 GetDialogTextOffset() { return m_dialogTextOffset; }

	public void SetMouseOverDescriptionOverride( string description ) { m_mouseOverDescriptionOverride = description; }
	public void ResetMouseOverDescriptionOverride() { m_mouseOverDescriptionOverride = null; }

	public float GetTextDisplayTime(string text)	{ return Mathf.Max(TEXT_DISPLAY_TIME_MIN, text.Length * TEXT_DISPLAY_TIME_CHARACTER) * m_textSpeedMultiplier; }

	public QuestScript GetGlobalScript() { return m_globalScript; }

	/// Returns true between ChangeRoom starting, and false again just before OnEnterAfterFade is called
	public bool GetRoomLoading() { return m_levelLoadedCalled == false; } 

	// Start the transition to a new room
	public void StartRoomTransition( Room room, bool force = false )
	{		
		if ( m_levelLoadedCalled == false )
		{
			Debug.LogError("Attempted to change rooms while already changing rooms!");
			return;
		}

		if ( m_initialised == false ) // HACK-Because room changes can happen when setting up characters initially, and we don't wanna change scene for these
			return;

		m_levelLoadedCalled = false;

		if ( room != null && (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != room.GetSceneName() || force ))
		{	
			// Fade out then change room
			StartCoroutine( CoroutineRoomTransition(room, force) ) ;
		}
	}


	public void StartDialog(string dialogName)
	{			
		DialogTree dialog = GetDialogTree(dialogName);		
		if ( dialog == null )
		{
			Debug.LogWarning("Couldn't start Dialog: "+dialogName+", it doesn't exist!");
			return;
		}
		
		dialog.OnStart();
		m_previousDialog = m_currentDialog;
		m_currentDialog = dialog;
		if ( StartScriptInteraction(dialog, SCRIPT_FUNCTION_DIALOG_START, null, false ) == false )
			StartScriptInteraction(dialog, SCRIPT_FUNCTION_DIALOG_START_OLD, null, false );
	}

	public void StopDialog()
	{
		if ( m_currentDialog !=  null )
		{
			StartScriptInteraction(m_currentDialog, SCRIPT_FUNCTION_DIALOG_STOP, null, false );
			m_currentDialog = null;
		}
		GetGui(Settings.DialogTreeGui).Visible = false;
	}


	// not currently used
	public void SetDialogOptionSelected(DialogOption option) { m_dialogOptionSelected = option; }

	// Proces a click on a gui dialog item- Returns true if dialog should unpause or hide to allow a sequence to run
	public bool OnDialogOptionClick(DialogOption option)//, PointerEventData.InputButton button)
	{

		if ( m_currentDialog == null )
			return false;

		m_guiConsumedClick = true;

		//if ( button != PointerEventData.InputButton.Left )
		//	return false;
		if ( option == null )
			return false;		
		if ( option.InlineId >= 0 )
		{
			m_inlineDialogResult = option.InlineId;
			option.Used = true;
			GetGui(Settings.DialogTreeGui).Visible = false;
			StopDialog();
			return true;
		}
		else 
		{
			if ( StartScriptInteraction(m_currentDialog, SCRIPT_FUNCTION_DIALOG_OPTION + option.Name, new object[]{option}, false )
				|| StartScriptInteraction(m_currentDialog, SCRIPT_FUNCTION_DIALOG_OPTION + option.Name, null, false ) )
			{
				option.Used = true;
				option.TimesUsed++;
				GetGui(Settings.DialogTreeGui).Visible = false;
				return true;
			}
		}
		return false;		
	}

	// Processes a click on a gui inventory item- returns true if the inventory should unpause or hide to allow a sequence to run
	public bool OnInventoryClick(string item, PointerEventData.InputButton button)
	{		
		if ( m_inventoryClickStyle == eInventoryClickStyle.OnMouseClick )
		{
			if ( Paused )
			{
				// Do some special handling to check for process click
				System.Reflection.MethodInfo method = null;
				if ( m_globalScript != null )
				{
					method = m_globalScript.GetType().GetMethod( "OnMouseClick", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance );
					if ( method != null ) 
						method.Invoke(m_globalScript,new object[]{ button == PointerEventData.InputButton.Left,  button == PointerEventData.InputButton.Right });

					// If any script interactions were queued, return 'true' to unpause the inventory
					if ( m_queuedScriptInteractions.Count > 0 )
						return true;
				}
				
			}
			return false;
		}

		bool clickHandled = false;
		bool shouldUnpause = false;
		m_guiConsumedClick = true;

		Inventory inv = GetInventory(item);
		if ( inv == null )
			return false;

		// If clicked item on itself, drop unselect it
		if ( inv == m_player.ActiveInventory )
		{
			m_player.ActiveInventory = null;
			return false;
		}

		if ( button == PointerEventData.InputButton.Left )
		{

			//m_lastClickable = component.GetData() as IQuestClickable;
			if ( m_player.HasActiveInventory )
			{
				// Use inventory item on inventory item

				Inventory activeInv = m_player.ActiveInventory.Data;
				clickHandled = StartScriptInteraction( inv, PowerQuest.SCRIPT_FUNCTION_USEINV_INVENTORY, new object[] {inv, activeInv}, true );
				if ( clickHandled == false )
				{
					// If click wasn't handled, try using inventory item in opposite order, if it works one way it should work the other
					clickHandled = StartScriptInteraction( activeInv, PowerQuest.SCRIPT_FUNCTION_USEINV_INVENTORY, new object[] {activeInv, inv}, true );
				}

				bool hadSpecificInteraction = clickHandled;

				if ( clickHandled  == false )
				{
					// Still not handled, so try "unhandled event" function
					clickHandled = StartScriptInteraction( this, "UnhandledUseInvInv", new object[] {inv, activeInv}, true );
					if ( clickHandled == false )
					{
						// If click wasn't handled, try using inventory item in opposite order, if it works one way it should work the other
						clickHandled = StartScriptInteraction( this, "UnhandledUseInvInv", new object[] {activeInv, inv}, true );
					}
				}

				if ( clickHandled )
				{
					shouldUnpause = true;
					if ( hadSpecificInteraction )
					{
						// Used the inventory item, so clear active inventory (only if no longer has inventory)
						if ( m_player.HasInventory(m_player.ActiveInventory) == false )
							m_player.ActiveInventory = null;
					}
				}
			}
			else if ( m_inventoryClickStyle == eInventoryClickStyle.UseInventory )
			{

				clickHandled = StartScriptInteraction( inv, PowerQuest.SCRIPT_FUNCTION_INTERACT_INVENTORY, new object[] {inv}, true );

				if ( clickHandled  == false )
				{
					// Still not handled, so try "unhandled event" function
					clickHandled = StartScriptInteraction( this, "UnhandledInteractInventory", new object[] {inv}, true );
				}

				if ( clickHandled )
					shouldUnpause = true;
			}
			else 
			{
				// Set active inventory	item
				m_player.ActiveInventory = inv;
				SystemAudio.Play("InventoryCursorSet");
			}

		}
		else if ( button == PointerEventData.InputButton.Right )
		{
			if ( m_player.HasActiveInventory )
			{
				// clear active inventory
				m_player.ActiveInventory = null;
			}
			else 
			{
				// Examine inventory
				clickHandled = StartScriptInteraction( inv, PowerQuest.SCRIPT_FUNCTION_LOOKAT_INVENTORY, new object[] {inv}, true );

				if ( clickHandled == false )
				{
					// Unhandled use inv on inv event						
					clickHandled = StartScriptInteraction( this, "UnhandledLookAtInv", new object[] {inv}, true );
				}

				if ( clickHandled )
				{
					shouldUnpause = true;
				}

			}				
		}

		// Returns true if the inventory needs to unpause to play a sequence
		return shouldUnpause;
	}

	// Returns true if game is in process of being restored from a save file
	public bool GetRestoringGame() { return m_restoring; }

	//
	// Misc utilities
	//


	/// Starts a coroutine, adding it to the list of current cancelable sequences
	public Coroutine StartQuestCoroutine( IEnumerator routine )
	{		
		Coroutine result = StartCoroutine(routine);
		if ( m_sequenceIsCancelable )
			m_backgroundSequences.Add(result); // currently in cancelable sequence, so put in background
		else 
			m_currentSequences.Add(result);
		return result;
	}

	/*
	/// Testing being able to start blocking routine at any point. 
	public Coroutine StartQuestCoroutineBlocking( IEnumerator routine )
	{
		Coroutine result = StartCoroutine(routine);
		if ( m_sequenceIsCancelable )
		{
			m_backgroundSequences.Add(result); // currently in cancelable sequence, so put in background
		}
		else 
		{
			m_currentSequences.Add(result);
		}

		// This will cause it to block main loop once reached. NB: not sure if this should only happen if "m_sequenceIsCancelable" is false...?
		m_queuedScriptInteractions.Add(result);
		return result;
	}*/
	
	/// Queues a coroutine to start on the next Blocking Update
	public Coroutine QueueCoroutine( IEnumerator routine )
	{
		Coroutine result = StartQuestCoroutine(routine);
		// This will cause it to block main loop once reached. NB: not sure if this should only happen if "m_sequenceIsCancelable" is false...?
		m_queuedScriptInteractions.Add(result);
		return result;
	}

	#endregion
	#region Functions: Public Editor
	//
	// public functions for editor
	//
	public bool GetActionEnabled(eQuestVerb action)
	{
		switch(action)
		{
			case eQuestVerb.Use: return m_enableUse;
			case eQuestVerb.Look: return m_enableLook;
			case eQuestVerb.Inventory: return m_enableInventory;
		}
		return true;
	}
	public bool GetSnapToPixel() { return m_snapToPixel; }
	public bool GetPixelCamEnabled() { return m_snapToPixel && m_pixelCamEnabled; }
	public float SnapAmount {get{ return m_snapToPixel ? 1.0f : 0.0f; }}
	public float EditorGetDefaultPixelsPerUnit() { return m_defaultPixelsPerUnit; }

	public List<RoomComponent> GetRoomPrefabs() { return m_roomPrefabs; }
	public List<CharacterComponent> GetCharacterPrefabs() { return m_characterPrefabs; }
	public List<InventoryComponent> GetInventoryPrefabs() { return m_inventoryPrefabs; }
	public List<DialogTreeComponent> GetDialogTreePrefabs() { return m_dialogTreePrefabs; }
	public List<GuiComponent> GetGuiPrefabs() { return m_guiPrefabs; }
	public List<Gui> GetGuis() { return m_guis; }
	public QuestCursorComponent GetCursorPrefab() { return m_cursorPrefab; }
	public QuestCameraComponent GetCameraPrefab() { return m_cameraPrefab; }
	public QuestText GetDialogTextPrefabEditor() { return m_dialogTextPrefab; }
	public List<AnimationClip> GetInventoryAnimations() { return m_inventoryAnimations; }
	public AnimationClip GetInventoryAnimation(string animName) { return m_inventoryAnimations.Find(item=> item == null ? false : string.Equals(item.name, animName, System.StringComparison.OrdinalIgnoreCase));  }
	public List<IQuestScriptable> GetAllScriptables()
	{
		List<IQuestScriptable> scriptables = new List<IQuestScriptable>();
		scriptables.Add(this as IQuestScriptable);
		m_characters.ForEach( item => scriptables.Add( item as IQuestScriptable ) );
		m_rooms.ForEach( item => scriptables.Add( item as IQuestScriptable ) );
		m_dialogTrees.ForEach( item => scriptables.Add( item as IQuestScriptable ) );
		m_inventoryItems.ForEach( item => scriptables.Add( item as IQuestScriptable ) );
		m_guis.ForEach( item => scriptables.Add( item as IQuestScriptable ) );
		return scriptables;
	}


	/// Returns true if there are any modal guis active (ie: ones that take control from game
	public bool GetModalGuiActive() 
	{ 
		return m_guis.Exists( item=> item.Modal && item.Visible ) || m_guiConsumedClick; 
	}

	/// Use when you want something else to capture input so it won't be handled in-game (eg: when mouse is over a GUI)
	public void CaptureInputOn(string source)
	{
		SV.m_captureInputSources.Add(source);
		Gui gui = GetGuis().Find( item=>item.ScriptName == "Source");
		if (gui != null )
			m_mouseOverClickable = gui;
	}
	/// Use when you want something else to capture input so it won't be handled in-game (eg: when mouse is over a GUI)
	public void CaptureInputOff(string source)
	{
		SV.m_captureInputSources.RemoveAll(item=>string.Equals(item,source,System.StringComparison.OrdinalIgnoreCase));
	}

	public int EditorGetVersion() { return m_version; }
	public void EditorSetVersion(int version) { m_version = version; }
	public int EditorNewVersion { get { return m_newVersion; } set { m_newVersion = value; } }

	public IQuestScriptable EditorGetAutoLoadScriptable() { return m_autoLoadScriptable; }
	//public List<IQuestScriptable> EditorGetHotloadedScriptables() { return m_hotLoadedScriptables; } // For now just recompiling all scriptables
	public void EditorSetHotLoadAssembly(Assembly assembly/*, List<IQuestScriptable> hotLoadedScriptables*/) 
	{ 
		m_hotLoadAssembly = assembly;
		// m_hotLoadedScriptables = hotLoadedScriptables;
	}
	public Assembly EditorGetHotLoadAssembly() { return m_hotLoadAssembly; }

	public string EditorGetAutoLoadFunction() { return m_autoLoadFunction; }


	#endregion
	#region Functions: Monobehaviour Functions

	//
	// Private internal functions
	//

	public bool GetRegainedFocus() 
	{ 
		bool result = m_lostFocus && m_hasFocus;
		if ( result )
		    m_lostFocus = false;
		return result; 
	}
	public bool GetLostFocus() { return m_lostFocus; }
	public void SetLostFocus(bool value) { m_lostFocus = value; }
	bool m_lostFocus = false;
	bool m_hasFocus = false;
	void OnApplicationFocus(bool hasfocus)
	{
		m_hasFocus = hasfocus;
		if  ( hasfocus )
		{
			UnityEngine.Cursor.visible = false;
			if ( Application.isEditor == false )
				UnityEngine.Cursor.lockState = CursorLockMode.Confined;
		}
		else 
		{
			m_lostFocus = true;
		}
	}

	void Awake()
	{
		// Ensure there's only 1 PowerQuest
		if ( HasInstance() )
		{
			Destroy(gameObject);
			return;	
		}	

		SetSingleton();
		DontDestroyOnLoad(this);


		if ( LAYER_UI < 0 )
			LAYER_UI = LayerMask.NameToLayer("UI");

		//
		// Ensure there's no null quest objects lingering in lists. This may be due to unity doing wierd things with the list, or them being deleted incorrectly. 
		//
		m_characterPrefabs.RemoveAll(item=>item==null);
		m_roomPrefabs.RemoveAll(item=>item==null);
		m_dialogTreePrefabs.RemoveAll(item=>item==null);
		m_guiPrefabs.RemoveAll(item=>item==null);
		m_inventoryPrefabs.RemoveAll(item=>item==null);

		//
		// Instantiate other systems (audio, time, etc)
		//
		foreach( Component obj in m_systems )
		{
			Transform trans = (GameObject.Instantiate(obj.gameObject) as GameObject).transform;
			trans.name = obj.name;
			trans.parent = transform;
		}

		// Set language
		if ( string.IsNullOrEmpty(Settings.Language) == false )
			Settings.Language = Settings.Language;



		//
		// initialise Data classes ( rooms, characters, etc.
		//		- Copies the default data from their prefabs into permanent data that's kept between scenes.
		//		- Instantiates "Scripts" classes
		//

		// Create the game script
		m_globalScript = QuestUtils.ConstructByName<QuestScript>(GLOBAL_SCRIPT_NAME);
		Debug.Assert( m_globalScript != null, "Couldn't find global script! Add GlobalScript.cs to the Game folder" );
			

		// Initialise Cursor
		if ( m_cursorPrefab != null )
		{
			m_cursor = new QuestCursor();
			QuestUtils.CopyFields(m_cursor, m_cursorPrefab.GetData());
			m_cursor.Initialise( m_cursorPrefab.gameObject );

			UnityEngine.Cursor.visible = false;
			UnityEngine.Cursor.lockState = CursorLockMode.Confined;
		}

		// Initialise Camera
		if ( m_cameraPrefab != null )
		{
			QuestUtils.CopyFields(m_cameraData,m_cameraPrefab.GetData());
		}

		// Initialise Rooms
		foreach (RoomComponent prefab in m_roomPrefabs) 
		{
			Room data = new Room();
			QuestUtils.CopyFields(data, prefab.GetData());
			m_rooms.Add(data);
			data.Initialise( prefab.gameObject );
		}


		// Initialise Guis
		foreach (InventoryComponent prefab in m_inventoryPrefabs) 
		{
			Inventory data = new Inventory();
			QuestUtils.CopyFields(data, prefab.GetData());
			m_inventoryItems.Add(data);
			data.Initialise( prefab.gameObject );
		}

		// Initialise Characters
		foreach (CharacterComponent prefab in m_characterPrefabs) 
		{
			Character data = new Character();
			QuestUtils.CopyFields(data, prefab.GetData());
			m_characters.Add(data);
			data.Initialise( prefab.gameObject );
		}

		// Initialise Dialogs
		foreach (DialogTreeComponent prefab in m_dialogTreePrefabs) 
		{
			DialogTree data = new DialogTree();
			QuestUtils.CopyFields(data, prefab.GetData());
			m_dialogTrees.Add(data);
			data.Initialise( prefab.gameObject );
		}


		// TODO: Give ability to change which character is the player. for now it's always the first
		m_player = m_characters[0];


		// Initialise Guis
		foreach (GuiComponent prefab in m_guiPrefabs) 
		{
			Gui data = new Gui();
			QuestUtils.CopyFields(data, prefab.GetData());
			m_guis.Add(data);
			data.Initialise( prefab.gameObject );
		}


	}

	// Use this for initialization
	void Start() 
	{		
		//
		// Init settings
		//
		m_settings.OnInitialise();

		ExtentionOnGameStart();

		//
		// Call OnGameStart
		//
		System.Reflection.MethodInfo method = m_globalScript.GetType().GetMethod( "OnGameStart", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance );
		if ( method != null ) method.Invoke(m_globalScript,null);

		OnSceneLoaded();

		// Register for OnSceneLoaded AFTER loading the scene first time (So we don't get things out of order. We want to call OnGameStart first)
		SceneManager.sceneLoaded += OnSceneLoaded;

	}

	void OnSceneLoaded( Scene scene, LoadSceneMode loadSceneMode ) { OnSceneLoaded(); }

	void OnSceneLoaded()
	{
		if ( m_levelLoadedCalled == false )
		{			
			m_transitioning = true;
			m_levelLoadedCalled = true;
			string sceneName = SceneManager.GetActiveScene().name;
			FadeOutBG(0,"ENTER");		
		
			if ( m_debugRestartCurrentRoom )
			{				
				// Debug Restart (pressing ~+f9)- hotload scripts again
				if ( Application.isEditor && m_hotLoadAssembly != null )
				{			
					//NB: this doesn't work :(			
					List<IQuestScriptable> scriptables = GetAllScriptables();
					foreach ( IQuestScriptable scriptable in scriptables )
					{
						scriptable.HotLoadScript(m_hotLoadAssembly);
					}
				}
				m_debugRestartCurrentRoom = false;
			}

			m_coroutineMainLoop = StartCoroutine(LoadRoomSequence(sceneName));

		}
	}

	#endregion
	#region Functions: Monobehaviour Update

	// Update is called once per frame
	void Update() 
	{
		// When restoring a save game, update the fade-in, but nothing else, until room is loaded.
		if ( m_restoring )
		{
			m_menuManager.Update();
			return;
		}

		// Call partial update fucntion for extentions 
		ExtentionUpdate();

		//
		// Update region collision (tinting, etc)
		//
		UpdateRegions();

		// Pathfinder.DrawDebugLines();

		//
		// Update input
		//

		if ( m_overrideMousePos == false )
		{
			m_mousePos = Vector2.zero;

			UnityEngine.Camera cam = UnityEngine.Camera.main;
			if ( cam != null )
			{				
				m_mousePos = cam.ScreenToWorldPoint( Input.mousePosition.WithZ(0) );
			}
		}

		if ( m_overrideMouseOverClickable == false )
		{
			m_mouseOverClickable = null;

			// Check UI elements getting the hit/hover

			// First check if mouse is over Unity Gui (checking "event" system)
			if ( EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) 
			{			
				List<RaycastResult> raycastResults = new List<RaycastResult>();
				EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = Input.mousePosition }, raycastResults);
				foreach (RaycastResult raycastResult in raycastResults)
				{
					if ( m_inventoryClickStyle == eInventoryClickStyle.OnMouseClick ) // Old games don't handle inventory clicks in OnMouseClick
					{
						InventoryComponent invComponent = raycastResult.gameObject.GetComponentInParent<InventoryComponent>();
						if ( invComponent != null && invComponent.GetData().Clickable )
						{
							m_mouseOverClickable = invComponent.GetData();
							break;
						}
					}

					GuiComponent guiComponent = raycastResult.gameObject.GetComponentInParent<GuiComponent>();
					if ( guiComponent.GetData().Clickable )
					{
						m_mouseOverClickable = guiComponent.GetData();
						break;
					}
				}

	 		}

			// If mouse isn't over Unity Gui then check other clickables
			if ( m_mouseOverClickable == null && SV.m_captureInputSources.Count <= 0 )
	 		{
				// Check if mouse is over gui/inv first (in UI layer)
				if ( m_cameraGui != null )
				{			
					Vector2 guiMousePos = m_cameraGui.ScreenToWorldPoint( Input.mousePosition.WithZ(0) );
					m_mouseOverClickable = GetObjectAt(guiMousePos, (1<<LAYER_UI) ); // Find object under mouse in gui layer
				}

				if ( m_mouseOverClickable == null ) // if not null, the mouse is over a clickable gui
				{
					m_mouseOverClickable = GetObjectAt(m_mousePos, ~(1<<LAYER_UI) ); // Find object under mouse (excluding gui)
				}
			}

		}

		UpdateDebugKeys();

		// When restoring a save game, update the fade-in, but nothing else, until room is loaded.
		if ( m_restoring )
		{
			m_menuManager.Update();
			return;
		}

		// Update cursor position and visibility... Not quite sure why I did this. Maybe to show hardware cursor if there's no normal cursor set up for some reason?
		if ( GetCursor() == null )
		{
			UnityEngine.Cursor.visible = !GetBlocked();
		}

		if ( Input.GetMouseButtonDown(0) )
			SkipDialog(true); // Skip dialog with click if it's been up for long enough
		else if ( Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Space) )
			SkipDialog(false); // Alternate skip buttons don't have the delay built in
		else if (Input.GetKey(KeyCode.Escape) && m_skipCutsceneButtonConsumed == false)
			SkipDialog(false); // Skip dialog while esc's held, as long as it wasn't consumed by the skip cutscene

		if ( Input.GetKey(KeyCode.Escape) == false )
			m_skipCutsceneButtonConsumed = false;
		
		if ( SystemTime.Paused == false )
		{

			// Update timers
			for (int i = 0; i < SV.m_timers.Count; i++)
			{
				SV.m_timers[i].t -= Time.deltaTime;
			}

			if ( m_cutscene && Input.GetKeyDown(KeyCode.Escape) && SystemTime.Paused == false )
			{
				SkipCutscene();
				m_skipCutsceneButtonConsumed = true;
			}
			
			
			if ( m_roomLoopStarted || m_transitioning == false ) // Don't call update between the RoomChange and OnEnter (so only if the loops started, or no longer transitioning)
			{

				//
				// Game Update (non blocking)
				// 
				if ( m_globalScript != null )
				{
					System.Reflection.MethodInfo method = m_globalScript.GetType().GetMethod( "Update", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance );
					if ( method != null ) method.Invoke(m_globalScript,null);
				}

				//
				// Room Update (non blocking)
				//
				if ( m_currentRoom != null && m_currentRoom.GetScript() != null )
				{
					System.Reflection.MethodInfo method = m_currentRoom.GetScript().GetType().GetMethod( "Update", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance );
					if ( method != null )
					{							
						method.Invoke(m_currentRoom.GetScript(),null);
					}
				}
			}
		}

		//
		// Update gui
		//
		m_menuManager.Update();
		
	}

	void LateUpdate()
	{
		if ( m_restartOnUpdate )
		{
			StopAllCoroutines();
			m_restartOnUpdate = false;
			GameObject.DestroyImmediate(gameObject);			
			if ( m_debugRestartCurrentRoom )			
				SceneManager.LoadScene(SceneManager.GetActiveScene().name);
			else 			
				SceneManager.LoadScene(0); 
		}
	}

	void UpdateDebugKeys()
	{
		
		if ( Input.GetKey(KeyCode.LeftControl) == false && Input.GetKeyDown(KeyCode.F5) && GetBlocked() == false  )
		{
			// Save slot 1
			Save(1, "QuickSave");
		}
		if (  Input.GetKeyDown(KeyCode.F7) && GetBlocked() == false  )
		{
			// Quickload
			if ( Debug.isDebugBuild && Input.GetKey(KeyCode.BackQuote) )
				RestoreSave(10); // If holding shift- load the "debug" save at the start of the scene
			else 			
				RestoreSave(1);
		}

		if ( Input.GetKeyDown(KeyCode.F9) && GetBlocked() == false )
		{
			// Restart			
			m_debugRestartCurrentRoom = Debug.isDebugBuild && Input.GetKey(KeyCode.BackQuote); // Holding ~ + F9 sets flag to restart at current room
			Restart();
		}

		if ( Debug.isDebugBuild && Input.GetKey(KeyCode.BackQuote) )
		{
			// debug cheat keys
			if ( Input.GetKeyDown(KeyCode.F10) && GetBlocked() == false  )
			{
				// Print slot info
				string dbgstr = "";
				foreach ( QuestSaveSlotData data in m_saveManager.GetSaveSlotData())
				{				
					dbgstr += string.Format("{0}: {1}\n", data.m_slotId, data.m_description);
				}
				Debug.Log(dbgstr);
			}
			if ( Input.GetKeyDown(KeyCode.I) ) // inventory test
			{
				foreach (Inventory item in m_inventoryItems)
				{
					m_player.AddInventory(item);
				}
			}
		}
	}
	
	#endregion
	#region Functions: Partial for extending functionality
	// Partial function to implement for a hook on Start, just before "OnGameStart" is called in global script
	partial void ExtentionOnGameStart();
	// Partial function to implement for a hook on Update
	partial void ExtentionUpdate();
	// Partial function to implement for a hook in the main blocking loop. After queued sequences are resumed, but before interactions are processed, etc.
	partial void ExtentionOnMainLoop();	

	#endregion
	#region Functions: Private functions

	bool HandleSkipDialogKeyPressed()
	{
		// When dialog is skipped, we want the mouse click to be marked as handled. For now just hack the left/right click buttons to do that. Could probably set a flag instead though
		bool result = m_skipDialog;
		m_skipDialog = false;
		m_leftClickPrev = true;
		m_rightClickPrev = true;
		if ( CallbackOnDialogSkipped != null )
			CallbackOnDialogSkipped.Invoke();
			
		return result;		
	}

	void Block()
	{
		m_blocking = true;
	}
	void Unblock()
	{
		m_blocking = false;
	}

	// Show display box
	void StartDisplay(string text, int id)
	{
		Gui gui = GetGui(Settings.DisplayBoxGui);
		if ( gui == null || gui.GetInstance() == null )
			return;

		m_displayActive = true;
		// Get tranlated string
		text = SystemText.GetDisplayText(text, id, "Narr");

		// Start audio
		SystemAudio.Stop(m_dialogAudioSource);
		m_dialogAudioSource = SystemText.PlayAudio(id, "Narr");

		if ( PowerQuest.Get.Settings.DialogDisplay != QuestSettings.eDialogDisplay.SpeechOnly || PowerQuest.Get.Settings.AlwaysShowDisplayText )
		{
			QuestText guiText = gui.GetInstance().GetComponentInChildren<QuestText>(true);
			if ( guiText != null )
			{
				guiText.SetText(text);
				gui.Visible = true;
			}
		}
	}


	// hide display box
	void EndDisplay()
	{
		SystemAudio.Stop(m_dialogAudioSource);

		Gui gui = GetGui(Settings.DisplayBoxGui);
		if ( gui == null || gui.GetInstance() == null )
			return;
		gui.Visible = false;
		m_displayActive = false;
	}

	void UpdateRegions()
	{
		// not using collision system, just looping over characters in the room and triggers in the room

		// Very slightly faster doing it here than in each region's update function, since don't have to re-check each character's room, etc.
		List<RegionComponent> regionComponents = m_currentRoom.GetInstance().GetRegionComponents();
		int regionCount = regionComponents.Count;

		for ( int charId = 0; charId < m_characters.Count; ++charId )
		{
			Character character = m_characters[charId];
			if ( character.Room == m_currentRoom )
			{				
				Vector2 characterPos = character.Position;
				Color tint = new Color(1,1,1,0);
				float scale = 1;
				for ( int regionId = 0; regionId < regionCount; ++regionId )
				{
					RegionComponent regionComponent = regionComponents[regionId];
					if ( regionComponent.UpdateCharactersOnRegion(charId, characterPos) )
					{	
						if ( character.UseRegionScaling )
						{
							float tmpScale = regionComponent.GetScaleAt(characterPos);
							if ( tmpScale != 1 )
								scale = tmpScale;
						}
						if ( character.UseRegionTinting )
						{
							tint = regionComponent.GetData().Tint;
						}
					}
				}

				if ( character.GetInstance() != null )
				{
					CharacterComponent charComponent = character.GetInstance().GetComponent<CharacterComponent>();
				
					// Apply scale to character
					charComponent.transform.localScale = new Vector3(scale*Mathf.Sign(charComponent.transform.localScale.x), scale, scale);
						
					// Apply tint to character
					//PowerSprite spriteComponent = character.GetInstance().GetComponent<PowerSprite>();
					PowerSprite[] spriteComponents = character.GetInstance().GetComponentsInChildren<PowerSprite>(true);
					foreach ( PowerSprite spriteComponent in spriteComponents )						
					{
						if ( spriteComponent != null )
							spriteComponent.Tint = tint;
					}
				}
			}

		}
	}


	// Called when a cutscene ends (either by skipping or when it naturally ends
	void OnEndCutscene()
	{
		// Snap the camera (only if we actually skipped a cutscene, and it didn't just naturally end)
		if ( m_cutscene && m_skipCutscene )
		{
			if ( m_cameraData.GetInstance() != null )
				m_cameraData.GetInstance().Snap();

			if ( CallbackOnEndCutscene != null )
				CallbackOnEndCutscene.Invoke();
			
			m_menuManager.FadeSkip();


			// "Flash" to fade in (if definitely in the 
			FadeInBG(0.15f, "CUTSCENE");

		}
		m_cutscene = false;
		m_skipCutscene = false;
	}

	public void OnPlayerWalkComplete()
	{
		if ( m_sequenceIsCancelable && m_backgroundSequence != null )
		{
			// swap background sequenct to be the current sequence
			m_currentSequence = m_backgroundSequence;
			m_backgroundSequence = null;
		}
		m_sequenceIsCancelable = false; 	
	}

	public void OnSay()
	{
		// any character talking stops the "Display" dialog
		if ( m_coroutineSay != null )
		{
			StopCoroutine(m_coroutineSay);
			EndDisplay();
		}
	}

	// NB: When this is called, any routine waiting on the dialog will hang and wait forever. But can use with SayBG if not yielding to it
	public void CancelSay()
	{
		// any character talking stops the "Display" dialog
		if ( m_coroutineSay != null )
		{
			StopCoroutine(m_coroutineSay);
			EndDisplay();
		}
		foreach ( Character character in m_characters )
		{
			character.CancelSay();
		}
	}

	IQuestClickable GetObjectAt( Vector2 pos, int layerMask )
	{
		IQuestClickable result = null;

		bool pickedGui = false;
		Collider2D[] picked = Physics2D.OverlapPointAll(pos, layerMask);
		if ( picked != null && picked.Length > 0 )
		{
			float lowestBaseline = float.MaxValue;
			for ( int i = 0; i < picked.Length && pickedGui == false; ++i ) 
			{
				IQuestClickable clickable = null;
				GameObject gameobj = picked[i].gameObject;
				{
					GuiDialogOption component = gameobj.GetComponent<GuiDialogOption>();
					if ( component != null ) clickable = component.Clickable;
				}
				if ( clickable == null && m_inventoryClickStyle == eInventoryClickStyle.OnMouseClick)
				{
					InventoryComponent component = gameobj.GetComponent<InventoryComponent>();
					if ( component != null ) clickable = component.GetData() as IQuestClickable;
				}
				if ( clickable == null )
				{
					
					GuiComponent component = gameobj.GetComponent<GuiComponent>();
					if ( component != null ) clickable = component.GetData() as IQuestClickable;
					/* Don't bother with this, since we're testing UI layer seperately anyway *
					// If picked a gui component, ignore everything else, since tha'ts handled seperately and steals the click					
					if ( clickable != null && clickable.Clickable ) 
						return null;
					**/
				}
				if ( clickable == null )
				{
					HotspotComponent component = gameobj.GetComponent<HotspotComponent>();
					if ( component != null ) clickable = component.GetData() as IQuestClickable;
				}
				if ( clickable == null )
				{
					PropComponent component = gameobj.GetComponent<PropComponent>();
					if ( component != null ) clickable = component.GetData() as IQuestClickable;
				}
				if ( clickable == null )
				{
					CharacterComponent component = gameobj.GetComponent<CharacterComponent>();
					if ( component != null ) clickable = component.GetData() as IQuestClickable;
				}
				if ( clickable != null )
				{
					if ( clickable.Clickable )
					{
						float baseline = clickable.Baseline + gameobj.transform.position.y;
						if ( baseline < lowestBaseline )
						{
							lowestBaseline = baseline;
							result = clickable;
						}
					}
				}
			}
		}
		return result;
	}


	// Turns on sequence cancelling, but doesn't restart main loop
	void EnableCancelInternal()
	{
		if ( m_allowEnableCancel )
		{
			m_sequenceIsCancelable = true;

			// Swap coroutines, set backgroundCoroutine flag so future coroutines go into the BG one
			m_backgroundSequences.Clear();
			m_backgroundSequences = m_currentSequences;
			m_currentSequences = new List<Coroutine>();

			// Set current sequence to background sequence
			m_backgroundSequence = m_currentSequence;

			// Return empty coroutine so main loop doesn't block
			m_currentSequence = StartCoroutine(CoroutineEmpty());
		}

	}

	// Used by clickables to check if usecount should show as incremented yet (makes more sense for it to be 0 until interaction is finished)
	public bool GetInteractionInProgress( IQuestClickable clickable, eQuestVerb verb )
	{
		if ( clickable == null )
			return false;
		// note: it's a list to cope with interactions that call 'HandleInteract()'
		for ( int i = 0; i < m_currentInteractionClickables.Count; ++i ) 
		{
			if ( m_currentInteractionClickables[i] == clickable && m_currentInteractionVerbs[i] == verb )
				return true;
		}
		return false;
	}

	// Called when player interaction starts (ie: player clicked something)
	void OnInteraction( IQuestClickable clickable, eQuestVerb verb )
	{
		// Cancel the current interaction before starting new one
		CancelCurrentInteraction();

		// Clear the list of things the current interaction has changed
		SV.m_currentInteractionOccurrances.Clear();
		m_currentInteractionClickables.Clear();
		m_currentInteractionVerbs.Clear();

		if ( clickable != null )
		{

			// Add this clickable & verb so we can roll back 'use count' later
			m_currentInteractionClickables.Add(clickable);
			m_currentInteractionVerbs.Add(verb);	
		
			// call 'clickable.OnInteraction' which increments the usecount
			clickable.OnInteraction(verb);
		}
	}

	// Called when HandleInteract/HandleLookAt is called (simulating player clicking something from another script)
	void OnHandleInteraction( IQuestClickable clickable, eQuestVerb verb )
	{
		// When HandleInteract is used, we want to increment use count for what it was done, without 'cancelling' the interaction that called HandleInteract
		m_currentInteractionClickables.Add(clickable);
		m_currentInteractionVerbs.Add(verb);

		// call 'clickable.OnInteraction' which increments the usecount
		clickable.OnInteraction(verb);
	}

	// Starts a MAIN script interaction, setting it to be the "current sequence" if there was a script to start, and enabling canceling if started with a walk
	// DON'T use for interactions triggered from other interactions (eg. not used for "onEnterRoom" since ChangeRoom could be in a sequence itself). If you do that it will mess up "canceling" and think you want to "queue" the interaction
	bool StartScriptInteraction( IQuestScriptable scriptable, string methodName, object[] parameters = null, bool stopPlayerMoving = false, bool cancelCurrentInteraction = false )
	{	
		QuestScript scriptClass = scriptable.GetScript();

		if ( stopPlayerMoving )
			CancelCurrentInteraction(); // Cancel if will stop player moving
		
		
		m_allowEnableCancel = true;// (regionHack == false); // always start with enableCancel on, unless it's a region, region coroutines can't be cancelled

		// Start the coroutine
		Coroutine result = null;
		try
		{
			result = StartScriptInteractionCoroutine(scriptClass, methodName, parameters, stopPlayerMoving, cancelCurrentInteraction);
			if ( result != null && result != m_consumedInteraction)
			{
				if ( m_currentSequence == null )
				{					
					// no sequence running, so set this as the current one
					m_currentSequence = result;

					// If first yield is walk, enable cancel. NOTE: This can cause issues if plr is already walking when script is started
					if ( m_player.Walking && stopPlayerMoving && m_currentDialog == null && cancelCurrentInteraction == false )
					{
						EnableCancelInternal();
					}
				}
				else 
				{				
					// When trying to start a sequence while another is already running, we queue it. This just happens with events like "StopDialog" that don't yield, but can trigger other yielding events
					m_queuedScriptInteractions.Add(result);
				}
			}

			// update the script to auto-load (ignored when not in editor)
			SetAutoLoadScript( scriptable, methodName, result != null );
		}
		catch
		{
			
		}
		return result != null;
	}

	// Starts a script interaction, returning the coroutine
	Coroutine StartScriptInteractionCoroutine( QuestScript scriptClass, string methodName, object[] parameters = null, bool stopPlayerMoving = false, bool cancelCurrentInteraction = false )
	{
		Coroutine result = null;
		if ( scriptClass != null )
		{
			System.Reflection.MethodInfo method = scriptClass.GetType().GetMethod( methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance );
			if ( method != null )
			{	
				if ( stopPlayerMoving )
					m_player.StopWalking();	

				// Start sequence				
				IEnumerator currentSequenceEnumerator = method.Invoke(scriptClass,parameters) as IEnumerator;					
				if ( currentSequenceEnumerator != null )
				{	
					// Cache some things to see if anything changed when invoked (to see if we should fallback to unhandled)
					bool wasTransitioning = m_transitioning;
					DialogTree prevDialog = m_currentDialog;

					if ( cancelCurrentInteraction && m_sequenceIsCancelable )
					{
						// OnEnterRegion and OnUpdateBlocking need this Hacky cancelable stuff to prevent their own functions from being added to the m_backgroundSequences and then cancelled
						m_sequenceIsCancelable = false; //m_regionHack = true; // If it ends up being necessary to use E.CancelCurrentInteraction() in UpdateBlocking/OnEnterRegion without blocking the script, we might have to change this to a flag, and check for it in StartQuestCoroutine()
						result = StartCoroutine(currentSequenceEnumerator);		
						m_sequenceIsCancelable = true; //m_regionHack = false;
					}
					else 
					{
						result = StartCoroutine(currentSequenceEnumerator);
					}		
					
					// If the enumerator.Current is the yield break one, return false so we don't have to wait an extra frame
					if ( currentSequenceEnumerator.Current == EMPTY_YIELD_INSTRUCTION || result == null)
					{						
						if ( m_transitioning && wasTransitioning == false || prevDialog != m_currentDialog )
						{
							// Check if there was a room change, or dialog started- which should count as 'consumed' interaction even if it doesn't yield
							m_consumedInteraction = StartCoroutine(CoroutineEmpty());
							result = m_consumedInteraction;
						}						
						else
						{
							result = null;
						}
					}
					else if ( currentSequenceEnumerator.Current == CONSUME_YIELD_INSTRUCTION )
					{
						m_consumedInteraction = StartCoroutine(CoroutineEmpty());
						result = m_consumedInteraction;
					}
					else if ( result != null )
					{
						if ( cancelCurrentInteraction )
							CancelCurrentInteraction(); // This cancels the "previous" interaction. Used for "OnEnterRegion" to cancel any current sequence. Needs to be called before the result is added to the currentSequences
						
						m_currentSequences.Add(result); // Add to list of current coroutines (for canceling)
					}
					
				}
			}
		}
		return result;
	}

	void SetAutoLoadScript( IQuestScriptable questScriptable, string functionName, bool functionBlocked )
	{
		if ( Application.isEditor == false )
			return;
		bool unhandled = functionName.StartsWith(STR_UNHANDLED);
		if ( functionBlocked )
		{
			if ( unhandled && m_autoLoadUnhandledScriptable != null )
			{
				// This was an unhandled event- we want to auto-load the event that was unhandled.
				m_autoLoadScriptable = m_autoLoadUnhandledScriptable;
				m_autoLoadFunction = m_autoLoadUnhandledFunction;
				return;
			}
			m_autoLoadScriptable = questScriptable;
			m_autoLoadFunction = functionName;
		}
		else // Function didn't block
		{
			if ( unhandled == false )
			{
				// Function didn't block, so store it incase there's an unhandled event next, so can auto-load the non-unhanled event function
				m_autoLoadUnhandledScriptable = questScriptable;
				m_autoLoadUnhandledFunction = functionName;		
			}
		}
		
	}

	#endregion
	#region Coroutines

	static IEnumerator CoroutineEmpty()
	{
		if ( PowerQuest.Get.GetSkippingCutscene() )
			yield break;
		yield return null;
	}

	static IEnumerator CoroutineWaitForTime(float time, bool skippable)
	{		
		bool first = true; // first frame the mouse will always be down, so don't skip until 2nd. When time starts as 0, we still want to pause for a single frame.
		while ( (time > 0.0f || first)
			&& PowerQuest.Get.GetSkippingCutscene() == false
			&& ( skippable == false || PowerQuest.Get.HandleSkipDialogKeyPressed() == false || first ) )
		{
			first = false;
			yield return new WaitForEndOfFrame();
			if ( SystemTime.Paused == false )
			{
				time -= Time.deltaTime;
			}
		}
	}

	IEnumerator CoroutineDelayedInvoke( float time, System.Action functionToInvoke)
	{
		yield return Wait(time);
		if ( functionToInvoke != null )
			functionToInvoke.Invoke();
	}

	IEnumerator CoroutineDisplay(string text, int id = -1)
	{
		if ( GetSkippingCutscene() )
			yield break;
		StartDisplay(text,id);
		yield return WaitForDialog(PowerQuest.Get.GetTextDisplayTime(text), m_dialogAudioSource, m_displayTextAutoAdvance, true);	
		EndDisplay();
	}

	IEnumerator CoroutineDisplayBG(string text, int id = -1)
	{
		if ( PowerQuest.Get.GetSkippingCutscene() )
			yield break;
		StartDisplay(text,id);
		yield return WaitForDialog(PowerQuest.Get.GetTextDisplayTime(text), m_dialogAudioSource, true, false);
		EndDisplay();
	}
	
	IEnumerator CoroutineWaitForDialog(float time, bool skippable, bool autoAdvance, AudioHandle audioSource )
	{		
		m_timeLastTextShown = Time.timeSinceLevelLoad;
		bool first = true; // first frame the mouse will always be down, so don't skip until 2nd
		while ( ShouldContinueDialog(first, time, skippable || m_waitingForBGDialogSkip, autoAdvance, audioSource) )
		{
			first = false;
			yield return new WaitForEndOfFrame();
			if ( SystemTime.Paused == false )
			{
				time -= Time.deltaTime;
			}
		}
	}

	bool ShouldContinueDialog( bool firstCall, float time, bool skippable, bool autoAdvance, AudioHandle audioSource )
	{
		bool result = true;

		// Check if time is up (when no audiosource, and auto-advance is on)
		if ( autoAdvance && audioSource == null )
			result &= (time > 0.0f);

		// Check if skipping cutscene
		result &= PowerQuest.Get.GetSkippingCutscene() == false;

		// Check if skip dialog button is pressed
		result &= ( skippable == false || PowerQuest.Get.HandleSkipDialogKeyPressed() == false || firstCall );

		// Check if has audio and if it's finished playing
		#if UNITY_SWITCH 
		result &= ( audioSource == null || audioSource.isPlaying );
		#else
		result &= ( audioSource == null || audioSource.isPlaying || Application.isFocused == false );
		#endif

		return result;
	}

	IEnumerator CoroutineFadeIn( string source, float time, bool skippable = false )
	{
		m_menuManager.FadeIn(time, source);
		yield return skippable ? WaitSkip(time) : Wait(time);
		m_menuManager.FadeSkip();
	}
	IEnumerator CoroutineFadeOut( string source, float time, bool skippable = false )
	{
		m_menuManager.FadeOut(time, source);
		yield return skippable ? WaitSkip(time) : Wait(time);
		m_menuManager.FadeSkip();
	}


	IEnumerator CoroutineRoomTransition( Room room, bool instant )
	{
		/* ORDER OF OPERATIONS FOR TRANSITION
			- m_transitioning = true
			- Wait for fade out
			- Main loop stopped
			- m_roomLoopStarted = false ( Update will no longer be called)
			- OnExitRoom
			- Wait a frame
			- Remove fades
			- Change Scene
		Then, after scene Loaded, in CoroutineRoomTransition
			- Create Room
			- Set m_currentRoom
			- Create/Setup Camera, Guis, Cursor
			- Spawn and position characters
			- m_initialised = true
			- Room instance OnLoadComplete
			- Debug Play-From function
			- Block
			- Wait a frame
			- OnEnterRoom
			- OnEnterRoomAfterFade (initial call)
			- Update regions
			- Camera onEnterRoom
			- start fading in
			- m_transitioning = false (allows Update to be called again)
			- Yield to OnEnterRoomAfterFade
			- Update starts being called
			- Unblock
			- m_roomLoopStarted = true;
			- Start MainLoop()
		*/


		bool wasBlocking = m_blocking;
		if ( wasBlocking == false )
			Block();
		
		string sceneName = room.GetSceneName();
		m_transitioning = true;
		if ( instant )
		{
			FadeOutBG(0,"ROOMINSTANT");
		}
		else 
		{
			yield return FadeOut("ROOMTRANS",m_settings.TransitionFadeTime/2.0f);
		}

		if ( m_coroutineMainLoop != null )
		{
			StopCoroutine(m_coroutineMainLoop);
			m_coroutineMainLoop = null;
			m_roomLoopStarted = false;
		}

		if ( m_restoring == false )
		{
			// Call OnExitRoom functions
			Coroutine onExit = StartScriptInteractionCoroutine(GetScript(), "OnExitRoom", new object[] { m_currentRoom, room } );
			SetAutoLoadScript( this, "OnExitRoom", onExit != null );
			if ( onExit != null )
				yield return onExit;

			if ( m_currentRoom != null && m_currentRoom.GetScript() != null )
			{
				onExit = StartScriptInteractionCoroutine(m_currentRoom.GetScript(), "OnExitRoom", new object[] { m_currentRoom, room } );
				SetAutoLoadScript( m_currentRoom, "OnExitRoom", onExit != null );
				if ( onExit != null )
					yield return onExit;
			}

			RestoreAllClickables();
			RestoreAllClickableCursors();

		}

		yield return new WaitForEndOfFrame();

		FadeInBG(0,"ROOMTRANS");
		if ( instant )
		{
			FadeInBG(0,"ROOMINSTANT");
			FadeInBG(0,"ENTER"); 
		}

		if ( wasBlocking == false )
			Unblock();

		UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
	}

	IEnumerator CoroutineWaitUntil(System.Func<bool> condition, bool skippable = false) 
	{ 
		bool first = true; // first frame the mouse will always be down, so don't skip until 2nd
		while ( condition != null && condition() == false
				&& PowerQuest.Get.GetSkippingCutscene() == false
				&& ( skippable == false || PowerQuest.Get.HandleSkipDialogKeyPressed() == false || first ) )
		{
		first = false;
		yield return null;
		}
	}
	IEnumerator CoroutineWaitWhile(System.Func<bool> condition, bool skippable = false)
	{ 
		bool first = true; // first frame the mouse will always be down, so don't skip until 2nd
		while ( condition != null && condition() == true 
			&& PowerQuest.Get.GetSkippingCutscene() == false
			&& ( skippable == false || PowerQuest.Get.HandleSkipDialogKeyPressed() == false || first ) )
		{
			first = false;
			yield return null;
		}
	}

	IEnumerator CoroutineWaitForDialog() { yield return WaitWhile(()=> {		
			// Check if any players are currently talking
			return m_displayActive || m_characters.Exists( item => item.Talking );
	} ); } 

	IEnumerator CoroutineChangeRoom( IRoom room )
	{		
		GetPlayer().Room = room;
		
		#if UNITY_2019_1_OR_NEWER
			Debug.Log("NB: C.Player.ChangeRoom() does not work correctly in Unity 2019 or later. Use C.Player.ChangeRoomBG() instead (for now).");
			yield break; // don't yield to function because it breaks unity in 2019+						
		#else
			yield return WaitUntil(()=> m_levelLoadedCalled && m_roomLoopStarted );
		#endif
	}
}

#endregion
}
