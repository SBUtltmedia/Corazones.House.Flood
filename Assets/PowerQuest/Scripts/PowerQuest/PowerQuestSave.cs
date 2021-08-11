using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using System.Reflection;
using PowerTools;

//
// PowerQuest Partial Class: Save/Restore functions
//

namespace PowerTools.Quest
{

public partial class PowerQuest
{

	#region Variables: Private

	QuestSaveManager m_saveManager = new QuestSaveManager();

	#endregion
	#region Functions: Save/Load public functions

	public List<QuestSaveSlotData> GetSaveSlotData() { return m_saveManager.GetSaveSlotData(); }
	public QuestSaveSlotData GetSaveSlotData( int slot ) { return m_saveManager.GetSaveSlot(slot); }
	public bool Save(int slot, string description)
	{
		Dictionary<string, object> data = new Dictionary<string, object>();
		// TODO: add anything we want to save to the data

		foreach( Character value in m_characters )
		{
			data.Add( "Char"+value.GetScriptName(), value );
		}
		foreach( Room value in m_rooms )
		{
			data.Add( "Room"+value.GetScriptName(), value );
		}
		foreach( Gui value in m_guis )
		{
			data.Add( "Gui"+value.GetScriptName(), value );
		}
		foreach( Inventory value in m_inventoryItems )
		{
			data.Add( "Inv"+value.GetScriptName(), value );
		}
		foreach( DialogTree value in m_dialogTrees )
		{
			data.Add( "Dlg"+value.GetScriptName(), value );
		}

		data.Add("Global",m_globalScript );
		data.Add("Camera", m_cameraData );
		data.Add("Cursor", m_cursor );
		data.Add("Settings",m_settings );
		data.Add("SV", m_savedVars);
		data.Add("Extra", 
			new ExtraSaveData() 
			{
				//m_paused = Paused,
				m_player = m_player.ScriptName,
				m_currentDialog = m_currentDialog != null ? m_currentDialog.ScriptName : string.Empty,
			});
		data.Add("Audio", SystemAudio.Get.GetSaveData());

		return m_saveManager.Save(slot, description, m_saveVersion, data);
	}

	public bool RestoreSave(int slot)
	{
		Dictionary<string, object> data = null;
		int restoredVersion = -1;

		// Stop all coroutines
		StopAllCoroutines();				
		GetMenuManager().ResetFade(); // Reset fade, so if faded out when restored the game doesn't start faded out. Note that this means you might want to do another 'FadeOutBG(0)' after this function is called, if you want a slower fadein.
		m_consumedInteraction = null;
		m_coroutineMainLoop = null;
		m_backgroundSequence = null;
		m_backgroundSequences.Clear();
		m_currentSequence = null;
		m_currentSequences.Clear();
		m_currentSequence = null;

		bool result = m_saveManager.RestoreSave(slot, m_saveVersionRequired, out restoredVersion, out data);

		object[] onPostRestoreParams = {restoredVersion};

		if ( result )
		{
			m_restoring = true;

			//
			// Load quest objects
			//
			for( int i = 0; i < m_characters.Count; ++i )
			{
				Character value = m_characters[i];
				string name = "Char"+value.GetScriptName();
				if ( data.ContainsKey(name ) )
					m_characters[i] = data[name] as Character;
			}
			m_player = m_characters[0];

			for( int i = 0; i < m_rooms.Count; ++i )
			{
				Room value = m_rooms[i];
				string name = "Room"+value.GetScriptName();
				if ( data.ContainsKey(name ) )
					m_rooms[i] = data[name] as Room;
			}
			for( int i = 0; i < m_guis.Count; ++i )
			{
				Gui value = m_guis[i];
				string name = "Gui"+value.GetScriptName();
				if ( data.ContainsKey(name ) )
					m_guis[i] = data[name] as Gui;
			}
			for( int i = 0; i < m_inventoryItems.Count; ++i )
			{
				Inventory value = m_inventoryItems[i];
				string name = "Inv"+value.GetScriptName();
				if ( data.ContainsKey(name) )
					m_inventoryItems[i] = data[name] as Inventory;
			}
			for( int i = 0; i < m_dialogTrees.Count; ++i )
			{
				DialogTree value = m_dialogTrees[i];
				string name = "Dlg"+value.GetScriptName();
				if ( data.ContainsKey(name) )
					m_dialogTrees[i] = data[name] as DialogTree;
			}
			{
				string name = "Global";
				if ( data.ContainsKey(name) )
					m_globalScript = data[name] as GlobalScript;
			}
			{
				string name = "Camera";
				if ( data.ContainsKey(name) )
					m_cameraData = data[name] as QuestCamera;
			}
			{
				string name = "Cursor";
				if ( data.ContainsKey(name) )
					m_cursor = data[name] as QuestCursor;
			}
			{
				string name = "Settings";
				if ( data.ContainsKey(name) )
					m_settings = data[name] as QuestSettings;
			}
			{
				string name = "SV";
				if ( data.ContainsKey(name) )
					m_savedVars = data[name] as SavedVarCollection;
			}
			{
				string name = "Extra";

				if ( data.ContainsKey(name) )
				{
					ExtraSaveData extraSaveData = data[name] as ExtraSaveData;
					SetPlayer( GetCharacter(extraSaveData.m_player) );
					m_currentDialog = GetDialogTree(extraSaveData.m_currentDialog);
					//Paussed = extraSaveData.m_paused;
				}
			}
			{
				string name = "Audio";
				if ( data.ContainsKey(name) )
					SystemAudio.Get.RestoreSaveData(data[name]);
			}

			//
			// Swap back in hotloaded scripts if running in editor.
			//
			if ( Application.isEditor )
			{
				if ( m_hotLoadAssembly != null )
				{
					List<IQuestScriptable> scriptables = GetAllScriptables();
					foreach ( IQuestScriptable scriptable in scriptables )
					{
						scriptable.HotLoadScript(m_hotLoadAssembly);
					}
				}
			}

			//
			// Call post-restore functions
			//

			
			// Call post restore on quest objects
			{
				for( int i = 0; i < m_characters.Count; ++i )
					m_characters[i].OnPostRestore(restoredVersion, m_characterPrefabs[i].gameObject);

				for( int i = 0; i < m_rooms.Count; ++i )
					m_rooms[i].OnPostRestore(restoredVersion, m_roomPrefabs[i].gameObject);

				for( int i = 0; i < m_guis.Count; ++i )
					m_guis[i].OnPostRestore(restoredVersion, m_guiPrefabs[i].gameObject);

				for( int i = 0; i < m_inventoryItems.Count; ++i )
					m_inventoryItems[i].OnPostRestore(restoredVersion, m_inventoryPrefabs[i].gameObject);

				QuestCameraComponent cameraInstance = GameObject.FindObjectOfType<QuestCameraComponent>();
				if ( cameraInstance != null )
					m_cameraData.SetInstance(cameraInstance);

				m_cursor.OnPostRestore(restoredVersion, m_cursorPrefab.gameObject);				

				m_settings.OnPostRestore(restoredVersion/*, m_cursorPrefab.gameObject*/);

				// update region collision to initial state
				{
					UpdateRegions();
					if ( m_currentRoom != null )
						m_currentRoom.GetInstance().GetRegionComponents().ForEach( item=>item.OnRoomLoaded() );
				}
			}

			// Call post restore on game scripts
			{
				List<IQuestScriptable> scriptables = GetAllScriptables();
				scriptables.ForEach( item => CallScriptPostRestore(item, onPostRestoreParams) );
			}

			// Call post restore on SaveManager (which calls it to custom save data)
			m_saveManager.OnPostRestore();

			// unblock again
			Unblock();
			
			// Need to load the scene the player's in, and start main loop
			StartRoomTransition((Room)GetPlayer().Room, true);
		}
		Unblock();

		return result;
	}

	public bool DeleteSave(int slot)
	{
		return m_saveManager.DeleteSave(slot);
	}

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

	**Examples saving a simple data class:**
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

	**Example using the [QuestSave] attribute:**
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
	public void AddSaveData(string name, object data, System.Action OnPostRestore = null ) { m_saveManager.AddSaveData(name,data,OnPostRestore); }

	/// Advanced save/restore function: For aving data not in a QuestScript. Call this when you've called AddSaveData, but no longer want to save that data.
	public void RemoveSaveData(string name) { m_saveManager.RemoveSaveData(name); }

	#endregion
	#region Save/Load helpers

	void CallScriptPostRestore(IQuestScriptable scriptable, object[] onPostRestoreParams )
	{
		if ( scriptable.GetScript() != null )
		{
			MethodInfo method = scriptable.GetScript().GetType().GetMethod( "OnPostRestore", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance );
			if ( method != null ) method.Invoke(scriptable.GetScript(), onPostRestoreParams);					
		}
	}
	#endregion

}

}
