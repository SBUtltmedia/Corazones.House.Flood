using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using System.Text.RegularExpressions;
using System.IO;
using PowerTools.Quest;
using PowerTools;
using System.Reflection;
using System.Linq;
using System;

namespace PowerTools.Quest
{


[CustomEditor(typeof(SystemText))]
public class SystemTextEditor : Editor 
{
	public static readonly char[] LABEL_DELIMITER_WRITE=new char[]{',',' '};
	public static readonly string WHITESPACE = " ";
	public static readonly string LABEL_DELIMITER_READ = ", ";

	SystemText m_component = null;

	// Regex explanation:
	//		- Finds things of format
	//			C.<character>.Say( /* comment */ $"<text>", 45 /*comment*/ );
	//			.Display( /* comment */ $"<text>", <id> /*comment*/ );
	//			SystemText.Localize( /* comment */ $"<text>", <id> /*comment*/ );
	//			.Description = "<text>"
	//		- eg: C.Jon.Say("yo dudes");
	//			or .Display($"whoo!", 32);
	//			or SystemText.Localize("&<id> Whatever");
	//			or C.Man.Description = "It's a silly man"
	//		- Comments, and ", <id>" are optional, whitespace is optional
	//							
	//		Optional whitespace:	\s*
	//		Optional comment:		(/\*.*\*/\s*)* 
	//		Character name:			(?<character> \w+ )
	//		The text string:		\$?"(?<text>.*)"
	//		The optional id:		(, \s* (?<id> \d* ) \s* )?
	//		The inline id:			(&(?<id>)\s)?
	//		<start> is everything before the "id" is added 
	//		<assignStart> is everything until the start quote for the assigned string
	//	
	static readonly Regex REGEX_DIALOG_METHOD = new Regex(
		//@"(  (public\s+\w+\s+(?<function>\w+)\s*\()    " // Check for function (NB: Only works on public functions, too hard to tell what's a func declaration otherwise. Eg: else if (blah)
		@"(  (^\s*((public|private)\s+\w+|IEnumerator)\s+(?<function>\w+)\s*\()"
		+@"| ( (?<assignStart> \.\s*Description\s* = \s*) \$?""(?<text>.*)"" )" // ".Desciption = "hi" 
		+@"| ((?<start> (" // beggining of <start> section, and alternate function
				+@"(C \s* \. \s* (?<character> \w+ ) \s* \. \s* Say(BG|NoSkip|BGSkip)?) " // "C.Guy.SayBG"
				+@" |  (\.\s*(?<character>Section)) " // ".Section"
				+@" |  (\.\s*Display(BG)?)  "	// "DisplayBG"
				+@" |  (SystemText\.(?<character>Localize)) ) " // "SystemText.Localize"  and end of alternates
			+@"  \s*\( \s* (/\*.*\*/\s*)*  " // Open bracket (" and comments
			+@" \$?""(?<text>.*)"" )" // The text inside "blah", and end of <start> section
			+@" \s* (, \s* (?<id> \d* ) \s* )? " // Optional id parameter ", 123 "
			+@" (/\*.*\*/\s*)* \) )"  // Whitespace, comments and Function ending " )"
		+")",
		RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture | RegexOptions.Compiled | RegexOptions.Multiline );
	
	static readonly string STR_DIALOG_COMMA = ", ";
	static readonly string STR_DIALOG_FUNCEND = ")";

	static readonly string SCRIPT_DIALOG_FILE_START = @"<html><head><style>
		body{ text-align: center; }
		ul {
			margin-top: 20px;
			margin-bottom: 20px;
			margin-right: auto;
			margin-left: auto;

			list-style: none;
			max-width: 700px;
			background: #fff;
			padding: 5px 14px;
			text-align: left;
		}
		li { font: 12px/14px Courier, fixed; }
		.sceneheader, .action, .character { padding-top: 1.5ex; }
		.sceneheader  { font-weight: bold; text-transform:uppercase; }
		.action { padding-right: 5%; }
		.character {  margin-left: 40%; text-transform:uppercase;  }
		.dialogue { margin-left: 25%; padding-right: 25%; }
		.parenthetical { margin-left: 32%; padding-right: 30%; }
		/* special case: dialogue followed by a parenthetical; the extra line needs to be suppressed */
		.dialogue + .parenthetical { padding-bottom: 0; }
		.transition { padding-top: 3ex; margin-left: 65%; padding-bottom: 1.5ex; }
		.id { float:left; padding-right: 0; color:#ccc; text-align: right; width: 23%; }
		</style>"+"\n<body><code><ul>";
	static readonly string SCRIPT_DIALOG_FILE_END = "</ul></code></body></html>";
	static readonly string SCRIPT_DIALOG_CHARACTER = "\t\t\t<li class=\"character\"><b>{0}</b></li>\n";
	static readonly string SCRIPT_DIALOG_LINE = "\t\t\t<li class=\"id\">({0}{1})</li><li class=\"dialogue\">{2}</li>\n";
	//static readonly string SCRIPT_DIALOG_LINE = "\t\t\t<li class=\"character\"><b>{0}</b> {1}</li><li class=\"dialogue\">{2}</li>\n";
	static readonly string SCRIPT_FILE_LINE = "\n\t<li class=\"sceneheader\">{0}</li>\n\n";
	static readonly string SCRIPT_FUNCTION_LINE = "\n\t\t<li class=\"action\">{0}</li>\n\n";

	[Tooltip("Enable this when you'd already recorded some dialog lines/translated some text, so it doesn't get muddled up")]
	[SerializeField] bool m_preserveIds = true;
	[Tooltip("If enabled, only spoken dialog lines are processed. Disable if you're planning on translating all game text")]
	[SerializeField] bool m_processDialogOnly = true;
	[SerializeField] string[] m_exportCharacters = null;
	[SerializeField] string[] m_exportRooms = null;
	
	[Tooltip("Comma separated list of language codes. Case sensitive, use the same case you put in Languages' codes")]
	[SerializeField] string[] m_exportLanguages = null;

	string m_currSourceFile = null;

	public override void OnInspectorGUI()
	{		
		m_component = (SystemText)target;

		EditorGUILayout.LabelField("Process Game Text From Scripts",EditorStyles.boldLabel);

		string helpText = string.Empty;
		if ( m_preserveIds )
			helpText = "Process Text reads through scripts and adds any text to this text system, assigning ids to each line of text.\n\nExisting dialog ids will not be changed.";
		else 
			helpText = "Process Text reads through scripts and adds any text to this text system, assigning ids to each line of text.\n\nDialog ids may be change.\nTick Preserve Ids if you've recorded dialog already.";

		if ( m_processDialogOnly )
			helpText += "\n\nOnly Spoken Dialog will be imported (for use in voice scripts)";
		else 
			helpText += "\n\nAll text will be imported (for voice scripts and translation)";
		EditorGUILayout.HelpBox(helpText, MessageType.Info);

		GUILayout.BeginHorizontal();
		m_preserveIds = GUILayout.Toggle( m_preserveIds, "Preserve Ids" );
		m_processDialogOnly = GUILayout.Toggle( m_processDialogOnly, "Dialog Only" );
		GUILayout.EndHorizontal();

		bool processText = GUILayout.Button("Process Text From Scripts");

		GUILayout.Space(10);	

		// Script generation
		EditorGUILayout.LabelField("Export Game Text",EditorStyles.boldLabel);
		// Characters
		{
			EditorGUI.BeginChangeCheck();
			string inLabels = (m_exportCharacters != null && m_exportCharacters.Length > 0) ?  string.Join(LABEL_DELIMITER_READ, m_exportCharacters) : "";
			string outLabels = EditorGUILayout.TextField("From Characters", inLabels);
			if ( EditorGUI.EndChangeCheck() )
			{					
				//outLabels = outLabels.Replace(WHITESPACE,string.Empty);
				outLabels = outLabels.ToLower();
				m_exportCharacters = outLabels.Split(LABEL_DELIMITER_WRITE, System.StringSplitOptions.RemoveEmptyEntries);
			}
		}

		// Rooms
		{
			EditorGUI.BeginChangeCheck();
			string inLabels = (m_exportRooms != null && m_exportRooms.Length > 0) ?  string.Join(LABEL_DELIMITER_READ, m_exportRooms) : "";
			string outLabels = EditorGUILayout.TextField("From Rooms", inLabels);
			if ( EditorGUI.EndChangeCheck() )
			{					
				//outLabels = outLabels.Replace(WHITESPACE,string.Empty);
				outLabels = outLabels.ToLower();
				m_exportRooms = outLabels.Split(LABEL_DELIMITER_WRITE, System.StringSplitOptions.RemoveEmptyEntries);
			}
		}

		// Languages
		{
			EditorGUI.BeginChangeCheck();
			string inLabels = (m_exportLanguages != null && m_exportLanguages.Length > 0) ?  string.Join(LABEL_DELIMITER_READ, m_exportLanguages) : "";
			string outLabels = EditorGUILayout.TextField("Languages", inLabels);
			if ( EditorGUI.EndChangeCheck() )
			{
				m_exportLanguages = outLabels.Split(LABEL_DELIMITER_WRITE, System.StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray<string>();
			}
		}

		if ( GUILayout.Button("Generate Script") )
		{
			GenerateScript( m_component );
		}
		GUILayout.BeginHorizontal();
		if ( GUILayout.Button("Export to CSV") )
		{
			ExportToCSV( m_component );
		}
		if ( GUILayout.Button("Import from CSV") )
		{
			ImportFromCSV( m_component );
		}
		GUILayout.EndHorizontal();

		GUILayout.Space(10);	

		EditorGUILayout.LabelField("Lip Syncing",EditorStyles.boldLabel);
		EditorGUILayout.HelpBox("Generate lip sync data. Requires voice acting files to be present, and Rhubarb to be installed", MessageType.Info);
		if ( GUILayout.Button("Process Lip Sync Data") )
		{
			PowerQuestEditor.GetPowerQuestEditor().RunRhubarb();
		}

		GUILayout.Space(10);	
		EditorGUILayout.LabelField("Internal Data",EditorStyles.boldLabel);

		DrawDefaultInspector();

		if ( processText )
		{
			ProcessAllText(m_component, m_preserveIds, m_processDialogOnly);			

			//m_targetObject.ApplyModifiedProperties();	
			EditorUtility.SetDirty(target);	

			AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
		}

	}



	static void GetFilePaths(string startingDirectory, string extention, ref List<string> paths)
  	{
		try
		{
			string[] files = Directory.GetFiles(startingDirectory);
			for ( int j = 0; j < files.Length; ++j )
			{
				string file = files[j];
				if ( file.EndsWith(extention) )
				{
					paths.Add(file);
				}
			}

			string[] directories = Directory.GetDirectories(startingDirectory);
			for ( int i = 0; i < directories.Length; ++i )
			{
				string dir = directories[i];
				GetFilePaths(dir, extention, ref paths);
			}
		}
		catch (System.Exception excpt)
		{
			Debug.LogError(excpt.Message);
		}
	}

	//
	// 
	//
	void ProcessFile( string filePath )
	{
		filePath = Path.GetFullPath(filePath);
		if ( m_processedFiles.Exists(item=>item == filePath) )
			return;
		try 
		{
			string content = File.ReadAllText(filePath);
			content = REGEX_DIALOG_METHOD.Replace( content, EvaluateDialogMatch );
			File.WriteAllText( filePath,  content );
			m_processedFiles.Add(filePath);
		} 
		catch (System.Exception ex) 
		{
			if ( ex is System.IO.FileNotFoundException )
			{
				//Debug.LogFormat("Skipping non-existant script: {0}", filePath);
			}
			else 
			{
				Debug.LogWarningFormat("Failed to process text in {0}: {1}",filePath, ex.ToString());
			}
		}
	}

	List<string> m_processedFiles = new List<string>();

	// Trawls through the game and adds text to the manager, inserting IDs into script files where it feels like it
	public void ProcessAllText( SystemText systemText, bool preserveIds, bool dialogOnly )
	{
		if ( PowerQuestEditor.IsReady() == false )
			return;
			
		if ( PowerQuestEditor.GetPowerQuestEditor().GetSmartCompileRequired() )
		{
			AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
			EditorUtility.DisplayDialog("Asset database requires import", "The asset database requires updating first, wait a few seconds then try again ;)","Ok");
			return;
		}

		// Clear list of processed files before starting processing again
		m_processedFiles.Clear();

		m_preserveIds = preserveIds;
		m_processDialogOnly = dialogOnly;
		m_component = systemText;

		PowerQuest powerQuest = PowerQuestEditor.GetPowerQuest();
		if ( powerQuest == null )
			return;

		// Loop through all script files and run teh regex over them
		systemText.EditorOnBeginAddText();

		// Process Room scripts
		foreach ( RoomComponent component in powerQuest.GetRoomPrefabs() )
		{
			m_currSourceFile = STR_ROOM+component.GetData().ScriptName;
			ProcessFile( QuestEditorUtils.GetFullPath(component.gameObject, "Room" + component.GetData().GetScriptName() +".cs") ); 

			if ( dialogOnly == false )
			{
				m_lastFunction = "Prop Description";
				component.GetPropComponents().ForEach( item=> 
					{
						bool hasCollider = item.GetComponent<Collider2D>() != null;
						if ( hasCollider )
							item.GetData().Description = AddStringWithEmbeddedId( item.GetData().Description);
					} );
				m_lastFunction = "Hotspot Description";
				component.GetHotspotComponents().ForEach( item=> 
					{ 
						item.GetData().Description = AddStringWithEmbeddedId( item.GetData().Description); 
					} );
				EditorUtility.SetDirty(component);
			}
		}

		// Process Character scripts
		foreach ( CharacterComponent component in powerQuest.GetCharacterPrefabs() )
		{
			m_currSourceFile = "Character- "+component.GetData().ScriptName;
			ProcessFile( QuestEditorUtils.GetFullPath(component.gameObject, "Character" + component.GetData().GetScriptName() +".cs") ); 

			if ( dialogOnly == false )
			{
				m_lastFunction = "Description";
				component.GetData().Description = AddStringWithEmbeddedId( component.GetData().Description);
				EditorUtility.SetDirty(component);
			}
		}

		// Process Inventory scripts
		foreach ( InventoryComponent component in powerQuest.GetInventoryPrefabs() )
		{
			m_currSourceFile = "Item- "+component.GetData().ScriptName;
			ProcessFile( QuestEditorUtils.GetFullPath(component.gameObject, "Inventory" + component.GetData().GetScriptName() +".cs") ); 

			if ( dialogOnly == false )
			{
				m_lastFunction = "Description";
				component.GetData().Description = AddStringWithEmbeddedId(component.GetData().Description);
				EditorUtility.SetDirty(component);
			}
		}

		// Process Dialog scripts
		foreach ( DialogTreeComponent component in powerQuest.GetDialogTreePrefabs() )
		{
			m_currSourceFile = "Dialog- "+component.GetData().ScriptName;
			ProcessFile( QuestEditorUtils.GetFullPath(component.gameObject, "Dialog" + component.GetData().GetScriptName() +".cs") ); 

			if ( dialogOnly == false )
			{
				m_lastFunction = "Dialog Option";
				component.GetData().Options.ForEach( item=> 
					{ 
						item.Text = AddStringWithEmbeddedId(item.Text); 
					} );				
			}
		}		

		// Process Game script
		m_currSourceFile = "Global Script";
		ProcessFile( PowerQuestEditor.PATH_GLOBAL_SCRIPT );

		m_currSourceFile = "Guis";
		// Process Gui descriptions
		foreach ( GuiComponent component in powerQuest.GetGuiPrefabs() )
		{			
			if ( dialogOnly == false )
			{
				m_lastFunction = "Gui Description";
				GuiComponent[] childComponents = component.GetComponentsInChildren<GuiComponent>(true);
				foreach ( GuiComponent item  in childComponents )
				{
					item.GetData().Description = AddStringWithEmbeddedId(item.GetData().Description);
				}

				// Look for [QuestLocalizable] attribute in guis. This attribute/function/system could be extended to more random components I guess.
				//ProcessLocalizableFieldAttributes(component.transform);
			}
		}

		if ( dialogOnly == false )
		{
			// Process QuestText components that may need localising
			m_currSourceFile = "General Text";
			ProcessQuestText();

			// Process all "SystemText.Localise" calls in game directory (that haven't been processed yet)
			List<string> paths = new List<string>();
			GetFilePaths(@Path.Combine("Assets", "Game"), ".cs", ref paths );
			foreach( string path in paths)
			{
				m_currSourceFile = Path.GetFileNameWithoutExtension(path);
				m_lastFunction = "";
				ProcessFile(path);
			}

			// Look for [QuestLocalizable] attribute. I assume this will be suuuuuuper slow lol.
			ProcessLocalizableFieldAttributes();
		}
	}

	string AddStringWithEmbeddedId(string line)
	{		
		if ( string.IsNullOrEmpty(line) )
			return line;
		int existingId = m_component.ParseIdFromText(ref line);
		TextData data = m_component.EditorAddText(line, m_currSourceFile,m_lastFunction,null,existingId,m_preserveIds);
		return string.Format("&{0} {1}", data.m_id.ToString(), data.m_string);
	}

	//
	//
	//
	string m_lastFunction = null;
	public string EvaluateDialogMatch( Match match )
	{	
		if ( m_component == null || match.Groups == null)
			return match.Value;

		// If it matches the function part of the expression, we just record that group and continue
		string function = match.Groups["function"].Value;
		if ( string.IsNullOrEmpty(function) == false  )
		{
			m_lastFunction = function;
			return match.Value;
		}

		string result = match.Value;
			
		string firstHalf = match.Groups["start"].Value;
		string character = match.Groups["character"].Value;
		string text = match.Groups["text"].Value;
		string existingId = match.Groups["id"].Value;
		string assignFirstHalf = match.Groups["assignStart"].Value;
		bool isAssignment = string.IsNullOrEmpty(assignFirstHalf) == false;

		if ( string.IsNullOrEmpty(text) )
			return match.Value;
		
		if ( isAssignment == false && string.IsNullOrEmpty(firstHalf) )
			return match.Value;


		int id = -1;
		if ( int.TryParse(existingId, out id) == false )
		{
			id = -1;
		}

		// if no character name, then it's the narrator
		if ( isAssignment )
		{
			character = null;
		}
		else if ( string.IsNullOrEmpty(character) )
		{
			character = "Narr"; // Hack to set chracter to "Narr" when it's a display function
		}
		else if ( character == "Section" )
		{
			m_lastFunction = Regex.Unescape(text); // Hack for "Section"
			return match.Value;
		}
		else if ( character == "Localize" ) 
		{
			character = null;
		}

		if ( character == null && m_processDialogOnly )
			return match.Value; // Ignore non-dialog
			

		text = Regex.Unescape(text);
		TextData textData = m_component.EditorAddText(text, m_currSourceFile, m_lastFunction, character, id, m_preserveIds);
		id = textData.m_id;

		if ( isAssignment )
		{
			// Final string is .Description = "&123 Blah"
			result = string.Format("{0}\"&{1} {2}\"", assignFirstHalf, id.ToString(), textData.m_string);
		}
		else 
		{
			// Final string is  'C.Jon.Say("blah"' + ', ' + id + ' );'
			result = string.Concat(firstHalf, STR_DIALOG_COMMA, id.ToString(), STR_DIALOG_FUNCEND); 
		}

		return result;
	}


	void ProcessQuestText()
	{
		List<string> paths = new List<string>();
		GetFilePaths("Assets", ".prefab", ref paths );

		for ( int i = 0; i < paths.Count; ++i )
		{
			GameObject obj = AssetDatabase.LoadMainAssetAtPath(paths[i]) as GameObject;
			if ( obj != null )
			{						
				bool dirty = false;
				m_lastFunction = "Text in "+ obj.name;
				QuestText[] textObjects = obj.GetComponentsInChildren<QuestText>(true);
				foreach( QuestText textObj in textObjects )
				{
					if ( textObj.GetShouldLocalize() )
					{
						textObj.SetText( AddStringWithEmbeddedId(textObj.GetUnlocalizedText()) );
						dirty = true;
					}
				}
				if ( dirty )
					EditorUtility.SetDirty(obj);	
			}
		}
	}

	void ProcessLocalizableFieldAttributes()
	{
		List<string> paths = new List<string>();
		GetFilePaths("Assets", ".prefab", ref paths );

		for ( int i = 0; i < paths.Count; ++i )
		{
			GameObject obj = AssetDatabase.LoadMainAssetAtPath(paths[i]) as GameObject;
			if ( obj != null )
			{
				m_lastFunction = "Text in "+ obj.name;				
				if ( ProcessLocalizableFieldAttributes(obj.transform) )
					EditorUtility.SetDirty(obj);	
					
			}
		}
	}

	// Finds [QuestLocalizable] attribute on string fields, and adds that text to system. returns true if found
	public bool ProcessLocalizableFieldAttributes(Transform transform, bool root = true)
	{
		bool result = false;
		for ( int i = 0; i < transform.childCount; ++i )
			result |= ProcessLocalizableFieldAttributes(transform.GetChild(i), false );

		Component[] components = transform.GetComponents<Component>();
		
		BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
		foreach( Component component in components) 
		{
			foreach(FieldInfo fieldInfo in component.GetType().GetFields(flags))
			{
				if ( fieldInfo.GetCustomAttributes(typeof(QuestLocalizeAttribute),true).Count() > 0 )
				{					
					if ( fieldInfo.FieldType == typeof(string))
					{
						string val = fieldInfo.GetValue(component) as string;
						if ( string.IsNullOrEmpty(val) == false )
						{
							val = AddStringWithEmbeddedId(val);
							fieldInfo.SetValue(component, val); 
							// Debug.Log("Found: "+ val);
							result = true;
						}
					}
				}
			}
		}
		return result;
	}

	static readonly string STR_ROOM = "Room- ";


	private string[] GetLanguageCodes(SystemText systemText) { return systemText.GetLanguages().Select(language => language.m_code).ToArray<string>(); }

	private int GetLanguageIndexByCode(SystemText systemText, string languageCode = null) {
		// find the language index by code
		if (languageCode == null)
			return -1;
		return Array.IndexOf(GetLanguageCodes(systemText), languageCode);
	}

	// Generates a screenplay style script for recording dialog
	public void GenerateScript( SystemText systemText )
	{
		if ( m_exportLanguages != null && m_exportLanguages.Length > 0)	{
			foreach(string languageCode in m_exportLanguages) {
				// if the lang code is not a valid one, do nothing
				if (!GetLanguageCodes(systemText).Contains(languageCode)) {
					Debug.LogWarning($"Language index not found for code {languageCode}. Mind the code is case sensitive.");
					continue;
				}
				// else generate a script with the selected language
				GenerateScriptForLanguage(systemText, languageCode);
			}
		} 
		else 
		{
			// if no language specified, use the predefined one (generate only one file)
			GenerateScriptForLanguage(systemText, systemText.GetLanguages()[0].m_code);
		}
	}

	private void GenerateScriptForLanguage( SystemText systemText, string languageCode = null )
	{
		// A lang code MUST be always specified or no script will be generated.
		if (languageCode == null) {
			Debug.LogWarning("Script generation invoked without language code. Exiting.");
			return;
		}
		
		int languageIndex = GetLanguageIndexByCode(systemText, languageCode);
		if ( languageIndex < 0) {
			Debug.LogWarning($"Language index not found for code {languageCode}. Exiting.");
			return;
		}

		m_component = systemText;

		string lastFile = null;
		string lastFunction = null;
		string lastCharacter = null; // TODO: use this to insert character when it's changed

		System.Text.StringBuilder builder = new System.Text.StringBuilder();
		List<TextData> list = systemText.EditorGetTextDataOrdered();
		builder.Append(SCRIPT_DIALOG_FILE_START);
		// builder.AppendFormat(SCRIPT_DIALOG_LINE, "fred", "23", "blah di blah di blah blah blah");
		int index = -1;
		foreach( TextData data in list )
		{
			index++;				

			// Only include character lines in script
			if ( string.IsNullOrEmpty(data.m_character) )
				continue;

			if (data.m_sourceFile != lastFile )
			{				
				// If Rooms are specified, only export specified rooms
				if ( m_exportRooms != null && m_exportRooms.Length > 0 && data.m_sourceFile.StartsWith(STR_ROOM) )
				{
					string roomName = data.m_sourceFile.Substring(STR_ROOM.Length);
					if ( System.Array.Exists( m_exportRooms, item => item.Equals(roomName,System.StringComparison.OrdinalIgnoreCase) ) == false )				
					{
						continue;
					}
				}
					
				lastFile = data.m_sourceFile;
				builder.AppendFormat(SCRIPT_FILE_LINE, lastFile);
				lastCharacter = null;
				lastFunction = null;
			}

			if (data.m_sourceFunction != lastFunction )
			{
				// If Characters are specified, only export specified characters
				if ( m_exportCharacters != null && m_exportCharacters.Length > 0 )
				{
					bool foundCharacter = false;
					for ( int i = index; i < list.Count; ++i )
					{
						TextData charCheckData = list[i];
						if ( charCheckData.m_sourceFunction != data.m_sourceFunction )
							break;
						if ( string.IsNullOrEmpty(charCheckData.m_character) )
							continue;
						if ( System.Array.Exists(m_exportCharacters, item => item.Equals(charCheckData.m_character,System.StringComparison.OrdinalIgnoreCase) ) )
						{
							foundCharacter = true;
							break;
						}
					}

					if ( foundCharacter == false )
					{
						// If we didn't find the character, skip this line. I should really skip the outer loop to the next function once this is hit, but it'll still work so whatever
						continue;
					}
				}


				lastFunction = data.m_sourceFunction;
				builder.AppendFormat(SCRIPT_FUNCTION_LINE, lastFunction);
				lastCharacter = null;
			}

			if ( lastCharacter != data.m_character )
			{
				builder.AppendFormat(SCRIPT_DIALOG_CHARACTER, data.m_character);
				lastCharacter = data.m_character;
			}

			if (languageIndex > 0) 
			{
				// index is greater than zero so we want one of the translations
				// TODO: check if the index is never out of bound
				builder.AppendFormat(SCRIPT_DIALOG_LINE, data.m_character, data.m_id.ToString(), data.m_translations[languageIndex-1]); // can be an empty string
			}
			else 
			{
				// index is 0 so we refer to the primary language
				builder.AppendFormat(SCRIPT_DIALOG_LINE, data.m_character, data.m_id.ToString(), data.m_string);
			}
			
		}
		builder.Append(SCRIPT_DIALOG_FILE_END);


		string scriptPath = EditorUtility.SaveFilePanel("Save Script File", "", $"Script-{languageCode}.html","html");
		if ( string.IsNullOrEmpty(scriptPath) == false )
		{
			File.WriteAllText(scriptPath, builder.ToString());
			Application.OpenURL(scriptPath);
		}

	}

	static readonly string CSV_HEADERS = "Character,ID,File,Context";
	static readonly int CSV_NUM_HEADERS = 4;
	static readonly int CSV_INDEX_LANGUAGES = CSV_NUM_HEADERS;

	public void ExportToCSV( SystemText systemText  )
	{

		m_component = systemText;

		//string lastFile = null;
		//string lastFunction = null;

		System.Text.StringBuilder builder = new System.Text.StringBuilder();
		List<TextData> list = systemText.EditorGetTextDataOrdered();
		builder.Append(CSV_HEADERS);

		// Add languages
		foreach (LanguageData language in systemText.GetLanguages())
		{
			builder.Append(",");
			builder.Append(language.m_description);
		}
		// builder.AppendFormat(SCRIPT_DIALOG_LINE, "fred", "23", "blah di blah di blah blah blah");
		foreach( TextData data in list )
		{

			builder.Append("\n\"");
			builder.Append(data.m_character);
			builder.Append("\",\"");
			builder.Append(data.m_id);
			builder.Append("\",\"");
			builder.Append(data.m_sourceFile);
			builder.Append("\",\"");
			builder.Append(data.m_sourceFunction);
			builder.Append("\",\"");
			builder.Append(data.m_string); // this is first language
			builder.Append('"');
			int languagesRemaining = systemText.GetNumLanguages()-1; // don't include first
			if ( data.m_translations != null )
			{
				foreach( string translation in data.m_translations )
				{
					builder.Append(",\"");
					builder.Append(translation);
					builder.Append('"');
					--languagesRemaining;
				}
			}

			for ( int i = 0; i < languagesRemaining; ++i )
			{
				builder.Append(",");
			}
		}

		string scriptPath = EditorUtility.SaveFilePanel("Export to CSV", "", "Export.csv","csv");
		if ( string.IsNullOrEmpty(scriptPath) == false )
		{
			try 
			{
				File.WriteAllText(scriptPath, builder.ToString(), System.Text.Encoding.Default);
			}
			catch (System.Exception e)
			{
				EditorUtility.DisplayDialog("CSV Export Failed","Failed to export to CSV file.\nCheck it's not open elsewhere.\n\nError: "+e.Message,"ok");
			}


			Application.OpenURL(Path.GetDirectoryName(scriptPath));
		}

	}


	public void ImportFromCSV( SystemText systemText )
	{
		string scriptPath = EditorUtility.OpenFilePanel("Import Text from CSV", "", "csv");
		if ( string.IsNullOrEmpty(scriptPath) )
			return;

		int lineId = -1;
		int numLanguages = systemText.GetNumLanguages();

		// Using CSV-Reader https://github.com/tspence/csharp-csv-reader

		FileStream stream = null;
		StreamReader streamReader = null;

		try
		{
			stream = File.OpenRead(scriptPath);
			streamReader = new StreamReader(stream, System.Text.Encoding.Default);

			using ( CSVFile.CSVReader reader = new CSVFile.CSVReader(streamReader, new CSVFile.CSVSettings() { HeaderRowIncluded = false }) )
			{
				foreach ( string[] line in reader )
				{
					++lineId;
					if ( lineId == 0 )
					{
						// Check for expected languages
						if ( line.Length < CSV_NUM_HEADERS + numLanguages )
						{
							string error = "Import canceled, unexpected columns:\nFound: ";
							for ( int i = 0; i < line.Length; ++i )
								error += line[i] + ", ";
							error += "\nExpected: "
								  + "Character,ID,File,Context";
							foreach (LanguageData language in systemText.GetLanguages())
								error+=","+language.m_description;
							Debug.LogError(error);
							break;
						}
						continue;
					}
					if ( line.Length < CSV_NUM_HEADERS+1 )
						continue; // skipping line, since it doesn't have the right amount of stuff
					string character = line[0];
					int id = -1;
					if ( int.TryParse(line[1], out id) == false )
						id = -1;
					string defaultText = line[CSV_INDEX_LANGUAGES];

					// Find the line
					TextData textData = systemText.EditorFindText(defaultText, id, character );
					if ( textData == null )
					{
						Debug.Log("Failed to import line (not found in text system): "+character+id+": "+defaultText);
					}
					else if ( numLanguages > 1 )
					{
						// Import other languages
						textData.m_translations = new string[numLanguages-1];
						for ( int i = 1; i < numLanguages && CSV_INDEX_LANGUAGES+i < line.Length; ++i)
						{
							textData.m_translations[i-1] = line[CSV_INDEX_LANGUAGES+i];
						}
					}

					textData.m_changedSinceImport = false;
					
				}
			}

		}
		catch (System.IO.IOException e )
		{
			EditorUtility.DisplayDialog("CSV Import Failed","Failed to open CSV file. \nCheck it's not already open elsewhere.\n\nError: "+e.Message ,"ok");
		}
		catch (System.Exception e)
		{	
			EditorUtility.DisplayDialog("CSV Import Failed","Failed to import CSV file.\n\nError: "+e.Message ,"ok");
		}
		finally
		{
			if ( streamReader != null )
				streamReader.Close();
			if ( stream != null )
				stream.Close();
		}
	}

	/// Reads in from rhubarb's output.txt into the TextData
	public static void ReadRhubarbData(SystemText systemText, int id)
	{
		if ( systemText == null )
	    {
	        return;
	    }

		if( id < 0)
		    return;

	    // Get data from processed line
		TextData data = systemText.EditorGetTextDataOrdered()[id];
		string[] phones = File.ReadAllLines("RhubarbOutput.txt");
		data.m_phonesTime = new float[phones.Length];
		data.m_phonesCharacter = new char[phones.Length];
		for (int i = 0; i < phones.Length; ++i )
		{
		    // read in phones (tab separated)
		    string[] strings = phones[i].Split('\t');
			data.m_phonesTime[i] = float.Parse(strings[0]);
			data.m_phonesCharacter[i] = strings[1][0];
		}
	}

	/// Runs the rhubarb tool to generate lip-sync data for the specific data id
	public static System.Diagnostics.Process StartRhubarb(SystemText systemText, int id)
	{
		TextData data = systemText.EditorGetTextDataOrdered()[id];

	    // Skip narrated lines
		if ( string.IsNullOrEmpty(data.m_character) || data.m_character == "Narr" )
		{
			return null;
		}

		// Skip lines that don't have dialog wav
		string fullFileName = "Voice\\"
			+ data.m_character
			+ data.m_id.ToString();			
		AudioClip clip = Resources.Load(fullFileName) as AudioClip;
		if ( clip == null )
		{
			return null;
		}		    			

		File.WriteAllText("RhubarbInput.txt", data.m_string);

		System.Diagnostics.Process rhubarbProcess = new System.Diagnostics.Process();
		rhubarbProcess.StartInfo.FileName = @Path.Combine("Assets", "PowerQuest", "Scripts", "PowerQuest", "Editor", "RunRhubarb.bat");
		rhubarbProcess.StartInfo.Arguments = @Path.Combine("Assets", "Audio", "Resources" + fullFileName + ".wav"+" "+systemText.GetLipsyncExtendedMouthShapes());
		rhubarbProcess.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Minimized;
		rhubarbProcess.Start();
		return rhubarbProcess;

	}

}
}
