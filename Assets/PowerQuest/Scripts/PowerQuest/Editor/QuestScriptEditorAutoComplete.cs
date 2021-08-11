using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using PowerTools.Quest;
using PowerTools;
using System.Linq;

namespace PowerTools.Quest
{


public partial class QuestScriptEditor
{	

	#region Variables: Static definitions

	static readonly string[] AC_KEYWORDS = 
		{ 
			"E", "R", "C", "I", "D", "G", "Plr",
			"P","H",/*"Hotspots", "Props",*/ "Regions", "Points",
			"Globals","Audio","Camera","Settings",
			"FaceClicked","WalkToClicked", "End", "Return", "Consume",
			"Display: ", "DisplayBG: ","Section: ",
			"bool","int","float","string","Vector2","enum","true","false","if","else","while","for","switch","case","default","break","continue","new","public",
		};
	static readonly string[] AC_KEYWORDS_R = { "Current", "Previous", "EnteredFromEditor" };
	static readonly string[] AC_KEYWORDS_C = { "Display(","DisplayBG(","Player","Plr" };
	static readonly string[] AC_KEYWORDS_I = { "Active", "Current" };
	static readonly string[] AC_KEYWORDS_D = { "Current", "Previous" };

	enum eAutoCompleteContext
	{
		Ignored, 	  // No longer matching current word (eg: after Display: )
		None, 		  // Searches for Charcter names (after 3 characters), keywords (walktoclicked, etc) or for E., C., or for Prop( Hotspot( or Point(, etc
		Characters,	  // C. -> List character names
		Rooms,		  // R. -> List Rooms
		InvItems,	  // I. -> List Items
		Dialogs,	  // D. -> List Dialog trees
		DialogOptions, // O. -> Dialog option funcs
		Guis,		  // G. -> List Gui
		Hotspots,	  // Hotspot(" ->  List
		Props,		  // Prop(" -> List items
		Regions,	  // Region(" ->  List
		Points,		  // Point(" ->List items
		IEngine,	  // E. -> Engine Funcs
		ICharacter,   // C.???. -> Char funcs
		IRoom,		  // R.???. -> Room funcs
		IInventory,   // I.???. -> Item funcs
		IDialogTree,  // D.???. -> Dialog funcs
		IDialogOption, // O.???. -> Dialog option funcs
		IGui,		  // G.???. -> Gui funcs
		IHotspot,	  // Hotspots.???. -> Hotspot funcs
		IProp,		  // Prop.???. -> Prop funcs
		IRegion,	  // Regions.???. -> Region funcs
		Globals,	  // Global script
		Audio,		  // Audio System
		ICamera,	  // QuestCamera
		Settings,	  // Settings
		EnumItem,	  // e???. -> enum contents eg eStateWindow.Open
		ObjectScript, // C.Dave.Script. or R.Kitchen.Script. or I.Bucket.Script

		// These contexts include a space or open bracket
		AnimChar,	// C.Dave.AnimIdle = " or C.Dave.PlayAnimation(
		AnimProp,	// P.Door.Animation = " or P.Door.PlayAnimation(
		Sound,      // Audio.Play(
		Count,

		FirstFullLineContext=AnimChar, // Some context are checked against whole line, rather than previous 'space'
		LastFullLineContext=Sound,
	};

	static readonly Regex[] AC_CONTEXT_REGEX = 
	{
		new Regex( @"^\w+:", RegexOptions.Compiled | RegexOptions.IgnoreCase ), // don't check
		new Regex( @"^(\w+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ),  // start of a word
		new Regex( @"^C\.(\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ), 		// eg: C.??
		new Regex( @"^R\.(\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ), 		// eg: R.??
		new Regex( @"^I\.(\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ), 		// eg: I.??
		new Regex( @"^D\.(\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ), 		// eg: D.??
		new Regex( @"^O\.(\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ), 		// eg: O.??
		new Regex( @"^G\.(\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ), 		// eg: G.??
		new Regex( @"^H\.(\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ),  // eg: H.??
		new Regex( @"^P\.(\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ), 	// eg: P.??
		new Regex( @"^Regions\.(\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ),  // eg: Region.??
		new Regex( @"^Points\.(\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ), 	// eg: Point.??
		new Regex( @"^E\.(\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ), 		// eg: E.??
		new Regex( @"^(?:C\.\w+|Plr)\.(\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ), 		// eg: C.Dave.?? or Plr.??
		new Regex( @"^R\.\w+\.(\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ), 		// eg: R.Kitchen.??
		new Regex( @"^(?:I\.\w+|item)\.(\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ), 		// eg: item. or I.Spanner.??
		new Regex( @"^(?:D\.\w+)\.(\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ), 		// eg: D.TalkSister.??
		new Regex( @"^(?:O\.\w+|option)\.(\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ), 		// eg: O.AskAboutHats.??
		new Regex( @"^(?:G\.\w+)\.(\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ), 		// eg: G.InfoBar.??
		new Regex( @"^(?:H\.\w+|hotspot)\.(\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ),  // eg: hotspot. or Hotspot.Door.??
		new Regex( @"^(?:P\.\w+|prop)\.(\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ), 	// eg: prop. or Prop.Door.??
		new Regex( @"^(?:Regions\.\w+|region)\.(\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ),  // eg: region. or Region.Door.??
		new Regex( @"^Globals\.(\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ), 		// eg: Globals.??
		new Regex( @"^Audio\.(\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ), 		// eg: Audio.??
		new Regex( @"^Camera\.(\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ), 		// eg: Camera.??
		new Regex( @"^Settings\.(\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ), 	// eg: Settings.??
		new Regex( @"^e[A-Z]\w*\.(\w*)$", RegexOptions.Compiled ), 	// eg: eStateWindow.??
		new Regex( @"^[CRI]\.\w+\.Script\.(\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ), 		// eg: C.Fred.Script. // eg: R.Kitchen.Script. // eg: I.Bucket.Script.

		new Regex( @"^\s*(?:C\.\w+|Plr)\.(?:(?:AnimIdle|AnimTalk|AnimWalk|Pose|NextPose|Animation)\s*=\s*|PlayAnimation(?:BG)?\(\s*)(""?\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ), // eg: C.Fred.AnimIdle = " // eg: C.Fred.PlayAnimation("
		new Regex( @"^\s*P\.\w+\.(?:Animation\s*=\s*|PlayAnimation(?:BG)?\(\s*)(""?\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ), // eg: P.Door.Animation = " // eg: P.Door.PlayAnimation("
		new Regex( @"^\s*Audio\.Play\w*\(\s*(""?\w*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase ), // eg: Audio.Play("
	};
	
	// Context's that should list functions in their class, rather than a custom list (like the names of characters)
	static readonly bool[] AC_CONTEXT_FUNCTION = 
	{
		false, 	// Ignored, 	
		false, 	// None, 		
		false,	// Characters,	
		false,	// Rooms,		
		false,	// InvItems,	
		false,	// Dialog,	
		false,	// Options,	
		false,	// Guis,	
		false, 	// Hotspots,	
		false,	// Props,		
		false, 	// Regions,
		false,	// Points,		
		true,	// IEngine,	
		true,	// ICharacter, 
		true,	// IRoom,		
		true,	// IInventory, 
		true,	// IDialog, 
		true,	// DialogOption, 
		true,	// IGui, 
		true, 	// IHotspot,	
		true,	// IProp,		
		true, 	// IRegion,	
		true,	// Globals,	
		true,	// Audio,		
		true,	// ICamera,	
		true,	// Settings,	
		false,	// eStateBlah.
		true,	// ObjectScript		
		false,  // AnimChar,
		false,  // AnimProp,
		false,  // Sound,   
	};
 
	static readonly Regex AC_IGNORELINE_REGEX = new Regex( @"^\s*(\w+:)|(//)|(/\*)", RegexOptions.Compiled );// Ignore dialog lines, and comments for autocomplete	
	static readonly char[] AC_STARTSEQCHARS = {'\n','\r','\t',' ','(',','};
	static readonly char[] AC_STARTSEQCHARS_FULLLINE = {'\n','\r' }; // Some context are checked against whole line, rather than previous 'space'

	#endregion
	#region Variables: Serialized

	#endregion
	#region Variables: Private

	eAutoCompleteContext m_acContext = eAutoCompleteContext.Ignored+1;
	List<string> m_acList = new List<string>();
	string m_acRemaining = string.Empty;
	int m_acSelectedIndex = 0;
	bool m_acCanceled = false;
	List<string>[] m_acLists = null;
	int m_acCursorIndexCached = -1;
	int m_acMethodCursorIndexCached = -1;
	List<string> m_acMethodParamList = new List<string>(3);
	string m_acMethodTextRemaining = string.Empty;
	System.Type m_lastObjectScriptType = null; // Used to find parameters of functions in R.<roomname>.Script. or C.<charactername>.Script.

	#endregion
	#region Functions: 

	public void BuildAutoCompleteLists(bool onRoomChange = false)
	{
		if ( PowerQuestEditor.IsReady() == false )
			return;

		if ( m_acLists == null ) // If not built then we want to build everything
		{
			onRoomChange = false;
			m_acLists = new List<string>[(int)eAutoCompleteContext.Count];
		}

		eAutoCompleteContext context = eAutoCompleteContext.Ignored+1;

		// Get Room
		RoomComponent room = null;
		if ( m_scriptType == eType.Room || m_scriptType == eType.Hotspot || m_scriptType == eType.Prop || m_scriptType == eType.Region )
		{
			
			room = PowerQuestEditor.Get.GetRoom(m_scriptClass.Substring(4));
			if ( room == null )
				Debug.Log("Autocomplete disabled until room reload");
		}
		else 
			room = PowerQuestEditor.Get.GetSelectedRoom();

		DialogTree dialogTree = null;
		if ( m_scriptType == eType.Dialog )
		{
			DialogTreeComponent dialogTreeComp = PowerQuestEditor.Get.GetDialogTree(m_scriptClass.Substring(6));
			if ( dialogTreeComp != null )
				dialogTree = dialogTreeComp.GetData();
		}

		for ( ; context < eAutoCompleteContext.Count; ++context )
		{

			if ( onRoomChange && context != eAutoCompleteContext.Hotspots && context != eAutoCompleteContext.Props && context != eAutoCompleteContext.Regions && context != eAutoCompleteContext.Points && context != eAutoCompleteContext.None )
				continue; // When changing rooms, only update list of room items (and in future, the script)
			
			List<string> contextList = new List<string>();
			switch (context)
			{
			case eAutoCompleteContext.None:
				{
					// add keywords
					contextList.AddRange( AC_KEYWORDS );

					// add charater dialog entries, eg: 'Dave:'
					PowerQuestEditor.GetPowerQuest().GetCharacterPrefabs().ForEach( item=> { if ( item != null ) contextList.Add( item.GetData().ScriptName+": "); } );

					//  Add functions
					//if ( m_scriptType != eType.Global ) // Removed this, since global script needs to be able to access its stuff like anything else.
					{
						System.Type scriptType = System.Type.GetType(string.Format("{0}, {1}", m_scriptClass, PowerQuestEditor.GetPowerQuest().GetType().Assembly.FullName ) );
						if ( scriptType != null )
						{
							System.Array.ForEach(scriptType.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static ),
								item => {if (item.IsSpecialName == false && item.Name[0] !='<') contextList.Add(item.Name);} );
							System.Array.ForEach(scriptType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static ),
								item => {if (item.IsSpecialName == false && item.Name[0] !='<') contextList.Add(item.Name);} );
							System.Array.ForEach(scriptType.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static ),
								item => {if (item.IsSpecialName == false && item.Name[0] !='<') contextList.Add(item.Name + "(" + (item.GetParameters().Length > 0 ? string.Empty : ")"));} );

							System.Array.ForEach(scriptType.GetNestedTypes( BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic ),
								item => {if (item.IsSpecialName == false && item.Name[0] !='<') contextList.Add(item.Name);} );							
						}
					}

					if ( m_scriptType != eType.Global )
					{
						// Add static fields from GlobalScript, these can be accessed via `using static GlobalScript;`
						System.Type scriptType = System.Type.GetType(string.Format("{0}, {1}", "GlobalScript", PowerQuestEditor.GetPowerQuest().GetType().Assembly.FullName ) );
						if ( scriptType != null )
						{
							System.Array.ForEach(scriptType.GetNestedTypes( BindingFlags.DeclaredOnly | BindingFlags.Public ),
								item => {if (item.IsSpecialName == false && item.Name[0] !='<') contextList.Add(item.Name);} );
						}
					}

					if ( m_scriptType == eType.Room )
					{
						contextList.Add("EnteredFromEditor");
						contextList.Add("FirstTimeVisited");
					}
					if ( m_scriptType == eType.Inventory )
						contextList.Add("item");
					if ( m_scriptType == eType.Prop )
						contextList.Add("prop");
					if ( m_scriptType == eType.Hotspot )
						contextList.Add("hotspot");
					if ( m_scriptType == eType.Region )
						contextList.Add("character");
					if ( m_scriptType == eType.Dialog )
					{
						
						contextList.Add("O");
						contextList.Add("option");
						contextList.Add("FirstTimeShown");
						contextList.Add("TimesShown");
						// Add dialog script base class functions (OptionOn() etc)
						System.Array.ForEach(typeof(DialogTreeScript<QuestScript>).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
							item => {if (item.IsSpecialName == false && item.Name[0] !='<') contextList.Add(item.Name + "(" + (item.GetParameters().Length > 0 ? string.Empty : ")"));} );						
					}

					// Add items with QuestAutocompletable attribute
					foreach ( System.Type type in PowerQuestEditor.GetPowerQuest().GetType().Assembly.GetTypes() )
					{						
						if ( type.GetCustomAttributes(typeof(QuestAutoCompletableAttribute),true).Count() > 0 )
						{
							contextList.Add(type.Name);
						}
					}					
				} break;
			case eAutoCompleteContext.Characters:
				{
					contextList.AddRange( AC_KEYWORDS_C );
					PowerQuestEditor.GetPowerQuest().GetCharacterPrefabs().ForEach( item=>  { if ( item != null ) contextList.Add(item.GetData().ScriptName); } );
				} break;
			case eAutoCompleteContext.Rooms:
				{
					contextList.AddRange( AC_KEYWORDS_R );
					PowerQuestEditor.GetPowerQuest().GetRoomPrefabs().ForEach( item=> contextList.Add(item.GetData().ScriptName) );
				} break;
			case eAutoCompleteContext.InvItems:
				{
					contextList.AddRange( AC_KEYWORDS_I );
					PowerQuestEditor.GetPowerQuest().GetInventoryPrefabs().ForEach( item=> contextList.Add(item.GetData().ScriptName) );
				} break;
			case eAutoCompleteContext.Dialogs:
				{
					contextList.AddRange( AC_KEYWORDS_D );
					PowerQuestEditor.GetPowerQuest().GetDialogTreePrefabs().ForEach( item=> contextList.Add(item.GetData().ScriptName) );
				} break;
			case eAutoCompleteContext.DialogOptions:
				{
					if ( dialogTree != null )
						dialogTree.Options.ForEach( item=> contextList.Add(item.Name));
				} break;
			case eAutoCompleteContext.Guis:
				{
					PowerQuestEditor.GetPowerQuest().GetGuiPrefabs().ForEach( item=> contextList.Add(item.GetData().ScriptName) );
				} break;
			case eAutoCompleteContext.Hotspots:
				{ 
					if ( room != null ) 
						room.GetHotspotComponents().ForEach( item=> contextList.Add(item.GetData().ScriptName) );
				} break;
			case eAutoCompleteContext.Props:
				{
					if ( room != null ) 
						room.GetPropComponents().ForEach( item=> contextList.Add(item.GetData().ScriptName) );
				} break;
			case eAutoCompleteContext.Regions:
				{ 
					if ( room != null ) 
						room.GetRegionComponents().ForEach( item=> contextList.Add(item.GetData().ScriptName) );
				} break;
			case eAutoCompleteContext.Points:
				{
					if ( room != null ) 
						room.GetData().GetPoints().ForEach( item=> contextList.Add(item.m_name) );
				} break;
			case eAutoCompleteContext.IEngine:
				{					
					System.Array.ForEach(typeof(IPowerQuest).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance),
						item => {if (item.IsSpecialName == false && item.Name[0] !='<') contextList.Add(item.Name + "(" + (item.GetParameters().Length > 0 ? string.Empty : ")"));} );
					System.Array.ForEach(typeof(IPowerQuest).GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance),
						item => contextList.Add(item.Name) );
				} break;
			case eAutoCompleteContext.ICharacter:
				{
					System.Array.ForEach(typeof(ICharacter).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance),
						item => {if (item.IsSpecialName == false && item.Name[0] !='<') contextList.Add(item.Name + "(" + (item.GetParameters().Length > 0 ? string.Empty : ")"));} );
					System.Array.ForEach(typeof(ICharacter).GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance),
						item => contextList.Add(item.Name) );
					contextList.Add("Script");
				} break;
			case eAutoCompleteContext.IInventory:
				{
					System.Array.ForEach(typeof(IInventory).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance),
						item => {if (item.IsSpecialName == false && item.Name[0] !='<') contextList.Add(item.Name + "(" + (item.GetParameters().Length > 0 ? string.Empty : ")"));} );
					System.Array.ForEach(typeof(IInventory).GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance),
						item => contextList.Add(item.Name) );
					contextList.Add("Script");
				} break;
			case eAutoCompleteContext.IDialogTree:
				{
					System.Array.ForEach(typeof(IDialogTree).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance),
						item => {if (item.IsSpecialName == false && item.Name[0] !='<') contextList.Add(item.Name + "(" + (item.GetParameters().Length > 0 ? string.Empty : ")"));} );
					System.Array.ForEach(typeof(IDialogTree).GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance),
						item => contextList.Add(item.Name) );
				} break;
			case eAutoCompleteContext.IDialogOption:
				{
					System.Array.ForEach(typeof(IDialogOption).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance),
						item => {if (item.IsSpecialName == false && item.Name[0] !='<') contextList.Add(item.Name + "(" + (item.GetParameters().Length > 0 ? string.Empty : ")"));} );
					System.Array.ForEach(typeof(IDialogOption).GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance),
						item => contextList.Add(item.Name) );
				} break;
			case eAutoCompleteContext.IGui:
				{
					System.Array.ForEach(typeof(IGui).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance),
						item => {if (item.IsSpecialName == false && item.Name[0] !='<') contextList.Add(item.Name + "(" + (item.GetParameters().Length > 0 ? string.Empty : ")"));} );
					System.Array.ForEach(typeof(IGui).GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance),
						item => contextList.Add(item.Name) );
				} break;
			case eAutoCompleteContext.IRoom:
				{
					System.Array.ForEach(typeof(IRoom).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance),
						item => {if (item.IsSpecialName == false && item.Name[0] !='<') contextList.Add(item.Name + "(" + (item.GetParameters().Length > 0 ? string.Empty : ")"));} );
					System.Array.ForEach(typeof(IRoom).GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance),
						item => { contextList.Add(item.Name);} );
					contextList.Add("Script");
				} break;
			case eAutoCompleteContext.IHotspot:
				{
					System.Array.ForEach(typeof(IHotspot).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance),
						item => {if (item.IsSpecialName == false && item.Name[0] !='<') contextList.Add(item.Name + "(" + (item.GetParameters().Length > 0 ? string.Empty : ")"));} );
					System.Array.ForEach(typeof(IHotspot).GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance),
						item => contextList.Add(item.Name) );
				} break;
			case eAutoCompleteContext.IProp:
				{
					System.Array.ForEach(typeof(IProp).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance),
						item => {if (item.IsSpecialName == false && item.Name[0] !='<') contextList.Add(item.Name + "(" + (item.GetParameters().Length > 0 ? string.Empty : ")"));} );
					System.Array.ForEach(typeof(IProp).GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance),
						item => contextList.Add(item.Name) );
				} break;
			case eAutoCompleteContext.IRegion:
				{
					System.Array.ForEach(typeof(IRegion).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance),
						item => {if (item.IsSpecialName == false && item.Name[0] !='<') contextList.Add(item.Name + "(" + (item.GetParameters().Length > 0 ? string.Empty : ")"));} );
					System.Array.ForEach(typeof(IRegion).GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance),
						item => contextList.Add(item.Name) );
				} break;
			case eAutoCompleteContext.Globals:
				{
					
					System.Array.ForEach(typeof(GlobalScript).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static),
						item => {if (item.IsSpecialName == false && item.Name[0] !='<') contextList.Add(item.Name + "(" + (item.GetParameters().Length > 0 ? string.Empty : ")"));} );
					System.Array.ForEach(typeof(GlobalScript).GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance),
						item => contextList.Add(item.Name) );
					System.Array.ForEach(typeof(GlobalScript).GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static),
						item => contextList.Add(item.Name) );
					System.Array.ForEach(typeof(GlobalScript).GetNestedTypes(BindingFlags.DeclaredOnly | BindingFlags.Public ),
						item => {if (item.IsSpecialName == false) contextList.Add(item.Name);} );
				} break;
			case eAutoCompleteContext.Audio:
				{					
					System.Array.ForEach(typeof(SystemAudio).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Static),
						item => {if (item.IsSpecialName == false && item.Name[0] !='<') contextList.Add(item.Name + "(" + (item.GetParameters().Length > 0 ? string.Empty : ")"));} );
				} break;
			case eAutoCompleteContext.ICamera:
				{					
					System.Array.ForEach(typeof(ICamera).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance ),
						item => {if (item.IsSpecialName == false && item.Name[0] !='<') contextList.Add(item.Name + "(" + (item.GetParameters().Length > 0 ? string.Empty : ")"));} );
					System.Array.ForEach(typeof(ICamera).GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance),
						item => contextList.Add(item.Name) );
				} break;
			case eAutoCompleteContext.Settings:
				{
					System.Array.ForEach(typeof(QuestSettings).GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance),
						item => contextList.Add(item.Name) );
				} break;
			case eAutoCompleteContext.AnimChar:
				{					
					// This needs to happen per character, so it's done during autocomplete
				} break;
			case eAutoCompleteContext.AnimProp:
				{						
					if ( room != null )
						room.GetAnimations().ForEach( item=>  { if ( item != null ) contextList.Add($"\"{item.name}\""); } );
				} break;
			case eAutoCompleteContext.Sound:
				{
					PowerQuestEditor.Get.GetSystemAudio().EditorGetAudioCues().ForEach( item=>  { if ( item != null ) contextList.Add($"\"{item.name}\""); } );					
				} break;
			}
			contextList = contextList.Distinct().ToList();
			contextList.Sort();
			m_acLists[(int)context] = contextList;
		}
	}


	void UpdateAutoComplete(bool force = false)
	{

		// Work back until whitespace
		TextEditor ted = FindTextEditor();
		if ( ted == null || PowerQuestEditor.IsReady() == false || string.IsNullOrEmpty(m_text) || m_text.Length != ted.text.Length )
		{
			m_acList.Clear();
			m_acMethodParamList.Clear();
			return;
		}

		if ( m_acLists == null )
			BuildAutoCompleteLists();

		if ( ted.cursorIndex != ted.selectIndex )
		{
			// Selecting things, so clear
			m_acList.Clear();
			m_acMethodParamList.Clear();
			return;
		}

		// Check if we're in a function parameter section and autocomplete that
		UpdateACMethodParams();

		if ( m_acCursorIndexCached == ted.cursorIndex && force == false )
			return;
		m_acCursorIndexCached = ted.cursorIndex;

		int index = ted.cursorIndex;

		if ( index < 0 || index-1 >= m_text.Length )
			return; // must be old invalid index

		// Find start of line, check if we should be autocompleting, don't autocomplete if after a Display: for example.
		if ( AC_IGNORELINE_REGEX.IsMatch( GetCurrentLine() ) )
		{
			ClearAutoComplete();
			return;
		}

		string expression = null;
		eAutoCompleteContext oldContext = m_acContext;
		int expressionStart = 0;
		
		bool foundContext = false;

		// Find the context, first for longer expressions containing spaces and open brackets.
		if ( foundContext == false )
		{
			// Find start of current expression- TODO Cleanup: extract this into function, it's done 3 times.
			if ( FindExpressionStart(index,true, out expressionStart, out expression) == false )
			{
				ClearAutoComplete();
				return;
			}

			// Find the context from list of context expressions
			m_acContext = eAutoCompleteContext.FirstFullLineContext;
			for ( ; m_acContext <= eAutoCompleteContext.LastFullLineContext; ++m_acContext )
			{
				if ( TryParse(m_acContext, ref expression) )
					break;
			}
			foundContext = m_acContext < eAutoCompleteContext.Count && m_acLists[(int)m_acContext] != null;
		}

		// If didn't find context yet, try again with shorter version.
		if ( foundContext == false )
		{
			// Find start of current expression
			if ( FindExpressionStart(index, false, out expressionStart, out expression) == false )
			{
				ClearAutoComplete();
				return;
			}

			// Find the context from list of context expressions
			m_acContext = eAutoCompleteContext.Ignored+1;
			for ( ; m_acContext < eAutoCompleteContext.FirstFullLineContext; ++m_acContext )
			{
				if ( TryParse(m_acContext, ref expression) )
					break;
			}

			foundContext = m_acContext < eAutoCompleteContext.Count && m_acLists[(int)m_acContext] != null;
		}

		// Add keywords depending on context
		if ( foundContext )
		{
			if ( m_acContext != oldContext ) // don't update entire list if context hasn't changed
			{
				// Un-cancel && reset selected index when context changes
				m_acCanceled = false;
				m_acSelectedIndex = 0;
			}

			m_acRemaining = expression;

			// Update access to other room or character scripts. (eg: RoomKitchen.Script, or it's alias in questscript, R.Kitchen.Script)
			if ( m_acContext != oldContext && m_acContext == eAutoCompleteContext.ObjectScript )
			{
				// Find Room name using reflection from R.<roomname>.Script.
				string input = m_text.Substring(expressionStart, ((index-expressionStart)-m_acRemaining.Length));
				Match match = Regex.Match(input, @"(\w\.)(\w+)(?:\.Script\.)");
				if ( match.Success )
				{
					char classType = match.Groups[1].Value[0];
					string className = match.Groups[2].Value;
					if ( classType == 'R' ) className = "Room"+className;
					if ( classType == 'C' ) className = "Character"+className;
					if ( classType == 'I' ) className = "Inventory"+className;

					// Find class by name
					System.Type scriptType = System.Type.GetType( string.Format("{0}, {1}", className, PowerQuestEditor.GetPowerQuest().GetType().Assembly.FullName ) );
					List<string> contextList = m_acLists[(int)m_acContext];
					contextList.Clear();

					if ( scriptType != null )
					{						
						// Save the type of the script we found so we can find function parameters later
						m_lastObjectScriptType = scriptType;

						System.Array.ForEach(scriptType.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static),
							item => {if (item.IsSpecialName == false && item.Name[0] !='<') contextList.Add(item.Name + "(" + (item.GetParameters().Length > 0 ? string.Empty : ")"));} );
						System.Array.ForEach(scriptType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance),
							item => contextList.Add(item.Name) );
						System.Array.ForEach(scriptType.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static),
							item => contextList.Add(item.Name) );
						System.Array.ForEach(scriptType.GetNestedTypes(BindingFlags.DeclaredOnly | BindingFlags.Public ),
							item => {if (item.IsSpecialName == false) contextList.Add(item.Name);} );
					}
				}
			}
			else if ( m_acContext != oldContext && m_acContext == eAutoCompleteContext.AnimChar )
			{
				// Add animation names dynamically
				string input = m_text.Substring(expressionStart, ((index-expressionStart)-m_acRemaining.Length));
				Match match = Regex.Match(input, @"^\s*(?:C\.)?(\w+)\."); // TODO: make this work for Plr. and C.Plr as well.
				if ( match.Success )
				{
					List<string> contextList = m_acLists[(int)m_acContext];
					contextList.Clear();

					string charName = match.Groups[1].Value;
					CharacterComponent charPrefab= PowerQuestEditor.GetPowerQuest().GetCharacterPrefabs().Find(character=>character.GetData().ScriptName == charName);
					if ( charPrefab == null ) // if didn't find prefab, assume it's Plr or C.Player or something
						charPrefab = PowerQuestEditor.GetPowerQuest().GetCharacterPrefabs()[0]; // Also assuming player is first in list still
					charPrefab?.GetAnimations()?.ForEach( item=>  
					{ 
						if ( item != null )
						{						
							// Add name with stripped R,L,U,D,UR,UL,DR,UL
							string anim = item.name;
							int sublen = anim.Length-1;
							char lastC = anim[sublen];
							if ( lastC=='R' || lastC=='L' )
								sublen--;
							lastC = anim[sublen];
							if ( lastC=='U' || lastC=='D' )
								sublen--;
							if ( sublen != anim.Length-1 )	
							{
								anim = anim.Substring(0,sublen+1);
								if ( contextList.Contains(anim) == false )
									contextList.Add($"\"{anim}\""); 
							}
							// Add the directional name
							contextList.Add($"\"{item.name}\""); 
						}
					} );
					
				}
			}

			// Add items that match remaining expression to the list
			m_acList.Clear();
			// Add items if start with typed in text
			m_acLists[(int)m_acContext].ForEach( item => { if ( item.StartsWith(m_acRemaining, System.StringComparison.OrdinalIgnoreCase) ) m_acList.Add(item); } );

			// Add items that don't start with, but contain what's being typed
			if ( m_acRemaining.Length > 1 )
				m_acLists[(int)m_acContext].ForEach( item => { if ( item.IndexOf(m_acRemaining, System.StringComparison.OrdinalIgnoreCase) != -1 && m_acList.Contains(item) == false ) m_acList.Add(item); } );

			// Dynamically add enum items. Could potentially do this dynamically with other types, but for now enums will suffice
			if ( m_acContext == eAutoCompleteContext.EnumItem )
			{				
				// find enum type using reflection
				string enumName = m_text.Substring(expressionStart, ((index-expressionStart)-m_acRemaining.Length)-1); // -1 to remove '.' at end of eStateBlah.
				System.Type scriptType = null;
				if ( scriptType == null )
				{
					// Look for global enums
					scriptType = System.Type.GetType(string.Format("{0}, {1}", enumName, PowerQuestEditor.GetPowerQuest().GetType().Assembly.FullName ) );
				}
				if ( scriptType == null )
				{
					// Look for globalscript enums (accessed via `using static GlobalScript;`)
					scriptType = System.Type.GetType(string.Format("GlobalScript+{0}, {1}", enumName, PowerQuestEditor.GetPowerQuest().GetType().Assembly.FullName ) );
				}
				if ( scriptType == null )
				{
					// Look for current script enums
					scriptType = System.Type.GetType(string.Format("{0}+{1}, {2}", m_scriptClass, enumName, PowerQuestEditor.GetPowerQuest().GetType().Assembly.FullName ) );
				}
				if ( scriptType == null )
				{
					// Look for powerquest enums
					scriptType = System.Type.GetType(string.Format("PowerTools.Quest.{0}, {1}", enumName, PowerQuestEditor.GetPowerQuest().GetType().Assembly.FullName ) );
				}

				if ( scriptType != null )
				{					
					string[] enumNames = System.Enum.GetNames(scriptType);
					// Add names if start with typed in text
					System.Array.ForEach(enumNames, item => { if ( item.StartsWith(m_acRemaining, System.StringComparison.OrdinalIgnoreCase) ) m_acList.Add(item); } );
					// Add items that don't start with, but contain what's being typed
					if ( m_acRemaining.Length > 1 )
						System.Array.ForEach(enumNames, item => { if ( item.IndexOf(m_acRemaining, System.StringComparison.OrdinalIgnoreCase) != -1 && m_acList.Contains(item) == false ) m_acList.Add(item); } );
				}	
	
			}


			// Don't show if there's only one match and it's been compeltely typed in anyway
			if ( m_acList.Count == 1 && m_acRemaining == m_acList[0] )
				m_acList.Clear();
			
			//Debug.Log( "AC: "+ m_acContext.ToString() + "-"+m_acRemaining);
		}
		else 
		{
			ClearAutoComplete();
		}
	}

	// Check if we're in a function parameter section and autocomplete that
	bool UpdateACMethodParams(bool force = false)
	{
		// Work back until whitespace
		TextEditor ted = FindTextEditor();
		if ( m_acMethodCursorIndexCached == ted.cursorIndex && force == false )
			return false;
		m_acMethodCursorIndexCached = ted.cursorIndex;

		// Clear method list
		m_acMethodParamList.Clear();

		//TextEditor ted = FindTextEditor();
		//m_acMethodParamList.Clear();
		///if ( m_acList.Count > 0 )
		//	return false;

		//	- When "In" a function param list- show function params. Comment too if possible (I imagine it's not)- would need attributes for the comments
		//		+ If not currently autocompleting any valid things
		//		+ Search backward for '(', cancel if hit ')', or new line, save as imagined cursor position
		int paramNum = 0;
		int index = -1;
		for ( int i = m_acMethodCursorIndexCached-1; i >= 0; --i )
		{			
			if ( m_text[i] == '(' )
			{
				index = i;
				break;
			}
			if ( m_text[i] == ')' || m_text[i] == '\n' )
			{
				break;
			}
			if ( m_text[i] == ',' )
				++paramNum;
		}

		if ( index == -1 )
			return false ; // None Found

		m_acMethodTextRemaining = m_text.Substring(index,m_acMethodCursorIndexCached-index);

		// We're inside a function!
		//		- Extract 'word' from that position
		//		- search regexes to find the "context".
		//		- and extract function name (bit after the '.')
		//		- Search backward for ',' to see how many parameters have been used, don't show ones that aren't.
				
		// Find start of current expression- TODO Cleanup: extract this into function, it's done 3 times.
		if ( FindExpressionStart(index, false, out int expressionStart, out string expression) == false )
		{
			return false;
		}

		// Find the context from list of context expressions
		eAutoCompleteContext functionContext = eAutoCompleteContext.Ignored+1;
		for ( ; functionContext < eAutoCompleteContext.Count; ++functionContext )
		{
			if ( TryParse(functionContext, ref expression) )
				break;
		}

		bool foundContext = functionContext < eAutoCompleteContext.Count && AC_CONTEXT_FUNCTION[(int)functionContext] && m_acLists[(int)functionContext] != null;

		if ( foundContext == false )
			return false;

		//		- Using context and function name, find the method info, and thusly, parameters
		System.Type type = null;
		switch (functionContext)
		{
			case eAutoCompleteContext.IEngine: type = typeof(IPowerQuest); break;	
			case eAutoCompleteContext.ICharacter: type = typeof(ICharacter); break;	
			case eAutoCompleteContext.IRoom: type = typeof(IRoom); break;	
			case eAutoCompleteContext.IInventory: type = typeof(IInventory); break;	
			case eAutoCompleteContext.IDialogTree: type = typeof(IDialogTree); break;	
			case eAutoCompleteContext.IDialogOption: type = typeof(IDialogOption); break;	
			case eAutoCompleteContext.IGui: type = typeof(IGui); break;	
			case eAutoCompleteContext.IHotspot: type = typeof(IHotspot); break;	
			case eAutoCompleteContext.IProp: type = typeof(IProp); break;	
			case eAutoCompleteContext.IRegion: type = typeof(IRegion); break;	
			case eAutoCompleteContext.Globals: type = typeof(GlobalScript); break;	
			case eAutoCompleteContext.Audio: type = typeof(SystemAudio); break;	
			case eAutoCompleteContext.ICamera: type = typeof(ICamera); break;	
			case eAutoCompleteContext.Settings: type = typeof(QuestSettings); break;	
			case eAutoCompleteContext.ObjectScript: type = m_lastObjectScriptType;  break;
		}

		if ( type == null )
			return false;

		// Get all matching functions
		IEnumerable<MethodInfo> methodInfos = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
			.Where( item => item.Name == expression && item.GetParameters().Count() > paramNum );
		
		foreach ( MethodInfo methodInfo in methodInfos )
		{
			// List all the functions
			StringBuilder builder = new StringBuilder();

			// Put return type at start
			bool blocking = ( methodInfo.ReturnType == typeof(Coroutine) || methodInfo.ReturnType == typeof(IEnumerator) );
			if ( blocking )
				builder.Append("void ");
			else 
				builder.Append(GetFriendlyName(methodInfo.ReturnType)).Append(' ');

			// Now open brackets, then function params
			builder.Append(expression).Append("( ");
			for ( int i = 0; i < methodInfo.GetParameters().Count(); ++i )
			{
				// Add comma between
				if ( i > 0 )
					builder.Append(", ");
					 
				// Bold the current param
				if( i == paramNum )
					builder.Append("<b>");

				// Build param, adding default value if there's one
				ParameterInfo parameter = methodInfo.GetParameters()[i];
				builder.Append(GetFriendlyName(parameter.ParameterType)).Append(' ').Append(parameter.Name);
				if ( (parameter.Attributes & ParameterAttributes.HasDefault) > 0 )
					builder.Append(" = ").Append(GetFriendlyName(parameter.DefaultValue));

				if( i == paramNum )
					builder.Append("</b>");
			}
			builder.Append(" )");
			if ( blocking )
				builder.Append(" <i>- blocking</i>");
			m_acMethodParamList.Add(builder.ToString());

		}

		return foundContext;

		//		- Show similar UI to autocomplete, with the different parameters. No autocomplete though.

	}	

	// Returns true if found an expressiopnindex at start of expression
	bool FindExpressionStart( int index, bool fullLine, out int expressionStart, out string expression )
	{
		expression = null;
		expressionStart = m_text.LastIndexOfAny(fullLine ? AC_STARTSEQCHARS_FULLLINE : AC_STARTSEQCHARS, Mathf.Max(0,index-1) ); 

		if ( expressionStart >= 0 ) ++expressionStart; // don't include the space
		expressionStart = Mathf.Clamp(expressionStart, 0, m_text.Length);
		if ( index-expressionStart <= 0 )
		{
			// Didn't find expression
			return false;
		}
		expression = m_text.Substring(expressionStart, index-expressionStart);
		return true;
	}

	void ClearAutoComplete()
	{
		m_acList.Clear();
		m_acContext = eAutoCompleteContext.Ignored;
		m_acRemaining = string.Empty;
		m_acCanceled = false;
		m_acSelectedIndex = 0;

		// Set cursor index to the current so we don't immediately just calc the autocomplete again
		if ( FindTextEditor() != null)
			m_acCursorIndexCached = FindTextEditor().cursorIndex;
	}


	bool HasAutoComplete()
	{
		return m_acList.Count > 0 && m_acCanceled == false;
	}

	bool DoAutoComplete()
	{

		TextEditor tEditor = FindTextEditor();
		if ( tEditor == null) 
			return false;

		if ( m_acList.Count <= 0 || m_acCanceled || m_acSelectedIndex < 0 || m_acSelectedIndex >= m_acList.Count)
			return false;

		// Wanna replace the m_acRemaining before the curr cursor position with current text and update the cursor position
		string replaceText = m_acList[ m_acSelectedIndex ];

		int replaceEnd = tEditor.cursorIndex;
		int replaceStart = replaceEnd - m_acRemaining.Length; 
		int replaceLen = m_acRemaining.Length;
		if ( replaceStart < 0 )
			return false;
		m_text = m_text.Remove(replaceStart, m_acRemaining.Length);
		m_text = m_text.Insert(replaceStart,replaceText);

		tEditor.text = m_text;
		tEditor.cursorIndex = replaceStart + replaceText.Length;
		tEditor.selectIndex = tEditor.cursorIndex;

		UpdateAutoComplete(true);

		GUI.changed = true;
		return true;
	}


	#endregion
	#region: Functions: Layout

	void LayoutAutoCompleteList(Rect rect, GUIStyle style)
	{		
		TextEditor editor = FindTextEditor();
		if ( m_acList.Count <= 0 || editor == null || m_acCanceled ) // TODO: don't show if remaining matches only 
			return; 

		float lineHeight = 15;
		int numLinesShown =  Mathf.Min(m_acList.Count, 5);

		float offsetX = -style.CalcSize( new GUIContent(m_acRemaining) ).x+18;
		// Adjust rect based on size of text
		rect.min = rect.min + editor.graphicalCursorPos + new Vector2(offsetX,lineHeight);
		// Set x/y based on cursor pos
		rect.max = rect.min + new Vector2( 220 , lineHeight * numLinesShown );

		// Backround + border
		EditorGUI.DrawRect(new Rect(rect) { height = rect.height+2, width = rect.width+2,center = rect.center },new Color(0.616f, 0.635f, 0.651f,1)); // border
		EditorGUI.DrawRect(rect, new Color(0.93f,0.93f,0.95f,1));
		GUI.contentColor = Color.black;

		int end = Mathf.Min( m_acSelectedIndex+2, m_acList.Count-1 );
		int start = Mathf.Max( end-4, 0 );

		for ( int i = start; i < start+5 && i < m_acList.Count; ++i )
		{
			if ( m_acSelectedIndex == i )
				EditorGUI.DrawRect(new Rect(rect){yMin = rect.yMin+((i-start)*lineHeight), height = lineHeight},  GUI.skin.settings.selectionColor );
			EditorGUI.LabelField(new Rect(rect){yMin = rect.yMin+((i-start)*lineHeight)},m_acList[i], Styles.AUTOCOMPLETE_LABEL);

		}
		GUI.contentColor = Color.white;
	}


	void LayoutAutoCompleteMethodParams(Rect rect, GUIStyle textAreaStyle)
	{		
		TextEditor editor = FindTextEditor();
		if ( m_acMethodParamList.Count <= 0 || editor == null )
			return; 

		float maxWidth = 0;
		GUIStyle style = new GUIStyle( EditorStyles.label) { richText = true };
		GUIContent tempGuiContext = new GUIContent(string.Empty);
		for ( int i = 0; i < m_acMethodParamList.Count; ++i )
		{
			tempGuiContext.text = m_acMethodParamList[i];
			float maxW = style.CalcSize( tempGuiContext).x;
			if ( maxW > maxWidth )
				maxWidth = maxW;
		}

		// Adjust rect based on size of text
		float offsetX = -textAreaStyle.CalcSize( new GUIContent(m_acMethodTextRemaining) ).x;
		rect.min = rect.min + editor.graphicalCursorPos + new Vector2(18 + offsetX,-5) + new Vector2(0, -15 * m_acMethodParamList.Count );
		// Set x/y based on cursor pos
		rect.max = rect.min + new Vector2(maxWidth, 15 * m_acMethodParamList.Count) ;

		// Backround + border
		EditorGUI.DrawRect(new Rect(rect) { height = rect.height+2, width = rect.width+2,center = rect.center },new Color(0.616f, 0.635f, 0.651f,1)); // border
		EditorGUI.DrawRect(rect, new Color(240.0f/255.0f,246.0f/255.0f,248.0f/255.0f,1));
		GUI.contentColor = Color.black;

		for ( int i = 0; i < m_acMethodParamList.Count; ++i )
		{
			//if ( m_acSelectedIndex == i )
			//	EditorGUI.DrawRect(new Rect(rect){yMin = rect.yMin+((i)*15), height = 15},  GUI.skin.settings.selectionColor );
			EditorGUI.LabelField(new Rect(rect){yMin = rect.yMin+((i)*15)},m_acMethodParamList[i], style);

		}
		GUI.contentColor = Color.white;

	}

	#endregion
	#region: Functions: Static helpers


	// Finds context, and returns the left over string
	static bool TryParse(eAutoCompleteContext context, ref string expression)
	{
		Match match = AC_CONTEXT_REGEX[(int)context].Match(expression);
		if ( match.Success == false )
			return false;

		// Replace "expression" with the remaining string
		if ( match.Groups.Count > 1 )
			expression = match.Groups[1].Value;

		return true;
	}

	public static string GetFriendlyName(object value)
	{
		if ( value == null )
			return "null";
		return value.ToString().ToLowerInvariant();
	}

	public static string GetFriendlyName(System.Type type)
	{
		if ( type == typeof(void))
			return "void";
	    if (type == typeof(int))
	        return "int";
	    else if (type == typeof(short))
	        return "short";
	    else if (type == typeof(byte))
	        return "byte";
	    else if (type == typeof(bool)) 
	        return "bool";
	    else if (type == typeof(long))
	        return "long";
	    else if (type == typeof(float))
	        return "float";
	    else if (type == typeof(double))
	        return "double";
	    else if (type == typeof(decimal))
	        return "decimal";
	    else if (type == typeof(string))
	        return "string";
	    else if (type.IsGenericType)
	        return type.Name.Split('`')[0] + "<" + string.Join(", ", type.GetGenericArguments().Select(x => GetFriendlyName(x)).ToArray()) + ">";
	    else
	        return type.Name;
	}

	#endregion
}

}