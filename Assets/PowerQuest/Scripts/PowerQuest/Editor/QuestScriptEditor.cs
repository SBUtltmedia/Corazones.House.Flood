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

namespace PowerTools.Quest
{

public partial class QuestScriptEditor : EditorWindow, IHasCustomMenu
{
	#region Variables: Static definitions

	public enum eType
	{
		Room,
		Prop,
		Hotspot,
		Region,
		Character,
		Inventory, 
		Dialog,
		Gui,
		Global,
		Other
	}

	// used to store "history" of files
	[System.Serializable]
	class ScriptFileData
	{
		// Path to file being opened
		public string m_file = null;
		// function name within file (null for header)
		public string m_function = null;
		// Class name if known (for autocomplete only)
		public string m_classname = null;
		// type of function if known (Ifor autocomplete only)
		public eType m_type = eType.Other;
		// whether the current function is a coroutine, in case can't check using reflection
		public bool m_isCoroutine = true;
		// Offset that the cursor was last on
		public int m_cursorIndex = 0;
	}
		
	static readonly int SCRIPT_TAB_WIDTH = 4;
	static readonly string STR_TAB = "\t";
	static readonly string STR_TAB_SPACES = "    ";
	static readonly double SAVE_ERROR_TIMEOUT = 16; // seconds after "save" that we'll pop-up any compile errors that occur. Won't work for longer compiles :(

	//static readonly GUIStyle STYLE_TOOLBAR_TOGGLE = new GUIStyle(EditorStyles.toggle) { font = EditorStyles.miniLabel.font, fontSize = EditorStyles.miniLabel.fontSize, padding = new RectOffset(15,0,3,0) };

	static readonly string REGEX_FUNCTION_START = @"\(.+\n*.*\{.*\n\r?";
	static readonly string REGEX_CLASS_START = @"public (?:partial )?class \w*.+\n*.*\{.*\n\r?";

	static readonly string STR_TABS = "\t\t";
	static readonly string STR_TABS_HEADER = "\t";

	static readonly string REGEX_YIELD_LINE_START = @"(?<=^\s*)yield return\s(";

	// Strings that should have the "yield return" removed from the start. The first group should contain whitespace
	static readonly string[] REGEX_YIELD_STRINGS = 
		{
			@"C\.\w*\.Say(?:NoSkip)?\("     +')',
			@"\w\.Display\("                +')',
			@"C\.\w*\.WalkTo\("             +')',
			@"C\.(\w*\.)?WalkToClicked"     +')',
			@"C\.(\w*\.)?Face\w*?(?<!BG)\(" +')',  // Has zero width  negative lookbehind assertion so it won't include 'BG'
			@"C\.\w*\.PlayAnimation\("      +')',
			@"C\.\w*\.WaitFor\w*?\("        +')',
			@"Prop\(.+\)\.PlayAnimation\("  +')', // TODO: these don't work for prop.MoveTo( style calls... needs fixing
			@"Prop\(.+\)\.PlayVideo\("      +')',
			@"Prop\(.+\)\.MoveTo\("         +')',
			@"Prop\(.+\)\.Fade\("           +')',
			@"Prop\(.+\)\.WaitFor\w*?\("    +')',
			@"E\.Wait"                      +')',
			@"E\.Break"                     +')',
			@"E\.ConsumeEvent"              +')',
			@"E\.FadeIn\("                  +')',
			@"E\.FadeOut\("                 +')',
			@"E\.ChangeRoom\("              +')',
			@"E\.Handle"                    +')',
			@"R\.\w*\.Enter\("              +')',
	};
	static Regex[] REGEX_YIELD_STRINGS_LOAD_COMPILED = null; // these are compiled lazily when first used
	static Regex[] REGEX_YIELD_STRINGS_SAVE_COMPILED = null; // these are compiled lazily when first used
			
	static readonly string REGEX_SAVE_YIELD_REPLACE = @"yield return $1"; 

	static readonly Regex[] REGEX_LOAD_MATCH = 
		{
			new Regex(@"(?<=^\s*)C\.(\w*)\.Say\(\s*\$?""(.*)""\s*\);", RegexOptions.Compiled), // C.Dave.Say("Hi"); -> Dave: Hi
			new Regex(@"(?<=^\s*)\w\.Display\(\s*\$?""(.*)""\s*\);", RegexOptions.Compiled), // C.Display("Ho"); -> Display: Ho
			new Regex(@"(?<=^\s*)\w\.DisplayBG\(\s*\$?""(.*)""\s*\);", RegexOptions.Compiled), // C.DisplayBG("Ho"); -> Display: Ho
			new Regex(@"(?<=^\s*)\w\.Section\(\s*\$?""(.*)""\s*\);", RegexOptions.Compiled), // C.DisplayBG("Ho"); -> Display: Ho
			new Regex(@"(?<=^\s*)C\.(\w*)\.Say\(\s*\$?""(.*)""\s*,\s*(\d+)\s*\);", RegexOptions.Compiled), // C.Dave.Say("Hi",123); -> Dave(123): Hi
			new Regex(@"(?<=^\s*)\w\.Display\(\s*\$?""(.*)""\s*,\s*(\d+)\s*\);", RegexOptions.Compiled), // C.Display("Ho",456); -> Display(456): Ho
			new Regex(@"(?<=^\s*)\w\.DisplayBG\(\s*\$?""(.*)""\s*,\s*(\d+)\s*\);", RegexOptions.Compiled), // C.DisplayBG("Ho",456); -> Display(456): Ho
			new Regex(@"(?<=^\s*)E\.WaitSkip\(2\.0f\);", RegexOptions.Compiled), // E.WaitSkip(2.0f); -> ......
			new Regex(@"(?<=^\s*)E\.WaitSkip\(1\.5f\);", RegexOptions.Compiled), // E.WaitSkip(1.5f); -> .....
			new Regex(@"(?<=^\s*)E\.WaitSkip\(1\.0f\);", RegexOptions.Compiled), // E.WaitSkip(1.0f); -> ....
			new Regex(@"(?<=^\s*)E\.WaitSkip\(\);", RegexOptions.Compiled), 	  // E.WaitSkip();	 -> ...
			new Regex(@"(?<=^\s*)E\.WaitSkip\(0\.25f\);", RegexOptions.Compiled),// E.WaitSkip(0.2f); -> ..
			new Regex(@"(?<=^\s*)E\.Break;", RegexOptions.Compiled), // E.Break; -> End
			new Regex(@"(?<=^\s*)E\.ConsumeEvent;", RegexOptions.Compiled), // E.ConsumeEvent; -> Consume
			new Regex(@"(?<=^\s*)yield break;", RegexOptions.Compiled), // yield break; -> Return
			new Regex(@"(?<=^\s*)C\.(Player\.)?WalkToClicked\(\);", RegexOptions.Compiled), 	// C.Player.WalkToClicked() -> WalkToClicked
			new Regex(@"(?<=^\s*)C\.(Player\.)?FaceClicked\(\);", RegexOptions.Compiled), 	// C.Player.FaceClicked() -> FaceClicked
			new Regex(@"(?<=^|\W)Hotspot\(\s*""(\w+)""\s*\)", RegexOptions.Compiled), 	// Hotspot("blah") -> Hotspots.blah.
			new Regex(@"(?<=^|\W)Prop\(\s*""(\w+)""\s*\)", RegexOptions.Compiled), 	// Prop("blah") -> Props.blah.
			new Regex(@"(?<=^|\W)Region\(\s*""(\w+)""\s*\)", RegexOptions.Compiled), 	// Region("blah") -> Regions.blah.
			new Regex(@"(?<=^|\W)Point\(\s*""(\w+)""\s*\)", RegexOptions.Compiled), 	// Point("blah") -> Points.blah		
			new Regex(@"(?<=^|\W)Option\(\s*""(\w+)""\s*\)", RegexOptions.Compiled), 	// Option("blah") -> O.blah.	
			new Regex(@"(?<=^|\W)Option\(\s*(\d+)\s*\)", RegexOptions.Compiled), 	// Option(1) -> O.1.
			new Regex(@"(?<=^|\W)C\.Plr\.", RegexOptions.Compiled), 	// C.Plr. -> Plr.
			new Regex(@"(?<=^|\W)GlobalScript\.Script\.", RegexOptions.Compiled), 	// GlobalScript.Script. -> Globals. No longer needed, but left to cleanup old scripts that might still be using GlobalScript.Script
			new Regex(@"(?<=^|\W)(R|C|I)(?:oom|haracter|nventory)(\w*)\.Script\.", RegexOptions.Compiled), 	// RoomKitchen.Script. => R.Kitchen.Script. (or CharacterDave => C.Dave, or InventoryBucket => I.Bucket)

		};

	static readonly string[] REGEX_LOAD_REPLACE = 
		{
			@"$1: $2",
			@"Display: $1",
			@"DisplayBG: $1",
			@"Section: $1",
			@"$1($3): $2",
			@"Display($2): $1",
			@"DisplayBG($2): $1",
			@"......",
			@".....",
			@"....",
			@"...",
			@"..",
			@"End",
			@"Consume",
			@"Return",
			@"WalkToClicked",
			@"FaceClicked",
			@"H.$1",
			@"P.$1",
			@"Regions.$1",
			@"Points.$1",
			@"O.$1",
			@"O.$1",
			@"Plr.",
			@"Globals.",
			@"$1.$2.Script.",
		};

	// Don't match the following (exceptions to REGEX_SAVE_MATCH)
	static readonly Regex[] REGEX_SAVE_NO_MATCH =
		{
			new Regex(@"(?<=^\s*)default: (.*)", RegexOptions.Compiled),
		};

	static readonly Regex[] REGEX_SAVE_MATCH = 
		{
			new Regex(@"\s+$", RegexOptions.Compiled), // strip trailing space

			// Strings with braces- these need the $ at the beggining of the string so variables can be included
			new Regex(@"^(\s*)Section: (.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
			new Regex(@"^(\s*)Display: (.*\{.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase), // $
			new Regex(@"^(\s*)Display: (.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
			new Regex(@"^(\s*)DisplayBG: (.*\{.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase), // $
			new Regex(@"^(\s*)DisplayBG: (.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
			new Regex(@"^(\s*)Display\((\d+)\): (.*\{.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase), // $
			new Regex(@"^(\s*)Display\((\d+)\): (.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
			new Regex(@"^(\s*)DisplayBG\((\d+)\): (.*\{.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase), // $
			new Regex(@"^(\s*)DisplayBG\((\d+)\): (.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
			new Regex(@"^(\s*)(\w+): (.*\{.*)", RegexOptions.Compiled), // $
			new Regex(@"^(\s*)(\w+): (.*)", RegexOptions.Compiled),			
			new Regex(@"^(\s*)(\w+)\((\d+)\): (.*\{.*)", RegexOptions.Compiled), //$
			new Regex(@"^(\s*)(\w+)\((\d+)\): (.*)", RegexOptions.Compiled),

			new Regex(@"^(\s*)\.\.\.\.\.\.", RegexOptions.Compiled),
			new Regex(@"^(\s*)\.\.\.\.\.", RegexOptions.Compiled),
			new Regex(@"^(\s*)\.\.\.\.", RegexOptions.Compiled),
			new Regex(@"^(\s*)\.\.\.", RegexOptions.Compiled),
			new Regex(@"^(\s*)\.\.", RegexOptions.Compiled),
			new Regex(@"^(\s*)End\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
			new Regex(@"^(\s*)Consume\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
			new Regex(@"^(\s*)Return\s*$", RegexOptions.Compiled),
			new Regex(@"^(\s*)WalkToClicked", RegexOptions.Compiled | RegexOptions.IgnoreCase),
			new Regex(@"^(\s*)FaceClicked", RegexOptions.Compiled | RegexOptions.IgnoreCase),
			new Regex(@"(^|\W)H\.(\w+)", RegexOptions.Compiled),
			new Regex(@"(^|\W)P\.(\w+)", RegexOptions.Compiled),
			new Regex(@"(^|\W)Regions\.(\w+)", RegexOptions.Compiled),
			new Regex(@"(^|\W)Points\.(\w+)", RegexOptions.Compiled),
			new Regex(@"(^|\W)O\.(\d+)", RegexOptions.Compiled),
			new Regex(@"(^|\W)O\.(\w+)", RegexOptions.Compiled),
			new Regex(@"(^|[^\.\w])Plr\.", RegexOptions.Compiled), // match start of line, or non-word that's not a '.'
			new Regex(@"(^|\W)R\.(\w+)\.Script\.", RegexOptions.Compiled),
			new Regex(@"(^|\W)C\.(\w+)\.Script\.", RegexOptions.Compiled),
			new Regex(@"(^|\W)I\.(\w+)\.Script\.", RegexOptions.Compiled),
		};

	// The $1 preserves any spacing that's at the start
	static readonly string[] REGEX_SAVE_REPLACE = 
		{
			@"",	// Strip trailing space
			@"$1C.Section(""$2"");", // C.Section("Ho"); -> Section: Ho
			@"$1C.Display($""$2"");", // C.Display($"Ho {variable}"); -> Display: Ho {variable}
			@"$1C.Display(""$2"");", // C.Display("Ho"); -> Display: Ho
			@"$1E.DisplayBG($""$2"");", // C.DisplayBG($"Ho {variable}""); -> Display: Ho {variable}
			@"$1E.DisplayBG(""$2"");", // C.DisplayBG("Ho"); -> Display: Ho
			@"$1C.Display($""$3"", $2);", // C.Display($"Ho {variable}",456); -> Display(456): Ho {variable}
			@"$1C.Display(""$3"", $2);", // C.Display("Ho",456); -> Display(456): Ho
			@"$1E.DisplayBG($""$3"", $2);", // C.DisplayBG($"Ho {variable}",456); -> Display(456): Ho {variable}
			@"$1E.DisplayBG(""$3"", $2);", // C.DisplayBG("Ho",456); -> Display(456): Ho
			@"$1C.$2.Say($""$3"");", // C.Dave.Say($"Hi {variable}"); -> Dave: Hi {variable}
			@"$1C.$2.Say(""$3"");", // C.Dave.Say("Hi"); -> Dave: Hi
			@"$1C.$2.Say($""$4"", $3);", // C.Dave.Say($"Hi {variable}",123); -> Dave(123): Hi {variable}
			@"$1C.$2.Say(""$4"", $3);", // C.Dave.Say("Hi",123); -> Dave(123): Hi
			
			@"$1E.WaitSkip(2.0f);", // E.WaitSkip(); -> ......
			@"$1E.WaitSkip(1.5f);", // E.WaitSkip(); -> .....
			@"$1E.WaitSkip(1.0f);", // E.WaitSkip(); -> ....
			@"$1E.WaitSkip();", // E.WaitSkip(); -> ...
			@"$1E.WaitSkip(0.25f);", // E.WaitSkip(); -> ..
			@"$1E.Break;", // E.Break; -> end
			@"$1E.ConsumeEvent;", // E.ConsumeEvent; -> consume
			@"$1yield break;", // yield break; -> Return
			@"$1C.WalkToClicked();", // C.Player.WalkToClicked() -> WalkToClicked
			@"$1C.FaceClicked();", // C.Player.FaceClicked() -> FaceClicked
			@"$1Hotspot(""$2"")", // // Hotspot("blah") -> Hotspots.blah.
			@"$1Prop(""$2"")", // // Prop("blah") -> Props.blah.
			@"$1Region(""$2"")", // // Region("blah") -> Regions.blah.
			@"$1Point(""$2"")", // // Point("blah") -> Points.blah.
			@"$1Option($2)", // // Option(3) -> O.3.
			@"$1Option(""$2"")", // // Option("blah") -> O.blah.
			@"$1C.Plr.", // // Plr. -> C.Plr.
			@"$1Room$2.Script.", // R.Kitchen.Script. -> RoomKitchen.Script.
			@"$1Character$2.Script.", // C.Dave.Script. -> CharacterDave.Script.
			@"$1Inventory$2.Script.", // I.Bucket.Script. -> InventoryBucket.Script.
		};


	static string STR_REPLACE_COLOR = @"<color=#009695>$1</color>"; // cyan : 009695 blue: 3364A4 pink: C12DAC
	static string STR_REPLACE_COLOR_DIALOG = @"<color=#3364A4>$1</color><color=#F57D00>$2</color>";
	static string STR_REPLACE_COLOR_COMMENT = @"<color=#118011>$1</color>"; // grey: 888888 green: 008000

	static readonly Regex[] REGEX_COLOR_DIALOG = 
		{
			new Regex(@"(?<=^\s*)(\w+:)(.*)", RegexOptions.Compiled | RegexOptions.Multiline),	        // Sister: hello
			new Regex(@"(?<=^\s*)(\w+\(\d+\):)(.*)", RegexOptions.Compiled | RegexOptions.Multiline),  // Sister(21): hello
			new Regex(@"()((?<!<c.*)"".*?"")", RegexOptions.Compiled | RegexOptions.Multiline)    // "Something in quotes", ignoring if there's another color on the line already
		};

	static readonly Regex[] REGEX_COLOR = 
		{
			new Regex(@"(?<=^\s*)(End\s*)$", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase),
			new Regex(@"(?<=^\s*)(Consume\s*)$", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase),
			new Regex(@"(?<=^\s*)(Return\s*)$", RegexOptions.Compiled | RegexOptions.Multiline),
			new Regex(@"(?<=^\s*)(WalkToClicked\s*)$", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase),
			new Regex(@"(?<=^\s*)(FaceClicked\s*)$", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase),
			new Regex(@"(?<=^\s*)(\.\.+\s*)$", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase),
			// Check for keywords. There's an extra check for any previous <color> on the same line (indicating a comment was there, not perfect, but better than nothing)
			new Regex(@"(?<=^|\W)((?<!<c.*)(?:bool|int|float|string|Vector2|enum|if|else|while|for|switch|case|default|break|continue|new|public|private|true|false)(?!\w))", RegexOptions.Compiled | RegexOptions.Multiline),
		};

	static readonly Regex[] REGEX_COLOR_COMMENT = 
	{
		new Regex(@"(//.*)", RegexOptions.Compiled | RegexOptions.Multiline),			// blah
		new Regex(@"(/\*.*?\*/)", RegexOptions.Compiled | RegexOptions.Singleline),		/* blah */
	};
	
	static readonly System.Type TYPE_COMPILERGENERATED = typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute);
	
	[System.Serializable]
	public class Colors
	{
		public enum eTheme { Custom = -1, LightMono, ClassicAGS, DarkVisualStudio, IrreverentPalenight, Synthwave15Bit, HotDogStand }
		
		public void SetTheme(eTheme theme)
		{
			switch( theme )
			{
				case eTheme.LightMono:
				{
					m_plainText  = new Color(0.0f, 0.0f, 0.0f, 1);
					m_event      = new Color(0.0f, 0.59f, 0.58f,1);
					m_speaker    = new Color(0.2f, 0.4f, 0.64f,1);
					m_dialog     = new Color(0.96f, 0.49f, 0.0f,1);
					m_comment    = new Color(0.067f, 0.501f, 0.067f,1);
					m_background = new Color(1,1,0.95f,1);
					m_sidebar    = new Color(0.93f,0.93f,0.95f,1);
					m_cursor     = new Color(0,0,0,1);
				} break;
				
				case eTheme.ClassicAGS:
				{				
					m_plainText  = ColorX.HexToRGB("000000");
					m_event      = ColorX.HexToRGB("0000F4");
					m_speaker    = ColorX.HexToRGB("2B91AF");
					m_dialog     = ColorX.HexToRGB("961B1B");
					m_comment    = ColorX.HexToRGB("1B7F1B");
					m_background = ColorX.HexToRGB("FFFFFF");
					m_sidebar    = ColorX.HexToRGB("F0F0F0");
					m_cursor     = ColorX.HexToRGB("000000");
				} break;
				
				case eTheme.DarkVisualStudio:
				{
					m_plainText  = new Color(0.89f, 0.96f, 1.0f, 1);
					m_event      = new Color(0.8962264f, 0.5876201f, 0.871734f,1);
					m_speaker    = new Color(0.3545746f, 0.6559497f, 0.9056604f,1);
					m_dialog     = new Color(0.8679245f, 0.5978644f, 0.487184f,1);
					m_comment    = new Color(0.46f, 0.64f, 0.36f,1);
					m_background = new Color(0.1176f,0.1176f,0.1176f,1);
					m_sidebar    = new Color(0.1450f,0.1450f,0.1450f,1);
					m_cursor     = new Color(1,1,1,1);
				} break;
				
				case eTheme.HotDogStand:
				{
					m_plainText  = Color.white;
					m_event      = Color.yellow;
					m_speaker    = Color.yellow;
					m_dialog     = Color.white;
					m_comment    = Color.black;
					m_background = Color.red;
					m_sidebar    = Color.yellow;
					m_cursor     = Color.yellow;
				} break;
				
				case eTheme.IrreverentPalenight:
				{
					m_plainText  = new Color(r: 0.6509804f, g: 0.6745098f, b: 0.8039216f, a: 1		);
					m_event      = new Color(r: 0.9411765f, g: 0.44313726f, b: 0.47058824f, a: 1	);
					m_speaker    = new Color(r: 0.78039217f, g: 0.57254905f, b: 0.91764706f, a: 1	);
					m_dialog     = new Color(r: 0.7647059f, g: 0.9098039f, b: 0.5529412f, a: 1		);
					m_comment    = new Color(r: 0.40392157f, g: 0.43137255f, b: 0.58431375f, a: 1	);
					m_background = new Color(r: 0.16078432f, g: 0.1764706f, b: 0.24313726f, a: 1	);
					m_sidebar    = new Color(r: 0.213452f, g: 0.2284135f, b: 0.292f, a: 1			);
					m_cursor     = new Color(r: 1f, g: 0.79607844f, b: 0.41960785f, a: 1			);
				} break;

				case eTheme.Synthwave15Bit:
				{
					m_plainText  = ColorX.HexToRGB("00fff1");
					m_event      = ColorX.HexToRGB("E9115F");
					m_speaker    = ColorX.HexToRGB("1ED9CB");
					m_dialog     = ColorX.HexToRGB("FF58C2");
					m_comment    = ColorX.HexToRGB("FF0070");
					m_background = ColorX.HexToRGB("2D1633");
					m_sidebar    = ColorX.HexToRGB("FF0486");
					m_cursor     = ColorX.HexToRGB("FFEB04");
				} break;
			}

		}
		
		public Color m_plainText = new Color(0.0f, 0.0f, 0.0f);
		public Color m_event =     new Color(0.0f, 0.59f, 0.58f);
		public Color m_speaker =   new Color(0.2f, 0.4f, 0.64f);
		public Color m_dialog =    new Color(0.96f, 0.49f, 0.0f);
		public Color m_comment =   new Color(0.067f, 0.501f, 0.067f,1);
		
		public Color m_background = new Color(1.0f, 1.0f, 0.95f, 1);
		public Color m_sidebar = new Color(0.93f,0.93f,0.95f,1); // TODO
		public Color m_cursor = Color.black;		
	}

	class Contents
	{
		public static readonly GUIContent PLAY = EditorGUIUtility.IconContent("PlayButton");
		public static readonly GUIContent PAUSE = EditorGUIUtility.IconContent("PauseButton");
		public static readonly GUIContent PREV = EditorGUIUtility.IconContent("Animation.PrevKey");
		public static readonly GUIContent NEXT = EditorGUIUtility.IconContent("Animation.NextKey");
		
		public static readonly GUIContent EYE = EditorGUIUtility.IconContent("ViewToolOrbit");

		public static readonly GUIContent SPEEDSCALE = EditorGUIUtility.IconContent("SpeedScale");
		public static readonly GUIContent ZOOM = EditorGUIUtility.IconContent("ViewToolZoom");
		public static readonly GUIContent LOOP_OFF = EditorGUIUtility.IconContent("RotateTool");
		public static readonly GUIContent LOOP_ON = EditorGUIUtility.IconContent("RotateTool On");
		public static readonly GUIContent PLAY_HEAD = EditorGUIUtility.IconContent("me_playhead");
		public static readonly GUIContent EVENT_MARKER = EditorGUIUtility.IconContent("Animation.EventMarker");
		public static readonly GUIContent ANIM_MARKER = EditorGUIUtility.IconContent("blendKey");		

		public static readonly GUIContent ERROR = EditorGUIUtility.IconContent("console.erroricon");
		public static readonly GUIContent ERROR_GREY = EditorGUIUtility.IconContent("console.erroricon.inactive.sml");
		
	}
	
	class Styles
	{		
		public static readonly GUIStyle TOOLBAR_TOGGLE = new GUIStyle(EditorStyles.toggle) { font = EditorStyles.miniLabel.font, fontSize = EditorStyles.miniLabel.fontSize, padding = new RectOffset(15,0,3,0) };
		public static readonly GUIStyle TOOLBAR_BUTTON = new GUIStyle(EditorStyles.toolbarButton);
		public static readonly GUIStyle TOOLBAR_LABEL = new GUIStyle(EditorStyles.toolbar) { alignment = TextAnchor.MiddleLeft };
		public static readonly GUIStyle AUTOCOMPLETE_LABEL = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.UpperLeft };
	}

	enum eTabEvent { None, Tab, Backspace, Delete, Indent, Outdent, Left, Right };

	static readonly string[] FUNCTION_SORT_ORDER = { "OnEnterRoom", "OnEnterRoomAfterFade", "OnExitRoom", "UpdateBlocking", "Update", "OnAnyClick", "OnWalkTo", "OnPostRestore" };

	#endregion
	#region Variables: Serialized

	[SerializeField] string m_path = string.Empty;
	[SerializeField] string m_fileName = string.Empty;
	[SerializeField] string m_function = string.Empty;
	[SerializeField] bool m_functionIsCoroutine = true;
	[SerializeField] bool m_loaded = false;
	[SerializeField] Vector2 m_scrollPosition = Vector2.zero;
	[SerializeField] string m_text = string.Empty;
	[SerializeField] string m_richText = string.Empty;
	[SerializeField] bool m_dirty = false;
	[SerializeField] List<string> m_functionNames = new List<string>();
	[SerializeField] List<string> m_functionNamesNice = new List<string>();
	[SerializeField] bool m_editingHeader = false;
	[SerializeField] bool m_locked = false;
	[SerializeField] bool m_autoLoad = false;
	[SerializeField] int m_fontSize = 14;
	[SerializeField] int m_historyIndex = 0;
	[SerializeField] List< ScriptFileData > m_history = new List<ScriptFileData>();
	

	#endregion
	#region Variables: Private

	bool m_refresh = false;
	bool m_wasTyping = false;
	TextEditor m_textEditor = null;
	string m_currentLine = string.Empty;
	int m_currentLineCachedIndex = -1; // Cursor index last time the current line was updated
	int m_scrollingCursorIndexCached = -1; // Cursor index last time scroll positon was checked

	// Used for autocomplete
	[SerializeField]eType m_scriptType = eType.Other;
	[SerializeField]string m_scriptClass = string.Empty;

	// Used for auto load option
	IQuestScriptable m_autoLoadScriptable = null;
	string m_autoLoadFunction = null;

	// Cached version of unity's tint colour during play mode
	Color m_untintColour = Color.black;

	// cached colours from QuestScriptEditor
	[SerializeField] Colors m_colors = new Colors();

	static GUIStyle s_textStyle = null;
	long m_compileErrorExpireTime = 0;

	//[SerializeField] System.DateTime m_sourceModifiedTime = System.DateTime.MinValue;
	//static List<QuestScriptEditor> s_editors = new List<QuestScriptEditor>();			
	

	#endregion
	#region Functions: Init

	public QuestScriptEditor()
	{
	}

	public static void Open(string file, string className, eType type = eType.Other, string function = null, bool isCoroutine = true )
	{
		Event ev = Event.current;
		if ( ev != null && ev.shift )
		{
			// Open in IDE
			ViewInEditor(file, function);
			return;
		}

		//Show existing window instance. If one doesn't exist, make one.
		QuestScriptEditor editor = EditorWindow.GetWindow<QuestScriptEditor>("Quest Script");
		if ( editor.m_locked )
		{
			// NB: If the first window is locked, but there's a second that isn't locked then this will open a third. To fix, would need to maintain static reference to all open windows. perhaps register on enable?
			editor = EditorWindow.CreateInstance<QuestScriptEditor>();
			editor.Show(true);
			editor.Repaint();
		}
		if ( editor != null )
			editor.Load( file, function, false, className, type, isCoroutine );
	}

	// Overrides for easily opening specific QuestObject scripts
	public static void Open(RoomComponent component, eType type, string functionName = null, string parameters = "", bool isCoroutine = true )
	{
		Open( component.GetPrefab(), component.GetData(), type, functionName, parameters, isCoroutine );
	}
	public static void Open(RoomComponent component, string functionName = null, string parameters = "", bool isCoroutine = true )
	{
		Open( component.GetPrefab(), component.GetData(), eType.Room, functionName, parameters, isCoroutine );
	}
	public static void Open(CharacterComponent component, string functionName = null, string parameters = "", bool isCoroutine = true )
	{
		Open( component.gameObject, component.GetData(), eType.Character, functionName, parameters, isCoroutine );
	}
	public static void Open(DialogTreeComponent component, string functionName = null, string parameters = "", bool isCoroutine = true )
	{
		Open( component.gameObject, component.GetData(), eType.Dialog, functionName, parameters, isCoroutine );
	}
	public static void Open(InventoryComponent component, string functionName = null, string parameters = "", bool isCoroutine = true )
	{
		Open( component.gameObject, component.GetData(), eType.Inventory, functionName, parameters, isCoroutine );
	}
	public static void Open(GuiComponent component, string functionName = null, string parameters = "", bool isCoroutine = true )
	{
		Open( component.gameObject, component.GetData(), eType.Gui, functionName, parameters, isCoroutine );
	}

	// Shows the window, adding the function to the file if it wasn't already there (similar to QuestEditorUtils.OpenScriptFunction(...))
	static void Open(GameObject gameObject, IQuestScriptable scriptable, eType type, string functionName = null, string parameters = "", bool isCoroutine = true )
	{
		string fileTemplateText = string.Empty;
		switch (type)
		{
		case eType.Prop:		fileTemplateText = PowerQuestEditor.TEMPLATE_ROOM_FILE; break;
		case eType.Hotspot:		fileTemplateText = PowerQuestEditor.TEMPLATE_ROOM_FILE; break;	
		case eType.Region:		fileTemplateText = PowerQuestEditor.TEMPLATE_ROOM_FILE; break;	
		case eType.Room:		fileTemplateText = PowerQuestEditor.TEMPLATE_ROOM_FILE; break;			
		case eType.Character: 	fileTemplateText = PowerQuestEditor.TEMPLATE_CHARACTER_FILE; break;
		case eType.Dialog: 		fileTemplateText = PowerQuestEditor.TEMPLATE_DIALOGTREE_FILE; break;
		case eType.Inventory: 	fileTemplateText = PowerQuestEditor.TEMPLATE_INVENTORY_FILE; break;
		case eType.Gui: 		fileTemplateText = PowerQuestEditor.TEMPLATE_GUI_FILE; break;
		}

		fileTemplateText = fileTemplateText.Replace("#NAME#", scriptable.GetScriptName());

		string fileName = scriptable.GetScriptClassName() +".cs";
		string path = QuestEditorUtils.GetScriptPath(gameObject, fileName);
		if ( string.IsNullOrEmpty(functionName) )
		{
			QuestEditorUtils.CreateScript(gameObject, fileName, fileTemplateText);
		}
		else 
		{
			QuestEditorUtils.CreateScriptFunction(path, fileTemplateText, functionName,parameters,isCoroutine);
		}
		
		Open(path, scriptable.GetScriptClassName(), type, functionName, isCoroutine );
				
	}

	static string GetTemplateText( eType type, IQuestScriptable scriptable )
	{
		string fileTemplateText = string.Empty;
		switch (type)
		{
			case eType.Prop:		fileTemplateText = PowerQuestEditor.TEMPLATE_ROOM_FILE; break;
			case eType.Hotspot:		fileTemplateText = PowerQuestEditor.TEMPLATE_ROOM_FILE; break;	
			case eType.Region:		fileTemplateText = PowerQuestEditor.TEMPLATE_ROOM_FILE; break;	
			case eType.Room:		fileTemplateText = PowerQuestEditor.TEMPLATE_ROOM_FILE; break;			
			case eType.Character: 	fileTemplateText = PowerQuestEditor.TEMPLATE_CHARACTER_FILE; break;
			case eType.Dialog: 		fileTemplateText = PowerQuestEditor.TEMPLATE_DIALOGTREE_FILE; break;
			case eType.Inventory: 	fileTemplateText = PowerQuestEditor.TEMPLATE_INVENTORY_FILE; break;
			case eType.Gui: 		fileTemplateText = PowerQuestEditor.TEMPLATE_GUI_FILE; break;
		}

		fileTemplateText = fileTemplateText.Replace("#NAME#", scriptable.GetScriptName());
		return fileTemplateText;
	}
	

	void OnEnable()
	{
		InitSpellCheck();

		BuildAutoCompleteLists();
		
		EditorApplication.update -= OnUpdate; // ensure it's only there once
		EditorApplication.update += OnUpdate;

		PowerQuestEditor.OnUpdateScriptColors += OnUpdateColors;
		OnUpdateColors();
	}


	void OnDestroy()
	{
		EditorApplication.update -= OnUpdate;

		PowerQuestEditor.OnUpdateScriptColors -= OnUpdateColors;
		Application.logMessageReceived -= OnLogMessageReceived;

	}

	void OnUpdate()
	{
		UpdateAutoLoad();
	}

	void UpdateAutoLoad()
	{		

		// Only "auto load" scripts when game is playing
		if ( Application.isPlaying == false || PowerQuest.GetValid() == false || PowerQuest.Get.EditorGetAutoLoadScriptable() == null || PowerQuest.Get.EditorGetAutoLoadScriptable().GetScript() == null )
			return;
		// Only autoload if tickbox is on, and don't have changes
		if ( m_autoLoad == false || m_dirty )
			return;

		// Also only continue if there's no edits in current script

		// only continue if autoload sctriptbale/function has changed
		if ( (PowerQuest.Get.EditorGetAutoLoadScriptable() == m_autoLoadScriptable && PowerQuest.Get.EditorGetAutoLoadFunction() == m_autoLoadFunction ) )
			return;

		// Cache auto load scriptables/functions so can detect changes
		m_autoLoadScriptable = PowerQuest.Get.EditorGetAutoLoadScriptable();
		m_autoLoadFunction = PowerQuest.Get.EditorGetAutoLoadFunction();

		string functionName = m_autoLoadFunction;
		string objectName  = m_autoLoadScriptable.GetScriptName();
		string className = m_autoLoadScriptable.GetScript().GetType().ToString();


		// FIND FILE
		// Find file (if it exists)
		string[] assets = AssetDatabase.FindAssets( className.ToString() + PowerQuestEditor.STR_SCRIPT_TYPE, PowerQuestEditor.STR_SCRIPT_FOLDERS );
		if ( assets.Length == 0 )
			return;
		string path = AssetDatabase.GUIDToAssetPath(assets[0]);

		if ( File.Exists(path) == false )
			return;

		// FIND TYPE
		// Work out the enum type from the classname/function name
		eType type = eType.Other;
		for ( int i = 0; i< (int)eType.Other; ++i )			
		{
			if ( className.StartsWith(((eType)i).ToString()) )
			{
				type = (eType)i;
				break;
			}
		}
		if ( type == eType.Room )
		{
			if ( functionName.Contains("Hotspot") )	type = eType.Hotspot;
			if ( functionName.Contains("Prop") ) 	type = eType.Prop;
			if ( functionName.Contains("Region") ) 	type = eType.Region;				
		}

		// FIND PARAMETERS
		// Work out the parameters from the type + function name? Could do by reflecton of functionnames
		string funcParams = string.Empty;
		string funcBaseName = functionName;
		if ( type == eType.Hotspot || type == eType.Prop || type == eType.Region )
		{
			int substrLen = functionName.Length-objectName.Length;
			if ( substrLen > 0 )
				funcBaseName = functionName.Substring(0,substrLen);  // Trying to get the function name without the actual name (eg: OnLookHotspot instead of OnLookHotspotDoor)
		}
		// Look up in dictionary of function name to parameter
		PowerQuestEditor.SCRIPT_FUNC_PARAM_MAP.TryGetValue(funcBaseName,out funcParams);

		//Debug.Log( "Auto Loading: " + className+": " + functionName + "("+funcParams+")" );

		// TODO: If the function doesn't already exist, we don't want to create it until it's saved

		// Create the function if it doens't exist, then open it
		QuestEditorUtils.CreateScriptFunction( path, functionName, funcParams, true );

		Load( path, functionName, false, className, type );

		// Repaint
		Repaint();

	}

	#endregion
	#region Public Access

	public bool GetLocked() { return m_locked; }

	//public static List<QuestScriptEditor> GetQuestScriptEditors() { return s_editors; }

	#endregion
	#region Gui Layout: Main


	void OnGUI()
	{
		if ( Event.current.type == EventType.Layout )
		{			
			//s_editors.RemoveAll(item=>item == null);
			//if ( s_editors.Contains(this) == false )
			//	s_editors.Add(this);

			
			if ( string.IsNullOrEmpty(m_fileName) )			
				titleContent.text = "Quest Script";
			else 
				titleContent.text = m_fileName;	
			/*
			if ( string.IsNullOrEmpty(m_function) )			
				titleContent.text = "Quest Script";
			else 
				titleContent.text = m_function;	
			*/

			if ( titleContent.image == null )		
				titleContent.image = EditorGUIUtility.FindTexture("d_UnityEditor.ConsoleWindow");
			titleContent.tooltip = m_scriptClass + ": "+m_function;


			if ( m_refresh )
			{
				// takes control away from keyboard
				GUIUtility.keyboardControl = 0;
				m_refresh = false;
			}

		}
		

		if ( m_loaded == false )
		{
			// Show fields for loading file
			OnGuiNoFunction();
		}
		else 
		{
			OnGuiEditor();
		}
	}

	// Handle lock button and custom menu
	GUIStyle m_lockButtonStyle = null;
	void ShowButton(Rect position)
	{
		if (  m_lockButtonStyle == null )
			m_lockButtonStyle = "IN LockButton";
		m_locked = GUI.Toggle(position, m_locked, GUIContent.none, this.m_lockButtonStyle);
	}

	void IHasCustomMenu.AddItemsToMenu(GenericMenu menu)
	{
		menu.AddItem(new GUIContent("Lock"), m_locked, ()=> { m_locked = !m_locked; } );
		menu.AddItem(new GUIContent("Duplicate"), false, ()=>
		{ 
			bool wasLocked = m_locked;
			if ( m_locked == false )
				m_locked = true;
			Open(m_path, m_scriptClass, m_scriptType, m_editingHeader ? null : m_function); 
			m_locked = wasLocked;
		} );
	}

	void OnGuiNoFunction()
	{

		//
		// Layout
		// 

		EditorGUILayout.BeginHorizontal();	

		int currFunction = m_functionNames.FindIndex(item=>string.Compare(item,m_function,true)== 0);
		EditorGUI.BeginChangeCheck();		
		currFunction = EditorGUILayout.Popup(currFunction,m_functionNamesNice.ToArray(), new GUIStyle(EditorStyles.toolbarPopup) );

		if ( EditorGUI.EndChangeCheck() && currFunction >= 0 && currFunction <= m_functionNames.Count )
		{
			if ( currFunction == 0 )
				Load(m_path);
			else 
				Load(m_path, m_functionNames[currFunction]);
			return;
		}

		EditorGUILayout.LabelField( m_fileName, Styles.TOOLBAR_LABEL, GUILayout.MinWidth(1) ); 

		//EditorGUILayout.LabelField( "   Editing: <color=white>" + m_function + "</color> in <color=white>" + fileName+"</color>", new GUIStyle(EditorStyles.boldLabel) { richText = true } );
		
		//GUIContent testContent = new GUIContent("View C#", Contents.EYE.image, "View .cs script in IDE");
		//if  ( GUILayout.Button( testContent, Styles.TOOLBAR_BUTTON, GUILayout.Width(200)) )
		if ( GUILayout.Button("View C#", Styles.TOOLBAR_BUTTON, GUILayout.MaxWidth(80)) )
		{
			Object scriptObj = AssetDatabase.LoadAssetAtPath<Object>(m_path);
			if ( scriptObj != null )
			{
				AssetDatabase.OpenAsset(scriptObj);
			}
			else 
			{						
				Debug.Log("Couldn't open script at " + m_path);	
			}		
		}
		EditorGUILayout.EndHorizontal();

	}


	void OnGuiEditor()
	{		
		//
		// KB shortcuts
		//

		bool doSave = false;	// Set when "save" input is used
		bool doCompile = false; // set when compile is used
		bool doCarriageReturn = false; // Set when should do carriage return
		bool doAutoComplete = false; // Set when input should trigger an auto complete
		bool forceIncrementUndoGroup = false; // Set when an input should trigger an undo step
		bool preventAutoCompleteThisUpdate = false;  // Set when autoComplete should be cleared
		eTabEvent tabEvent = eTabEvent.None;
				
		bool needsSave = m_dirty;
		bool neverCompile = PowerQuestEditor.GetPowerQuestEditor().GetSmartCompileEnabled() == false;
		bool needsCompile = PowerQuestEditor.GetPowerQuestEditor().GetSmartCompileRequired() && Application.isPlaying == false || m_dirty;

		// Ctrl+s to save.. can't do it :( use Ctrl+shift+s or ctrl+enter instead.
		Event ev = Event.current;

		if ( focusedWindow == this )
		{	
			if ( ev.type == EventType.MouseDown && ev.type == 0 )
			{
				// When nagivate cursor with mouse, set an undo step (if was typing), and prevent autocomplete from showing up
				if ( m_wasTyping )
					forceIncrementUndoGroup = true; // Keys that force an undo step, 
				preventAutoCompleteThisUpdate = true;
				m_wasTyping = false;		
			}
			
			// Ctrl+ mousewheel zooms
			if ( ev.type == EventType.ScrollWheel && ev.control )
			{
				if ( ev.delta.y < 0 )
					m_fontSize++;
				else 
					m_fontSize--;
				m_fontSize = Mathf.Clamp(m_fontSize,4,42);
				s_textStyle.fontSize = m_fontSize;
				ev.Use();
			}


			if ( (ev.type == EventType.KeyDown) && FindTextEditor() != null )
			{
				bool textNavigateKeyDown = ev.keyCode == KeyCode.UpArrow || ev.keyCode == KeyCode.DownArrow || ev.keyCode == KeyCode.LeftArrow  || ev.keyCode == KeyCode.RightArrow || ev.keyCode == KeyCode.Home || ev.keyCode == KeyCode.End || ev.keyCode == KeyCode.PageUp || ev.keyCode == KeyCode.PageDown;

				if ( ev.control && (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.S)  )
				{ 
					// Save with ctrl+enter, Ctrl+S
					doSave |= needsSave;
					ev.Use();
				}
				else if ( ev.keyCode == KeyCode.F7)
				{
					// Save+Compile with F7
					doSave |= needsSave;
					doCompile |= needsCompile || needsSave;
					ev.Use();
				}
				else if ( HasAutoComplete() && (ev.keyCode == KeyCode.UpArrow || ev.keyCode == KeyCode.DownArrow) )
				{
					// Select autocomplete line
					m_acSelectedIndex += ev.keyCode == KeyCode.UpArrow ? -1 : 1;
					m_acSelectedIndex = Mathf.Clamp(m_acSelectedIndex,0,m_acList.Count-1);
					ev.Use();
				}
				else if ( ev.control == false && HasAutoComplete() && (ev.character == '\n' || ev.character == '.' || ev.character == ':' || ev.character == '\t') )
				{
					// Do Autocomplete
					forceIncrementUndoGroup = true;
					doAutoComplete = true;
					m_wasTyping = true;
					if ( ev.character != '.' )
						ev.Use();
				}
				else if ( HasAutoComplete() && ev.keyCode == KeyCode.Escape )
				{
					// Cancel autocomplete
					m_acCanceled = true;
					ev.Use();
				}
				else if ( ev.keyCode == KeyCode.Escape )
				{
					// Don't let escape fall through or it'll delete any chagnes to the text box!
					ev.Use();
				}
				else if ( ev.control == false && ev.character == '\n' )
				{
					// Do carriage return
					forceIncrementUndoGroup = true;
					doCarriageReturn = true;
					m_wasTyping = true;
					ev.Use();
				} 
				else if ( ev.character == '\t' )
				{		
					if ( ev.shift )
					{
						// Outdent
						tabEvent = eTabEvent.Outdent;
					}
					else 
					{
						// if selection, indent (if not spanning multi-lines, it should really replace with single tab
						if ( m_textEditor.cursorIndex == m_textEditor.selectIndex )
						{
							tabEvent = eTabEvent.Tab;
						}
						else 
						{
							// else							
							tabEvent = eTabEvent.Indent;
						}
					}
					forceIncrementUndoGroup = true; // Keys that force an undo step, 
					m_wasTyping = true;
					ev.Use();						
					
				}
				else if ( ev.keyCode == KeyCode.Space || ev.keyCode == KeyCode.Backspace || ev.keyCode == KeyCode.Delete || textNavigateKeyDown )
				{					
					// Force an undo step when was typing, and started navigating text instead (or pressed, space, backspace, delete, etc)
					if ( m_wasTyping )
						forceIncrementUndoGroup = true; // Keys that force an undo step, 
					m_wasTyping = false;
				}
				else 
				{
					// Set flag to show was typing
					m_wasTyping = true;					
				}

				// Handle spectial stuff for "tabs"
				if ( m_textEditor.selectIndex == m_textEditor.cursorIndex )
				{
					// Handle backspace and delete, left and right arrows for tabs
					if ( ev.keyCode == KeyCode.Backspace || ev.keyCode == KeyCode.LeftArrow )
					{
						// Check if should delete tab or skip over with arrow keys
						if ( m_textEditor.cursorIndex > 4  && m_textEditor.text.Substring(m_textEditor.cursorIndex-4, 4) == STR_TAB_SPACES )
						{
							tabEvent = ev.keyCode == KeyCode.Backspace ? eTabEvent.Backspace : eTabEvent.Left;
							ev.Use();		
						}
					}
					if ( ev.keyCode == KeyCode.Delete || ev.keyCode == KeyCode.RightArrow )
					{
						// Check if should delete tab or skip over with arrow keys
						if ( m_textEditor.cursorIndex < m_textEditor.text.Length-5  && m_textEditor.text.Substring(m_textEditor.cursorIndex, 4) == STR_TAB_SPACES )
						{
							tabEvent = ev.keyCode == KeyCode.Delete ? eTabEvent.Delete : eTabEvent.Right;
							ev.Use();		
						}
					}
				}

				// Don't autocomplete when navigating around with arrows
				if ( HasAutoComplete() == false && textNavigateKeyDown )
					preventAutoCompleteThisUpdate = true;
			}
		}

		//
		// Layout
		// 

		FindTintColour();
		SetupTextStyle();

		EditorGUILayout.BeginHorizontal();		

		//
		// Layout List of function names
		//
		
		int currFunction = m_functionNames.FindIndex(item=>string.Compare(item,m_function,true)== 0);
		EditorGUI.BeginChangeCheck();
		currFunction = EditorGUILayout.Popup(currFunction,m_functionNamesNice.ToArray(), new GUIStyle(EditorStyles.toolbarPopup) );
		if ( EditorGUI.EndChangeCheck() && currFunction >= 0 && currFunction <= m_functionNames.Count )
		{			
			if ( currFunction == 0 )
				Load(m_path,null,false,m_scriptClass,m_scriptType);
			else if ( currFunction == m_functionNames.Count-1)
			{
				// + New function button
				PopupWindow.Show(new Rect(EditorGUILayout.GetControlRect()), new NewFunctionWindow(
					(item)=>
					{
						Debug.Log("Creating: "+item.GetFunction());
						QuestEditorUtils.CreateScriptFunction(m_path, item.GetFunction(), item.m_coroutine );
						Open(m_path,m_scriptClass,m_scriptType,item.m_name,item.m_coroutine);
					}
				));
				
			}
			else 
				Load(m_path, m_functionNames[currFunction],false, m_scriptClass,m_scriptType);
			return;
		}

		//
		// Layout Current file name (todo: dropdown of recent files)
		//

		EditorGUILayout.LabelField( m_fileName, Styles.TOOLBAR_LABEL ); 

		// Prev-next function in history control
		EditorGUI.BeginDisabledGroup( m_historyIndex + 1 >= m_history.Count );
		string prevFunc = m_history.IsIndexValid(m_historyIndex+1) ? string.Format("{1}\n{0}",m_history[m_historyIndex+1].m_file,m_history[m_historyIndex+1].m_function) : "Previous";
		bool doPrevScript = GUILayout.Button( new GUIContent(Contents.PREV) {tooltip=prevFunc}, Styles.TOOLBAR_BUTTON, GUILayout.Width(25) ); 		
		if ( ev.type == EventType.KeyDown && ev.alt && ev.keyCode == KeyCode.LeftArrow && focusedWindow == this )
			doPrevScript = true;
		EditorGUI.EndDisabledGroup();
		EditorGUI.BeginDisabledGroup( m_historyIndex <= 0 );
		string nextFunc = m_history.IsIndexValid(m_historyIndex-1) ? string.Format("{1}\n{0}",m_history[m_historyIndex-1].m_file,m_history[m_historyIndex-1].m_function) : "Next";
		GUI.tooltip = "Next function";
		bool doNextScript = GUILayout.Button( new GUIContent(Contents.NEXT) {tooltip=nextFunc}, Styles.TOOLBAR_BUTTON, GUILayout.Width(25) );
		if ( ev.type == EventType.KeyDown && ev.alt && ev.keyCode == KeyCode.RightArrow && focusedWindow == this )
			doNextScript = true;
		EditorGUI.EndDisabledGroup();

		// Auto load toggle
		EditorGUI.BeginDisabledGroup(m_dirty);
		m_autoLoad = GUILayout.Toggle(m_autoLoad, "Auto-Load", Styles.TOOLBAR_TOGGLE, GUILayout.MaxWidth(80));
		EditorGUI.EndDisabledGroup();

		// View Source
		if ( GUILayout.Button("View C#", Styles.TOOLBAR_BUTTON, GUILayout.MaxWidth(60)) )
		{
			ViewInEditor(m_path, m_function, m_textEditor);	
		}

		// Revert
		bool doRevert = GUILayout.Button(m_dirty ? "Revert" : "Reload", EditorStyles.toolbarButton, GUILayout.MaxWidth(60) );

		// Compile
		
		if ( neverCompile )
		{
			// Save
			EditorGUI.BeginDisabledGroup(m_dirty == false);
			doSave |= GUILayout.Button("Save",  Styles.TOOLBAR_BUTTON, GUILayout.MaxWidth(80) );
			EditorGUI.EndDisabledGroup();
		}
		else 
		{
			
			EditorGUI.BeginDisabledGroup(m_dirty == false);
			doSave |= GUILayout.Button("Save",  Styles.TOOLBAR_BUTTON, GUILayout.MaxWidth(60) );
			EditorGUI.EndDisabledGroup();
		
			EditorGUI.BeginDisabledGroup(needsCompile == false && needsSave == false );			
			doCompile |= GUILayout.Button("Compile", Styles.TOOLBAR_BUTTON, GUILayout.MaxWidth(60));
			doSave |= doCompile;
			EditorGUI.EndDisabledGroup();	
		}

		EditorGUILayout.EndHorizontal();

		// Scroll for text box
		m_scrollPosition = EditorGUILayout.BeginScrollView(m_scrollPosition);

		// Record textbox undo
		GUI.SetNextControlName("ScriptEdit");
		Undo.RecordObject(this, "Script Edit");
		if ( forceIncrementUndoGroup )
		{			
			Undo.IncrementCurrentGroup();
			//Debug.Log("Increment undo group: "+Undo.GetCurrentGroup());
		}
				
		/*
		CharacterInfo cinfo;
		Resources.GetBuiltinResource<Font>("Arial.ttf").GetCharacterInfo('\t',out cinfo);
		cinfo.advance = 1;
		*/
		// Calculate the text area size
		Rect rect = GUILayoutUtility.GetRect( new GUIContent(m_text),s_textStyle,GUILayout.ExpandHeight(true));
		rect.width = rect.width+ 50.0f;
		rect.xMax = rect.xMax+100;

		// Header
		EditorGUI.DrawRect(new Rect(rect) { height = 15 }, UntintedColor(0.616f, 0.635f, 0.651f,1)); // header border
		EditorGUI.DrawRect(new Rect(rect) { height = 14, width = rect.width-2 }, UntintedColor(0.82f,0.82f,0.85f,1)); // header border

		// Sidebar
		EditorGUI.DrawRect(rect,UntintedColor(m_colors.m_sidebar));

		// Border
		rect.xMin = rect.xMin+25; // TODO: need to add somethign to pad out the scroll view horzontally to account for this.
		EditorGUI.DrawRect(rect,UntintedColor(0.5f, 0.5f, 0.5f,0.6f));

		// Text Area rect
		rect.width = rect.width-2;
		rect.x = rect.x+1;

		EditorGUI.DrawRect(rect,UntintedColor(m_colors.m_background));

		GUI.backgroundColor = new Color(1,1,1,0);		
		GUI.contentColor = new Color(1,1,1,0);

		EditorGUI.BeginChangeCheck();

		Color cursorColor = GUI.skin.settings.cursorColor;
		Color selectionColor = GUI.skin.settings.selectionColor;
		GUI.skin.settings.cursorColor = UntintedColor(m_colors.m_cursor);
		GUI.skin.settings.selectionColor = UntintedColor(0.71f, 0.835f, 1);

		// Handle carriage returns to auto-indent
		TextEditor tEditor = FindTextEditor();
		if ( tEditor != null )
		{
			if ( doAutoComplete )
				DoAutoComplete();
						
			if ( doCarriageReturn )
			{
				// Find amount of tabs/spaces at start of current line
				string spaces = Regex.Match(GetCurrentLine(),@"^(\s*)").ToString();
				m_text = m_text.Insert(tEditor.cursorIndex,"\n"+spaces);
				tEditor.text = m_text;
				tEditor.cursorIndex = tEditor.cursorIndex + 1 + spaces.Length;
				tEditor.selectIndex = tEditor.cursorIndex;
				GUI.changed = true;
			}

			if (  tabEvent != eTabEvent.None )
			{
				// TODO: Undo/redo not working for tab events
				//Undo.IncrementCurrentGroup(); // TODO: this doesn't seem to work, the TextEditor isn't regiestering as having changed, so the step is collapsed										

				switch (tabEvent)
				{
					case eTabEvent.Tab:
					{
						m_text = m_text.Insert(tEditor.cursorIndex,STR_TAB_SPACES);
						tEditor.text = m_text;
						tEditor.cursorIndex = tEditor.cursorIndex + 4;
						tEditor.selectIndex = tEditor.cursorIndex;
					} break;
					case eTabEvent.Backspace:
					{
						m_text = m_text.Remove(tEditor.cursorIndex-4,4);
						tEditor.text = m_text;
						tEditor.cursorIndex = tEditor.cursorIndex - 4;
						tEditor.selectIndex = tEditor.cursorIndex;
					} break;
					case eTabEvent.Delete:
					{
						m_text = m_text.Remove(tEditor.cursorIndex,4);
						tEditor.text = m_text;
						tEditor.selectIndex = tEditor.cursorIndex;
					} break;
					case eTabEvent.Indent:
					{
						// Multi-line indent 

						// Find start of line
						int startIndex = tEditor.selectIndex;
						int endIndex = tEditor.cursorIndex;
						if ( startIndex > endIndex )
							Utils.Swap(ref startIndex,ref endIndex);
						int pos = startIndex;
						while ( pos >= 1 && m_text[pos-1] != '\n' && m_text[pos-1] != '\r' )
							pos--;
						bool foundNewline = true;
						while ( foundNewline )
						{
							// Add spaces to start of line
							m_text = m_text.Insert(pos,STR_TAB_SPACES);
							if ( pos < startIndex )
								startIndex += 4;
							endIndex += 4;
							pos += 4;

							// Find start of next line
							foundNewline = false;
							bool findEndOfLine = true;
							while ( pos < m_text.Length && pos <= endIndex )
							{
								if ( findEndOfLine )
								{
									if ( m_text[pos] == '\n' || m_text[pos] == '\r' )
										findEndOfLine = false; // found end of line, now find start of next line
								}
								else
								{
									// We're looking for first non-newline character after a newline character
									if ( m_text[pos] != '\n' && m_text[pos] != '\r' )
									{
										foundNewline = true;
										break;
									}
								}
								pos++;
							}
						}

						tEditor.text = m_text;
						tEditor.cursorIndex = startIndex;
						tEditor.selectIndex = endIndex;

					} break;
					case eTabEvent.Outdent:
					{
						// Multi-line outdent 

						// Find start of line
						int startIndex = tEditor.selectIndex;
						int endIndex = tEditor.cursorIndex;
						if ( startIndex > endIndex )
							Utils.Swap(ref startIndex,ref endIndex);
						int pos = startIndex;
						while ( pos >= 1 && m_text[pos-1] != '\n' && m_text[pos-1] != '\r' )
							pos--;
						bool foundNewline = true;
						while ( foundNewline )
						{
							// Remove spaces from start of line (up to 4)
							for ( int i = 0; i < 4 && pos < m_text.Length && m_text[pos] == ' '; ++i )
							{
								m_text = m_text.Remove(pos,1);
								if ( startIndex > pos )
									startIndex--;
								if ( endIndex > pos )
									endIndex--;
							}

							// Find start of next line
							foundNewline = false;
							bool findEndOfLine = true;
							while ( pos < m_text.Length && pos <= endIndex )
							{
								if ( findEndOfLine )
								{
									if ( m_text[pos] == '\n' || m_text[pos] == '\r' )
										findEndOfLine = false; // found end of line, now find start of next line
								}
								else
								{
									// We're looking for first non-newline character after a newline character
									if ( m_text[pos] != '\n' && m_text[pos] != '\r' )
									{
										foundNewline = true;
										break;
									}
								}
								pos++;
							}
						}

						tEditor.text = m_text;
						tEditor.cursorIndex = startIndex;
						tEditor.selectIndex = endIndex;

					} break;
					case eTabEvent.Left: 
					{
						tEditor.cursorIndex = tEditor.cursorIndex - 4; 
						tEditor.selectIndex = tEditor.cursorIndex;
					} break;
					case eTabEvent.Right: 
					{
						tEditor.cursorIndex = tEditor.cursorIndex + 4; 
						tEditor.selectIndex = tEditor.cursorIndex;
					} break;

				}

				GUI.changed = true;
			}

			//tEditor.UpdateScrollOffsetIfNeeded(Event.current);
			//m_scrollPosition = tEditor.scrollOffset;

		}

		OnGuiSpellCheckSuggestions(ev,rect);

		tEditor = FindTextEditor(); // havnt tested whether rereshing this here is really necessary

		// Hack fix for bug/"feature" where dragging mouse selects all text in textbox if no text is selected
		int oldCursorIndex = -1;
		if ( focusedWindow == this && rect.Contains(ev.mousePosition) && ev.type == EventType.MouseUp )
		{				
			if (tEditor.cursorIndex == tEditor.selectIndex) 
				oldCursorIndex = FindTextEditor().cursorIndex;
		}

		//
		// Layout the editable text area
		//		
		m_text = WithoutSelectAll(() => EditorGUI.TextArea( rect, m_text, s_textStyle));

		if ( oldCursorIndex >= 0 )
		{
			tEditor.cursorIndex = oldCursorIndex;
			tEditor.selectIndex = oldCursorIndex;
		}
		GUI.skin.settings.cursorColor = cursorColor;
		GUI.skin.settings.selectionColor = selectionColor;
		
		bool updateTextDisplay = false;
		if ( EditorGUI.EndChangeCheck() )
		{
			m_dirty = true;
			updateTextDisplay = true;
		}
		
		OnGuiSpellCheckLayout(ev, rect, updateTextDisplay);

		//
		// Layout rich text version of the text area
		//	
		{
			if ( updateTextDisplay )
			{
				UpdateRichText();
			}

			GUI.contentColor = UntintedColor(m_colors.m_plainText);
			s_textStyle.wordWrap = false;
			GUI.Label(rect,m_richText,s_textStyle);
		}

		GUI.color = Color.white;
		GUI.contentColor = Color.white;

		// Clear autocomplete after mouse has been moved
		if ( preventAutoCompleteThisUpdate )
				ClearAutoComplete();		
		
		// Update auto complete. Only expensive if cursor has changed postiion
		UpdateAutoComplete();

		// Do autocomplete box
		LayoutAutoCompleteList(rect, s_textStyle);
		LayoutAutoCompleteMethodParams(rect, s_textStyle);

		EditorGUILayout.EndScrollView();

		//
		// Update scroll position based on cursor
		//				
		if ( tEditor != null && m_scrollingCursorIndexCached != tEditor.cursorIndex && ev.type == EventType.Repaint && focusedWindow == this ) // have to check it's a repaint frame, cause the sroll rect is invalid otherwise
		{
			m_scrollingCursorIndexCached = tEditor.cursorIndex;
			// Scroll to keep text on screen - Build the rect that we want to keep inside of. Start with the scroll-rect and offset by scroll pos, then add borders
			Rect scrollrect = GUILayoutUtility.GetLastRect();
			scrollrect.position = m_scrollPosition;
			scrollrect.xMin += 20;
			scrollrect.xMax -= 50;
			scrollrect.yMin += 5;
			scrollrect.yMax -= 31;					

			Vector2 scrollOffset = scrollrect.CalcDistToPoint(tEditor.graphicalCursorPos);
			if ( scrollOffset.sqrMagnitude > 1 )
			{					
				m_scrollPosition -= scrollOffset;
				Repaint(); // need to repaint if we've moved
			}
		}

		//GUILayout.Space(10);

		if ( doRevert )
			Load(m_path, m_editingHeader ? null : m_function, true);
		if ( doSave )
			Save();
		if ( doCompile )
		{
			{	// Used to do this defocus on Save, not sure why! But I'll do it on  compile now, in case it was necessary eeep!
				GUI.FocusControl(null);
				EditorGUI.FocusTextInControl(string.Empty);
			}
			RegisterForCompileError();
			if ( Application.isPlaying )
				PowerQuestEditor.HotloadScriptsCmd();				
			else 
				PowerQuestEditor.GetPowerQuestEditor().PerformSmartCompile();
		}
		
		if ( doPrevScript )
		{
			if ( m_historyIndex+1 < m_history.Count )
			{
				m_historyIndex++;
				Load(m_history[m_historyIndex]);
			}
		}
		else if ( doNextScript )
		{
			if ( m_historyIndex-1 < m_history.Count )
			{
				m_historyIndex--;
				Load(m_history[m_historyIndex]);
			}
		}

		if ( doCarriageReturn || doAutoComplete || tabEvent != eTabEvent.None || doPrevScript || doNextScript )
			Repaint();
	} 


	void RegisterForCompileError()
	{			
		Application.logMessageReceived -= OnLogMessageReceived; // ensure only registered once
		Application.logMessageReceived += OnLogMessageReceived;		
		m_compileErrorExpireTime = System.DateTime.UtcNow.AddSeconds(SAVE_ERROR_TIMEOUT).ToFileTimeUtc();
	}

	// Callback on log message
	void OnLogMessageReceived(string message, string stackTrace, LogType logType)
	{
		// todo: call this on compile

		if ( System.DateTime.Compare(System.DateTime.FromFileTimeUtc(m_compileErrorExpireTime),System.DateTime.UtcNow) < 0 )
		{
			// time's expired, do nothing, except remove callback
			Debug.Log("Error message check expired");
			Application.logMessageReceived -= OnLogMessageReceived;
			BuildAutoCompleteLists(true);
		}
		else if ( logType == LogType.Error )
		{			
			if ( message.Contains( Path.GetFileName(m_path) ))
			{
				EditorUtility.DisplayDialog("Compile Error", "There's an error in your script.\n---\n"+message+"\n---\nCheck the Console for more details", "Ok");
				Application.logMessageReceived -= OnLogMessageReceived;
			}
		}
	}

	SpellingMistake m_clickedMistake = null;

	static void ViewInEditor( string path, string function, TextEditor textEditor = null )
	{
		Object scriptObj = AssetDatabase.LoadAssetAtPath<Object>(path);
		if ( scriptObj != null )
		{
			// Update line number
			int foundLine = 0;
			if ( string.IsNullOrEmpty(function) == false )
			{
				try 
				{				
					int lineNum = 0;
					foreach ( string line in File.ReadAllLines(path) )
					{
						if ( line.Contains(function+'(') )
						{
							// Add cursor index offset
							int lineOffset = 0;
							if ( textEditor != null )
							{
								for ( int charPos = 0; charPos < textEditor.cursorIndex && charPos < textEditor.text.Length; ++charPos)
									if ( textEditor.text[charPos] == '\n' ) ++lineOffset;
							}

							foundLine = lineNum + lineOffset + 3;
							break;
						}
						++lineNum;
					}
				}
				catch {}
			}
			
			AssetDatabase.OpenAsset(scriptObj, foundLine);
		}
		else 
		{						
			Debug.Log("Couldn't open script at " + path);	
		}	
	}

	#endregion
	#region Functions: Save/Load

	// Finds index of first character after the brace starting function to last character before brace ending the function, returns success
	static bool FindHeaderLines( string text, out int startIndex, out int endIndex )
	{	
		//
		// Find function, match braces and grab text between braces
		//
		startIndex = -1;
		endIndex = -1;

		Regex regex = new Regex(REGEX_CLASS_START, RegexOptions.Compiled );
		Match match = regex.Match(text);
		if ( match.Index <= 0 )
			return false;

		startIndex = match.Index + match.Length;
		endIndex = Regex.Match(text, @"^.*\(.+\n*.*\{.*\n\r?",RegexOptions.Multiline).Index;

		if ( endIndex < startIndex || startIndex < 0 || endIndex >= text.Length-1)
			return false;
		
		return true;
	}
	
	// Finds index of first character after the brace starting function to last character before brace ending the function, returns success
	static bool FindFunctionLines( string text, string functionName, out int startIndex, out int endIndex )
	{	
		//
		// Find function, match braces and grab text between braces
		//
		startIndex = -1;
		endIndex = -1;

		Regex regex = new Regex(functionName+REGEX_FUNCTION_START, RegexOptions.Compiled );
		Match match = regex.Match(text);
		if ( match.Index <= 0 )
			return false;
		startIndex = match.Index + match.Length;

		// Find end brace
		int count = 1;
		endIndex = startIndex+1;
		for ( ; endIndex < text.Length; ++endIndex )
		{
			char ch = text[endIndex];
			if ( ch == '{' )
				count++;
			else if ( ch == '}' )
				count--;
			if ( count == 0 )
			{
				endIndex--;
				break;
			}
		}
		if ( endIndex < startIndex || startIndex < 0 || endIndex >= text.Length-1)
			return false;
		
		return true;
	}


	void ReadAllFunctionNames(string path)
	{
		if ( PowerQuestEditor.IsOpen() == false )
			return;

		// Using reflection, find the class name, and then methods inside
		string fileName = Path.GetFileNameWithoutExtension(path);
		//Debug.Log(fileName);
		m_functionNames.Clear();
		m_functionNames.Add("<header>");	

		Assembly assembly = ( PowerQuest.Exists ) ? PowerQuest.Get.EditorGetHotLoadAssembly() : null;
		if ( assembly == null )
			assembly = typeof(PowerQuest).Assembly;
		System.Type t = System.Type.GetType(string.Format("{0}, {1}", fileName,  assembly.FullName ) );
		if ( t != null )
		{
			MethodInfo[] methods = t.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			foreach ( MethodInfo method in methods )
			{
				// if ( functionToWaitFor.Method.IsSpecialName == false && functionToWaitFor.Method.Name[0] == '<') // <- maybe this is cheaper and does the same thing? comp-generated classes are named like "<CallingClass>_d__02"
				if ( System.Attribute.IsDefined(method, TYPE_COMPILERGENERATED) == false )
					m_functionNames.Add(method.Name);
			}
		}
		else 
		{
			// NB: This doesn't matter- Just means there's no thing yet
			// Debug.LogWarning("Couldn't find class "+fileName);
		}
		
		// Room script function sorting
		//if ( m_scriptType == eType.Room )
		{
			// First list room functions - this is totally slow
			
			int index =1 ; // start at 1 to skip <header>							
			m_functionNames.Insert(index++,null);
			bool seperate = false;
			foreach( string str in FUNCTION_SORT_ORDER )
			{
				int oldIndex = m_functionNames.FindIndex(index,(item)=>str.Equals(item));
				if ( oldIndex > 0 )
				{
					m_functionNames.Swap(index,oldIndex);					
					++index;
					seperate=true;
				}
			}
			if (seperate)			
			{
				m_functionNames.Insert(index++,null);
				seperate = false;
			}

			// Next list object functions
			// Hotspots
			// Props
			// Regions
			// Characters
			// Now remaining functions in order			
			for (int i = index; i < m_functionNames.Count; ++i )
			{
				int oldIndex = m_functionNames.FindIndex(index,(item)=>item.Contains("Hotspot"));
				if ( oldIndex > 0 )
				{
					m_functionNames.Swap(index,oldIndex);				
					++index;
					seperate = true;
				}
			}
			if (seperate)			
			{
				m_functionNames.Insert(index++,null);
				seperate = false;
			}

			for (int i = index; i < m_functionNames.Count; ++i )
			{
				int oldIndex = m_functionNames.FindIndex(index,(item)=>item.Contains("Prop"));
				if ( oldIndex > 0 )
				{
					m_functionNames.Swap(index,oldIndex);				
					++index;
					seperate = true;
				}
			}
			if (seperate)			
			{
				m_functionNames.Insert(index++,null);
				seperate = false;
			}

			for (int i = index; i < m_functionNames.Count; ++i )
			{
				int oldIndex = m_functionNames.FindIndex(index,(item)=>item.Contains("Region"));
				if ( oldIndex > 0 )
				{
					m_functionNames.Swap(index,oldIndex);				
					++index;
					seperate = true;
				}
			}
			if (seperate)			
			{
				m_functionNames.Insert(index++,null);
				seperate = false;
			}
			for (int i = index; i < m_functionNames.Count; ++i )
			{
				int oldIndex = m_functionNames.FindIndex(index,(item)=>item.Contains("Character"));
				if ( oldIndex > 0 )
				{
					m_functionNames.Swap(index,oldIndex);				
					++index;
					seperate = true;
				}
			}
			if (seperate)			
			{
				m_functionNames.Insert(index++,null);
				seperate = false;
			}
		}

		// Todo: dialog script function sorting?
		{

		}
		
		m_functionNames.Add(null);
		m_functionNames.Add("+ New");

		m_functionNamesNice.Clear();
		for ( int i =0; i< m_functionNames.Count;++i)
		{
			m_functionNamesNice.Add(ObjectNames.NicifyVariableName(m_functionNames[i]).Replace("Hotspot","-").Replace("Prop","-"));
		}		

		// update the last time functions were read- but not if we're still compiling it- this doesn't work.  really want "sourceCompiledTime" 
		//if ( EditorApplication.isCompiling == false )
		//{			
		//	m_sourceModifiedTime = System.DateTime.Now;
		//}
	}

	// Returns whether the function being edited is a coroutine (using reflection, so cache it if need to do it a lot). Currently only done on save
	bool CalcFunctionIsCoroutine()
	{
		if ( PowerQuestEditor.IsOpen() == false )
			return true;
		if ( m_editingHeader )
			return false;

		// Using reflection, find the class name, and then methods inside
		string fileName = Path.GetFileNameWithoutExtension(m_path);
		//Debug.Log(fileName);
		System.Type t = System.Type.GetType(string.Format("{0}, {1}", fileName,  typeof(PowerQuest).Assembly.FullName  ) );
		if ( t != null )
		{
			MethodInfo methodInfo = t.GetMethod(m_function, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic );
			if ( methodInfo != null )
				return methodInfo.ReturnType == typeof(IEnumerator);
			else 
			{
				// This happens when just added a function to edit for first time. Can't check return type, cause it's not compiled yet.
				// Debug.LogWarning(string.Format("QuestScript: Failed to find function {0} in {1}. Saving as blocking function.", m_function, fileName));								
			}
		}
		else 
		{
			// This happens when just added a script to edit for first time. Can't check return type, cause it's not compiled yet.			
			//Debug.LogWarning(string.Format("QuestScript: Failed to find class {0}. Saving as blocking function.", fileName));			
		}
		
		// Fall back to the m_functionIsCoroutine. Won't be accurate if picked from the function list, but function won't be in the list unless it's been compiled anyway, so shouldn't matter
		return m_functionIsCoroutine;
	}	

	// Classname and script type are just for autocomplete, not required to load
	void Load(string path, string functionName = null, bool onPlayerRevert = false, string className = null, eType scriptType = eType.Other, bool isCoroutine = true )
	{
		Load( new ScriptFileData() {
				m_file = path,
				m_function = functionName,
				m_classname = className,
				m_type = scriptType,
				m_isCoroutine = isCoroutine },onPlayerRevert);
		
	}

	void Load( ScriptFileData fileData, bool onPlayerRevert = false )
	{
		string path = fileData.m_file;
		string functionName = fileData.m_function;

		if ( PowerQuestEditor.GetPowerQuestEditor() == null )
			return;

		if ( m_dirty )
		{
			if ( onPlayerRevert )
			{
				if ( EditorUtility.DisplayDialog("Lose Changes?","Revert changes to "+m_function+"?","Revert", "Cancel") == false ) 
					return;
			}
			else 
			{
				if ( EditorUtility.DisplayDialog("Save Changes?","You have unapplied changes to "+m_function+"\n\nSave them first?","Save", "Discard") )
					Save();
			}
		}

		//GUI.FocusControl("ScriptEdit");
		m_dirty = false;
		m_loaded = false;

		if ( onPlayerRevert == false )
		{
			// update history - TODO: If found in list already, ignore
			if ( m_history.IsIndexValid(m_historyIndex) )
			{
				if ( m_textEditor != null)
					m_history[m_historyIndex].m_cursorIndex = m_textEditor.cursorIndex;

				if ( m_history[m_historyIndex] != fileData )
				{
					// New thing to add

					if ( m_historyIndex > 0 )
					{
						// Remove any history ahead of the current position
						for (int id = m_historyIndex--; id >= 0; id--) 
							m_history.RemoveAt(id);
					}

					// Add new file to history, reset history index
					m_history.Insert(0, fileData);
					m_historyIndex = 0;
				}
			}
			else 
			{
				// Current history position invalid, add new file to history, reset history index
				m_history.Insert(0, fileData);
				m_historyIndex = 0;
			}
			while ( m_history.Count > 20 ) // Don't let history get too long
				m_history.RemoveAt(m_history.Count-1);
		}

		//
		// Set vars
		//
		m_path = fileData.m_file;
		m_fileName = Path.GetFileName(m_path);
		m_function = fileData.m_function;
		m_functionIsCoroutine = fileData.m_isCoroutine;
		if ( onPlayerRevert == false )
		{
			m_scriptType = fileData.m_type;
			m_scriptClass = fileData.m_classname;
		}			

		//
		// Open file + read all text
		//
		try 
		{
			m_text = File.ReadAllText(path);
		} 
		catch (System.Exception e)
		{ 
			Debug.LogError("Failed to load from "+m_path+": "+e.Message);
		}

		ReadAllFunctionNames(m_path);
		if ( m_functionNames.Contains(m_function) == false )
		{
			m_functionNames.Add(m_function);
			m_functionNamesNice.Add(ObjectNames.NicifyVariableName(m_function));
		}

		// If function isn't set and ther's no function names, stop here.
		if ( string.IsNullOrEmpty(m_function) && m_functionNames.Count <= 0)
		{
			m_refresh = true;
			return;
		}

		/*/
		// If function isn't set, select first function name
		if ( string.IsNullOrEmpty(m_function) )
			m_function = m_functionNames[0];
		/**/

		// if function isn't set, load header
		m_editingHeader = string.IsNullOrEmpty(functionName);	
		if ( m_editingHeader )
			m_function = "<header>";
		/**/

		int startIndex,endIndex;
		if ( m_editingHeader )
		{
			if ( FindHeaderLines(m_text,out startIndex, out endIndex) == false )
				return;			
		}
		else if ( FindFunctionLines(m_text,functionName,out startIndex, out endIndex) == false )
		{
			return;
		}
		string text = m_text.Substring(startIndex,endIndex-startIndex);			

		//
		// Loop over lines
		//
		StringReader reader = new StringReader(text);
		string line = reader.ReadLine();
		m_text = string.Empty;

		while ( line != null )
		{
			//
			// Remove/lower Tabs
			//
			{
				int numWhitespaceChars = 0;
				int allowedSpaces = SCRIPT_TAB_WIDTH * (m_editingHeader ? 1 : 2);
				for ( int i = 0; allowedSpaces > 0 && i < line.Length; ++i )
				{
					if ( line[i] == '\t' )
					{
						numWhitespaceChars++;
						allowedSpaces -= SCRIPT_TAB_WIDTH;
					}
					else if ( line[i] == ' ')
					{
						numWhitespaceChars++;
						allowedSpaces--;
					}
					else
					{
						break;
					}
				}
				line = line.Substring(numWhitespaceChars);
			}

			//
			// Replace remaining tabs with spaces
			//
			line = line.Replace(STR_TAB, STR_TAB_SPACES);

			//
			// Remove "yield return" from known types
			//

			// lazy-compile reg-ex
			if (REGEX_YIELD_STRINGS_LOAD_COMPILED == null )
			{				
				REGEX_YIELD_STRINGS_LOAD_COMPILED = new Regex[REGEX_YIELD_STRINGS.Length];
				for ( int i = 0; i < REGEX_YIELD_STRINGS.Length; ++i )
					REGEX_YIELD_STRINGS_LOAD_COMPILED[i] = new Regex(REGEX_YIELD_LINE_START+REGEX_YIELD_STRINGS[i], RegexOptions.Compiled | RegexOptions.Multiline );
			}

			foreach ( Regex regex in REGEX_YIELD_STRINGS_LOAD_COMPILED )
			{
				line = regex.Replace( line, "$1");
			}

			//
			// Replace specific functions
			//
			Debug.Assert(REGEX_LOAD_MATCH.Length == REGEX_LOAD_REPLACE.Length);
			for ( int i = 0; i < REGEX_LOAD_MATCH.Length; ++i )
			{ 
				line = REGEX_LOAD_MATCH[i].Replace( line, REGEX_LOAD_REPLACE[i] );
			}

			// Next Line
			m_text += line+"\n";
			line = reader.ReadLine();
		}

		UpdateSpellCheck();
		UpdateRichText();

		m_loaded = true;
		m_refresh = true;

		BuildAutoCompleteLists(true);
		ClearAutoComplete();
	}

	void Save()
	{
		// Not sure why this was done on save, but it's now been moved to Compile anyway.
		//GUI.FocusControl(null);
		//EditorGUI.FocusTextInControl(string.Empty);

		//
		// check for matching braces (so don't break things)
		//
		int count = 0;
		for (int i = 0; i< m_text.Length; ++i)
		{
			if ( m_text[i] == '{' ) count++;
			if ( m_text[i] == '}' ) count--;
		}
		if ( count != 0 )
		{
			EditorUtility.DisplayDialog("Mismatched braces!","Mismatched braces!\nThis must be fixed before saving.\n\n(You probably forgot a '{' somewhere)","Oops!");
			return;
		}

		//
		// Loop over lines
		//
		StringReader reader = new StringReader(m_text);
		string line = reader.ReadLine();
		string outText = string.Empty;
		bool isCoroutine = CalcFunctionIsCoroutine();
		while ( line != null )
		{				
			//
			// Replace specific functions
			//
			bool noMatch = System.Array.Exists( REGEX_SAVE_NO_MATCH, item=> item.IsMatch(line) );
			if ( noMatch == false )
			{
				Debug.Assert(REGEX_SAVE_MATCH.Length == REGEX_SAVE_REPLACE.Length);
				for ( int i = 0; i < REGEX_SAVE_MATCH.Length; ++i )
				{
					line = REGEX_SAVE_MATCH[i].Replace( line, REGEX_SAVE_REPLACE[i] );
				}
			}

			//
			// Add "yield return" to known types
			//
			if ( isCoroutine ) // Only add "yield return" if it's a blocking function
			{

				// lazy-compile reg-ex
				if (REGEX_YIELD_STRINGS_SAVE_COMPILED == null )
				{				
					REGEX_YIELD_STRINGS_SAVE_COMPILED = new Regex[REGEX_YIELD_STRINGS.Length];
					for ( int i = 0; i < REGEX_YIELD_STRINGS.Length; ++i )
						REGEX_YIELD_STRINGS_SAVE_COMPILED[i] = new Regex(@"(?<=^\s*)("+REGEX_YIELD_STRINGS[i], RegexOptions.Compiled | RegexOptions.Multiline );
				}

				foreach ( Regex regexStr in REGEX_YIELD_STRINGS_SAVE_COMPILED )
				{
					line = regexStr.Replace(line,REGEX_SAVE_YIELD_REPLACE);

				}
			}

			//
			// Replace spaces with tabs
			//
			line = line.Replace(STR_TAB_SPACES, STR_TAB);

			//
			// Remove/lower Tabs
			//
			line = (m_editingHeader ? STR_TABS_HEADER : STR_TABS)+line;

			// Next Line
			outText += line+'\n';
			line = reader.ReadLine();
		}

		//
		// Open file and read all file text
		//
		string fileText = string.Empty;
		try 
		{
			fileText = File.ReadAllText(m_path);
		} 
		catch (System.Exception e)
		{ 
			Debug.LogError("Failed to save to "+m_path+": "+e.Message);
			return;
		}

		//
		// Replace function with new text
		//

		int startIndex,endIndex;
		if ( m_editingHeader )
		{
			if ( FindHeaderLines(fileText,out startIndex, out endIndex) == false )
				return;
		}
		else 
		{
			if ( FindFunctionLines(fileText,m_function,out startIndex, out endIndex) == false )
				return;
		}

		//Debug.Log(outText);

		outText = fileText.Substring(0,startIndex) + outText + fileText.Substring(endIndex);

		//
		// Save the file
		//		
		try 
		{
			File.WriteAllText(m_path, outText);
		} 
		catch (System.Exception e)
		{ 
			Debug.LogError("Failed to save to "+m_path+": "+e.Message);
			return;
		}

		// TODO: if editing header, reload function list/autocomplete
		
		if ( Application.isPlaying == false )
		{
			PowerQuestEditor.GetPowerQuestEditor().RequestAssetRefresh();
		}
		else  
		{			
			/* Compile is now only done when "compile" is pressed (or game is defocussed). The following was moved under onCompile
			RegisterForCompileError();
			// hot load - this is done automatically. but it's nicer if it's done on apply so it's ready when you click on the game
			PowerQuestEditor.HotloadScriptsCmd();
			*/
		}
		m_dirty = false;

	}

	#endregion
	#region Functions: 
	

	// Updates the text colors from the PQ editor settings. Special thanks to Dom De Re (@dom_dre).
	void OnUpdateColors()
	{

		if ( PowerQuestEditor.IsOpen() )
			m_colors = PowerQuestEditor.Get.GetScriptEditorColors();
		
		STR_REPLACE_COLOR =
			System.String.Format(
				@"<color=#{0}>$1</color>",
				ColorUtility.ToHtmlStringRGB(m_colors.m_event)
			);
		STR_REPLACE_COLOR_DIALOG =
			System.String.Format(
				@"<color=#{0}>$1</color><color=#{1}>$2</color>",
				ColorUtility.ToHtmlStringRGB(m_colors.m_speaker),
				ColorUtility.ToHtmlStringRGB(m_colors.m_dialog)
			);
		STR_REPLACE_COLOR_COMMENT =
			System.String.Format(
				@"<color=#{0}>$1</color>",
				ColorUtility.ToHtmlStringRGB(m_colors.m_comment)
			);

		if ( PowerQuestEditor.IsOpen() && s_textStyle != null)
			s_textStyle.font = Font.CreateDynamicFontFromOSFont( PowerQuestEditor.Get.GetScriptEditorFont(), m_fontSize);

		UpdateRichText();
		Repaint();
	}

	void UpdateRichText() 
	{  
		// TODO: do this line by line, and when editing code, just edit that line (also this means can handle longer comments)
		m_richText = m_text;
		foreach( Regex pattern in REGEX_COLOR_COMMENT )
		{
			m_richText = pattern.Replace(m_richText, STR_REPLACE_COLOR_COMMENT);
		}
		// TODO: don't colour things that are between comments.
		foreach( Regex pattern in REGEX_COLOR_DIALOG )
		{
			m_richText = pattern.Replace(m_richText, STR_REPLACE_COLOR_DIALOG);
		}
		foreach( Regex pattern in REGEX_COLOR )
		{
			m_richText = pattern.Replace(m_richText, STR_REPLACE_COLOR);
		}
	}

	T WithoutSelectAll<T>(System.Func<T> guiCall)
 	{
		 bool preventSelection = (Event.current.type == EventType.MouseDown);
		 Color oldCursorColor = GUI.skin.settings.cursorColor;
	 
		 if (preventSelection)
			 GUI.skin.settings.cursorColor = new Color(0, 0, 0, 0);
	 
		 T value = guiCall();
	 
		 if (preventSelection)
			 GUI.skin.settings.cursorColor = oldCursorColor;
	 
		 return value;
 	}

	// Returns line of m_text that cursor is on, caching it for efficiency
	string GetCurrentLine()
	{		
		TextEditor ted = FindTextEditor();
		if ( ted != null )
		{
			if ( m_currentLineCachedIndex == ted.cursorIndex )
				return m_currentLine;
			m_currentLineCachedIndex = ted.cursorIndex;

			int index = ted.cursorIndex -1;
			if ( index < 0 ) index = 0;
			// Find start of current line
			if ( index >= m_text.Length )
			{
				m_currentLine = string.Empty;
				return m_currentLine;
			}
		
			int lineStart = m_text.LastIndexOf('\n',index);
			if ( lineStart >= 0 ) ++lineStart; // don't include the \n from previous line						
			lineStart = Mathf.Clamp(lineStart, 0, m_text.Length);
			if ( lineStart >= m_text.Length )
			{
				m_currentLine = string.Empty;
				return m_currentLine;
			}
			int lineEnd = m_text.IndexOf('\n',lineStart);
			if ( lineEnd <= 0 ) lineEnd = m_text.Length;
			lineEnd = Mathf.Clamp(lineEnd, 0, m_text.Length);
			if ( lineEnd - lineStart > 0 )
				m_currentLine = m_text.Substring(lineStart,lineEnd - lineStart);
			else
				m_currentLine = string.Empty;
		}
		//Debug.Log($"'{m_currentLine}'");		
		return m_currentLine;
	}

	void OnFocus()
	{
		// Remove control when the window's focused since otherwise it may remain on another text box and cause weird issues with the cached m_textEditor
		GUI.FocusControl("none");

		// Update functions

		// Check if need to reload functions. TODO: (should also check if should reload file text too)
		//if ( File.Exists(m_path) && File.GetLastWriteTime(m_path) > m_sourceModifiedTime ) - can't check source modified time cause it might not be compiled yet
		{
			ReadAllFunctionNames(m_path);		
		}

		/*// Go back to last scrolling cursor pos on focus (Doesn't seem to work). Try again some time later
		TextEditor tEditor = FindTextEditor();		
		if ( tEditor != null )
			tEditor.cursorIndex = m_scrollingCursorIndexCached;		

		// Scroll position seems to get changed, or ignored or something. NEeds investigating
		Debug.Log("ScrollPos: "+m_scrollPosition);	
		*/

	}	

	TextEditor FindTextEditor()
	{
		
		if ( m_textEditor != null )
			return m_textEditor;
		if ( focusedWindow != this ) 
			return m_textEditor;
			
		m_textEditor = typeof(EditorGUI)
			.GetField("activeEditor", BindingFlags.Static | BindingFlags.NonPublic)
			.GetValue(null) as TextEditor; 
 		if ( m_textEditor == null )
		{
			m_textEditor = typeof(EditorGUI)
				.GetField("s_RecycledEditor", BindingFlags.Static | BindingFlags.NonPublic)
				.GetValue(null) as TextEditor; 
		}

		return m_textEditor;
	}

	void SetupTextStyle()
	{
		if ( s_textStyle == null || s_textStyle.font == null )
		{
			// Set up the text box style
			s_textStyle = new GUIStyle(EditorStyles.textArea);// new GUIStyle(GUI.skin.textArea);
			s_textStyle.fontSize = m_fontSize;
			s_textStyle.richText = true;
			s_textStyle.margin = new RectOffset(5,5,0,0);
			s_textStyle.padding = new RectOffset(10,30,10,5);
			s_textStyle.wordWrap = false;
			
			if ( PowerQuestEditor.IsOpen() )
				s_textStyle.font = Font.CreateDynamicFontFromOSFont( PowerQuestEditor.Get.GetScriptEditorFont(), m_fontSize);
			
			s_textStyle.normal.textColor = Color.white;
			//s_textStyle.font.material.mainTexture.filterMode = FilterMode.Point;
		}
	}


	//
	// Functions for un-tinting unity's play colour so it doesn't look horrible when editing scripts during play mode
	//
	void FindTintColour() 
	{
		if ( m_untintColour != Color.black )
			return; // already got it

		string str = EditorPrefs.GetString("Playmode tint","");
		// Color string is in format like this: "Playmode tint;0.8;0.8;0.8;1"
		string[] tok = str.Split(';');
		if ( tok.Length !=  5 )
			return;
		try
		{
			float r = float.Parse(tok[1]);
			float g = float.Parse(tok[2]);
			float b = float.Parse(tok[3]);
			float a = float.Parse(tok[4]);
			m_untintColour = new Color(r,g,b,a);
		}
		catch
		{
			Debug.Log("Unable to parse Playmode tint");
		}
	}	

	Color UntintedColor(float r, float g, float b, float a = 1) { return UntintedColor(new Color(r,g,b,a)); }
	Color UntintedColor(Color col)
	{
		if ( Application.isPlaying && m_untintColour != Color.black )
		{
			return new Color(col.r/m_untintColour.r, col.g/m_untintColour.g, col.b/m_untintColour.b, col.a/m_untintColour.a );
		}
		return col;
	}

	#endregion
}

#region Class NewFunctionWindow

// Window for creating a new function
public class NewFunctionWindow : PopupWindowContent 
{
	public static readonly string[] RETURN_TYPE_NAMES = new string[] { "IEnumerator", "void", "bool", "int", "float", "string", "Vector2", "Other" };
	public enum eReturnType { Coroutine, Function }//, Bool, Int, Float, String, Vector2, Other }
	
	System.Action<NewFunctionWindow> OnClickCreate = null;

	public string m_name = "MyFunction";	
	public bool m_coroutine = true;
	public eReturnType m_returnType = eReturnType.Coroutine;
	public string m_returnString = "void";
	
	public NewFunctionWindow(){}
	public NewFunctionWindow(System.Action<NewFunctionWindow> onClickCreate){OnClickCreate += onClickCreate;}

	public string GetFunction()
	{
		return m_returnString+' '+m_name+"()";
		/*/ Disabled 'advanced' return types for now. better to keep this one simple i think, and use VS for more complex?
		if ( m_returnType == eReturnType.Other )
			return m_returnString+' '+m_name+"()";
		return RETURN_TYPE_NAMES[(int)m_returnType]+' '+m_name+"()";
		**/
	}

	override public void OnGUI(Rect rect) 
	{
		//titleContent.text = "New Function";
		editorWindow.minSize = new Vector2(400,120);
		editorWindow.maxSize = new Vector2(400,120);		

		EditorGUILayout.LabelField("Create New:", EditorStyles.miniBoldLabel);

		GUILayout.BeginHorizontal();
		m_returnType = (eReturnType) EditorGUILayout.EnumPopup((System.Enum)m_returnType, GUILayout.MaxWidth(100));
		/*/ Disabled 'advanced' return types for now. better to keep this one simple i think, and use VS for more complex?
		if ( m_returnType == eReturnType.Other )
		{
			m_returnString = EditorGUILayout.TextField(m_returnString).Trim();
		}
		else 
		/**/
		{
			m_returnString = RETURN_TYPE_NAMES[(int)m_returnType];
		}
		m_name = EditorGUILayout.TextField(m_name).Trim();

		
		/*/
		if ( m_coroutine )
			m_returnType = eReturnType.IEnumerator;
		else if ( m_returnType == eReturnType.IEnumerator)
			m_returnType = eReturnType.None;
		/**/
		m_coroutine = ( m_returnType == eReturnType.Coroutine );

		bool create = GUILayout.Button("Create");
		GUILayout.EndHorizontal();

		if ( m_coroutine )
		{
			EditorGUILayout.HelpBox("Creates new coroutine (blocking) function: '"+ GetFunction() +"'\n\nCoroutine functions pause the script until they're done, so you put regular dialog in them, and other blocking functions like 'C.Dave.WalkTo(1,2);'.\n\nTo call this function in a sequence use E.WaitFor("+m_name+");", MessageType.None);
		}
		else 
		{
			EditorGUILayout.HelpBox("Creates new function: '"+ GetFunction() +"'\n\nRegular functions like this can't wait for dialog, or other blocking functions like `C.Dave.WalkTo()`", MessageType.None);			
		}

		if ( create )
		{
			if ( OnClickCreate != null)
				OnClickCreate.Invoke(this);		
			editorWindow.Close();
		}

	}

}

#endregion

}
