using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Audio;

namespace PowerTools.Quest
{


[ExecuteInEditMode]
/// The PowerQuest Audio System. Provides convenient, 2d specific audio functionality, using audio cues.
/**
 * Functions are accessable from Quest Scripts using 'Audio.'
 * From other scripts, use the static functions from 'SystemAudio.'
 * 
 * Features:
 * - This is an audio cue based system. You create an AudioCue in your project, set up which clips it plays and data like volume, pitch, etc. Then you play that "Cue". 
 * - Cues can be added to SystemAudio so that you can play them by name. Then it's as simple as calling "Audio.Play("gunshot");
 * - Cues have a lot of controls for randomisation so common sounds don't get repetive: set min/max pitch, a list of clips to randomly choose from, and even string multiple cues together with random delays
 * - Type based volume control: Flag cues with a type (eg SoundEffect, Music, Dialog), and then set those volumes seperately
 * - Simple functions for playing music and an ambient loop and crossfading between them
 * - Stop/Pause/Modify volume/pitch/pan, etc of currently playing sounds by their cue name, or the AudioHandle returned from the Play Function. eg: `Audio.GetHandle("gunshot").pitch = 0.5f`, `Audio.Stop("gunshot");`
 * - Ducking of music when playing dialog
 * - Simple 2d falloff and stereo control: Some basic controls set how sounds get quieter, and move left/right based on their position relative to the camera
 * - Automatic pooling system for efficiency
 * - Save/Restore audio state
 */
public class SystemAudio : SingletonAuto<SystemAudio>
{	
	#region Definitions

	[System.Serializable]
	class AudioTypeVolume
	{
		public AudioCue.eAudioType m_type = AudioCue.eAudioType.Sound;
		public float m_volume = 1.0f;
		// Default mixer group for the type of sound. Can be overridden per-cue.
		public AudioMixerGroup m_mixerGroup = null;
	};

	class ClipInfo
	{
		//ClipInfo used to maintain default audio source info
		public AudioCue cue { get; set; }
		public AudioHandle handle { get; set; }
		public int type { get; set; }
		public float defaultVolume { get; set; }
		public float defaultPitch { get; set; }
		public float targetVolume { get; set; } // targetVolume used for fading in/out (especially when handling save/load)
		public float fadeDelta {get;set;} // volume change per second
		public bool stopAfterFade { get; set; } // Whether to stop after fadeout
		public Transform emmitter { get; set; }
		public bool paused { get; set; }
	}

	static List<AudioHandle> s_defaultAudioHandleList = new List<AudioHandle>();

	static readonly string STRING_NAME_PREFIX = "Audio: ";

	static int LAYER_NOPAUSE = -1;

	// The initial size of the audio pool, if this limit is reached, more will be added at runtime, no big deal. Just stops potential framerate drop first time a bunch of sounds are played
	static readonly int AUDIO_SOURCE_POOL_SIZE = 16;

	#endregion 
	#region Editable vars

	[Header("Default volume levels(and/or mixer groups), by type (Music, SFX, Dialog)")]
	[ReorderableArray]
	[SerializeField] List<AudioTypeVolume> m_volumeByType = new List<AudioTypeVolume>();
	[SerializeField] float m_musicDuckingMaxVolume = 0.5f;

	[Header("Default falloff values (radios of screen width)")]
	[SerializeField] float m_falloffMinVol = 0.2f;
	[SerializeField] float m_falloffPanMax= 0.75f;

	[SerializeField] float m_falloffStart = 1.2f;
	[SerializeField] float m_falloffEnd = 1.2f;

	[SerializeField] float m_falloffPanStart = 0.5f;
	[SerializeField] float m_falloffPanEnd = 2.0f;

	[Header("Misc settings")]
	[Tooltip("Default minimum time between the same sound playing again")]
	[SerializeField] float m_noDuplicateTime = 0.05f;

	[Header("List of audio cues that can be played by name")]
	[Tooltip("If set, any cues in the Audio folder will be automatically added (you don't have to click the button in the cue)")]
	[SerializeField] bool m_autoAddCues = true;
	[Tooltip("Audio cues that can be played by name")]
	[SerializeField, ReorderableArray] List<AudioCue> m_audioCues = new List<AudioCue>();

	#endregion
	#region private vars

	List<ClipInfo> m_activeAudio = new List<ClipInfo>();
	AudioHandle m_activeMusic;
	AudioHandle m_activeAmbientLoop = null;

	Transform m_audioListener = null;

	bool m_hasFocus = true;

	List<AudioSource> m_audioSources = new List<AudioSource>();

	Camera m_cameraGame = null;

	// For save data
	string m_musicCueName = string.Empty;
	float m_musicVolOverride = 1;
	string m_ambientCueName = null;

	#endregion
	#region Public Static Functions

	/// Sets the volume (from 0.0 to 1.0) for audio of a particular type (eg: Sound effects, Music, Dialog) 
	public static void SetVolume( AudioCue.eAudioType type, float volume )
	{		
		SystemAudio self = SystemAudio.Get;
		if ( self == null )
		{
			Debug.LogWarning("Failed to set AudioSystem volume. It hasn't been initialised");
			return;
		}

		float oldVolume = 1.0f;
		bool exists = false;

		if ( volume <= 0 ) // If volume goes to zero we lose the scale information. So make it -1 instead... mathyhack!
			volume = -1;

		for ( int i = 0; i < self.m_volumeByType.Count; ++i )
		{
			if ( self.m_volumeByType[i].m_type == type )
			{
				oldVolume = self.m_volumeByType[i].m_volume;
				self.m_volumeByType[i].m_volume = volume;
				exists = true;
				break;
			}
		}

		if ( exists == false )
		{
			// Doesn't exist yet, so add
			self.m_volumeByType.Add( new AudioTypeVolume() { m_type = type, m_volume = volume } );
		}

		float volChange = volume / oldVolume;
		// Update active audio volumes
		for ( int i = 0; i < self.m_activeAudio.Count; ++i )
		{
			ClipInfo info = self.m_activeAudio[i];
			if ( info != null && (info.type & (int)type) != 0)
			{					
				if ( info.handle.source != null )
					info.handle.source.volume *= volChange;	
				info.defaultVolume *= volChange;
			}
		}

	}

	/// Retrieves the volume for audio of a particular type (eg: Sound effects, Music, Dialog) 
	public static float GetVolume( AudioCue.eAudioType type )
	{
		SystemAudio self = SystemAudio.Get;
		for ( int i = 0; i < self.m_volumeByType.Count; ++i )
		{
			if ( self.m_volumeByType[i].m_type == type )
			{
				return Mathf.Clamp01(self.m_volumeByType[i].m_volume);
			}
		}
		return 1.0f;
	}

	/// Gets the base volume of a particular sound effect (usually use the AudioHandle for this)
	public float GetVolume( AudioHandle source )
	{
		ClipInfo clipInfo = m_instance.m_activeAudio.Find(item=>item.handle == source);
		if ( clipInfo != null )
			return clipInfo.defaultVolume;
		return 0;
	}

	/// Sets the base volume of a particular sound effect (usually use the AudioHandle for this)
	public void SetVolume( AudioHandle source, float volume )
	{
		ClipInfo clipInfo = m_instance.m_activeAudio.Find(item=>item.handle == source);
		if ( clipInfo != null && clipInfo.stopAfterFade == false ) // NB: once stop after fade is called, don't allow volume changes through this function			
		{
			clipInfo.defaultVolume = volume;
			clipInfo.targetVolume = volume;
		}
	}



	/// Retrieves a sound cue by name
	static public AudioCue GetCue(string cueName)
	{
		return m_instance.m_audioCues.Find(item=>string.Equals(cueName, item.name, System.StringComparison.OrdinalIgnoreCase) );
	}

	/// Play a cue by name. This is the main way to play sounds. If emmitter is set, the sound will falloff/pan as it goes off camera. 
	/**
	 * Eg: Audio.Play("Gunshot", C.Gunman.Instance);
	 */
	static public AudioHandle Play( string cueName, Transform emmitter = null )
	{
		AudioCue cue = m_instance.m_audioCues.Find(item=>string.Equals(cueName, item.name, System.StringComparison.OrdinalIgnoreCase));
		if ( cue == null && Debug.isDebugBuild && string.IsNullOrEmpty(cueName) == false)
		{
			Debug.LogWarning("Sound cue not found: "+cueName);
		}
		return Play( cue , emmitter );
	}

	/// Play the specified cue with extended options. If emmitter is set, the sound will falloff/pan as it goes off camera. Can also set volume and pitch overrides, and override the start time of the cue
	static public AudioHandle Play(AudioCue cue, Transform emitter = null, float volumeMult = 1, float pitchMult = 1, float fromTime = 0 )
	{
		return Play( cue, ref s_defaultAudioHandleList, emitter, volumeMult, pitchMult, fromTime);
	}

	/// Advanced Play function with extended options, and returning a list of all handles started (for when multiple are started). If emmitter is set, the sound will falloff/pan as it goes off camera. Can also set volume and pitch overrides, and override the start time of the cue
	static public AudioHandle Play(AudioCue cue, ref List<AudioHandle> handles, Transform emmitter = null, float volumeMult = 1, float pitchMult = 1, float fromTime = 0 )
	{
		if ( cue == null )
			return new AudioHandle(null);

		SystemAudio self = SystemAudio.Get;

		AudioCue.Clip cueClip = cue.GetClip();	

		if ( cueClip == null )
			return new AudioHandle(null);

		if ( Application.isPlaying && PowerQuest.Get.GetSkippingCutscene() && cue.m_loop == false && ((int)AudioCue.eAudioType.Music & cue.m_type) == 0 ) // Quest hack- don't play sounds while skipping cutscenes
			return new AudioHandle(null);

		if ( Random.value > cue.m_chance )
			return new AudioHandle(null);

		AudioClip clip = cueClip.m_sound;

		if ( clip == null )
			return new AudioHandle(null);

		// Check if sound is already being played within X time. The if the cue can override this time, or default (-1) to the parent
		for ( int i = 0; i < self.m_activeAudio.Count; ++i )
		{
			ClipInfo info = self.m_activeAudio[i];
			if ( info.handle != null && info.handle.clip == clip
				&& ( info.handle.time < ( cue.m_noDuplicateTime >= 0 ? cue.m_noDuplicateTime : self.m_noDuplicateTime) ) )
			{
				//Debug.Log("AUD: duplicate"+info.source.name);
				return new AudioHandle(null);
			}
		}

		float volume = cueClip.m_volume.GetRandom() * volumeMult;

		volume *= cue.m_volume.GetRandom();

		// Apply type volumes & default mixer groups
		AudioMixerGroup defaultMixerGroup = null;
		for ( int i = 0; i < self.m_volumeByType.Count; ++i )
		{
			if ( ((int)self.m_volumeByType[i].m_type & cue.m_type) != 0 )
			{
				volume *= self.m_volumeByType[i].m_volume;
				defaultMixerGroup = self.m_volumeByType[i].m_mixerGroup;
			}			
		}

		float pitch =  cue.m_pitch.GetRandom() *  cueClip.m_pitch.GetRandom() * pitchMult;

		// Create the source
		AudioSource source = self.SpawnAudioSource( 
			( Debug.isDebugBuild ) ? STRING_NAME_PREFIX + clip.name : STRING_NAME_PREFIX, // don't bother with strings if not debugging
			self.transform.position);

		{
			AudioReverbFilter sourceFilter = source.gameObject.GetComponent<AudioReverbFilter>();
			if ( sourceFilter == null ) 
			{
				self.AddSourceFilters(source.gameObject); 
				sourceFilter = source.gameObject.GetComponent<AudioReverbFilter>();
			}
			sourceFilter.enabled = cue.m_reverbPreset != AudioReverbPreset.Off;
			if ( sourceFilter.enabled )
			{
				sourceFilter.reverbPreset = cue.m_reverbPreset;
			}

		}
		{
			AudioEchoFilter sourceFilter = source.gameObject.GetComponent<AudioEchoFilter>();
			sourceFilter.enabled = cue.m_echoFilter != null;
			if ( sourceFilter.enabled )
			{
				sourceFilter.delay = cue.m_echoFilter.delay;
				sourceFilter.decayRatio = cue.m_echoFilter.decayRatio;
				sourceFilter.dryMix = cue.m_echoFilter.dryMix;
			}
		}
		{
			AudioDistortionFilter sourceFilter = source.gameObject.GetComponent<AudioDistortionFilter>();
			sourceFilter.enabled = cue.m_distortionLevel > 0;
			if ( sourceFilter.enabled )
			{
				sourceFilter.distortionLevel = cue.m_distortionLevel;
			}
		}
		{
			AudioHighPassFilter sourceFilter = source.gameObject.GetComponent<AudioHighPassFilter>();
			sourceFilter.enabled = cue.m_highPassFilter != null;
			if ( sourceFilter.enabled )
			{
				sourceFilter.cutoffFrequency = cue.m_highPassFilter.cutoffFrequency;
				sourceFilter.highpassResonanceQ = cue.m_highPassFilter.highpassResonanceQ;
			}
		}
		{
			AudioLowPassFilter sourceFilter = source.gameObject.GetComponent<AudioLowPassFilter>();
			sourceFilter.enabled = cue.m_lowPassFilter != null;
			if ( sourceFilter.enabled )
			{
				sourceFilter.cutoffFrequency = cue.m_lowPassFilter.cutoffFrequency;
				sourceFilter.lowpassResonanceQ = cue.m_lowPassFilter.lowpassResonanceQ;
			}
		}
		{
			AudioChorusFilter sourceFilter = source.gameObject.GetComponent<AudioChorusFilter>();
			sourceFilter.enabled = cue.m_chorusFilter != null;
			if ( sourceFilter.enabled )
			{
				sourceFilter.delay = cue.m_chorusFilter.delay;
				sourceFilter.depth = cue.m_chorusFilter.depth;
				sourceFilter.dryMix = cue.m_chorusFilter.dryMix;
				sourceFilter.wetMix1 = cue.m_chorusFilter.wetMix1;
				sourceFilter.wetMix2 = cue.m_chorusFilter.wetMix2;
				sourceFilter.wetMix3 = cue.m_chorusFilter.wetMix3;
				sourceFilter.rate = cue.m_chorusFilter.rate;				
			}
		}
		
		if ( cue.m_mixerGroup != null )
			source.outputAudioMixerGroup = cue.m_mixerGroup;
		else 
			source.outputAudioMixerGroup = defaultMixerGroup;
		

		/* Use audiosystem as source of all sounds */
		source.transform.parent = self.transform;

		source.transform.localPosition = Vector3.zero;

		self.SetSource(ref source, clip, volume, pitch, 128, emmitter );		
		source.loop = cue.m_loop;

		if ( fromTime <= 0.0f && cueClip.m_startTime > 0 )
		{
			fromTime = cueClip.m_startTime;
		}		
		if ( fromTime > 0.0f ) // There's a known unity error where starting from time errors. So don't set source time if it's close to the end (NB: This still may happen, need to watch for it)
			source.time = Mathf.Min(fromTime, (source.clip.length-0.01f)*0.9f); // There's a known unity error where starting from time errors. So don't set source time if it's close to the end (NB: This still may happen, need to watch for it)
		else
			source.time = 0.0f;

		// Play (with start delay if one exists)
		float startDelay = cue.m_startDelay.GetRandom();
		if ( startDelay > 0.0f )
		{
			source.PlayDelayed(startDelay);
		}
		else 
		{
			
			source.Play();	
			if ( source.isPlaying == false && source.time > 0 )
			{
				// Must have errored when trying to play from time (ther'es a known unity bug) so try playing again without start offset
				if ( Debug.isDebugBuild ) Debug.LogWarning("Failed to play sound from specific time. Retrying from beginning");
				source.time = 0;
				source.Play();
			}
		}

		AudioHandle handle = new AudioHandle(source);

		if ( handles != s_defaultAudioHandleList )
		{
			if ( handles == null )
				handles = new List<AudioHandle>();
			handles.Add(handle);
		}

		// Stop clip before it's end
		if ( cueClip.m_endTime > 0 )
		{			
			float stopTime = 	cueClip.m_endTime;
			if ( cueClip.m_startTime > 0 )
				stopTime -= cueClip.m_startTime;
			stopTime /= pitch;
			if ( startDelay > 0 )
				stopTime += startDelay;			
			handle.source.SetScheduledEndTime(AudioSettings.dspTime + (double)stopTime);
		}

		// Shake camera
		/*if ( self.m_camera != null && cueClip.m_shakeTime > 0 )
		{
			self.m_camera.SendMessage("Shake", cueClip.m_shakeTime, cueClip.m_shakeType );
		}*/

		//Set the source as active
		self.AddActiveAudio(new ClipInfo{
			handle = handle, 
			cue = cue,
			type = cue.m_type,
			defaultVolume = volume, 
			targetVolume = volume,
			defaultPitch = pitch,
			emmitter = (emmitter == null ? self.transform : emmitter) });

		// NB: Probably should apply "active audio" updates to the clip immediately

		if ( cue.m_alsoPlay != null )
			Play( cue.m_alsoPlay, ref handles, emmitter );

		
		return handle;
	}

	/// Plays a specific audio clip rather than an audio cue
	static public AudioHandle Play( AudioClip clip, int type = (int)AudioCue.eAudioType.Sound, Transform emmitter = null, float volume = 1, float pitch = 1, bool loop = false, AudioMixerGroup mixerGroup = null )
	{
		if ( clip == null)
			return null;

		SystemAudio self = SystemAudio.Get;

		// Check if sound is already being played within X time
		for ( int i = 0; i < self.m_activeAudio.Count; ++i )
		{
			ClipInfo info = self.m_activeAudio[i];
			if ( info.handle != null && info.handle.clip == clip && info.handle.time < self.m_noDuplicateTime )
			{
				//Debug.Log("AUD: duplicate"+info.source.name);
				return null;
			}
		}

		// Apply type volumes
		AudioMixerGroup defaultMixerGroup = null;
		for ( int i = 0; i < self.m_volumeByType.Count; ++i )
		{
			if ( ((int)self.m_volumeByType[i].m_type & type) != 0 )
			{
				volume *= self.m_volumeByType[i].m_volume;
				defaultMixerGroup = self.m_volumeByType[i].m_mixerGroup;
			}
		}

		// Create the source
		AudioSource source = self.SpawnAudioSource( 
			( Debug.isDebugBuild ) ? STRING_NAME_PREFIX + clip.name : STRING_NAME_PREFIX, // don't bother with strings if not debugging
			self.transform.position);
		// Disable filters
		{
			AudioReverbFilter sourceFilter = source.gameObject.GetComponent<AudioReverbFilter>();
			if ( sourceFilter != null )
				sourceFilter.enabled = false;

		}
		{
			AudioEchoFilter sourceFilter = source.gameObject.GetComponent<AudioEchoFilter>();
			if ( sourceFilter != null )
				sourceFilter.enabled = false;
		}
		{
			AudioDistortionFilter sourceFilter = source.gameObject.GetComponent<AudioDistortionFilter>();
			if ( sourceFilter != null )
				sourceFilter.enabled = false;
		}
		{
			AudioHighPassFilter sourceFilter = source.gameObject.GetComponent<AudioHighPassFilter>();
			if ( sourceFilter != null )
				sourceFilter.enabled = false;			
		}
		{
			AudioLowPassFilter sourceFilter = source.gameObject.GetComponent<AudioLowPassFilter>();
			if ( sourceFilter != null )
				sourceFilter.enabled = false;
		}
		{
			AudioChorusFilter sourceFilter = source.gameObject.GetComponent<AudioChorusFilter>();
			if ( sourceFilter != null )
				sourceFilter.enabled = false;
		}
		
		if ( mixerGroup != null )
			source.outputAudioMixerGroup = mixerGroup;
		else 		
			source.outputAudioMixerGroup = defaultMixerGroup;

		/* Use audiosystem as source of all sounds */
		source.transform.parent = self.transform;

		source.transform.localPosition = Vector3.zero;

		self.SetSource(ref source, clip, volume, pitch, 0, emmitter ); // NB: Priority is 0 (highest) for clips, since they're usually dialog	
		source.loop = loop;
		source.time = 0;
		source.Play();

		// Shake camera
		/*if ( self.m_camera != null && cueClip.m_shakeTime > 0 )
		{
			self.m_camera.SendMessage("Shake", cueClip.m_shakeTime, cueClip.m_shakeType );
		}*/

		// if no emmitter specified, emit from audio system
		if ( emmitter == null )
		{
			emmitter = self.transform;
		}

		AudioHandle handle = new AudioHandle(source);

		//Set the source as active
		self.AddActiveAudio(new ClipInfo{
			handle = handle, 
			cue = null,
			type = type,
			defaultVolume = volume, 
			targetVolume = volume,
			defaultPitch = pitch,
			emmitter = emmitter == null ? self.transform : emmitter } );

		return handle;
	}	

	/// Pauses the specified sound by it's handle
	public static void Pause(AudioHandle handle)
	{
		if ( handle == null )
			return;
		ClipInfo clipInfo = m_instance.m_activeAudio.Find( clip=>clip.handle == handle );
		if ( clipInfo != null && clipInfo.paused == false ) 
		{
			clipInfo.paused = true;
			handle.source.Pause();
		}
	}

	/// Resumes the specified sound by it's handle
	public static void UnPause(AudioHandle handle)
	{
		if ( handle == null )
			return;
		ClipInfo clipInfo = m_instance.m_activeAudio.Find( clip=>clip.handle == handle );
		if ( clipInfo != null && clipInfo.paused == true ) 
		{
			clipInfo.paused = false;
			//source.UnPause();
			handle.source.Play();	
		}
	}

	/// Pauses the specified sound by it's cue name
	public static void Pause(string cueName)
	{
		foreach ( ClipInfo clip in m_instance.m_activeAudio )
		{
			if ( clip.cue != null && string.Equals( clip.cue.name, cueName, System.StringComparison.OrdinalIgnoreCase ) )
			{
				Pause(clip.handle);
			}
		}
	}

	/// Resumes the specified sound by it's cue  name
	public static void UnPause(string cueName)
	{
		foreach ( ClipInfo clip in m_instance.m_activeAudio )
		{
			if ( clip.cue != null && string.Equals( clip.cue.name, cueName, System.StringComparison.OrdinalIgnoreCase ) )
			{
				UnPause(clip.handle);
			}
		}
	}

	/// Gets a playing AudioHandle by its cue name
	public static AudioHandle GetHandle(string cueName)
	{
		ClipInfo info = m_instance.m_activeAudio.Find(item=>item.cue != null && string.Equals( item.cue.name, cueName, System.StringComparison.OrdinalIgnoreCase ) );
		return  info == null ? null :  info.handle;
	}

	/// Gets any currently playing AudioHandles by their cue name
	public static AudioHandle[] GetHandles(string cueName)
	{
		List<ClipInfo> info = m_instance.m_activeAudio.FindAll(item=>item.cue != null && string.Equals( item.cue.name, cueName, System.StringComparison.OrdinalIgnoreCase ) );
		if ( info != null && info.Count > 0 )
		{
			AudioHandle[] handles = new AudioHandle[info.Count];
			for ( int i = 0; i < info.Count; ++i )
				handles[i] = info[i].handle;
			return handles;
		}
		return null;
	}

	/// Stops the specified sound by it's cue name
	public static void Stop(string cueName, float overTime = 0)
	{
		// Copy list because stop may remove some
		//ClipInfo[] allAudio = new ClipInfo[m_instance.m_activeAudio.Count];
		//m_instance.m_activeAudio.CopyTo(allAudio);
		foreach ( ClipInfo clip in m_instance.m_activeAudio )
		{
			if ( clip != null && clip.cue != null && string.Equals( clip.cue.name, cueName, System.StringComparison.OrdinalIgnoreCase ) )
			{
				Stop(clip.handle,overTime);
			}
		}
	}

	/// Stops the specified sound by it's handle
	public static void Stop(AudioHandle handle, float overTime = 0)
	{		
		if ( m_instance && handle != null && handle.isPlaying )
		{				
			// This calls back to SystemAudio once "isPlaying" is set false, so the active audio can be removed. Hacky, but means Stop can be called either from here or the handle.
			if ( overTime <= 0 )
				handle.Stop(); 
			else 
				m_instance.StartFade(handle,0,overTime,true);			
		}
	}

	/// Play a music track using the cue name, with optional crossfade time
	public static AudioHandle PlayMusic( string cueName, float fadeTime = 0 ) { return PlayMusic(cueName, fadeTime, fadeTime); }
	/// Play a music track using the cue name, with seperate fade out and fade in times
	public static AudioHandle PlayMusic( string cueName, float fadeOutTime, float fadeInTime ) 
	{
		AudioCue cue = m_instance.m_audioCues.Find(item=>string.Equals(cueName, item.name, System.StringComparison.OrdinalIgnoreCase));
		if ( cue == null && Debug.isDebugBuild && string.IsNullOrEmpty(cueName) == false)
		{			
			Debug.LogWarning("Music sound cue not found: "+cueName);
		}
		return PlayMusic( cue, fadeOutTime, fadeInTime );
	}

	/// Play a music cue, with optional crossfade time
	public static AudioHandle PlayMusic( AudioCue cue, float fadeTime = 0 ) { return PlayMusic(cue, fadeTime,fadeTime); }
	/// Play a music cue, with seperate fade out and fade in times
	public static AudioHandle PlayMusic( AudioCue cue, float fadeOutTime, float fadeInTime ) 
	{
		StopMusic(fadeOutTime);
		m_instance.m_musicCueName = cue == null ? null : cue.name;
		m_instance.m_musicVolOverride = 0;
		m_instance.m_activeMusic = Play(cue);
		if ( fadeInTime > 0 && cue != null )
		{
			//m_instance.StartCoroutine( m_instance.CoroutineFadeIn(m_instance.m_activeMusic, fadeInTime) );
			m_instance.StartFadeIn( m_instance.m_activeMusic, fadeInTime );
		}
		return m_instance.m_activeMusic;
	}

	/// Play a music track using the cue name, Crossfades beteween two music tracks, attempting to keep them synced (assumes identical length/tempo/etc). Optional volume multiplier
	public static AudioSource PlayMusicSynced( string name, float fadeTime, float volumeOverride = 0 ) 
	{
		return PlayMusicSynced( m_instance.m_audioCues.Find(item=>string.Equals(name, item.name, System.StringComparison.OrdinalIgnoreCase)), fadeTime, volumeOverride );
	}

	/// Crossfades beteween two music tracks, attempting to keep them synced (assumes identical length/tempo/etc). Optional volume multiplier
	public static AudioSource PlayMusicSynced( AudioCue cue, float fadeTime, float volumeOverride = 0)
	{
		if ( m_instance.m_activeMusic == null )
			return PlayMusic(cue);
		float syncTime = m_instance.m_activeMusic.time;
		StopMusic(fadeTime*1.5f);
		m_instance.m_musicCueName = cue.name;
		m_instance.m_activeMusic = Play(cue,null,1,1,syncTime);
		// set the volume of the clip
		if ( volumeOverride > 0.0f )
		{
			m_instance.SetVolume(m_instance.m_activeMusic, volumeOverride);
			m_instance.m_musicVolOverride = volumeOverride;
		}
		//m_instance.StartCoroutine( m_instance.CoroutineFadeIn(m_instance.m_activeMusic, fadeTime) );
		m_instance.StartFadeIn( m_instance.m_activeMusic, fadeTime );
		return m_instance.m_activeMusic;
	}

	/// Stops the currently playing music, with optional fade out time
	public static void StopMusic(float fadeTime = 0)
	{
		Stop(SystemAudio.Get.m_activeMusic, fadeTime);
		m_instance.m_activeMusic = null;		
		m_instance.m_musicCueName = null;
	}

	/// Plays an ambient sound by it's cue name, with optional fade time. Sound cue is assumed to be looping.
	public static void PlayAmbientSound( string name, float fadeTime = 0.4f )
	{
		PlayAmbientSound(name,fadeTime,fadeTime);
	}
	/// Plays an ambient sound by it's cue name, with seperate fade out and fade in times. Sound cue is assumed to be looping.
	public static void PlayAmbientSound( string name, float fadeoutTime, float fadeInTime )
	{
		StopAmbientSound(fadeoutTime);
		m_instance.m_activeAmbientLoop = SystemAudio.Play(name);
		if ( fadeInTime > 0 )
			m_instance.m_activeAmbientLoop.FadeIn(fadeInTime);
		m_instance.m_ambientCueName = name;
	}	

	/// Stops the current ambient sound cue, with optional fade time
	public static void StopAmbientSound(float overTime = 0.4f)
	{
		Stop(m_instance.m_activeAmbientLoop, overTime);
		m_instance.m_ambientCueName = string.Empty;
	}

	/// <summary>
	/// Sets a sound's volume and stereo pan based on distance to it.
	///  Use this function when you want sounds to be louder when standing near them, and quieter as you walk away.
	///  The function should be placed in your room's Update() function.
	///  eg. 
	///  ~~~
	///	void Update()
	//  {
	//		// Plays fire dist
	//  	AudioCue.UpdateCustomFalloff( "FireCrackle", P.Fireplace.LookAtPoint, C.Plr.Position, 10, 200, 0.2f, 1.0f );
	//  }
	///  ~~~
	/// </summary>
	/// <param name="cueName">Cue name to fade.</param>
	/// <param name="soundPos">The location of the sound. Eg: `P.Fireplace.LookAtPoint`.</param>
	/// <param name="listenerPos">The location of the listener (usually the player). Eg: `C.Player.Position`.</param>
	/// <param name="closeDist">How close should the player be to the sound for it to be at maxVol. In game units.</param>
	/// <param name="farDist">How far should the player be from the sound for it to be at minVol. In game units.</param>
	/// <param name="farVol">The volume when the player is standing FAR from the sound (eg. quiet). From 0 to 1.</param>
	/// <param name="closeVol">The volume when the player is standing CLOSE to the sound (eg. loud). From 0 to 1.</param>
	/// <param name="farPan">The maximum amount to pan in stereo, when the player is FAR from the sound. From 0 to 1.</param>
	public static void UpdateCustomFalloff(string cueName, Vector2 soundPos, Vector2 listenerPos, float closeDist, float farDist, float farVol = 0, float closeVol = 1, float farPan = 0.7f )
	{
		AudioHandle fireHandle = SystemAudio.GetHandle(cueName);
		if ( fireHandle == null )
			return;
		
		float diff = soundPos.x - listenerPos.x;
		float vol = Mathf.Lerp(closeVol,farVol, Utils.EaseCubic(
			Mathf.InverseLerp(closeDist,farDist,Vector2.Distance(soundPos,listenerPos))));
		float pan = Mathf.Lerp(0,farPan, Utils.EaseCubic(
			Mathf.InverseLerp(closeDist,farDist,Mathf.Abs(diff))));
		pan = pan*Mathf.Sign(diff);
		fireHandle.volume = vol;
		fireHandle.panStereo = pan;
	}

	#endregion
	#region Public, advanced Functions

	public bool GetAnyMusicPlaying() 
	{		
		return m_activeAudio.Exists( item=> (item.type&(int)AudioCue.eAudioType.Music)>0  );
	}

	/// Advanced function to retrieve a cue's default volume
	public float GetCueVolume(AudioCue cue, AudioClip specificClip = null)
	{
		if ( cue == null )
			return 0;
		SystemAudio self = SystemAudio.Get;

		AudioCue.Clip cueClip = cue.GetClipData(specificClip);				
		if ( cueClip == null )
			cueClip = cue.GetClip();

		float volume = cue.m_volume.GetRandom() * cueClip.m_volume.GetRandom() * 1;

		// Apply type volumes
		for ( int i = 0; i < self.m_volumeByType.Count; ++i )
		{
			if ( ((int)self.m_volumeByType[i].m_type & cue.m_type) != 0 )
				volume *= self.m_volumeByType[i].m_volume;
		}

		return volume;

	}

	/// Advanced function that pauses all sounds (used when pausing game)
	public void PauseAllSounds() 
	{ 
		foreach (ClipInfo audioClip in m_activeAudio) 
		{
			try 
			{
				if (audioClip.handle != m_activeMusic) 
				{
					Pause (audioClip.handle);	                
				}
			} 
			catch 
			{
				continue;
			}
		}
	}

	/// Advanced function that resumes all paused sounds (used when pausing game)
	public void ResumeAllSounds() 
	{
		foreach (ClipInfo audioClip in m_activeAudio) 
		{
			try 
			{
				if (!audioClip.handle.isPlaying) 
				{					
					UnPause(audioClip.handle);
				}
			} 
			catch 
			{
				continue;
			}
		}
	}

	/// Advanced function for retrieving the active music handle
	public AudioHandle GetActiveMusicHandle() { return  m_activeMusic; }

	/// Advanced function returns true if the passed cue is the active music
	public bool GetIsActiveMusic(AudioCue cue)
	{
		if ( m_activeMusic == null )
			return cue == null;
		if ( cue == null )
			return false;
		return cue.GetClipData(m_activeMusic.clip) != null;
	}


	/// Editor function for adding an audio cue to the list of cues playable by name. Primarily used by editor. Returns true if added, false if it already existed
	public bool EditorAddCue(AudioCue cue)
	{
		if ( m_audioCues.Contains(cue) == false )
		{
			m_audioCues.Add(cue);
			return true;
		}
		return false; 
	}

	/// Editor function for retrieving all audio cues
	public List<AudioCue> EditorGetAudioCues() { return m_audioCues; }
	/// If set, any cues in the Audio folder will be automatically added (you don't have to click the button in the cue)
	public bool EditorGetAutoAddCues() { return m_autoAddCues; }

	/// Starts fading in a handle. In powerquest it's usually more convenient to use `Audio.Play("MySound").FadeIn(overTime);`
	public void StartFadeIn(AudioHandle handle, float time)
	{
		float targetVolume = GetVolume(handle);
		SetVolume(handle,0);
		StartFade(handle, targetVolume, time);
	}

	/// Starts fading out a handle, optionally stopping it once faded out. In powerquest it's usually more convenient to use `Audio.Stop("MySound", overTime);
	public void StartFadeOut(AudioHandle handle, float time, bool stopOnFinish = false )
	{
		StartFade(handle,0,time,true);
	}

	/// Starts fading a handle to the target volume, optionally stopping it once finished. In powerquest it's usually more convenient to use `Audio.GetCue("MySound").Fade(targetVolume, overTime);`
	public void StartFade( AudioHandle handle, float targetVolume, float time, bool stopOnFinish = false )
	{
		ClipInfo clipInfo = m_activeAudio.Find( clip=>clip.handle == handle );
		if ( clipInfo == null )
			return;

		float oldTarget = clipInfo.targetVolume;

		clipInfo.targetVolume = targetVolume;
		clipInfo.stopAfterFade = stopOnFinish;

		if ( time <= 0 )
		{
			clipInfo.defaultVolume = targetVolume;
			clipInfo.targetVolume = targetVolume;
			if ( stopOnFinish )
				handle.Stop();
			return;
		}

		// Calc fade over time delta
		float dist = Mathf.Abs( targetVolume - oldTarget );
		if ( dist <= 0 )
		{
			if ( stopOnFinish )
				handle.Stop();
			return;
		}

		clipInfo.fadeDelta = dist/time;
	}

	// Save system callbacks
	//////////////////////////////////////////////////////////////////////

	/// @cond InternalSystem

	class SaveData
	{
		public string m_musicCueName = null;
		public float m_musicVolOverride = -1;
		public float m_musicTime = 0;
		public string m_ambientCueName = null;

		public class ActiveAudioSaveData
		{
			public string cueName = null;
			public bool paused = false;
			public float time = 0;
			public float pan = 0;
			public float pitch = 1;
			public float volume = 1;
			public float volumeTarget = 1;
			public bool stopAfterFade = false;

			public ActiveAudioSaveData() {}
			public ActiveAudioSaveData(ClipInfo clipInfo)
			{
				cueName = clipInfo.cue.name;
				paused = clipInfo.paused;
				time =  clipInfo.handle.time;
				pitch = clipInfo.defaultPitch;
				pan = clipInfo.handle.panStereo;
				volume = clipInfo.defaultVolume;
				volumeTarget = clipInfo.targetVolume;
				stopAfterFade = clipInfo.stopAfterFade;
			}
		}
		public ActiveAudioSaveData[] activeAudio = null;
	}


	/// Internal system function
	public object GetSaveData()
	{		
		SaveData result = new SaveData();
		result.m_musicCueName = m_musicCueName;
		result.m_musicVolOverride = m_musicVolOverride;
		result.m_ambientCueName = m_ambientCueName;
		result.m_musicTime = m_activeMusic == null ? 0 : m_activeMusic.time;

		result.activeAudio = new SaveData.ActiveAudioSaveData[m_activeAudio.Count];
		for ( int i = 0; i < m_activeAudio.Count; ++i )
		{
			ClipInfo clipInfo = m_activeAudio[i];				
			if ( clipInfo != null && clipInfo.cue != null
				&& clipInfo.handle.isPlaying
				&& clipInfo.handle != m_activeMusic && clipInfo.handle != m_activeAmbientLoop )
			{				
				result.activeAudio[i] = new SaveData.ActiveAudioSaveData(clipInfo);
			}
		}

		return result as object;
	}

	/// Internal system function
	public void RestoreSaveData(object obj)
	{
		if ( obj == null || obj as SaveData == null )
			return;
		SaveData data = obj as SaveData;

		// Stop/fade current sounds and start them from the ones in data.m_activeAudio. 
		foreach ( ClipInfo clip in m_activeAudio )
		{
			if ( clip != null && clip.handle != null )
				Stop(clip.handle,0.1f);
		}

		if ( data.activeAudio != null )
		{
			// Start cues from save data
			foreach ( SaveData.ActiveAudioSaveData activeAudio in data.activeAudio )
			{
				if ( activeAudio == null )
					continue;
				AudioHandle handle = Play( activeAudio.cueName );				
				ClipInfo clipInfo = m_instance.m_activeAudio.Find( item=>item.handle == handle );
				if ( clipInfo == null )
					continue;
				clipInfo.defaultVolume = activeAudio.volume;
				clipInfo.targetVolume = activeAudio.volume;
				clipInfo.defaultPitch = activeAudio.pitch;
				clipInfo.stopAfterFade = activeAudio.stopAfterFade;
				handle.time = activeAudio.time;
				handle.source.panStereo = activeAudio.pan;
				if ( activeAudio.paused )
					Pause(handle);
				else if ( clipInfo.targetVolume != clipInfo.defaultVolume )
					StartFadeIn(handle,0.1f); // if not already fading, fade the sound in
			}
		}

		if ( string.IsNullOrEmpty( data.m_musicCueName ) )
		{
			StopMusic(0.1f);
			m_musicVolOverride = -1;
		}
		else
		{
			AudioHandle handle = PlayMusic(data.m_musicCueName);
			if ( handle != null )
			{
				if ( data.m_musicVolOverride > 0 )
				{
					m_musicVolOverride = data.m_musicVolOverride;
					SetVolume(m_instance.m_activeMusic, m_musicVolOverride);	
				}
				if ( m_activeMusic != null )					
					m_activeMusic.time = data.m_musicTime % ((m_activeMusic.clip.length-0.01f)*0.9f); // There's a known unity error where starting from time errors. So don't set source time if it's close to the end (NB: This still may happen, need to watch for it)
				
				m_instance.StartFadeIn( m_instance.m_activeMusic, 0.1f );
			}
		}

		// Restart ambient sound
		if ( string.IsNullOrEmpty( data.m_ambientCueName ) )
		{
			StopAmbientSound(0.1f);
		}
		else 
		{
			PlayAmbientSound(data.m_ambientCueName);
		}

	}

	/// @endcond

	#endregion
	#region Private Functions

	void OnSceneLoaded( Scene scene, LoadSceneMode loadSceneMode )
	{
		AudioListener audioListener = System.Array.Find<AudioListener>( GameObject.FindObjectsOfType<AudioListener>(), item => item.enabled );
		if ( audioListener != null )
			m_audioListener = audioListener.transform;
		if ( m_audioListener == null )
		{
			if ( Debug.isDebugBuild ) Debug.Log("Unable to find audio listener in scene");
			return;
		}

		/* PowerQuest: Don't stop sound effects when loading scene
		// Stop all sound effects - Loop through in reverse since clips may be stopped, removing them from this list
		for (int i = m_activeAudio.Count - 1; i >= 0; i--)
		{
			ClipInfo info = m_activeAudio[i];
			if ( (info.type & (int)AudioCue.eAudioType.Music) == 0 )
			{
				// Not music, so end it. Give it a nice fade...
				Stop( info.handle, 0.1f);
			}
		}
		*/

	}


	void Awake() 
	{

		SceneManager.sceneLoaded += OnSceneLoaded;

		SetSingleton();
		if ( Application.isPlaying )
			Object.DontDestroyOnLoad(this);	

		if ( LAYER_NOPAUSE == -1 )
			LAYER_NOPAUSE = LayerMask.NameToLayer("NoPause");

		if ( Application.isPlaying )
		{
			for ( int i = 0; i < AUDIO_SOURCE_POOL_SIZE; ++i )
			{
				GameObject soundLoc = new GameObject(STRING_NAME_PREFIX);
				m_audioSources.Add( soundLoc.AddComponent<AudioSource>() );
				soundLoc.layer = LAYER_NOPAUSE;
				soundLoc.SetActive(false);
				soundLoc.transform.parent = transform;
			}
		}
		m_audioCues.RemoveAll(item=>item == null);


		if ( m_audioListener == null )
		{
			AudioListener listener = ((AudioListener)GameObject.FindObjectOfType(typeof(AudioListener)) );
			if ( listener != null ) 
				m_audioListener = listener.transform;
		}
		if ( m_audioListener == null )
			if ( Debug.isDebugBuild ) Debug.LogWarning("Unable to find audio listener in scene");


		m_activeMusic = null;

	}

	void OnApplicationFocus(bool hasFocus )
	{	    
		m_hasFocus = hasFocus;
	}

	// Update is called once per frame
	void Update() 
	{
		if ( m_audioListener == null )
		{
			AudioListener listener = ((AudioListener)GameObject.FindObjectOfType(typeof(AudioListener)) );
			if ( listener != null ) 
				m_audioListener = listener.transform;
		}

		transform.position = m_audioListener.position;
		UpdateActiveAudio();		

	}

	float GetFalloff( Vector2 soundPos )
	{
		if (m_cameraGame == null )			
			return 1.0f;
		float xStart = m_cameraGame.orthographicSize * m_cameraGame.aspect * m_falloffStart; // Falloff starts half a screen past the edge
		float xFalloff = (xStart * m_falloffEnd);
		return Mathf.Lerp( 1, m_falloffMinVol, Utils.EaseCubic((Mathf.Abs(soundPos.x - m_cameraGame.transform.position.x) - xStart) / xFalloff ) );

	}

	float GetPanPos( Vector2 soundPos )
	{
		if (m_cameraGame == null )	
			return 0.0f;
		float xStart = m_cameraGame.orthographicSize * m_cameraGame.aspect * m_falloffPanStart; // Falloff starts half a screen past the edge
		float xFalloff = (xStart * m_falloffPanEnd);
		float dist = soundPos.x - m_cameraGame.transform.position.x;
		xFalloff = Mathf.Lerp( 0, m_falloffPanMax, Utils.EaseCubic( (Mathf.Abs(dist) - xStart) / xFalloff ) );
		xFalloff *= Mathf.Sign(dist);
		return xFalloff;
	}

	void SetSource(ref AudioSource source, AudioClip clip, float volume, float pitch, int priority, Transform emmitter) 
	{
		source.spatialize = false;
		source.priority = priority;
		source.pitch = pitch;
		source.clip = clip;
		source.playOnAwake = false;

		if ( emmitter )
		{
			source.volume = volume * GetFalloff(emmitter.position);
			source.panStereo = GetPanPos(emmitter.position);	
		}
		else 
		{
			source.volume = volume;
			source.panStereo = 0;	
		}
	}	

	// Adds clip to active audio for updating
	void AddActiveAudio( ClipInfo info )
	{
		m_activeAudio.Add(info);

		// Update activeaudio for this clip immediately incase it starts playing before update loop
		UpdateActiveAudioClip(info);
	}

	// Updates all active audio
	void UpdateActiveAudio() 
	{ 
		// First check if the game has focus, since if not, the audio is potentially still active
		if ( m_hasFocus == false )
			return;

		if ( m_cameraGame == null )
			m_cameraGame = Camera.main;

		bool playingDialog = GetVolume(AudioCue.eAudioType.Dialog) > 0.5f // Only duck if dialog volume is turned up more than 50%
			&& m_activeAudio.Exists(item=> item != null && (item.type & (int)AudioCue.eAudioType.Dialog) != 0 );

		float finalDuckVol = 1;
		if ( playingDialog )
		{
			finalDuckVol = m_musicDuckingMaxVolume;
		}

		List<ClipInfo> toRemove = new List<ClipInfo>();	    
		foreach (ClipInfo audioClip in m_activeAudio) 
		{
			if (audioClip.handle == null || (audioClip.handle.isPlaying == false && audioClip.paused == false ) ) 
			{
				toRemove.Add(audioClip);
			} 
			else 
			{
				UpdateActiveAudioClip(audioClip, finalDuckVol);
			}
		}

		// Cleanup
		foreach (ClipInfo audioClip in toRemove) 
		{
			m_activeAudio.Remove(audioClip);
			if ( audioClip.handle != null )
				audioClip.handle.Stop();
		}

		#if !UNITY_2018_3_OR_NEWER
		// this dunnae really work, because the clip isn't destroyed ever because you can't use destroy with a timer in edit mode		
		if ( Application.isEditor && Application.isPlaying == false && m_activeAudio.Count <= 0  )
		{
			DestroyImmediate(gameObject);
		}
		#endif

	}

	// updates single active audio clip
	void UpdateActiveAudioClip( ClipInfo audioClip, float duckingVolume = 1.0f )
	{
		float volumeMod = 1.0f;		
		float panMod = 0;

		if ( audioClip.emmitter != null && audioClip.emmitter != transform )
		{
			//audioClip.position = audioClip.emmitter.position;
			volumeMod *= GetFalloff(audioClip.emmitter.position);	
			panMod += GetPanPos(audioClip.emmitter.position);
			audioClip.handle.panStereo = panMod;
		}	

		// Update Fading
		if ( Utils.ApproximatelyZero(audioClip.fadeDelta) == false && Mathf.Approximately(audioClip.targetVolume, audioClip.defaultVolume) == false )
		{
			audioClip.defaultVolume = Mathf.MoveTowards(audioClip.defaultVolume,audioClip.targetVolume, audioClip.fadeDelta*Time.deltaTime);
			// If flagged to stop on fadeout, stop the source
			if ( audioClip.stopAfterFade && Mathf.Approximately(audioClip.targetVolume, audioClip.defaultVolume) && audioClip.handle.isPlaying  )				
				audioClip.handle.source.Stop();
		}

		// Duck music
		if ( duckingVolume < 1.0f && (audioClip.type & (int)AudioCue.eAudioType.Music) != 0 && duckingVolume < audioClip.defaultVolume )
		{
			volumeMod = duckingVolume / audioClip.defaultVolume;
		}

		audioClip.handle.source.volume = audioClip.defaultVolume * volumeMod;
	}

	AudioSource SpawnAudioSource( string name, Vector2 position )
	{
		AudioSource source = null;
		for ( int i = 0; i < m_audioSources.Count; ++i )
		{
			source = m_audioSources[i];
			if ( source == null )
			{
				// source has been deleted, add new one in place
				GameObject soundLoc = new GameObject(STRING_NAME_PREFIX);			
				//Create the source
				source = soundLoc.AddComponent<AudioSource>();
				soundLoc.layer = LAYER_NOPAUSE;
				m_audioSources[i] = source;
				soundLoc.SetActive(false);
				soundLoc.transform.parent = transform;
				break;
			}
			else if ( source.gameObject.activeSelf == false )
			{
				break;
			}
			source = null;
		}

		if ( source == null )
		{
			// no pooled source found, add new source
			GameObject soundLoc = new GameObject(STRING_NAME_PREFIX);			
			//Create the source
			source = soundLoc.AddComponent<AudioSource>();
			soundLoc.layer = LAYER_NOPAUSE;
			m_audioSources.Add(source);
			soundLoc.SetActive(false);
			soundLoc.transform.parent = transform;
		}
		if ( source != null )
		{			
			if ( source.isPlaying )
				Debug.Log("Reusing audio Source that's playing: "+source.clip.name);
			source.gameObject.SetActive(true);
			if ( Debug.isDebugBuild )
				source.gameObject.name = name;
			source.gameObject.transform.position = position;
		}
		else 
		{
			if ( Debug.isDebugBuild ) Debug.Log("Failed to spawn audio source");
		}
		return source;

	}

	void AddSourceFilters(GameObject source)
	{

		{
			AudioLowPassFilter filter = source.AddComponent<AudioLowPassFilter>();
			filter.enabled = false;
		}
		{
			AudioHighPassFilter filter = source.AddComponent<AudioHighPassFilter>();
			filter.enabled = false;
		}
		{
			AudioEchoFilter filter = source.AddComponent<AudioEchoFilter>();
			filter.enabled = false;
		}
		{
			AudioChorusFilter filter = source.AddComponent<AudioChorusFilter>();
			filter.enabled = false;
		}
		{
			AudioDistortionFilter filter = source.AddComponent<AudioDistortionFilter>();
			filter.enabled = false;
		}
		{
			AudioReverbFilter filter = source.AddComponent<AudioReverbFilter>();
			filter.enabled = false;
		}
	}

	#endregion

}

#region Class: AudioHandle
/// Audio handle- Use for changing a sound that's currently playing
/**
 * - All the Play(...) functions in the audio system return an AudioHandle. You can store and use this to manipluate the clip that's playing. Especially useful for looping sounds (at least, if you ever want to stop them)
 * - It wraps around unity's "AudioSource", but gives access to it so you can still use whatever functionality unity provides with it's AudioSources
 * - See Unity's docs on AudioSource for more specific info
 */
public class AudioHandle
{
	public AudioHandle(AudioSource source) { m_source = source; }

	static bool IsPlaying( AudioHandle handle ) { return handle != null && handle.isPlaying == false; }
	static bool IsNullOrStopped( AudioHandle handle ) { return handle == null || handle.isPlaying == false; }

	public static implicit operator AudioSource( AudioHandle handle ) { return handle == null ? null : handle.m_source; }

	/// Retrieves the unity AudioSource this handle is using
	public AudioSource source { get { return m_source; } }
	/// Is the clip playing right now? (Read Only). Note: will return false when AudioSource.Pause is called.
	public bool isPlaying { get { return m_source != null && m_source.isPlaying; } }
	/// The base volume of clip (0.0 to 1.0) Before falloff, ducking, etc is applied. To get the final volume, use source.volume. 
	public float volume { get { return m_source == null ? 0 : SystemAudio.Get.GetVolume(this); } set { if ( m_source != null ) SystemAudio.Get.SetVolume(this,value); } }
	/// The pitch of the audio source. See Unity docs for more info.
	public float pitch { get { return m_source == null ? 0 : m_source.pitch; } set { if ( m_source != null ) m_source.pitch = value; } }
	/// Pans a playing sound in a stereo way (left or right). See Unity docs for more info.
	public float panStereo { get { return m_source == null ? 0 : m_source.panStereo; } set { if ( m_source != null ) m_source.panStereo = value; } }
	/// Playback position in seconds. See Unity docs for more info
	public float time { get { return m_source == null ? 0 : m_source.time; } set { if ( m_source != null ) m_source.time = Mathf.Min(value, (m_source.clip.length-0.01f)*0.9f); } } // Hack, cause sounds won't play if time gets set too close to end
	/// Is the audio clip looping 
	public bool loop { get { return m_source == null ? false : m_source.loop; } }
	/// The audio clip that is playing
	public AudioClip clip { get { return m_source == null ? null : m_source.clip; } }
	/// Pauses playing the clip
	public void Pause() { SystemAudio.Pause(this); }
	/// Resumes playing the clip after pausing
	public void UnPause() { SystemAudio.UnPause(this); }
	/// Stops the clip, optionally fading out over time)
	public void Stop(float overTime = 0.0f) 
	{ 		
		if ( SystemAudio.GetValid() && isPlaying && overTime > 0.0f )
		{
			SystemAudio.Stop(this, overTime);  
			return;
		}

		if ( m_source != null )
		{
			m_source.gameObject.SetActive(false);
			m_source = null;
		}

		// Call through to SystemAudio to ensure removed from list
		SystemAudio.Stop(this);  
	}

	/// Fades in the sound from zero, call directly after playing the sound to fade it in. eg `Audio.Play("FireCrackling").FadeIn(1);`
	public AudioHandle FadeIn(float overTime)
	{
		SystemAudio.Get.StartFadeIn(this,overTime);
		return this;
	}

	/// Fades the volume from it's current volume to another over the specified length of time
	public AudioHandle Fade(float targetVolume, float overTime)
	{
		SystemAudio.Get.StartFade(this,targetVolume,overTime);
		return this;
	}


	// The audio source
	AudioSource m_source = null;
}
#endregion

}
