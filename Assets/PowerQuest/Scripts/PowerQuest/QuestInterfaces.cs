using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PowerTools.Quest
{

/*
QUEST INTERFACES

This file contains interfaces to the various types of objects that are usually accessed from PowerQuest script files

It makes a good list of the functionality that are easily available when doing your scripting/dialog

*/



#region IPowerQuest - eg. E.Wait(2)

/// PowerQuest is the main hub for the adventure game engine (Access with E) - eg. E.Wait(2);
/**
 * Most adventure game functionality not tied to a room/character/item/etc is accessed through here.
 * Including:
 * - Pausing scripts with Wait commands
 * - Displaying text boxes
 * - Save/Restore games
 * - Accessing Camera, Cursor, and other adventure game objects
 * - Access to game settings
 * - Triggering "interactions" of other objects
 * - Little helper utilities
 * - Eg.

	        E.FadeOut(1);			
			E.Wait(3);
			if ( E.FirstOccurance("AteBadPie") )
				E.ChangeRoom(R.Vomitorium);
			E.Save(1);
			E.StartCutscene;
 */
public partial interface IPowerQuest
{

	//
	// Yield instructions
	//


	/// yield return this in an empty function (so it doesn't give an error, and doesn't consume an update cycle like a doing "yield break" does)
	YieldInstruction Break { get; }

	/// yield return this if nothing is happening in a function, but you don't want to fall back to an Unhandled Event
	YieldInstruction ConsumeEvent { get; }

	//
	// Access to other objects
	//

	/// Convenient shortcut to the game camera
	ICamera Camera { get; }

	/// Convenient shortcut to the Cursor
	ICursor Cursor { get; }

	//
	// Timing/Waiting functions
	//

	/// Wait for time (or default 0.5 sec)
	Coroutine Wait(float time = 0.5f);

	/// Wait for time (or default 0.5 sec). Pressing button will skip the waiting
	Coroutine WaitSkip(float time = 0.5f);

	/// <summary>
	/// Use this when you want to yield to another function that returns an IEnumerator
	/// Usage: yield return E.WaitFor( SimpleExampleFunction ); or yield return E.WaitFor( ()=>ExampleFunctionWithParams(C.Dave, "lol") );
	/// </summary>
	/// 
	/// <param name="functionToWaitFor">A function that returns IEnumerator. Eg: `SimpleExampleFunction` or, `()=/>ExampleFunctionWithParams(C.Dave, 69)` if it has params</param>
	Coroutine WaitFor( PowerQuest.DelegateWaitForFunction functionToWaitFor );

	/// Use this when you want to wait until a condition is net. You need the ()=> to 
	/// Usage: yield return E.WaitWhile( ()=> C.Player.Walking )
	Coroutine WaitWhile( System.Func<bool> condition, bool skippable = false );

	/// Use this when you want to wait until a condition is net. You need the ()=> to 
	/// Usage: yield return E.WaitUntil( ()=> C.Player.Position.x > 0 )
	Coroutine WaitUntil( System.Func<bool> condition, bool skippable = false );

	/// Waits until the current dialog has finished. Useful for waiting to the end of SayBG commands
	Coroutine WaitForDialog();

	/// Invokes the specified function after the specified time has elapsed (non-blocking). EG: `E.DelayedInvoke(1, ()=/>{ C.Player.FaceLeft(); } );`
	void DelayedInvoke(float time, System.Action functionToInvoke);

	/// Returns true if there's a blocking script currently running 
	bool GetBlocked();

	//
	// Narrator 
	//

	/// Display narrator dialog
	Coroutine Display( string dialog, int id = -1 );
	/// Display narrator dialog (without blocking)
	Coroutine DisplayBG( string dialog, int id = -1 );

	//
	// Cutscenes
	//

	/// Starts a cutscene. If player presses esc, the game will skip forward until the next EndCutscene() \sa StartCutscene \sa EndCutscene
	void StartCutscene();	
	/// Ends a cutscene. When plyer presses esc after a "StartCutscene", this is where they'll skip to \sa StartCutscene \sa EndCutscene
	void EndCutscene();
	/// Returns true when a cutscene is currently being skipped.
	/// Can be used to check if your own code should be skipped, or if a sequence you're in has been skipped.
	/// 
	/// eg:  `while ( m_boolToWaitFor && E.GetSkippingCutscene() == false ) yield return E.Wait(0);`
	/// 
	/// \sa StartCutscene \sa EndCutscene
	bool GetSkippingCutscene();

	//
	// Screen transitions (fading to/from a color)
	//

	/// Fade the screen from the current FadeColor
	Coroutine FadeIn( float time = 0.2f, bool skippable = true );
	/// Fade the screen to the current FadeColor
	Coroutine FadeOut( float time = 0.2f, bool skippable = true );
	/// Fade the screen from the current FadeColor (non-blocking)
	void FadeInBG( float time = 0.2f );
	/// Fade the screen to the current FadeColor (non-blocking)
	void FadeOutBG( float time = 0.2f );

	/// Returns true if a fadeout/in is currently active
	bool GetFading();
	/// Get/Set the face colour
	Color FadeColor { get; set; }
	/// Return fade color to it's original value
	void FadeColorRestore();

	//
	// Pause/Unpause the game
	//

	/// Gets or sets whether the game is paused
	bool Paused { get; set; }
	/// Pauses the game. Pass a string as a source in case multiple things have paused/unpaused the game
	void Pause(string source = null); 
	/// Unpauses the game. Use the same source string you paused the game with (if any).
	void UnPause(string source = null);

	//
	// Start/Stop timers
	//

	/** Starts timer with a *name*, counting down from `time` in seconds. 

		Use the same *name* to check whether the timer has finished by calling the `GetTimerExpired(string name)` function. The name is NOT case sensitive.

		You can check the current time remaining on a timer by calling the `GetTimer(string name)` function, using the same name used to start the timer.

		Pass time as 0 to disable a currently running timer.

		**NOTE:** the timer will not tick while the game is paused.
		
		**Example:**

		    `E.SetTimer("egg",6) );`

		Will set the timer "egg" to expire after 6 seconds.

		**Rolling your own timers:**
		AGS users are familiar with the SetTimer() function, which is why it is included. However, it's good to know how to make your own timers, it's a fundamental building block of game coding! 

		This is how most coders would implement a simple timer:

		In your script body or header, add a float variable:
	        float m_myTimer = 0;

		When you want to start a timer, in an interaction script for example:
		    m_myTimer = 4; // Set to 4 seconds

		And in your Update function:
		~~~
		    if ( m_myTimer > 0) // If the timer is started
		    {
		        m_myTimer -= Time.deltaTime; // Subtract the time since last update
		        if ( m_myTimer <= 0 ) // Check if the timer's elapsed
		        {
		            // The timer has elapsed! Do something!
		        }
		    }
		~~~	
		\sa GetTimerExpired \sa GetTimer
		
	*/
	void SetTimer(string name, float time);
	/** Checks whether the timer with specified `name` has expired. If the timeout set with SetTimer has elapsed, returns *true*. Otherwise, returns *false*.

		**Note that this function will only return true once** - after that, the timer will always return false until restarted

		**Example: (in UpdateBlocking)**
		    if ( E.GetTimerExpired("egg") ) 
                Display: Egg timer expired
		
		will display a message when timer "egg" expires.
		\sa SetTimer \sa GetTimer 
	*/
	bool GetTimerExpired(string name);
	/// Returns the time remaining on the timer with specified `name`. 
	/// \sa SetTimer \sa GetTimerExpired 
	float GetTimer(string name);

	//
	// Change room
	//

	/// Change the current room. Same as calling C.Player.Room = room;
	void ChangeRoomBG(IRoom room);
	/// Change the current room. And blocks until after OnEnterAfterFade of the new room finishes. NB: This does NOT currently work in Unity 2019 and later.
	Coroutine ChangeRoom(IRoom room);

	//
	// Access to Quest Objects (rooms, characters, inventory, dialog, guis)
	//

	/// Retrieve a quest script by it's type. So you can access your functions/variables in your own scripts. Eg: E.GetScript<RoomKitchen>().m_tapsOn = true;
	T GetScript<T>() where T : QuestScript;

	/// The room the player's in (R.Current)
	Room GetCurrentRoom();
	/// Retrieve a room by it's name
	Room GetRoom(string scriptName);

	/// Gets or sets the current player controlled character
	ICharacter Player { get; set; }

	/// Get the current player controlled character
	Character GetPlayer();

	/// Set the current player controlled character. If in another room, will trigger room transition.
	void SetPlayer(ICharacter character);


	/// Retrieve a character by it's name. eg `E.GetCharacter("Dave");` Usually you would just use `C.Dave`
	Character GetCharacter(string scriptName);

	/// Shortcut to the current player's active inventory (the one currently selected for use on things). You can use the shorter `I.Active`
	IInventory ActiveInventory {get;set;}

	/// Retrieve an inventory item by it's name. Eg `E.GetInventory("Screwdriver");`, Usually you would just use `I.Screwdriver`
	Inventory GetInventory(string scriptName);

	/// Retrieve the currently active dialog. Eg. `E.GetCurrentDialog().OptionOff(1);`. Same as `D.Current`
	// DialogTree GetCurrentDialog();

	/// Retrieve an dialog tree by it's name. Eg: `E.GetDialogTree("ChatWithBarney")`. Usually you would just use `D.ChatWithBarney`
	DialogTree GetDialogTree(string scriptName);

	/** Shows a dialog with the specified text options, and waits until something's selected before continuing. Use IPowerQuest.InlineDialogResult to check what was clicked on afterwards
		~~~
        Barney: You fight like a dairy farmer!
        E.WaitForInlineDialog("How appropriate, you fight like a cow", "I am rubber, you are glue", "I'm shakin' I'm shakin'");
        if ( E.InlineDialogResult == 2 )
            WinSwordFight();
		~~~
	*/
	Coroutine WaitForInlineDialog(params string[] options);

	/// Retrieves the option that was picked in the last call to WaitForInlineDialog()
	int InlineDialogResult {get;}

	/// Retreive a Gui item by it's name
	Gui GetGui(string scriptName);

	/// Find a prefab to spawn by name. Usage: E.GetSpawnablePrefab("SparkleEffect").Spawn(...);
	GameObject GetSpawnablePrefab(string name);

	//
	// Access to useful system data
	//

	/// Returns the Gui Camera
	UnityEngine.Camera GetCameraGui();

	/// Returns the Gui Canvas
	Canvas GetCanvas();

	/// Returns the current mouse position
	Vector2 GetMousePosition();
	/// Returns the "clickable object" that the mouse is over (could be a character, hotspot, etc). Returns null of the mouse cursor isn't over anything
	IQuestClickable GetMouseOverClickable();
	/// Returns the type of clickable that the mouse is over as an eQuestClickableType (eg, could be a character, hotspot, etc).
	eQuestClickableType GetMouseOverType();
	/// Returns the display name of the object the mouse is over
	string GetMouseOverDescription();

	/// Returns the "Look at" position of the last thing clicked
	Vector2 GetLastLookAt();
	/// Returns the "Walk To" position of the last thing clicked
	Vector2 GetLastWalkTo();

	/// Returns the currentvertical resolution including any overrides from the current room
	float VerticalResolution { get ;}
	/// Returns the project's vertical resolution set in PowerQuest
	float DefaultVerticalResolution { get; }

	//
	// Settings
	//

	/// The game settings object
	QuestSettings Settings {get;}


	/// Sets the text speed multiplier, for slowing/speeding up game text
	void SetTextSpeedMultiplier(float multiplier);
	/// Gets the text speed multiplier, for slowing/speeding up game text
	float GetTextSpeedMultiplier();

	//
	// Functions for handling mouse clicks on things
	//

	/// Starts the specified action for the verb on whatever the mouse is over (whatever the current GetMouseOverClickable() happens to be ). 
	/**
	 * This would usually be called from the OnMouseClick function in your global script
	 * Returns true if the click resulted in a blocking function
	 */
	bool ProcessClick( eQuestVerb verb );
	bool ProcessClick( eQuestVerb verb, IQuestClickable clickable, Vector2 mousePosition );

	//
	// Functions that let scripts call other scripts interaction functions
	//

	/// Runs a "Use Hotspot" sequence
	Coroutine HandleInteract( IHotspot target );
	/// Runs a "Look at Hotspot" sequence
	Coroutine HandleLookAt( IHotspot target );
	/// Runs a "Use inventory on hostpot" sequence
	Coroutine HandleInventory( IHotspot target, IInventory item );
	/// Runs a "Use Prop" sequence
	Coroutine HandleInteract( IProp target );
	/// Runs a "Look at Prop" sequence
	Coroutine HandleLookAt( IProp target );
	/// Runs a "Use inventory on Prop" sequence
	Coroutine HandleInventory( IProp target, IInventory item );
	/// Runs a "Use Character" sequence
	Coroutine HandleInteract( ICharacter target );
	/// Runs a "Look at Character" sequence
	Coroutine HandleLookAt( ICharacter target );
	/// Runs a "Use inventory on Character" sequence
	Coroutine HandleInventory( ICharacter target, IInventory item );
	/// Runs a "Use Inventory" sequence
	Coroutine HandleInteract( IInventory target );
	/// Runs a "Look at Inventory" sequence
	Coroutine HandleLookAt( IInventory target );
	/// Runs a "Use inventory on Inventory" sequence
	Coroutine HandleInventory( IInventory target, IInventory item );

	//
	// Misc utilities
	//

	// Allows the current sequence to be cancelled by clicking something else. Automatically done for first "WalkTo" in an interaction.
	//void EnableCancel();

	/// Stops sequence from being cancelled when user clicks something else while walking there. Place either at start of script to prevent first WalkTo being cancelable.
	void DisableCancel();

	// Advanced function- allows you to cancel current sequence in progress. Use to interupt player interactions when something else happens (eg: on trigger or something in UpdateBlocking)
	//void CancelCurrentInteraction();

	/// Registers something "occuring", and returns whether it's the first time it's occurred
	/**
	 * Usage:
	 * if ( FirstOccurance("unlockdoor") ) 
	 * 		C.Display("You unlock the door");
	 * else
	 * 		C.Display("It's already unlocked");
	 * 		
	 *  \sa Occurrance \sa GetOccuranceCount
	 */
	bool FirstOccurance(string uniqueString);

	/// Registers something "occuring", and returns the number of time's it's occurred. Returns 0 the first time, then 1, etc.
	/**
	 * Usage:
	 * if ( Occurance("knocked on door") < 3 )
	 * 		C.Display("You knock on the door");
	 * 		
	 *  \sa FirstOccurance \sa GetOccuranceCount
	 */
	int Occurrance(string thing);

	/// Checks how many times something has occurred, without incrementing the occurance
	/**
	 * Usage:
	 * if ( GetOccuranceCount("knocked on door") == 3 )
	 * 		C.Doorman("Who's there?");
	 * 		
	 *  \sa FirstOccurance \sa Occurrance
	 */
	int GetOccuranceCount(string thing);


	/// Restart the game from the first scene
	void Restart();


	/// Helper function that temporarily disables all clickables, except those specified. Useful when you want to set only certain props clickable for a short time. Eg: `E.DisableAllClickablesExcept("Ropes","BrokenGlass");`
	void DisableAllClickablesExcept(params string[] exceptions);

	/// Helper function that restores clickables disabled with the DisableAllClickablesExcept function
	void RestoreAllClickables();

	/// Set all clickables to have a specific cursor temporarily, restore using RestoreAllClickableCursors(). Eg: `E.SetAllClickableCursors("None", "Ropes","BrokenGlass");`
	void SetAllClickableCursors( string cursor, params string[] exceptions);

	/// Resets all clickable cursors after a call to "SetAllClickableCursors"
	void RestoreAllClickableCursors();

	//
	// Save/Load
	//

	/// Returns a list of all save slot data
	List<QuestSaveSlotData> GetSaveSlotData();
	/// Returns save slot data for a particular save game. The data has info about the name, etc of the save file.
	QuestSaveSlotData GetSaveSlotData( int slot );
	/// Saves the game to a particular slot with a particular description
	bool Save(int slot, string description);
	/// Restores the game from a particular slot
	bool RestoreSave(int slot);
	/// Deletes a save game from a particular slot
	bool DeleteSave(int slot);

	/// Advanced save/restore function: For saving data not in a QuestScript...
	/**
	To use, call AddSaveData in Start, passing in a name for what you want to save, and the object containing the data you want to save.
	 
	If you want to do things when the game is restored, pass the function you want ot be called as OnPostRestore
	
	Notes:
	- The object to be saved must be a class containing the data to be saved (can't just pass in a value type like an int, float or Vector2).
	- By default all data in a class is saved, except for:
		- GameObjects, and MonoBehaviours
		- Variables with the [QuestDontSave] attribute (NOTE: THIS IS NOT YET IMPLEMENTED, BUG DAVE IF NEEDED!)
		- If you store references to other things that shouldnt be saved in your scripts, that may cause problems. Best thing is to dave know, he can add a feature tohelp with that
	-  you can add the [QuestSave] attribute to the class
		- When you do that, ONLY the variables that also have the [QuestSave] attribute are saved.
		- You can put this tag on a Monobehaviour class, when you just want to save a few of its variables without having to put them in their own seperate class		
 
 
	__Examples saving a simple data class:__
	~~~
	class MyComponent : Monobehaviour
	{
		// Class to store the save data in
	 	class SaveData
	 	{
			public int myInt;
			public float myFloat;
			[QuestDontSave] public bool myNotSavedBool;
	 	}
	 
		SaveData m_saveData;

		void Start()
		{
			PowerQuest.Get.AddSaveData( "myData", m_saveData );
		}
		void OnDestroy()
		{
			Powerquest.Get.RemoveSaveData("myData");
		}
	}
	~~~

	__Example using the [QuestSave] attribute:__
	~~~
	[QuestSave]
	class MyComponent : Monobehaviour
	{
		[QuestSave] int myInt;
		[QuestSave] float myFloat;
		bool myNotSavedBool;
	 
		SaveData m_saveData;

		void Start()
		{
			PowerQuest.Get.AddSaveData( "myData", this );
		}
		void OnDestroy()
		{
			Powerquest.Get.RemoveSaveData("myData");
		}
	}
	~~~
	*/	 
	void AddSaveData(string name, object data, System.Action OnPostRestore = null );
	/// Advanced save/restore function: For aving data not in a QuestScript. Call this when you've called AddSaveData, but no longer want to save that data.
	void RemoveSaveData(string name);

}

#endregion
#region Characters - eg. C.Bob.FaceLeft();

/** Characters: Contains functions for manipluating Characters - eg. C.Bob.FaceLeft(); - Eg.
	
			C.Barney.Room = R.Current;
			C.Player.WalkTo( P.Tree );
			if ( C.Player.Talking == false )
				C.Player.PlayAnimation("EatPizza");
			C.Bill.Position =  Points.UnderTree;
			C.Barney.SayBG("What's all this then?");
			Dave: Ah... Nothing!
			C.Player.AnimWalk = "Run";
			C.Barney.Description = "A strange looking fellow with a rat-tail";

 */
public partial interface ICharacter : IQuestClickableInterface
{
	/// Gets/Sets the name shown to players
	string Description {get;set;}

	/// The name used in scripts
	string ScriptName {get;}

	/// Access to the actual game object component in the scene
	MonoBehaviour Instance{get;}

	/// The room the character's in. Setting this moves the character to another room. If the player character is moved, the scene will change to the new room. \sa ChangeRoom() \sa ChangeRoomBG()
	IRoom Room {get;set;}

	/// Returns the last room visited before the current one
	IRoom LastRoom { get; }

	/// The location of the character. Eg: `C.Player.Position = new Vector2(10,20);` or `C.Dave.Position = Point.UnderTree;`
	Vector2 Position{ get;set; }
	/// The positiont the character is currently at, or walking towards
	Vector2 TargetPosition{ get; }

	/// Set the location of the character, with an optional 'facing' direction.
	/** 
	Eg `C.Dave.SetPosition(12,34);` or `C.Barney.SetPosition(43,21,eFace.Right);`. 
	Tip: Move your cursor over the scene window and press Ctrl+M to copy the coordinates to the clipboard. Then paste them into this function!
	\sa Position \sa Facing
	*/
	void SetPosition(float x, float y, eFace face = eFace.None);
	/// Set the location of the character, with an optional 'facing' direction. 
	/** 
	Eg `C.Dave.SetPosition(new Vector2(12,34));` or `C.Barney.SetPosition(Points.UnderTree,eFace.Right);` 
	\sa Position \sa Facing
	*/
	void SetPosition( Vector2 position, eFace face = eFace.None );

	/// The position of the character's baseline (for sorting)
	float Baseline { get;set; }

	/// The speed the character walks horizontally and vertically. Eg: `C.Player.WalkSpeed = new Vector2(10,20);`
	Vector2 WalkSpeed { get;set; }

	/// Whether character turns before walking
	bool TurnBeforeWalking { get;set; }
	/// Whether character turns before facing (eg: with C.Player.FaceLeft();
	bool TurnBeforeFacing { get;set; }
	/// How fast characters turn (turning-frames-per-second)
	float TurnSpeedFPS { get;set; }

	/// Whether the walk speed adjusts to match the size of th character when scaled by regions
	bool AdjustSpeedWithScaling { get;set;}
		
	/// Gets/sets the character's blocking width and height.
	/**
	The solid size determines how large of a blocking rectangle the character exerts to stop other characters walking through it. If this is set to 0,0 (the default), then the blocking rectangle is automatically calculated to be the character's width, and 5 pixels high.
	You can manually change the setting by entering a blocking height in pixels, which is the size of walkable area that the character effectively removes by standing on it.
	\sa Solid
	*/
	Vector2 SolidSize { get; set; }

	/// Gets/sets whether the character uses it's sprite as it's hotspot, instead of a collider specified in the inspector
	bool UseSpriteAsHotspot { get; set; }

	/// The enumerated direction the character is facing. Useful when you want to set a character's face direction without them turning
	/** 
	 Example:	 
	     // If Dave is facing right, immediately change him to be facing up-left 
	     if ( C.Dave.Facing == eFace.Right )
	         C.Dave.Facing = eFace.UpLeft;
	 */
	eFace Facing{get;set; }

	/// Gets or Sets whether clicking on the object triggers an event
	bool Clickable { get;set; }
	/// Gets or sets whether the character visible flag is set. \sa Show()
	bool Visible  { get;set; }
	/// Gets whether the character is *actually* visible and in the current room. Same as `(C.Fred.Visible && C.Fred.Room != null && C.Fred.Room == R.Current)`
	bool VisibleInRoom  { get; }
	/// Gets/sets whether the character can be walked through by other characters. 
	/// If this is set to true, then the character is solid and will block the path of other characters. If this is set to false, then the character acts like a hologram, and other characters can walk straight through him. 	\sa Solid 
	bool Solid { get; set; }
	/// Gets or sets whether the character can move/walk. If false, WalkTo commands are ignored.
	bool Moveable { get;set; }
	/// Gets a value indicating whether this <see cref="PowerTools.Quest.ICharacter"/> is currently walking.
	bool Walking { get; }
	/// Gets a value indicating whether this <see cref="PowerTools.Quest.ICharacter"/> is currently talking.
	bool Talking { get; }	
	/// Gets a value indicating whether this <see cref="PowerTools.Quest.ICharacter"/> is playing an animation from `PlayAnimation()`, or `Animation = "someAnim"` NOT a regular Idle, walk or Talk anim.
	bool Animating { get; }
	/// Whether this instance is the current player.
	bool IsPlayer {get;}
	/// Gets or sets the text colour for the character's speech text
	Color TextColour  { get;set; }
	/// Gets or sets the position the player's dialog text will be shown (in world space). If set to zero, the default will be used (Displayed above the character sprite).
	Vector2 TextPositionOverride { get;set; }
	/// Gets or sets the idle animation of the character
	string AnimIdle { get;set; }
	/// Gets or sets the walk animation of the character
	string AnimWalk { get;set; }
	/// Gets or sets the talk animation of the character
	string AnimTalk { get;set; }
	/// Gets or sets the lipsync mouth animation of the character, attached to a node
	string AnimMouth { get;set; }
	/// Gets or sets the cursor to show when hovering over the object. If empty, default active cursor will be used
	string Cursor { get; set; }
	/// <summary>
	/// Gets or sets a value indicating whether this <see cref="PowerTools.Quest.ICharacter"/> use region tinting.
	/// </summary>
	/// <value><c>true</c> if use region tinting; otherwise, <c>false</c>.</value>
	bool UseRegionTinting { get;set; }
	/// <summary>
	/// Gets or sets a value indicating whether this <see cref="PowerTools.Quest.ICharacter"/> use region scaling.
	/// </summary>
	/// <value><c>true</c> if use region scaling; otherwise, <c>false</c>.</value>
	bool UseRegionScaling { get;set; }
	/// Returns true the first time the player "uses" the object.
	bool FirstUse { get; }
	/// Returns true the first time the player "looked" at the object.
	bool FirstLook { get; }
	/// Returns the number of times player has "used" at the object. 0 when it's the first click on the object.
	int UseCount {get;}
	/// Returns the number of times player has "looked" at the object. 0 when it's the first click on the object.
	int LookCount {get;}
	/// Gets or sets the walk to point. 
	/** 
		Note that this is an offset from the character's position:
		- To get their actual WalkTo point in scene-coordinates, add it like this: `Vector2 actualWalkToPos = C.Barney.WalkToPoint + C.Barney.Position;`
		- To set their actual WalkTo point from scene-coordinates, subtract it like this: `C.Barney.WalkToPoint = Points.BarneyNewWalkTo - C.Barney.Position;`
	 */
	Vector2 WalkToPoint { get;set; }
	/// Gets or sets the look at point
	/** 
		Note that this is an offset from the character's position:
		- To get their actual LookAt point in scene-coordinates, add it like this: `Vector2 actualLookAtPos = C.Barney.LookAtPoint + C.Barney.Position;`
		- To set their actual LookAt point from scene-coordinates, subtract it like this: `C.Barney.LookAtPoint = Points.BarneyNewLookAt - C.Barney.Position;`
	 */
	Vector2 LookAtPoint { get;set; }
	/// Make the character walk to a position in game coords. 
	/**
	If 'anywhere' is true, the character will ignore walkable areas

	There are a few different WalkTo functions to serve different needs.
	eg: `C.Dave.WalkTo(12,34);`, C.Dave.WalkTo(Points.IntoSky, true);`, `C.Dave.WalkTo(C.Barney);`, or the quest script command `WalkToClicked`
	\sa WalkToBG() \sa StopWalking()
	*/
	Coroutine WalkTo(float x, float y, bool anywhere = false );
	/// Make the character walk to a position in game coords. 
	/**
	If 'anywhere' is true, the character will ignore walkable areas

	There are a few different WalkTo functions to serve different needs.
	eg: `C.Dave.WalkTo(12,34);`, C.Dave.WalkTo(Points.IntoSky, true);`, `C.Dave.WalkTo(C.Barney);`, or the quest script command `WalkToClicked`
	\sa WalkToBG() \sa StopWalking()
	*/
	Coroutine WalkTo(Vector2 pos, bool anywhere = false);
	/// Make the character walk to the walk-to-point of a clickable object. (characters, props, hotspots, etc)
	/**
	If 'anywhere' is true, the character will ignore walkable areas

	There are a few different WalkTo functions to serve different needs.
	eg: `C.Dave.WalkTo(12,34);`, C.Dave.WalkTo(Points.IntoSky, true);`, `C.Dave.WalkTo(C.Barney);`, or the quest script command `WalkToClicked`
	\sa WalkToBG() \sa StopWalking()
	*/
	Coroutine WalkTo(IQuestClickableInterface clickable, bool anywhere = false );
	
	/// Make the character walk to a position in game coords without halting the script
	/**
	eg: `C.Dave.WalkToBG(12,34);` or `C.Barney.WalkToBG(43,21,eFace.Left);`
	If 'anywhere' is true, the character will ignore walkable areas
	If 'thenFace' is specified, the character will face that direction once they finish walking
	\sa WalkTo() \sa StopWalking()
	*/
	void WalkToBG( float x, float y, bool anywhere = false, eFace thenFace = eFace.None );
	/// Make the character walk to a position in game coords without halting the script
	/**
	If 'anywhere' is true, the character will ignore walkable areas
	If 'thenFace' is specified, the character will face that direction once they finish walking
	\sa WalkTo() \sa StopWalking()
	*/
	void WalkToBG( Vector2 pos, bool anywhere = false, eFace thenFace = eFace.None );
	/// Make the character walk to the walk-to-point of a clickable object without halting the script
	void WalkToBG(IQuestClickableInterface clickable, bool anywhere = false, eFace thenFace = eFace.None);
	/// Make the character walk to the walk-to-point of the last object clicked on. In QuestScripts you can use the shortcut `WalkToClicked`
	Coroutine WalkToClicked(bool anywhere = false);
	
	/// Stop the character walking or moving. Also clears any waypoints.
	Coroutine StopWalking();

	/// Tells the character to move to a location directly, after it has finished its current move. Ignores walkable areas.
	/** This function allows you to queue up a series of moves for the character to make, if you want them to take a preset path around the screen. Note that any moves made with this command ignore walkable areas.
	 
	This is useful for situations when you might want a townsperson to wander onto the screen from one side, take a preset route around it and leave again.

	If 'thenFace' is specified, the character will face that direction once they finish walking

	Example: 
		C.Barney.Walk(160, 100);
		C.Barney.AddWaypoint(50, 150);
		C.Barney.AddWaypoint(50, 50, eFace.Right);
	Tells character Barney to first of all walk to the centre of the screen normally (obeying walkable areas), then move to the bottom left corner and then top left corner afterwards, then face right.
	\sa Walk \sa StopWalking
	 */
	void AddWaypoint(float x, float y, eFace thenFace = eFace.None);
	/// Tells the character to walk to a point directly, after it has finished its current move. Ignores walkable areas.
	/** This function allows you to queue up a series of moves for the character to make, if you want them to take a preset path around the screen. Note that any moves made with this command ignore walkable areas.

	This is useful for situations when you might want a townsperson to wander onto the screen from one side, take a preset route around it and leave again.

	If 'thenFace' is specified, the character will face that direction once they finish walking

	Example: 
		C.Barney.Walk(Points.Center);
		C.Barney.AddWaypoint(Points.BotLeft);
		C.Barney.AddWaypoint(Points.TopLeft, eFace.Right);
	Tells character Barney to first of all walk to the centre of the screen normally (obeying walkable areas), then move to the bottom left corner and then top left corner afterwards, Then face right.
	\sa Walk \sa StopWalking
	 */
	void AddWaypoint(Vector2 pos, eFace thenFace = eFace.None);

	/// Make the character move to a position in game coords without playing their walk animation.
	/**
	If 'anywhere' is true, the character will ignore walkable areas
	
	This is the same as `WalkTo()`, except that the character won't face the direction they're moving, and won't play their walk animation. In the majority of cases you should use `WalkTo()`

	eg: `C.Dave.MoveTo(12,34);`, C.Dave.MoveTo(Points.IntoSky, true);`.
	\sa WalkTo() \sa MoveToBG() \sa StopWalking()
	*/
	Coroutine MoveTo(float x, float y, bool anywhere = false );
	/// Make the character move to a position in game coords without playing their walk animation.
	/**
	If 'anywhere' is true, the character will ignore walkable areas
	
	This is the same as `WalkTo()`, except that the character won't face the direction they're moving, and won't play their walk animation. In the majority of cases you should use `WalkTo()`

	eg: `C.Dave.MoveToBG(12,34);`, C.Dave.MoveToBG(Points.IntoSky, true);`.
	\sa WalkTo() \sa MoveToBG() \sa StopWalking()
	*/
	Coroutine MoveTo(Vector2 pos, bool anywhere = false);
	/// Make the character move to the walk-to-point of a clickable object. Their Walk animation will NOT be played.
	/**
	If 'anywhere' is true, the character will ignore walkable areas
	
	This is the same as `WalkTo()`, except that the character won't face the direction they're moving, and won't play their walk animation. In the majority of cases you should use `WalkTo()`

	eg: `C.Dave.MoveToBG(12,34);`, C.Dave.MoveToBG(Points.IntoSky, true);`.
	\sa WalkTo() \sa MoveToBG() \sa StopWalking()
	*/
	Coroutine MoveTo(IQuestClickableInterface clickable, bool anywhere = false );	
	/// Make the character move to a position in game coords without halting the script. Their Walk animation will NOT be played.
	/**
	This is the same as `WalkToBG()`, except that the character won't face the direction they're moving, and won't play their walk animation. In the majority of cases you should use `WalkTo()`
	eg: `C.Dave.MoveToBG(12,34);`, C.Dave.MoveToBG(Points.IntoSky, true);`
	If 'anywhere' is true, the character will ignore walkable areas
	\sa MoveTo() \sa WalkTo() \sa StopWalking()
	*/
	void MoveToBG( float x, float y, bool anywhere = false );
	/// Make the character move to a position in game coords without halting the script. Their Walk animation will NOT be played.
	/**
	This is the same as `WalkToBG()`, except that the character won't face the direction they're moving, and won't play their walk animation. In the majority of cases you should use `WalkTo()`
	If 'anywhere' is true, the character will ignore walkable areas
	\sa MoveTo() \sa WalkTo() \sa StopWalking()
	*/
	void MoveToBG( Vector2 pos, bool anywhere = false );
	/// Make the character move to the walk-to-point of a clickable object without halting the script. Their Walk animation will NOT be played.
	/**
	This is the same as `WalkToBG()`, except that the character won't face the direction they're moving, and won't play their walk animation. In the majority of cases you should use `WalkTo()`
	If 'anywhere' is true, the character will ignore walkable areas
	\sa MoveTo() \sa WalkTo() \sa StopWalking()
	*/
	void MoveToBG(IQuestClickableInterface clickable, bool anywhere = false );	

	/// Moves the character to another room. If the player character is moved, the scene will change to the new room and script will wait until after OnEnterRoomAfterFade finishes. NB: This does NOT currently work in Unity 2019 and later.
	Coroutine ChangeRoom(IRoom room);
	/// Moves the character to another room. If the player character is moved, the scene will change to the new room.
	void ChangeRoomBG(IRoom room);
	/// Obsolete: Set's visible & clickable (Same as `Enable(bool clickable)`), and changes them to the current room (if they weren't there already) \sa Hide()	
	[System.Obsolete("Show(bool clickable) is obsolete. Use Show(), and Clickable property. Note that Show/Hide functions now remember previous state of visible/clickable/solid and restore it.")]
	void Show( bool clickable );		
	/// Shows the character again after a call to Hide(), moving them to current room, and forcing Visible to true.
	/// You can optionally pass in a position or face direction. If not passed no change will be made.
	/// The Enable() function is similar, but doesn't set Visible to true, or move them to the current room.
	/// \sa Hide() \sa Disable() \sa Enable()
	void Show( Vector2 pos = new Vector2(), eFace facing = eFace.None );	
	/// Shows the character again after a call to Hide(), moving them to current room, and forcing Visible to true.
	/// You can optionally pass in a position or face direction. If not passed no change will be made.
	/// The Enable() function is similar, but doesn't set Visible to true, or move them to the current room.
	/// \sa Hide() \sa Disable() \sa Enable()
	void Show( float posX, float posy, eFace facing = eFace.None );
	/// Shows the character again after a call to Hide(), moving them to current room, and forcing Visible to true.
	/// You can optionally pass in a position or face direction. If not passed no change will be made.
	/// The Enable() function is similar, but doesn't set Visible to true, or move them to the current room.
	/// \sa Hide() \sa Disable() \sa Enable()
	void Show( eFace facing );
	/// Hides the character until Show() is called. Saves you setting Visible, Clickable, Solid all to false. (Same as `Disable()`) \sa Show() \sa Disable() \sa Enable()
	void Hide();
	/// Enables the character again after a call to `Disable()` or `Hide()`. Does NOT move them to the current room, or set Visible like `Show()` does. \sa Disable() \sa Hide() \sa Show()
	void Enable();
	/// Obsolete: Set's visible & clickable, and changes them to the current room (if they weren't there already)
	[System.Obsolete("Show(bool clickable) is obsolete. Use Show(), and Clickable property. Note that Show/Hide functions now remember previous state of visible/clickable/solid and restore it.")]
	void Enable(bool clickable);
	/// Disables the character until Enable() is called. Saves you setting Visible, Clickable, Solid all to false. \sa Show() \sa Hide() \sa Enable()
	void Disable();
	/// Faces character in a direction. Turning, unless instant is false
	Coroutine Face( eFace direction, bool instant = false );
	/// Faces character towards the look-at-point of a clickable (character, prop, hotspot)
	Coroutine Face( IQuestClickable clickable, bool instant = false );
	/// Faces character towards the look-at-point of a clickable (character, prop, hotspot)
	Coroutine Face( IQuestClickableInterface clickable, bool instant = false );
	/// Faces character down (towards camera)
	Coroutine FaceDown(bool instant = false);
	/// Faces character up (away from camera)
	Coroutine FaceUp(bool instant = false);
	/// Faces character left
	Coroutine FaceLeft(bool instant = false);
	/// Faces character right
	Coroutine FaceRight(bool instant = false);
	/// Faces character a specified direction
	Coroutine FaceUpRight(bool instant = false);
	/// Faces character a specified direction
	Coroutine FaceUpLeft(bool instant = false);
	/// Faces character a specified direction
	Coroutine FaceDownRight(bool instant = false);
	/// Faces character a specified direction
	Coroutine FaceDownLeft(bool instant = false);
	/// Faces character towards a position in on screen coords
	Coroutine Face(float x, float y, bool instant = false);
	/// Faces character towards a position in on screen coords
	Coroutine Face(Vector2 location, bool instant = false);
	/// Faces character towards the look-at-point of the last object clicked on
	Coroutine FaceClicked(bool instant = false);
	/// Faces character in opposite direction to current
	Coroutine FaceAway(bool instant = false);
	/// Faces character in a direction
	Coroutine FaceDirection(Vector2 directionV2, bool instant = false);
	
	/// Faces character in a direction. Turning, unless instant is false. Does NOT halt script.
	void FaceBG( eFace direction, bool instant = false );
	/// Faces character towards the look-at-point of a clickable (character, prop, hotspot). Does NOT halt script.
	void FaceBG( IQuestClickable clickable, bool instant = false );
	/// Faces character towards the look-at-point of a clickable (character, prop, hotspot). Does NOT halt script.
	void FaceBG( IQuestClickableInterface clickable, bool instant = false );
	/// Faces character down (towards camera). Does NOT halt script.
	void FaceDownBG(bool instant = false);
	/// Faces character up (away from camera). Does NOT halt script.
	void FaceUpBG(bool instant = false);
	/// Faces character left. Does NOT halt script.
	void FaceLeftBG(bool instant = false);
	/// Faces character right. Does NOT halt script.
	void FaceRightBG(bool instant = false);
	/// Faces character a specified direction. Does NOT halt script.
	void FaceUpRightBG(bool instant = false);
	/// Faces character a specified direction. Does NOT halt script.
	void FaceUpLeftBG(bool instant = false);
	/// Faces character a specified direction. Does NOT halt script.
	void FaceDownRightBG(bool instant = false);
	/// Faces character a specified direction. Does NOT halt script.
	void FaceDownLeftBG(bool instant = false);
	/// Faces character towards a position in on screen coords. Does NOT halt script.
	void FaceBG(float x, float y, bool instant = false);
	/// Faces character towards a position in on screen coords. Does NOT halt script.
	void FaceBG(Vector2 location, bool instant = false);
	/// Faces character towards the look-at-point of the last object clicked on. Does NOT halt script.
	void FaceClickedBG(bool instant = false);
	/// Faces character in opposite direction to current. Does NOT halt script.
	void FaceAwayBG(bool instant = false);
	/// Faces character in a direction. Does NOT halt script.
	void FaceDirectionBG(Vector2 directionV2, bool instant = false);
		
	/// Causes the character to face another character, and continue to turn to face them if either character moves. 
	/// <param name="character">The character to face</param>
	/// <param name="minWaitTime">The minimum time delay before they'll turn to face the other character. Default 0.2 seconds.</param>
	/// <param name="maxWaitTime">The maximum time delay before they'll turn to face the other character. Default 0.4 seconds.</param>
	/// \sa `StopFacingCharacter();`
	void StartFacingCharacter(ICharacter character, float minWaitTime = 0.2f, float maxWaitTime = 0.4f);	
	/// Stops the chracter facing another after a call to `StartFacingCharacter()`
	void StopFacingCharacter();

	/// Make chracter speak a line of dialog. eg. `C.Barney.Say("Hello");` Note that in QuestScript window you can just type `Barney: Hello` \sa SayBG() \sa E.Display()
	Coroutine Say(string dialog, int id = -1);
	/// Make chracter speak a line of dialog, without halting the script.  eg. `C.Barney.SayBG("Some dialog I'm saying in the background");` \sa Say() \sa E.DisplayBG() \sa CancelSay()
	Coroutine SayBG(string dialog, int id = -1);
	/// Cancel any current dialog the character's speaking
	void CancelSay();

	/// Play an animation on the character. Will return to idle after animation ends. 
	/// \sa PlayAnimationBG() \sa Animation \sa AnimIdle \sa AnimWalk \sa AnimTalk
	Coroutine PlayAnimation(string animName);
	/// Play an animation on the character without halting the script. Will return to idle after animation ends, unless pauseAtEnd is true.
	/**
		If pauseAtEnd is true, the character will stay on the last frame until StopAnimation() is called. Otherwise they will return to idle once the animation has finished playing
		\sa Animation \sa PlayAnimation() \sa StopAnimation \sa AnimIdle \sa AnimWalk \sa AnimTalk
	*/
	void PlayAnimationBG(string animName, bool pauseAtEnd = false);
	
	/// Gets or sets an override animation on the character. Until StopAnimation() is called, this will play, and AnimIdle,AnimTalk,AnimWalk will all be ignored. 
	/// This is the equivalent of calling PlayAnimationBG() with pauseAtEnd set to true.
	/// \sa StopAnimation() \sa AnimIdle \sa AnimWalk \sa AnimTalk \sa PlayAnimation() \sa PlayAnimationBG()	
	string Animation {get;set;}
	// Pauses the currently playing animation
	void PauseAnimation();
	// Resumes playing the current animation
	void ResumeAnimation();
	// Stops the current animation- returns to Idle animation
	void StopAnimation();

	// Gets/Sets name of the sound used for footsteps for this character. Add "Footstep" event in the anim editor (with "Anim prefix" ticked)
	string FootstepSound {get;set;}

	/// Advanced/Experimental: Adds a function to be called on an animation event here. Eg: to play a sound or effect on an animation tag. 
	/** Usage:
		Add an event to the anim  called "Trigger" with a string matching the tag you want (eg: "Shoot")
		Then call C.Player.AddAnimationTrigger( "Shoot", true, ()=>Audio.PlaySound("Gunshot") ); 
	*/
	void AddAnimationTrigger(string triggerName, bool removeAfterTriggering, System.Action action);
	/// Removes an existing animation trigger
	void RemoveAnimationTrigger(string triggerName);
	/// Waits until an Event/Tag in the current animation is reached
	/** Usage:
		Add an event to the anim  called "Trigger" with a string matching the tag you want (eg: "Shoot")
		Then call yield return C.Player.WaitForAnimTrigger("Shoot");
	*/
	Coroutine WaitForAnimTrigger(string eventName);

	/// Players can have more than one polygon collider for clicking on. Add them in the Character Component, and set which is active with this function
	int ClickableColliderId { get; set; }

	//
	// Inventory stuff 
	//
	/// Gets or sets the active inventory  (item that's currently selected/being used)
	IInventory ActiveInventory {get;set;}
	/// Gets or sets the active inventory  (item that's currently selected/being used)
	string ActiveInventoryName {get;set;}
	/// Returns true if there's any active inventory (item that's currently selected/being used)
	bool HasActiveInventory {get;}

	/// Returns total number of inventory items that he player has
	float GetInventoryItemCount();
	/// <summary>
	/// Gets the number of a particular inventory item the player has
	/// </summary>
	/// <returns>The inventory quantity.</returns>
	/// <param name="itemName">Name of the inventory item</param>
	float GetInventoryQuantity(string itemName);
	/// Returns true if the player has the specified inventory item
	bool HasInventory(string itemName);
	/// Returns true if the player has, or ever had the specified inventory item
	bool GetEverHadInventory(string itemName);
	/// <summary>
	/// Adds an item to the player's inventory
	/// </summary>
	/// <param name="itemName">Name of the inventory item.</param>
	/// <param name="quantity">The quantity of the item to add.</param>
	void AddInventory(string itemName, float quantity = 1);
	/// <summary>
	/// Removes an item from the player's inventory.
	/// </summary>
	/// <param name="itemName">The name of the inventory item to remove.</param>
	/// <param name="quantity">Quantity of the item to remove.</param>
	void RemoveInventory( string itemName, float quantity = 1 );

	/// <summary>
	/// Gets the number of a particular inventory item the player has
	/// </summary>
	/// <returns>The inventory quantity.</returns>
	/// <param name="item">The inventory item to check</param>
	float GetInventoryQuantity(IInventory item);
	/// Returns true if the player has the specified inventory item
	bool HasInventory(IInventory item);
	/// Returns true if the player has, or ever had the specified inventory item
	bool GetEverHadInventory(IInventory item);

	/// <summary>
	/// Adds an item to the player's inventory
	/// </summary>
	/// <param name="item">The inventory item to add.</param>
	/// <param name="quantity">The number of the item to add.</param>
	void AddInventory(IInventory item, float quantity = 1);
	/// <summary>
	/// Removes an item from the player's inventory.
	/// </summary>
	/// <param name="item">The item to remove.</param>
	/// <param name="quantity">Quantity of the item to remove.</param>
	void RemoveInventory( IInventory item, float quantity = 1 );

	/// Remove all inventory items from the player
	void ClearInventory();

	/// Access to the specific quest script for the character. Pass the specific character class as the templated parameter so you can access specific members of the script. Eg: GetScript<CharacterBob>().m_saidHi = true;
	T GetScript<T>() where T : CharacterScript<T>;

	/// Access to the base class with extra functionality used by the PowerQuest
	Character Data {get;}

};

#endregion
#region IRoom - eg. R.Kitchen.PlayerVisible = false

/** Room: Contains functions and data for manipluating Rooms - Eg.

	        if ( R.Current.FirstTimeVisited )
				R.Kitchen.ActiveWalkableArea = 2;
*/
public partial interface IRoom
{
	/// Access to the actual game object component in the scene
	RoomComponent Instance {get;}

	/// Gets/Sets the name shown to players
	string Description { get; }
	/// The name used in scripts
	string ScriptName { get; }

	/// Change the current room. Same as calling C.Player.Room = room;
	void EnterBG();
	/// Change the current room to this one. Can be yielded too, and blocks until after OnEnterAfterFade of the new room finishes
	Coroutine Enter();
	/// Gets/sets whether this is the current room. Setting this changes the room ( same as `C.Player.Room = R.RoomName;` )
	bool Active { get;set; }
	/// Gets/sets whether this is the current room. Setting this changes the room ( same as `C.Player.Room = R.RoomName;`, or setting room to `Active = true`.
	bool Current { get;set; }
	/// Returns true if the room has ever been visited by the plyaer
	bool Visited { get; }
	/// Returns true if it's currently the first time the player has visited the room
	bool FirstTimeVisited { get; }
	/// Returns The number of times the room has been visited
	int TimesVisited { get;}
	/// Gets or sets the index currently active walkable area for the room. These are added in the editor.
	int ActiveWalkableArea { get; set; }
	/// Gets or sets whether the player character is visisble in this room
	bool PlayerVisible { get; set; }
	/// Access to the room's bounds
	RectCentered Bounds { get; set; }
	/// Access to the room's scroll bounds
	RectCentered ScrollBounds { get; set; }

	/// Sets the vertical resolution of this room (How many pixels high the camera view will be). If non-zero, it'll override the default set in PowerQuest. 
	float VerticalResolution { get;set; }
	/// Sets the vertical resolution of this room (How many pixels high the camera view will be) as a multiplier of the default vertical resolution set in PowerQuest. For temporary zoom changes use Camera.Zoom.
	float Zoom { get;set; }

	/// Retreives a hotspot by name
	Hotspot GetHotspot(string name);
	/// Retreives a prop by name
	Prop GetProp(string name) ;
	/// Retreives a region by name
	Region GetRegion(string name);

	/// Retreives a position by name
	Vector2 GetPoint(string name);
	/// Moves a named room position to another location
	void SetPoint(string name, Vector2 location);
	/// Moves a named room position to the location of another named position
	void SetPoint(string name, string fromPosition);

	/// Get the room's hotspot
	List<Hotspot> GetHotspots();
	/// Get the room's prop
	List<Prop> GetProps();

	/// Access to the specific quest script for the room. Use the specific room script as the templated parameter so you can access specific members of the script. Eg: GetScript<RoomKitchen>().m_tapOn = true;
	T GetScript<T>() where T : RoomScript<T>;

	/// Access to the base class with extra functionality used by the PowerQuest
	Room Data {get;}
}

#endregion
#region IProp - eg. Prop("door").Animation = "DoorOpen;

/** Prop: Contains functions and data for manipluating Props in rooms. Eg.
	
			P.GoldKey.Hide();
			P.Ball.MoveTo(10,20,5);
			P.Door.PlayAnimation("SlamShut");
			P.Door.Animation = "Closed";
*/
public partial interface IProp : IQuestClickableInterface
{
	//
	//  Properties
	//
	/// Gets/Sets the name shown to players
	string Description { get; set; }
	/// The name used in scripts
	string ScriptName { get; }
	/// Access to the actual game object component in the scene
	MonoBehaviour Instance { get; }
	/// Gets or sets whether the object is visible
	bool Visible { get; set; }
	/// Gets or Sets whether the prop is collidable (NB: Not yet implemented, can use hotspots and set as not Walkable instead)
	//bool Collidable { get; set; }
	/// Gets or Sets whether clicking on the object triggers an event
	bool Clickable { get; set; }
	/// The location of the prop
	Vector2 Position { get; set; }
	/// Set the location of the prop
	void SetPosition(float x, float y);
	/// Move the prop over time
	Coroutine MoveTo(float x, float y, float speed);
	/// Move the prop over time
	Coroutine MoveTo(Vector2 toPos, float speed);
	/// Move the prop over time, non-blocking
	void MoveToBG(Vector2 toPos, float speed);
	/// Gets or sets the baseline used for sorting
	float Baseline { get; set; }
	/// Gets or sets the walk to point
	Vector2 WalkToPoint { get; set; }
	/// Gets or sets the look at point
	Vector2 LookAtPoint { get; set; }
	/// Gets or sets the cursor to show when hovering over the object. If empty, default active cursor will be used
	string Cursor { get; set; }
	/// Returns true the first time the player "uses" the object.
	bool FirstUse { get; }
	/// Returns true the first time the player "looked" at the object.
	bool FirstLook { get; }
	/// Returns the number of times player has "used" at the object. 0 when it's the first click on the object.
	int UseCount {get;}
	/// Returns the number of times player has "looked" at the object. 0 when it's the first click on the object.
	int LookCount {get;}

	/// The prop's animation, change this to change the visuals of the prop
	string Animation { get; set; }
	/// Whether an animation is currently playing on the prop
	bool Animating { get; }

	/// Set's visible & clickable (Same as `Enable()`)
	void Show( bool clickable = true );
	/// Set's invisible & non-clickable (Same as `Disable()`)
	void Hide();
	/// Set's visible & clickable
	void Enable( bool clickable = true );
	/// Set's invisible & non-clickable
	void Disable();

	/// Plays an animation on the prop. Will return to playing Animation once it ends
	/** NB: Animation play/pause/resume/stop stuff doesn't get saved. If you want to permanently change anim, set the Animation property */ 
	Coroutine PlayAnimation(string animName);
	/// Plays an animation on the prop. Will return to playing Animation once it ends (Non-blocking)
	/** NB: Animation play/pause/resume/stop stuff doesn't get saved. If you want to permanently change anim, set the Animation property */ 
	void PlayAnimationBG(string animName);
	/// Pauses the currently playing animation
	void PauseAnimation();
	/// Resumes the currently paused animation
	void ResumeAnimation();


	#if ( UNITY_SWITCH == false )
	/// Starts video playback if the prop has a video component. Returns once the video has completed, or on mouse click if skippableAfterTime is greater than zero
	/** NB: Video playback position isn't currently saved */
	Coroutine PlayVideo(float skippableAfterTime = -1);
	/// Starts video playback if the prop has a video component
	void PlayVideoBG();
	/// Gets the prop's VideoPlayer component (if it has one). This can be used to pause/resume/stop video playback
	UnityEngine.Video.VideoPlayer VideoPlayer { get; }
	#endif

	/// Adds a function to be called on an animation event here. Eg: to play a sound or effect on an animation tag. 
	/** Usage:
		Add an event to the anim with the name you want (eg: "boom")
		Then add the trigger with the same name `Prop("dynamite").AddAnimationTrigger( "boom", true, ()=>Audio.PlaySound("explode") ); `
	*/
	void AddAnimationTrigger(string triggerName, bool removeAfterTriggering, System.Action action);

	/// Removes an existing animation trigger
	void RemoveAnimationTrigger(string triggerName);

	/// Waits until an Event/Tag in the current animation is reached
	/** Usage:
		Add an event to the anim with the name you want (eg: "boom")
		Then call `yield return Prop("dynamite").WaitForAnimTrigger("boom");` 
	*/
	Coroutine WaitForAnimTrigger(string eventName);

	/// Fade the sprite's alpha
	Coroutine Fade(float start, float end, float duration );
	/// Fade the sprite's alpha (non-blocking)
	void FadeBG(float start, float end, float duration );

	/// Access to the base class with extra functionality used by the PowerQuest
	Prop Data {get;}

}

#endregion
#region IHotspot - eg. Hotspot("tree").Clickable = false;

/** Hotspot: Contains functions and data for manipluating Hotspots in rooms - Eg:
	
			H.BlueCup.Cursor = "Drink";
			if  ( H.Tree.UseCount > 0 )
				H.Tree.Description = "Someone's cut it down";
*/
public partial interface IHotspot : IQuestClickableInterface
{	
	/// Gets/Sets the name shown to players
	string Description {get;set;}
	/// The name used in scripts
	string ScriptName {get;}
	/// Access to the actual game object component in the scene
	MonoBehaviour Instance {get;}
	/// Gets or Sets whether clicking on the object triggers an event
	bool Clickable {get;set;}
	/// Gets or sets the baseline used for sorting
	float Baseline {get;set;}
	/// Gets or sets the walk to point
	Vector2 WalkToPoint {get;set;}
	/// Gets or sets the look at point
	Vector2 LookAtPoint {get;set;}
	// Gets or Sets the tint color to apply to the character that's standing on the hotspot (NB: Not yet implmented)
	string Cursor {get;set;}
	/// Returns true the first time the player "uses" the object.
	bool FirstUse { get; }
	/// Returns true the first time the player "looked" at the object.
	bool FirstLook { get; }
	/// Returns the number of times player has "used" at the object. 0 when it's the first click on the object.
	int UseCount {get;}
	/// Returns the number of times player has "looked" at the object. 0 when it's the first click on the object.
	int LookCount {get;}

	/// Set's visible & clickable (Same as `Enable()`)
	void Show();
	/// Set's invisible & non-clickable (Same as `Disable()`)
	void Hide();
	/// Set's visible & clickable
	void Enable();
	/// Set's invisible & non-clickable
	void Disable();

	/// Access to the base class with extra functionality used by the PowerQuest
	Hotspot Data {get;}

}


#endregion
#region IRegion - eg. Region("Quicksand").Walkable = false;

/** Region: Contains functions and data for manipluating Regions in rooms - Eg.

			if ( R.DiscoFloor.GetCharacterOnRegion( C.Dave ) )
				R.DiscoFloor.Tint = Color.blue;
			R.Chasm.Walkable = false;				
*/
public partial interface IRegion
{
	/// The name used in scripts
	string ScriptName {get;}
	/// Access to the actual game object component in the scene
	MonoBehaviour Instance {get;}
	/// Gets or Sets whether walking on to the region triggers OnEnterRegion and OnExitRegion events, or tints things
	bool Enabled {get;set;}
	/// Gets or sets whether the player can walk on this hotspot. Use to create obstructions.
	bool Walkable {get;set;}
	// Gets or Sets the tint color to apply to the character that's standing on the hotspot (NB: Not yet implmented)
	Color Tint { get;set;}

	/// Returns true if the specified character is standing inside the region
	bool GetCharacterOnRegion(ICharacter character);

	/// Access to the base class with extra functionality used by the PowerQuest
	Region Data {get;}

}
#endregion
#region IInventory - eg: I.Crowbar.SetActive()

/** Inventory: Contains functions and data for manipluating Inventory Items - Eg.
	
			I.RubberChicken.Add();
	        I.Active.Description = "A rubber chicken with a pulley in the middle"			
			if ( I.Sword.Active )
				Display: You can't use a sword on that
			if ( I.HeavyRock.EverCollected )
				Dave: I'm not lugging any more of those around			
*/
public partial interface IInventory
{
	/// Gets/Sets the name shown to players
	string Description { get; set; }
	string AnimGui { get; set; }
	string AnimCursor { get; set; }
	string AnimCursorInactive { get; set; }
	/// The name used in scripts
	string ScriptName { get; }

	/// Gives the inventory item to the current player. Same as C.Player.AddInventory(item)
	void Add( int quantity = 1 );
	/// Gives the inventory item to the current player and set's it as active inventory. Same as C.Player.AddInventory(item)
	void AddAsActive( int quantity = 1 );
	/// Removes the item from the current player. Same as C.Player.RemoveInventory(item)
	void Remove( int quantity = 1 );
	/// Whether this item is the active item for the current player (ie: selected item to use on stuff)
	bool Active { get; set; }
	/// Sets this item as the active item for the current player (ie: selected item to use on stuff)
	void SetActive();
	/// Whether the current player has the item in their inventory
	bool Owned { get; set; } 
	/// Whether the item  has ever been collected
	bool EverCollected { get; } 

	/// Access to the specific quest script for the object. Use the specific item class as the templated parameter so you can access specific members of the script. Eg: GetScript<InventoryKey>().m_inDoor = true;
	T GetScript<T>() where T : InventoryScript<T>;
	/// Access to the base class with extra functionality used by the PowerQuest
	Inventory Data { get; }

}


#endregion
#region IDialogTree - eg. D.MeetSister.Start()

/** Dialog Tree: Contains functions and data for manipluating Dialog trees- Eg.
	
			D.SayHi.Start();
			D.TalkToFred.OptionOn("AskAboutPies");
*/
public partial interface IDialogTree
{
	/// The name used in scripts
	string ScriptName {get;}
	/// A list of the dialog options of the dialog tree
	List<DialogOption> Options {get;}
	/// Returns the number of enabled dialog options currently available to the player
	int NumOptionsEnabled {get;}

	/// True the first time the dialog tree is shown (or if its never been shown). 
	bool FirstTimeShown {get;}
	/// The number of times the dialog tree has been shown
	int TimesShown {get;}

	/// Starts the dialog
	void Start();
	/// Stops/ends the dialog
	void Stop();

	/// Finds a dialog option with the specified name
	DialogOption GetOption(string option);
	/// Finds a dialog option with the specified id
	DialogOption GetOption(int option);

	//
	// AGS style option on/off functions
	//

	/// Turns on one or more options. Eg: `D.ChatWithBarney.OptionOn(1,2,3);` \sa OptionOff \sa OptionOffForever
	void OptionOn(params int[] option);
	/// Turns off one or more options. Eg: `D.ChatWithBarney.OptionOff(1,2,3);` \sa OptionOn \sa OptionOffForever
	void OptionOff(params int[] option);
	/// Turns one or more options off permanantly. Future OptionOn calls will be ignored. Eg: `D.ChatWithBarney.OptionOffForever(1,2,3);` \sa OptionOn \sa OptionOff
	void OptionOffForever(params int[] option);
	
	/// Turns on one or more options. Eg: `D.ChatWithBarney.OptionOn("Yes","No","Maybe");` \sa OptionOff \sa OptionOffForever
	void OptionOn(params string[] option);
	/// Turns off one or more options. Eg: `D.ChatWithBarney.OptionOff("Yes","No","Maybe");` \sa OptionOn \sa OptionOffForever
	void OptionOff(params string[] option);
	/// Turns one or more options off permanantly. Future OptionOn calls will be ignored. Eg: `D.ChatWithBarney.OptionOffForever("Yes","No","Maybe");` \sa OptionOn \sa OptionOff
	void OptionOffForever(params string[] option);

	/// Check if the specified option is on
	bool GetOptionOn(int option);
	/// Check if the specified option is off forever
	bool GetOptionOffForever(int option);
	/// Check if the specified option has been used
	bool GetOptionUsed(int option);

	/// Check if the specified option is on
	bool GetOptionOn(string option);
	/// Check if the specified option is off forever
	bool GetOptionOffForever(string option);
	/// Check if the specified option has been used
	bool GetOptionUsed(string option);

	///////////

	/// Shortcut access to options eg: `D.MeetSarah["hello"].Off();`. Note that from dialog tree scripts you can access their options with `O.hello` instead
	DialogOption this[string option] {get;}

	/// Access to the specific quest script for the object. Use the specific dialog class as the templated parameter so you can access specific members of the script. Eg: GetScript<DialogSister>().m_saidHi = true;
	T GetScript<T>() where T : DialogTreeScript<T>;
	/// Access to the base class with extra functionality used by the PowerQuest
	DialogTree Data {get;}
}


#endregion
#region IDialogOption - eg. option.OffForever();

/** Dialog Option: Functions for manipulating a single dialog option
	
			option.On();
			option.Description = "Are you sure you don't want some beef?";
			option.OffForever();
*/
public partial interface IDialogOption
{
	/// The name used to uniquely identify this option
	string ScriptName { get; }

	/// The description shown in the dialog tree
	string Description { get; set; }

	/// Whether the option is On (ie. True when option is On, false when option is Off)
	bool Visible { get; }
	/// Whether the option is OffForever. (ie. True when OffForever, False when On, or Off)
	bool Disabled { get; }	

	/** Whether the option is shown as having been seleted by the player.
	 * 
	 * Setting this changes the color of the dialog option, to show players whether there's more to see. 
	 * You can set this to let users know there's more to read, or not.  
	 * 
	 * Note that UseCount will NOT reset to zero when you set Used = false. So `option.Used == false` is NOT the same as `option.TimesUsed == 0`. (This can be useful)
	*/
	bool Used { get; set; }
	
	/** The number of times this option has been selected
	 * Note that UseCount will NOT reset to zero when you set Used = false. So `option.Used == false` is NOT the same as `option.TimesUsed == 0`. (This can be useful)
	 * 
	 * Eg: 
	 *		if ( TimesUsed == 3 )
	 *			Barney: This is the third time you've asked me this!!	 *		
	 */
	int TimesUsed { get; }

	/// Turns option on in it's dialog (unless Disabled/HideForever() has been used)
	void On();
	/// Turns option off in it's dialog
	void Off();
	/// Disables this option so it'll never come back on, even if Show() is called
	void OffForever();
}

#endregion
#region IGui - eg. G.Toolbar.Visible = false

/// Gui: Contains functions for and data manipluating Gui objects.
public partial interface IGui
{
	/// The name used in scripts
	string ScriptName { get; }
	/// Access to the actual game object component in the scene
	MonoBehaviour Instance {get;}
	/// Access to the base class with extra functionality used by the PowerQuest
	Gui Data {get;}
	/// Gets or sets whether the object is visible
	bool Visible { get;set; }
	/// Gets or Sets whether clicking on the object triggers an event
	bool Clickable { get;set; }
	/// Gets or Sets whether this gui pauses the game below it
	bool Modal { get;set; }
	/// The location of the gui
	Vector2 Position { get;set; }
	/// Gets or sets the baseline used for sorting
	float Baseline { get;set; }
}


#endregion
#region ICamera - eg. E.Camera.Lock(...)

/// Camera: contains functions and data for manipulating the game camera - Interface to QuestCamera
public partial interface ICamera
{
	/// Returns the camera's game object component
	QuestCameraComponent GetInstance();

	/// Sets whether PowerQuest controls the camera, set to false if you want to control the camera yourself (eg. animate it)
	bool Enabled {get;set;}

	/// Sets whether overrides to camera position ignore room bounds. Useful for snapping camera to stuff off to the side of the room in cutscenes
	bool IgnoreBounds {get;set;}

	/// Returns the index of the character that the camera is following
	ICharacter GetCharacterToFollow();
	/// Sets which character the camera will follow, with option to transition over specified time
	void SetCharacterToFollow(ICharacter character, float overTime = 0);

	/// Gets the current position override coords as a vector
	Vector2 GetPositionOverride();
	/// Returns true if the camera has a position override
	bool GetHasPositionOverride();
	/// Returns true if the camera's position is overriden, or if it's still transitioning back to the player
	bool GetHasPositionOverrideOrTransition();

	/// Overrides the camera position with a specific X,Y. Optionally, transitions to the new position over time.
	void SetPositionOverride(float x, float y = 0, float transitionTime = 0 ) ;

	/// Overrides the camera position with a specific Vector. Optionally, transitions to the new position over time.
	void SetPositionOverride(Vector2 positionOverride, float transitionTime = 0 );

	/// Resets any position override, returning to folling the current camera, optionally transitions over time.
	void ResetPositionOverride(float transitionTime = 0);

	/// Gets the current camera zoom (mulitplier on default/room vertical height)
	float GetZoom();
	/// Returns true if the camera has a zoom override
	bool GetHasZoom();

	/// Returns true if the camera's zoom is overriden, or if it's still transitioning back to default
	bool GetHasZoomOrTransition();

	/// Sets a camera zoom (mulitplier on default/room vertical height)
	void SetZoom(float zoom, float transitionTime = 0);
	/// Removes any zoom override, returning to the default/room vertical height
	void ResetZoom(float transitionTime = 0);

	/// Returns the actual position of the camera
	Vector2 GetPosition();

	/// Snaps the camera to it's target position. Use to cancel the camera from smoothly transitioning to the player position
	void Snap();
		
	/// <summary>
	/// Shake the camera with the specified intensity, duration and falloff.
	/// </summary>
	/// <param name="intensity">Intensity- The strength to shake the camera (in pixels/game units).</param>
	/// <param name="duration">Duration- How long to shake camera at full intensity.</param>
	/// <param name="falloff">Falloff- How long in seconds it takes for camera to go from full intensity to zero.</param>
	void Shake(float intensity = 1.0f, float duration = 0.1f, float falloff = 0.15f);
	/// Shake the camera with the specified data.
	void Shake(CameraShakeData data);
}


#endregion
#region ICursor - eg. E.Cursor.Anim = "Crosshairs";

/// Cursor: contains functions and data for manipulating the mouse cursor - Interface to QuestCursor
public partial interface ICursor
{

	/// Shows or hides the mouse cursor
	bool Visible  {get;set;}

	/// Gets/Sets the default animation that plays when mouse is over a clickable object
	string AnimationClickable  {get;set;}

	/// Gets/Sets the default animation that plays when mouse is NOT over a clickable object
	string AnimationNonClickable  {get;set;}

	/// Gets/Sets the default animation that plays when there's an active inventory item (if not using inventory items)
	string AnimationUseInv {get;set;}

	/// Gets/Sets the default animation that plays when the mosue is over gui
	string AnimationOverGui  {get;set;}

	/// Gets/Sets whether the mouse should be hidden when there's a blocking script
	bool HideWhenBlocking {get;set;}

	/// Returns true if cursor is hovering over something with a cursor set to "None"
	bool NoneCursorActive { get; }

	/// Returns true if cursor is hovering over something with a cursor set as one of the "Inventory Override Anims" set in the inspector. Used for "exit" hotspot arrow cursors, etc
	bool InventoryCursorOverridden { get; }

	/// Gets/Sets the mouse cursor position, overiding the actual position of the mouse. Reset with "ClearPositionOverride()"
	Vector2 PositionOverride  {get;set;}
	/// True if SetPositionOverride() was called or PositionOverride was set
	bool HasPositionOverride { get; }
	/// Sets the mouse cursor position, overiding the actual position of the mouse. Reset with "ClearPositionOverride()"
	void SetPositionOverride(Vector2 position);
	/// Removes any position override from the cursor, returning it to normal mouse position
	void ClearPositionOverride();

	/// Outline colour used to highlight inventory (pixel art only)
	Color InventoryOutlineColor { get; set; }

	/// Gets the QuestCursorComponent of the cursor's game object
	QuestCursorComponent GetInstance();
}


#endregion

}
