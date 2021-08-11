using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using System;

namespace PowerTools.Quest
{


public static class QuestUtils
{

	//
	// Reflection doodads
	//

	static readonly BindingFlags BINDING_FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;	// NB: I added 'declared only' not sure if it'll break anything yet!

	// Copies properties and variables from one class to another
	public static void CopyFields<T>(T to, T from)
	{
		System.Type type = to.GetType();
		if (type != from.GetType()) return; // type mis-match

		FieldInfo[] finfos = type.GetFields(BINDING_FLAGS);
		
		foreach (var finfo in finfos) 
		{
			finfo.SetValue(to, finfo.GetValue(from));
		}
	}

	// Used to copy data when assembly has changed due to hotloading a script
	public static void CopyHotLoadFields<T>(T to, T from)
	{
		System.Type toType = to.GetType();

		// Match fields by name, since types are potentially different due to assembly change. this means that if type changes we'll assert
		FieldInfo[] finfos = toType.GetFields(BINDING_FLAGS);
		FieldInfo[] finfosFrom = from.GetType().GetFields(BINDING_FLAGS);
		foreach (FieldInfo finfo in finfos) 
		{
			var finfoFrom = System.Array.Find(finfosFrom, item=>item.Name == finfo.Name);
			if ( finfoFrom != null )
			{	
				System.Type fieldType = finfo.ReflectedType;
				try
				{	
					object fromValue = finfoFrom.GetValue(from);
					if ( fromValue is System.Enum ) // Enums could be in a different assembly, so the type won't match, but can just cast from int to handle most cases.
						finfo.SetValue( to, (int)fromValue );
					else
						finfo.SetValue( to, fromValue );

				}
				catch (System.Exception e )
				{
					// Doesn't matter so m uch if we can't copy some data for hotswap
					Debug.LogWarning(e.ToString()); 
				}
			}
		}
	}

	// Copies variables from a newly instantiated version to the passed in class
	public static void InitWithDefaults<T>( T toInit ) where T : class
	{

		/*
			Ok, doing some crazy stuff here, so bear with me-
				If a variable was added since the game was saved and we want to load it back, the varibale won't be set (it's not in the save file). But it won't even have it's default variable either.
				This function gets called before the class is deserialised, so I'm constructing a fresh instance of this class, and copying the defaults from that.
				Then when it's deserialised, the missing/ignored values will still have been set up.

			Note that it doesn't work with monobehaviours since they need to be created by unity, (not new'd)
		*/

		T newInstance = System.Activator.CreateInstance(toInit.GetType()) as T;
		if ( newInstance != null )
		{
			// Now shallow copy everything from the newInstance, to toInit
			CopyFields(toInit,newInstance);
		}

	}

	public static void HotSwapScript<T>( ref T toSwap, string name, Assembly assembly ) where T : class
	{
		if (toSwap == null)
			return;			
		T old = toSwap;
		toSwap = QuestUtils.ConstructByName<T>(name, assembly);
		CopyHotLoadFields(toSwap, old);
	}

	// Instantiates and returns a class by it's name, returning null if it wasn't found. Returns as the templated type (eg. base type of class you're instantiating)
	public static T ConstructByName<T>(string name) where T : class
	{		
		T result = null;
		try 
		{	
			#if UNITY_2018_1_OR_NEWER // Added for .NET 2.0 core support
				System.Type type = System.Type.GetType( string.Format("{0}, {1}", name,  typeof(PowerQuest).Assembly.FullName ));
				result = type.GetConstructor(new System.Type[]{}).Invoke(new object[]{}) as T;
			#else 
				System.Runtime.Remoting.ObjectHandle handle = System.Activator.CreateInstance("Assembly-CSharp", name, new object[0]);
				if ( handle != null )
				{
					result = handle.Unwrap() as T;
				}
			#endif
		} 
		catch
		{
			// Assume that this just means the class doesn't exist, which is fine, we'll just return null.
		}
		return result;
	}
	// Instantiates and returns a class by it's name, returning null if it wasn't found. Returns as the templated type (eg. base type of class you're instantiating)
	public static T ConstructByName<T>(string name, Assembly assembly) where T : class
	{
		T result = null;
		try 
		{	
			#if UNITY_2018_1_OR_NEWER // Added for .NET 2.0 core support
				System.Type type = System.Type.GetType( string.Format("{0}, {1}", name, assembly.FullName ));
				result = type.GetConstructor(new System.Type[]{}).Invoke(new object[]{}) as T;
			#else 
				System.Runtime.Remoting.ObjectHandle handle = System.Activator.CreateInstance(assembly.GetName().ToString(), name);//,name, new object[0])
				if ( handle != null ) 
				{
					result = handle.Unwrap() as T;
				}
			#endif
		} 
		catch
		{
			// Assume that this just means the class doesn't exist, which is fine, we'll just return null.
		}
		return result;
	}

	// Diagnostic code
	
	#if UNITY_EDITOR || DEVELOPMENT_BUILD
	static System.Diagnostics.Stopwatch s_stopwatch = new System.Diagnostics.Stopwatch();
	#endif
	public static void StopwatchStart() 
	{ 
		#if UNITY_EDITOR || DEVELOPMENT_BUILD
		s_stopwatch.Start(); 
		#endif
	}
	public static void StopwatchStop(string logTxt) 
	{ 
		#if UNITY_EDITOR || DEVELOPMENT_BUILD
		// Get the elapsed time as a TimeSpan value.
		TimeSpan ts = s_stopwatch.Elapsed;

		// Format and display the TimeSpan value.
		string elapsedTime = String.Format("{0:00}:{1:000}",
			ts.Seconds,
			ts.Milliseconds);
		Debug.Log(logTxt + elapsedTime);		
		s_stopwatch.Reset();
		#endif
	}

}

// Attribute used for including global enums in autocomplete
[AttributeUsage(AttributeTargets.All)]
public class QuestAutoCompletableAttribute : System.Attribute
{
	public QuestAutoCompletableAttribute(){}
}

// Attribute used for adding functions to debug startup fucntions
[System.AttributeUsage( System.AttributeTargets.Method )]
public class QuestPlayFromFunctionAttribute : System.Attribute
{
	public QuestPlayFromFunctionAttribute(){}
}

// Attribute used to mark string fields that should be added to the text system to support localization. Eg: [QuestLocalize, SerializeField] string m_description;
[AttributeUsage(AttributeTargets.Field)]
public class QuestLocalizeAttribute : System.Attribute
{
	public QuestLocalizeAttribute(){}
}

}
