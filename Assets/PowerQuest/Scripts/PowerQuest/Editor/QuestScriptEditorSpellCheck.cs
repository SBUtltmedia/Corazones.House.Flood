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
using WeCantSpell.Hunspell;

namespace PowerTools.Quest
{

public partial class QuestScriptEditor
{	
	#region Variables: Private

	// Spell check stuff
	class SpellingMistake
	{
		public int start = 0;
		public int length = 0;
		public int line = 0;
		public int offset = 0;
		public string word = null;
		//public IEnumerable<string> suggestions = null;
	}
	static WordList m_spellCheck = null;
	List<SpellingMistake> m_spellingMistakes = new List<SpellingMistake>();

	// [SerializeField] List<string> m_spellCheckIgnoredWords = new List<string>();


	#endregion

	static bool SpellCheckEnabled { get {  return PowerQuestEditor.Get != null ? PowerQuestEditor.Get.SpellCheckEnabled : false; } }
	static List<string> SpellCheckIgnoredWords { get { return PowerQuestEditor.Get.SpellCheckIgnoredWords; } }
	static string SpellCheckDictionaryPath { get { return PowerQuestEditor.Get.SpellCheckDictionaryPath; } }
 
	public static void InitSpellCheck(bool force = false)
	{		
		// Initialise spellchecker word list
		if ( SpellCheckEnabled && (m_spellCheck == null || force) )
		{
			// find dictionatry 			
			string path = SpellCheckDictionaryPath;			
			if ( string.IsNullOrEmpty(path) )
				path = "Assets/Plugins/PowerQuest/ThirdParty/Editor/SpellCheck/en_US.dic";				
			path = Path.GetDirectoryName(path) + '\\' + Path.GetFileNameWithoutExtension(path);
			string dicPath = path+".dic";
			string affPath = path+".aff";		
			try
			{	
				m_spellCheck =  WordList.CreateFromFiles(dicPath,affPath);
			}
			catch
			{
				Debug.LogError("Failed to load dictonary at "+dicPath+".  Disabling Spell Check.");
				PowerQuestEditor.Get.SpellCheckEnabled = false;
			}
		}
	}

	void OnGuiSpellCheckSuggestions(Event ev, Rect rect)
	{
		if ( SpellCheckEnabled == false )
			return;
		//
		// Spell check suggestions buttons (on right-click mouse up)
		//			
		if ( m_spellingMistakes.Count > 0 && ev.type == EventType.MouseUp && ev.button == 1 )
		{
			// Probably a smarter way of doing this is to convert mouse coords to "character offset" and then use that to find the word you're at. At least, if I end up adding more context menu things
			float lineHeight = s_textStyle.lineHeight;
			float charWidth = s_textStyle.CalcSize(new GUIContent("XX")).x - s_textStyle.CalcSize(new GUIContent("X")).x;
			float xMin = rect.x + s_textStyle.padding.left - 2;
			float yMin = rect.y + s_textStyle.padding.top - 2;	
	
			m_spellingMistakes.ForEach( item=> 
			{ 	
				
				if (  (new Rect(
					xMin + charWidth * item.offset,
					yMin+lineHeight * item.line,
					4 + item.length * charWidth,
					lineHeight
					)).Contains(ev.mousePosition) )
				{
					ev.Use();
					m_clickedMistake = item;

					GenericMenu menu = new GenericMenu();
					

					IEnumerable<string> suggestions = m_spellCheck.Suggest( item.word );
					foreach( string suggestion in suggestions )
					{
						menu.AddItem( new GUIContent(suggestion),false, CorrectSpelling, suggestion );
					}
					menu.AddSeparator("");
					menu.AddItem( new GUIContent("Ignore"), false, IgnoreSpellCheckWord, item.word );

					// display the menu
           			menu.ShowAsContext();
				}
				
			});	
		}

	}

	void OnGuiSpellCheckLayout(Event ev, Rect rect, bool updateTextDisplay )
	{
		
		if ( SpellCheckEnabled == false )
			return;
		//
		// Handle spell check
		//
		{
			if ( updateTextDisplay )
			{
				// Pretty inefficient!
				UpdateSpellCheck(); 
			}
			
			if ( m_spellingMistakes.Count > 0 && ev.type == EventType.Repaint )
			{
				// Find character size so we can get offset of a word
				float lineHeight = s_textStyle.lineHeight;
				float charWidth = s_textStyle.CalcSize(new GUIContent("XX")).x - s_textStyle.CalcSize(new GUIContent("X")).x;
				float xMin = rect.x + s_textStyle.padding.left - 2;
				float yMin = rect.y + s_textStyle.padding.top - 2;
				
				m_spellingMistakes.ForEach( item=> 
				{ 				
					// Find offset from character
					EditorGUI.DrawRect( new Rect(
						xMin + charWidth * item.offset,
						yMin+lineHeight * item.line,
						4 + item.length * charWidth,
						lineHeight
						), Color.red.WithAlpha(0.1f));					
				});
			}
		}
	}
	
	void IgnoreSpellCheckWord(object word)
	{
		//Debug.Log("Add to dictionary: "+word);
		SpellCheckIgnoredWords.Add((string)word);
		m_clickedMistake = null;
		UpdateSpellCheck();
	}

	void CorrectSpelling(object correction)
	{
		if ( SpellCheckEnabled == false )
			return;
		if ( m_clickedMistake != null )
		{			
			Undo.IncrementCurrentGroup();
			Undo.RecordObject(this, "Correct Spelling");
			
			m_text = m_text.Remove(m_clickedMistake.start,m_clickedMistake.length).Insert(m_clickedMistake.start, (string)correction);			
			TextEditor tEditor = FindTextEditor();		
			if ( m_textEditor != null )
			{
				m_textEditor.text = m_text;				
				tEditor.cursorIndex = m_clickedMistake.start + ((string)correction).Length;
				tEditor.selectIndex = tEditor.cursorIndex;				
			}
			
			m_dirty = true;
			UpdateSpellCheck();
			UpdateRichText();
			Repaint();
		}
		m_clickedMistake = null;
	}

	void UpdateSpellCheck()
	{
		if ( SpellCheckEnabled == false || m_spellCheck == null )
			return;

		m_spellingMistakes.Clear();
		foreach( Regex pattern in REGEX_COLOR_DIALOG )
		{
			// Spell check strings			
			MatchCollection matches = pattern.Matches(m_text);
			foreach ( Match match in matches )
			{
				Group dialogGroup = match.Groups[2];
				string dialog = dialogGroup.Value;				
				//Debug.Log(dialog);
				var words = Regex.Matches(dialog,@"[\w'-]+");
				if ( words.Count > 1 ) // ignore identifiers
				{
					foreach (Match word in words)
					{
						//Debug.Log(word.ToString());		
						string wordString = word.ToString();			
						if ( m_spellCheck.Check(wordString) == false && SpellCheckIgnoredWords.Contains(wordString) == false)
						{
							// Find line from character
							int start = dialogGroup.Index+word.Index;
							int offset = start;
							int line = 0;
							for (int i = 0; i < start; ++i )
							{
								if ( m_text[i] == '\n' )
								{
									line++;
									offset = start - (i+1);
								}
							}

							m_spellingMistakes.Add(new SpellingMistake() {start = dialogGroup.Index+word.Index, length = word.Length, word = wordString,  line=line, offset=offset });
						}
					}
				}
			}
		}		
	}

}

}
