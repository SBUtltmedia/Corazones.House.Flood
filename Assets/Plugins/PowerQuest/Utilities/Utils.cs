using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace PowerTools.Quest
{


public class Utils
{
	// Normalizes the vector and returns the magnitude
	public static float NormalizeMag( ref Vector2 vector )
	{
		if ( ApproximatelyZero(vector.x, float.Epsilon) )
		{
			if ( ApproximatelyZero(vector.y,float.Epsilon) )
			{
				vector = Vector2.zero;
				return 0.0f;
			}
			else 
			{				
				float mag = Mathf.Abs(vector.y);
				vector.Set(0, Mathf.Sign(vector.y));
				return mag;
			}
		}
		else if ( ApproximatelyZero(vector.y, float.Epsilon) )
		{
			float mag = Mathf.Abs(vector.x);
			vector.Set(Mathf.Sign(vector.x),0);
			return mag;
		}
		else 
		{	
			float mag = vector.magnitude;
			if ( mag == 0 ) // Maybe could happen - check to avoid NaN
				vector = Vector2.zero;
			else 		
				vector /= mag;
				
			return mag;
		}
	}
	
	// Returns the Vector2 rotated 90 counterclockwise
	public static Vector2 GetVector2Tangent( Vector2 vector )
	{				
		return new Vector2(-vector.y, vector.x);
	}
	
	// Returns the Vector2 rotated 90 counterclockwise
	public static Vector2 GetVector2TangentR( Vector2 vector )
	{				
		return new Vector2(vector.y, -vector.x);
	}
		
 	// Snap to a grid value
	public static Vector3 Snap( Vector3 pos, float snapTo = 1 )
	{	
		return new Vector3( Snap(pos.x, snapTo), Snap(pos.y, snapTo), Snap(pos.z, snapTo) );
	}
	
	public static Vector2 Snap( Vector2 pos, float snapTo = 1 )
	{	
		return new Vector2( Snap(pos.x, snapTo), Snap(pos.y, snapTo) );
	}
	
	public static float Snap( float pos, float snapTo = 1)
	{		
		if ( snapTo < 0.001f )
			return pos;
        return Mathf.Floor(pos / snapTo) * snapTo; // Changed this to floor... will this break something?? Everything??
	}

	public static float Flip( float value, bool flip )
	{
	    return flip ? -value : value;
	}

	public static bool Approximately(float a, float b, float epsilon)
	{
		return (a > b) ? (a < b + epsilon) : a > b - epsilon;
	}
	
	public static bool ApproximatelyZero( float a, float epsilon )
	{	
		return ( a > 0 ) ? (a < epsilon) : a > -epsilon;
	}

	public static bool ApproximatelyZero( float a )
	{	
		return ( a > 0 ) ? (a < Mathf.Epsilon) : a > -Mathf.Epsilon;
	}
	
	public static bool IsInLayerMask(GameObject obj, LayerMask mask)
	{
       return ((mask.value & (1 << obj.layer)) > 0);
    }
	
	public static float EaseCubic( float ratio )
	{
		ratio = Mathf.Clamp01(ratio);
		return (-2.0f*ratio*ratio*ratio + 3.0f*ratio*ratio);
	}
	
	public static float EaseInCubic( float ratio )
	{
		ratio = Mathf.Clamp01(ratio);
		ratio *= 0.5f;
		return (-2.0f*ratio*ratio*ratio + 3.0f*ratio*ratio) * 2.0f;
	}

	public static float EaseOutCubic( float ratio )
	{
		ratio = Mathf.Clamp01(ratio);
		ratio = ratio * 0.5f + 0.5f;
		return (-2.0f*ratio*ratio*ratio + 3.0f*ratio*ratio) * 2.0f - 1.0f;
	}
	
	public static float EaseCubic( float start, float end, float ratio )
	{
		return start + ((end-start) * EaseCubic(ratio));
	}
	
	public static float Interpolate(float from, float to, float min, float max, float val)
	{
		float denom = max-min;
		if ( denom == 0 )
			return from;
		
		return Mathf.Lerp( from, to, 
				Mathf.Clamp01( (val-min)/ (denom) ) );
	}
		
	public static float Loop( float val, float min, float max )
	{
		while ( val < min ) val += (max-min);
		while ( val > max ) val -= (max-min);
		return val;
	}
	
	public static Quaternion GetDirectionRotation( Vector2 direction )
	{		
		if ( ApproximatelyZero( direction.y, float.Epsilon ) == false ) // When too close to zero, FromToRotation doesn't work
		{
			Quaternion result = Quaternion.FromToRotation( Vector3.right, direction );		
			if ( Approximately( result.z, 1.0f, float.Epsilon ) )
				return result;
		}
		
		return Quaternion.Euler(0,0,Mathf.Rad2Deg * Mathf.Atan2(direction.y, direction.x));
		//Quaternion result = Quaternion.identity;
		//result.eulerAngles = new Vector3(0,0, Mathf.Rad2Deg * Mathf.Atan2(direction.y, direction.x));
		//return result;
	}

	// Returns angle normalized 2d direction vector in degrees between 0 and 360
	public static float GetDirectionAngle( Vector2 directionNormalised )
	{		
		if ( Utils.ApproximatelyZero( directionNormalised.y ) )
		{
			if ( directionNormalised.x < 0 )
				return 180.0f;
			else 
				return 0;
		}
		else if ( Utils.ApproximatelyZero( directionNormalised.x ) )
		{
			if ( directionNormalised.y < 0 )
				return 270.0f;
			else 
				return 90.0f;
		}
		
		return Mathf.Repeat( Mathf.Rad2Deg * Mathf.Atan2(directionNormalised.y, directionNormalised.x), 360 );
	}
		
	public static void Swap<T>(ref T lhs, ref T rhs)
	{
	    T temp;
	    temp = lhs;
	    lhs = rhs;
	    rhs = temp;
	}
	
	public static float ClampAngle( float angle, float min, float max )
	{
		// Rotate so 0degrees is opposite to the center (makes flipping left/right work)
							
		float offsetNeg180 = (180.0f - ((min + max)*0.5f));
		
		min = Mathf.Repeat(min+offsetNeg180, 360.0f);
		max = Mathf.Repeat(max+offsetNeg180, 360.0f);
		angle = Mathf.Repeat(angle+offsetNeg180, 360.0f);
		
		return Mathf.Clamp( angle, min, max) - offsetNeg180;
					
	}
	
	public static bool IsWithinAngle( float angle, float min, float max )
	{
		angle = Mathf.Repeat( angle-min, 360 );
		max = Mathf.Repeat( max-min, 360 );		
		return angle >= 0 && angle < max;		
	}
	
	public static bool IsPointInPolygon( Vector2 point, List<Vector2> polygon )
	{
		bool intersects = false;
		
		// From 
		int nvert = polygon.Count;
		int i,j;
		for ( i = 0, j = nvert-1; i < nvert; j = i++) 
		{			
			if ( ( (polygon[i].y > point.y) != (polygon[j].y > point.y) ) 
				&& (point.x < (polygon[j].x-polygon[i].x) * (point.y-polygon[i].y) / (polygon[j].y-polygon[i].y) + polygon[i].x) )
			{
				intersects = !intersects;
			}
		}
		
		return intersects;
	}
	
	// Returns seconds since unix epoch
	public static int GetUnixTimestamp()
	{
		var epochStart = new System.DateTime(1970, 1, 1, 8, 0, 0, System.DateTimeKind.Utc);
		return (int)(System.DateTime.UtcNow - epochStart).TotalSeconds;
	}
	
	
	public static bool GetTimeIncrementPassed( float time )
	{	
		// Time has ticked over if the remainder of previous time is higher than the current (ie. time has gone backwards)
		return ( ((Time.timeSinceLevelLoad - Time.deltaTime) % time) > (Time.timeSinceLevelLoad % time) );
	}

	// Returns true when random time between min and max has passed. the ref time variable stores the period for the current increment
	public static bool GetTimeIncrementPassed( float min, float max, ref float period)
	{
			if ( period <= 0 )
				period = Random.Range(min,max);
			period -= Time.deltaTime;
			if ( period <= 0 )
			{
				period = Random.Range(min,max);
				return true;
			}
			return false;

	}

	
	/*
	public static void ShuffleList<T>( ref List<T> list )
	{
		T temp;
		int j = 0;
		int count = list.Count;
		for ( int i = count - 1; i >= 1; --i )
		{
			j = Random.Range(0,i+1);
			// swap
			temp = list[i];
			list[i] = list[j];
			list[j] = temp;
		}
	}
	
	public static List<T> ShuffleListCopy<T>( List<T> list )
	{
		int count = list.Count;
		List<T> result = new List<T>(count);
		result[0] = list[0];
		
		int j = 0;
		for ( int i = 1; i < count; ++i )
		{
			j = Random.Range (0,i+1);
			if ( j != i )
			{
				result[i] = result[j];
			}
			result[j] = list[i];
		}
		return result;
	}
	
	public static void ShuffleArray<T>( ref T[] list )
	{
		T temp;
		int j = 0;
		int count = list.Length;
		for ( int i = count - 1; i >= 1; --i )
		{
			j = Random.Range(0,i+1);
			// swap
			temp = list[i];
			list[i] = list[j];
			list[j] = temp;
		}
	}
	
	public static T[] ShuffleArrayCopy<T>( T[] list )
	{
		int count = list.Length;
		T[] result = new T[count];
		result[0] = list[0];
		
		int j = 0;
		for ( int i = 1; i < count; ++i )
		{
			j = Random.Range (0,i+1);
			if ( j != i )
			{
				result[i] = result[j];
			}
			result[j] = list[i];
		}
		return result;
	}*/
	

	
	public static T[] CreateFilledArray<T>( int size, T value )
	{
		T[] arr = new T[size];
		for ( int i = 0; i < arr.Length; ++i ) 
		{
			arr[i] = value;
		}
		return arr;
	}
	
	static readonly float ASPECT_16_9 = 16.0f/9.0f;
	static readonly float ASPECT_16_9_INV = 1.0f/ASPECT_16_9;
	
	// Converts a ratio in screen coords that was assuming 16:9 so it's the equivalent in 4:3, etc.
	// Useful if you want ghosts to not go outside the left 10% of screen, and have good values that work in 1080p
	public static float NormalizeScreenRatioTo1080X( Camera camera, float ratio )
	{	
		if ( camera == null )
			return ratio;
		return ratio * (camera.aspect * ASPECT_16_9_INV);
	}
	
	public static float NormalizeScreenRatioTo1080Y( Camera camera, float ratio )
	{	
		if ( camera == null )
			return ratio;
		return ratio * ((1.0f/camera.aspect) * ASPECT_16_9);
	}

	public static int 	MaskSetAt(int mask, int index, bool value) { return ( value ) ? (mask | (1 << index)) : (mask & ~(1 << index));  }
	public static int 	MaskSetAt(int mask, int index) { return mask | (1 << index); }
	public static int 	MaskUnsetAt(int mask, int index) { return mask & ~(1 << index); }
	public static bool 	MaskIsSet(int mask, int index) { return (mask & 1 << index) != 0; }

	public static tEnum ToEnum<tEnum>(string str) where tEnum : struct, System.IConvertible
	{		
		if ( System.Enum.TryParse(str, true, out tEnum result) )
			return result;
		// Couldn't parse it, show a warning
		if ( Debug.isDebugBuild ) Debug.LogWarning ("Failed to parse enum "+str+" from " + typeof(tEnum).ToString());
		return default(tEnum);
	}
	
	// Random direction vector within the two angles specified in degrees
	public static Vector2 RandomDirection(float minAngle = 0, float maxAngle = 360)
	{
		float theta = Random.Range(minAngle,maxAngle) * Mathf.Deg2Rad;
		return new Vector2(Mathf.Cos(theta),Mathf.Sin(theta));
	}
	
	/// Random point in a circle
	public static Vector2 RandomPointInCircle( float radius )
	{
		// Explanation: https://stackoverflow.com/questions/5837572/generate-a-random-point-within-a-circle-uniformly/5837686#5837686
		radius = Mathf.Sqrt(Random.value) * radius;
		float theta = Random.value*Mathf.PI*2.0f;		
		return new Vector2(radius*Mathf.Cos(theta),radius*Mathf.Sin(theta));
	}
	
	/// Random point in circle with options to have a minimum radius, and only a segment of an angle. NB: No error checking so garbage in = garbage out.
	public static Vector2 RandomPointInCircle( float minRadius, float maxRadius, float minAngle = 0, float maxAngle = 360 )
	{
		// Explanation: https://stackoverflow.com/questions/5837572/generate-a-random-point-within-a-circle-uniformly/5837686#5837686
		float r = Mathf.Sqrt(Random.Range(Mathf.Pow(minRadius/maxRadius,2),1))*maxRadius;
		float theta = Random.Range(minAngle,maxAngle) * Mathf.Deg2Rad;
		return new Vector2(r*Mathf.Cos(theta),r*Mathf.Sin(theta));
	}
}

public static class ExtentionMethods
{	
	/*	
	public static tEnum ToEnum<tEnum>(this string str) where tEnum : struct, System.IConvertible
	{		
		if ( System.Enum.TryParse(str, true, out tEnum result) )
			return result;
		// Couldn't parse it, show a warning
		if ( Debug.isDebugBuild ) Debug.LogWarning ("Failed to parse enum "+str+" from " + typeof(tEnum).ToString());
		return default(tEnum);
	}	
	public static bool IsEnum<tEnum>(this string str, tEnum toCompare) where tEnum : struct, System.IConvertible
	{		
		return str.ToEnum<tEnum>().Equals(toCompare);
	}
	*/

	

	public static Rect Encapsulate( this Rect rect, Rect other )
	{
		return Rect.MinMaxRect( Mathf.Min(rect.xMin, other.xMin), Mathf.Min(rect.yMin, other.yMin),
			 Mathf.Max(rect.xMax, other.xMax), Mathf.Max(rect.yMax, other.yMax) );
	}

	/// Returns distance the point would have to move to be inside the rect (or 0 if inside already)
	public static Vector2 CalcDistToPoint( this Rect rect, Vector2 point )
	{
		if ( rect.Contains(point) )
			return Vector2.zero;
		Vector2 result = Vector2.zero;
		if ( point.x < rect.xMin )
			result.x = rect.xMin - point.x;
		if ( point.x > rect.xMax )
			result.x = rect.xMax - point.x;
		
		if ( point.y < rect.yMin )
			result.y = rect.yMin - point.y;
		if ( point.y > rect.yMax )
			result.y = rect.yMax - point.y;

		return result;
	}

	public static Vector2 WithOffset( this Vector2 vector, float x, float y )
	{
		return new Vector2(vector.x+x, vector.y+y);
	}
	

	public static Vector2 Scaled( this Vector2 vector, Vector2 scale )
	{
		return Vector2.Scale(vector,  scale);
	}
	
	// Returns the Vector3 from vector2 and z
	public static Vector2 WithX( this Vector2 vector, float x )
	{				
		return new Vector2(x, vector.y);
	}
	public static Vector2 WithY( this Vector2 vector, float y )
	{				
		return new Vector2(vector.x, y);
	}
	public static Vector3 WithZ( this Vector2 vector, float z )
	{				
		return new Vector3(vector.x, vector.y, z);
	}
	public static Vector3 WithX( this Vector3 vector, float x )
	{				
		return new Vector3(x, vector.y, vector.z);
	}
	public static Vector3 WithY( this Vector3 vector, float y )
	{				
		return new Vector3(vector.x, y, vector.z);
	}
	public static Vector3 WithXY( this Vector3 vector, float x, float y )
	{				
		return new Vector3(x, y, vector.z);
	}
	public static Vector3 WithZ( this Vector3 vector, float z )
	{				
		return new Vector3(vector.x, vector.y, z);
	}

	public static Vector2 WithFlippedX( this Vector2 vector)
	{				
		return new Vector2(-vector.x, vector.y);
	}
	public static Vector2 WithFlippedY( this Vector2 vector )
	{				
		return new Vector2(vector.x, -vector.y);
	}
	public static Vector3 WithFlippedX( this Vector3 vector)
	{				
		return new Vector3(-vector.x, vector.y, vector.z);
	}
	public static Vector3 WithFlippedY( this Vector3 vector )
	{				
		return new Vector3(vector.x, -vector.y, vector.z);
	}

	public static Vector2 Clamp( this Vector2 vector, Vector2 min, Vector2 max )
	{
		vector.x = Mathf.Clamp(vector.x,min.x,max.x);
		vector.y = Mathf.Clamp(vector.y,min.y,max.y);
		return vector;
	}
	public static Vector3 Clamp(this Vector3 vector, Vector3 min, Vector3 max )
	{
		vector.x = Mathf.Clamp(vector.x,min.x,max.x);
		vector.y = Mathf.Clamp(vector.y,min.y,max.y);
		vector.z = Mathf.Clamp(vector.z,min.z,max.z);
		return vector;
	}
	public static Vector2 Clamp01(this Vector2 vector)
	{
		vector.x = Mathf.Clamp01(vector.x);
		vector.y = Mathf.Clamp01(vector.y);
		return vector;
	}
	public static Vector3 Clamp01(this Vector3 vector )
	{
		vector.x = Mathf.Clamp01(vector.x);
		vector.y = Mathf.Clamp01(vector.y);
		vector.z = Mathf.Clamp01(vector.z);
		return vector;
	}

	// Returns the Vector2 rotated 90 counterclockwise
	public static Vector2 GetTangent( this Vector2 vector )
	{				
		return new Vector2(-vector.y, vector.x);
	}
	
	// Returns the Vector2 rotated 90 counterclockwise
	public static Vector2 GetTangentR( this Vector2 vector )
	{				
		return new Vector2(vector.y, -vector.x);
	}
		
 	// Snap to a grid value
	public static Vector3 Snap( this Vector3 pos, float snapTo )
	{	
		return new Vector3( Utils.Snap(pos.x, snapTo), Utils.Snap(pos.y, snapTo), Utils.Snap(pos.z, snapTo) );
	}	
	
	public static Vector2 Snap( this Vector2 pos, float snapTo )
	{	
		return new Vector2( Utils.Snap(pos.x, snapTo), Utils.Snap(pos.y, snapTo) );
	}


 	// Checks if two vectors are approximately equal
	public static bool ApproximatelyEquals( this Vector3 pos, Vector3 other )
	{	
		return (pos-other).sqrMagnitude < float.Epsilon;
	}	
	
	public static bool ApproximatelyEquals( this Vector2 pos, Vector2 other )
	{	
		return (pos-other).sqrMagnitude < float.Epsilon;
	}

	public static bool IsInLayerMask(this GameObject obj, LayerMask mask)
	{
       return ((mask.value & (1 << obj.layer)) > 0);
    }
		
	public static Quaternion GetDirectionRotation( this Vector2 direction )
	{		
		if ( direction.y != 0.0f )
			return Quaternion.FromToRotation( Vector3.right, direction );		
		
		Quaternion result = Quaternion.identity;
		result.eulerAngles = new Vector3(0,0, Mathf.Rad2Deg * Mathf.Atan2(direction.y, direction.x));
		return result;
	}
		
	public static float GetDirectionAngle( this Vector2 directionNormalised )
	{		
		if ( Utils.ApproximatelyZero( directionNormalised.y ) )
		{
			if ( directionNormalised.x < 0 )
				return 180.0f;
			else 
				return 0;
		}
		else if ( Utils.ApproximatelyZero( directionNormalised.x ) )
		{
			if ( directionNormalised.y < 0 )
				return 270.0f;
			else 
				return 90.0f;
		}
		
		return Mathf.Repeat( Mathf.Rad2Deg * Mathf.Atan2(directionNormalised.y, directionNormalised.x), 360 );
	}
	
	public static T GetComponentInParents<T>(this GameObject gameObject) where T : Component
	{
		for(Transform t = gameObject.transform; t != null; t = t.parent)
		{
			T result = t.GetComponent<T>();
			if(result != null)
				return result;
		}
		
		return null;
	}
	
	// Removes elements from a list that are null (if nullable) or zero if a float or something.
	public static void RemoveDefaultElements<T>( this List<T> list ) 
	{
		int count = list.Count;
		for ( int i = count-1; i >= 0; i-- )
		{
			if ( object.Equals(list[i], default(T) ) )
			{
				list.RemoveAt(i);
			}
		}
	}

	public static T LastOrDefault<T>( this IList<T> list ) { return ( list == null || list.Count == 0 ) ? default(T) : list[list.Count-1]; }
	public static T FirstOrDefault<T>( this IList<T> list ) { return ( list == null || list.Count == 0 ) ? default(T) : list[0]; }
	public static T ElementAtOrDefault<T>( this IList<T> list, int index ) { return ( list == null || index < 0 || index < list.Count) ? default(T) : list[index]; }

	public static T LastOrDefault<T>( this T[] list ) { return ( list == null || list.Length == 0 ) ? default(T) : list[list.Length-1]; }
	public static T FirstOrDefault<T>( this T[] list ) { return ( list == null || list.Length == 0 ) ? default(T) : list[0]; }
	public static T ElementAtOrDefault<T>( this T[] list, int index ) { return ( list == null || index < 0 || index < list.Length) ? default(T) : list[index]; }
	
	// Swaps elements within a list
	public static List<T> Swap<T>(this List<T> list, int indexA, int indexB)
	{
		T tmp = list[indexA];
		list[indexA] = list[indexB];
		list[indexB] = tmp;
		return list;
	}

	// Shuffles the list
	public static void Shuffle<T>( this IList<T> list )
	{
		T temp;
		int j = 0;
		int count = list.Count;
		for ( int i = count - 1; i >= 1; --i )
		{
			j = Random.Range(0,i+1);
			// swap
			temp = list[i];
			list[i] = list[j];
			list[j] = temp;
		}
	}
	
	// Returns a copy of the shuffled list
	public static List<T> ShuffleListCopy<T>( this List<T> list )
	{
		int count = list.Count;
		List<T> result = new List<T>(list);
		//result.AddRange(list);
		
		int j = 0;
		for ( int i = 1; i < count; ++i )
		{
			j = Random.Range (0,i+1);
			if ( j != i )
			{
				result[i] = result[j];
			}
			result[j] = list[i];
		}
		return result;
	}
	
	// Shuffles the array
	public static void Shuffle<T>( this T[] list )
	{
		T temp;
		int j = 0;
		int count = list.Length;
		for ( int i = count - 1; i >= 1; --i )
		{
			j = Random.Range(0,i+1);
			// swap
			temp = list[i];
			list[i] = list[j];
			list[j] = temp;
		}
	}
	
	// Returns a copy of the shuffled array
	public static T[] ShuffleCopy<T>( this T[] list )
	{
		int count = list.Length;
		T[] result = new T[count];
		result[0] = list[0];
		
		int j = 0;
		for ( int i = 1; i < count; ++i )
		{
			j = Random.Range (0,i+1);
			if ( j != i )
			{
				result[i] = result[j];
			}
			result[j] = list[i];
		}
		return result;
	}

	public static T Choose<T>(this IList<T> values, System.Func<T,float> getWeight)
	{
		float sum = 0;
		for ( int i = 0; i < values.Count; ++i )
			sum += getWeight(values[i]);
		float rand = Random.value*sum;
		for ( int i = 0; i < values.Count; ++i )
		{
			rand -= getWeight(values[i]);
			if ( rand < 0 )
				return values[i];			
		}
		return values[values.Count - 1];
	}
	
	/*
	public static int BitSetAt(this int val, int index ) { val |= 1 << index; return val;}	
	public static int BitUnsetAt(this int val, int index) { val &= ~(1 << index); return val;}
	public static bool BitIsSet(this int val, int index) { return (val & 1 << index) != 0; }	
	
	public static int BitSetAt( this int val,  params int[] bitsSet )
	{
		for( int i = 0; i< bitsSet.Length; ++i )
		{
			val |= 1 <<  bitsSet[i];
		}
		return val;
	}*/
	/*
	public int Value { get{ return m_value; } }
	public void SetAt(int index) { m_value |= 1 << index; }
	public void UnsetAt(int index) { m_value |= ~(1 << index); }
	public bool IsSet(int index) { return (m_value & 1 << index) != 0; }
	*/
	
	public static Color WithAlpha(this Color col, float alpha )
	{
		return new Color(col.r,col.g,col.b,alpha);
	}
	
	public static bool IsIndexValid<T>( this List<T> list, int index ) { return ( index >= 0 && index < list.Count ); }
	
	public static bool IsIndexValid<T>( this T[] list, int index ) { return ( index >= 0 && index < list.Length ); }	

	public static T[] Populate<T>(this T[] arr, T value )
	{
		for ( int i = 0; i < arr.Length; ++i ) {
			arr[i] = value;
		}
		return arr;
	}
}

/// <summary>
/// Shuffled index.
/// Usage: 
///  - Instantiate the shuffled index with desired count. 
///  - Cast to "int" to get the value (eg. (int)shuffledIndex), increment with shuffledIndex++);
///  - Increment with ++ operator to get next shuffled value (unless you pass autoIncrement as TRUE)
/// </summary>
public class ShuffledIndex
{
	static Dictionary<int, ShuffledIndex> s_premadeShuffledIndexes = new Dictionary<int, ShuffledIndex>();

    int m_current = -2; // -2 so if next() is called first, it'll still reshuffle the first time because the index will still be invalid
	int[] m_ids = null;
	
	public ShuffledIndex(int count)
	{
		m_ids = new int[count];
		for (int i = 0; i < count; ++i )
		{
			m_ids[i] = i;
		}
	}

    /// Returns shuffled index with max range (inclusive). This is not a unique list, any callers will have the same. Useful when just want to shuffle something that's happening a few times in a row without creating a list first
	public static int Random(int max)
	{
		int count = max+1; // increment so that it represents "count" rather than "max"
		ShuffledIndex shuf = null;
		if ( s_premadeShuffledIndexes.TryGetValue(count, out shuf) == false )
		{
			shuf = new ShuffledIndex(count);
			s_premadeShuffledIndexes.Add(count,shuf);
		}
		return shuf.Next();
	}
	
	public int Next()
	{
		m_current++;
		return (int)this;
	}
	
	public static implicit operator int(ShuffledIndex m)
	{
		if ( m.m_ids.Length == 0 )
			return -1;
		if ( m.m_current < 0 || m.m_current >= m.m_ids.Length )
		{           
			int previousValue = m.m_current < 0 ? -1 : m.m_ids[m.m_ids.Length - 1];

			m.m_ids.Shuffle();           
			m.m_current = 0;

			// Check if it's repeating the last value and if so, swap the first elements around so you don't get 2 in a row (only if more than 1 element)
			if ( m.m_ids.Length > 1 && previousValue == m.m_ids[0] )
				Utils.Swap(ref m.m_ids[0], ref m.m_ids[1]);
		}
		return m.m_ids[m.m_current];

	}

	public int Length { get{ return m_ids.Length; } }
	public int Count { get{ return m_ids.Length; } }

	/// Moves current id to the used 
	public void SetCurrent(int id)
	{
		if ( m_ids == null || m_ids.Length == 0 || id < 0 || id >= m_ids.Length )
			return;

		// if haven't shuffled yet, do so
		if ( m_current < 0 ) 
			this.Next();

		// find the index of the id we want
		int newCurrentIndex = System.Array.FindIndex(m_ids, (item)=>item == id);

		// if we're already there, do nothing
		if ( newCurrentIndex == m_current )
			return; // dont need to do anything
		
		if ( newCurrentIndex < m_current)
		{			
			// If already passed that id, swap the value with the current index
			Utils.Swap(ref m_ids[newCurrentIndex], ref m_ids[m_current]);
		}
		else if ( newCurrentIndex > m_current )
		{			
			// If haven't passed the index yet, go to next index and swap with current index (unless we're on that one)

			// swap new current index with next
			Next();
			if ( m_current != newCurrentIndex )
				Utils.Swap(ref m_ids[newCurrentIndex], ref m_ids[m_current]);
		}
	}

    public static ShuffledIndex operator ++(ShuffledIndex m)
    {
    	m.Next();
    	return m;
	}

	/*
	public void Sync( string saveString, SaveHelper file )
	{
		int currentId = (int)this;
		file.Sync(saveString+"cid", ref currentId);

		BigBadBitMask mask = new BigBadBitMask();
		if ( file.GetSaving() )
		{			
			// Set ids of items that have been used
			for ( int i = 0; i < m_current; ++i )
			{				
				mask.SetAt(m_ids[i],true);
			}			
		}
		file.Sync( saveString, ref mask.m_masks );

		if ( file.GetLoading() )
		{			
			if ( m_current < 0 )
				Next(); // shuffle list if it wasn't initialised yet

			// Loop through ids, and put any used up ones at the start
			int startOffset = 0;
			for ( int i = 0; i < m_ids.Length; ++i )
			{
				if ( mask.GetAt(m_ids[i])  )
				{					
					// Used this one so increment m_current, and put the id at the end of the used ids (where first is)									
					Utils.Swap( ref m_ids[startOffset], ref m_ids[i] );					
					startOffset++;	
				}
			}
			// Now put current at the end of the used one items, and update its position.
			m_current = System.Array.FindIndex(m_ids, item => item == currentId);
			if ( startOffset < m_ids.Length && startOffset != m_current )
			{
				if ( m_current >= 0 )
					Utils.Swap( ref m_ids[startOffset], ref m_ids[m_current] );
				m_current = startOffset;
			}
		}
	}*/
}

// Creates a shuffled list of indexes that have weights
public class WeightedShuffledIndex
{

	public float m_totalWeight = 0;	
	float m_maxWeightInv = 0;
	ShuffledIndex m_shuffledIndex = null;

	float[] m_weights = null;

	public delegate float DelegateGetWeight<T>(T item);

	public int Length { get{ return m_weights != null ? m_weights.Length : 0; } }
	public int Count { get{ return m_weights != null ? m_weights.Length : 0; } }

	public bool GetInitialised<T>(List<T> list)
	{
		return m_weights != null && m_shuffledIndex != null && m_shuffledIndex.Count == list.Count;
	}

	public float GetRatio(int item)
	{
		if ( m_totalWeight <= 0 )
			return 0;
		if ( item < 0 || item >= m_weights.Length )
			return 0;
		return m_weights[item]/m_totalWeight;
	}
	public float GetTotalWeight() { return m_totalWeight; }
	public float GetMaxWeight() { return ( m_maxWeightInv <= 0 ) ? 1.0f : 1.0f/m_maxWeightInv; }
	
	/// Chooses a random item from the list- uses static list of shuffle indexes, so won't be truely shuffled, but still useful
	/// Usage: 
	/// [SerializeField, WeightedArray] MyList[] m_myWeightedList;
	/// SetWeights(m_myList, (item)=>item.m_weight )
	public static T Select<T>( T[] list, DelegateGetWeight<T> getWeightFunc ) where T : class
	{
		if ( list == null || list.Length == 0 )
			return null;		

		float maxWeight = 0;
		//list.ForEach(item=> {if ( item.m_weight > maxWeight ) item.m_weight = maxWeight;});
		System.Array.ForEach( list, item=> { maxWeight = Mathf.Max(maxWeight, getWeightFunc(item));} );	
		while (true)
		{
			int newIndex = ShuffledIndex.Random(list.Length-1);
			float weight = getWeightFunc(list[newIndex]);
			if ( weight > 0.0f && UnityEngine.Random.value <= (weight / maxWeight) ) // check if pass weight check.
				return list[newIndex];
		}
	}

	/// Usage: 
	/// List<MyList> m_myList;
	/// SetWeights(m_myList, (item)=>item.m_weight )
	public void SetWeights<T>( List<T> list, DelegateGetWeight<T> getWeightFunc )
	{
		if ( list == null || list.Count == 0 )
			return;
		m_shuffledIndex = new ShuffledIndex(list.Count);
		m_weights = new float[list.Count];
		for ( int i = 0; i < list.Count; ++i )
		{
			m_weights[i] = getWeightFunc(list[i]);
		}
		UpdateWeights();
	}

	public void Init(int size)
	{
		if ( size == 0 )
			return;
		m_shuffledIndex = new ShuffledIndex(size);
		m_weights = new float[size];
	}
	public void SetWeight(int index, float weight)
	{
		weight = Mathf.Max(0,weight);
		if ( m_weights != null && m_shuffledIndex != null && m_shuffledIndex.Count != m_weights.Length )
		{
			Debug.LogError("Call Init before SetWeight");
			return;
		}
		m_weights[index] = weight;
		UpdateWeights();
	}

	public int Next()
	{
		m_shuffledIndex.Next();
		return (int)this;
	}

	public static implicit operator int(WeightedShuffledIndex m)
	{
		if ( m.m_maxWeightInv <= 0 )
			m.UpdateWeights();

		if ( m.m_maxWeightInv <= 0 )
			return -1;

		if ( m.m_shuffledIndex == null || m.m_shuffledIndex.Count != m.m_weights.Length )
		{
			m.m_shuffledIndex = new ShuffledIndex(m.m_weights.Length);
		}

		int result = -1;
		while (result == -1)
		{
			int newIndex = m.m_shuffledIndex.Next();
			// Break if item found that passes "chance" test. Eg: if weight = 5, and max = 10, have 50% chance of selecting.
			if ( m.m_weights[newIndex] > 0.0f && UnityEngine.Random.value <= m.m_weights[newIndex] * m.m_maxWeightInv )
			{
				result = newIndex;
			}
		}
		return result;
	}

	void UpdateWeights()
	{
		m_totalWeight = 0;
		m_maxWeightInv = 0;

		float maxWeight = 0;
		foreach ( float weight in m_weights )
		{
			m_totalWeight += weight;
			if ( weight > maxWeight )
				maxWeight = weight;
		}

		if ( maxWeight > 0 )
			m_maxWeightInv = 1.0f/maxWeight;

		if ( m_totalWeight <= 0 )
		{
			m_totalWeight = 1;
		}	
	}
	
}

[System.Serializable]
public class MinMaxRange
{
	public MinMaxRange( ) {}
	
	public MinMaxRange( float val )
	{
		m_min = val;
		m_max = val;
		m_value = val;
		m_hasMax = false;
	}
	
	public MinMaxRange( float min, float max )
	{
		m_min = min;
		m_max = max;
		m_value = min;
		m_hasMax = true;
	}
	
	// Public for editor... damnit. there's gotta be a better way :(
	public float m_min = 0;
	public float m_max = 0;
	public bool m_hasMax = false;	
	public bool m_hasValue = false;
	
	float m_value = 0;
	
	public float Min { get { return m_min; } }
	public float Max { get { return m_hasMax ? m_max : m_min; } }
	public float Value { get { return (float)this; } }
	public float Lerp(float ratio) 
	{ 
		if ( m_hasMax == false ) 
			return m_min; 
		return Mathf.Lerp(m_min, m_max, ratio); 
	}
	
	public static implicit operator float(MinMaxRange m) 
	{
		if ( m.m_hasValue == false )
		{		
			if ( m.m_hasMax )
			{
				m.m_value = Random.Range(m.m_min, m.m_max);
				m.m_hasValue = true;
				return m.m_value;
			}
			else
			{
				m.m_value = m.m_min;
				m.m_hasValue = true;
			}
		}
		return m.m_value;
	}
	
	public static implicit operator int(MinMaxRange m) 
	{
		return Mathf.RoundToInt((float)m);
	}
	
	public void Randomise()
	{
	
		if ( m_hasMax )
		{
			m_value = Random.Range(m_min, m_max);
			m_hasValue = true;
		}
		else
		{
			m_value = m_min;
			m_hasValue = true;
		}
		
	}
	
	public float GetRandom()
	{
		if ( m_hasMax )
		{
			return Random.Range(m_min, m_max);
		}
		return m_min;		
	}
	
	public bool IsZero()
	{
		return m_min == 0 && m_hasMax == false;
	}
}


// Counts references with a source string- strings are saved 
public class SourceList
{
	Dictionary<string, int> m_list = new Dictionary<string,int>( System.StringComparer.InvariantCultureIgnoreCase );

	public int Add( string source )
	{
		int result;
		if ( m_list.TryGetValue(source, out result ) )
		{
			result++;
		}
		else 
		{
			result = 1;
		}	
		m_list[source] = result;	
		return result;
	}

	public void Remove( string source )
	{		
		int result;
		if ( m_list.TryGetValue(source, out result ) )
		{
			result--;
			if ( result == 0 )
				m_list.Remove(source);	
			else 
				m_list[source] = result;

		}
	}	

	public bool Empty() { return m_list.Count == 0; }
	public void Clear() { m_list.Clear(); }
	public int Count() { return m_list.Count; }
	public int CountAll() 
	{ 
		int result = 0;
		foreach( int value in m_list.Values)
			result  += value;
		return result; 
	}
	public int Count(string source) 
	{ 
		int result = 0;
		m_list.TryGetValue(source, out result );
		return result; 
	}
	public bool Contains( string source ) { return m_list.ContainsKey(source); }

}

public class TimedList<T>
{
	
	Dictionary< T, float > m_list = new Dictionary<T, float>();
	
	List<T> m_keys = new List<T>();

	public void Add( T source )
	{
		m_list[source] = 0;
		m_keys = new List<T>(m_list.Keys);
		//Debug.Log("TimedList " +source+" count: "+m_list.Count);
	}
	
	public void Add( T source, float time )
	{
		m_list[source] = time;
		m_keys = new List<T>(m_list.Keys);
		//Debug.Log("TimedList " +source+" count: "+m_list.Count);
	}

	public void AddAdditive( T source, float time )
	{
		if ( m_list.ContainsKey(source) )
			m_list[source] += time;
		else 
		{
			m_list[source] = time;
			m_keys = new List<T>(m_list.Keys);			
		}
		//Debug.Log("TimedList " +source+" count: "+m_list.Count);
	}
	
	public bool Remove( T source )
	{
		if ( m_list.Remove(source) )
		{
			m_keys = new List<T>(m_list.Keys);
			return true;
		}
		return false;
		//Debug.Log("TimedList " +source+" count: "+m_list.Count);
	}	
	
	
	public bool Empty() { return m_list.Count == 0; }
	public void Clear() { m_list.Clear(); m_keys.Clear();  }
	public int Count() { return m_list.Count; }
	public bool Contains( T source ) { return m_list.ContainsKey(source); }
	public Dictionary< T, float > GetList() { return m_list; }
	
	
	// updates timers, returns false if there's no entries remaining in the list	
	public bool Update()
	{
		// Update list		
		
		foreach (T key in m_keys)
		{
			float timeRemaining = m_list[key];
			if ( timeRemaining > 0 )
			{
				timeRemaining -= Time.smoothDeltaTime;
				if ( timeRemaining <= 0 )
				{
					m_list.Remove(key);
					m_keys = new List<T>(m_list.Keys);
				}
				else
				{
					m_list[key] = timeRemaining;
				}
			}
		}	
		return m_list.Count > 0;
		
	}
	
	// updates timers, returns true if any change to list was made
	public bool UpdateReturnModified()
	{
		// Update list		
		bool modified = false;
		foreach (T key in m_keys)
		{
			float timeRemaining = m_list[key];
			if ( timeRemaining > 0 )
			{
				timeRemaining -= Time.smoothDeltaTime;
				if ( timeRemaining <= 0 )
				{
					modified = true;
					m_list.Remove(key);
					m_keys = new List<T>(m_list.Keys);
				}
				else
				{
					m_list[key] = timeRemaining;
				}
			}
		}	
		return modified;
		
	}
	
	// updates timers, returns false if there's no entries remaining in the list, and returns removed items
	public bool Update( out List<T> removed )
	{
		// Update list
		removed = null;		
		foreach (T key in m_keys)
		{
			float timeRemaining = m_list[key];
			if ( timeRemaining > 0 )
			{
				timeRemaining -= Time.smoothDeltaTime;
				if ( timeRemaining <= 0 )
				{
					if ( removed == null ) removed = new List<T>();
					removed.Add(key);
					m_list.Remove(key);
					m_keys = new List<T>(m_list.Keys);
				}
				else
				{
					m_list[key] = timeRemaining;
				}
			}
		}	
		return m_list.Count > 0;
		
	}

}



public struct BitMask
{	
	public BitMask( int mask )
	{
		m_value = mask;
	}

	
	public BitMask( params int[] bitsSet )
	{
		m_value = 0;
		for( int i = 0; i< bitsSet.Length; ++i )
		{
			m_value |= 1 <<  bitsSet[i];
		}
	}
	
	public static implicit operator int(BitMask m) 
	{
		return m.m_value;	
	}
		
	public int Value { get{ return m_value; } set { m_value = value; } }
	public void SetAt(int index) { m_value |= 1 << index; }
	public void SetAt<T>(T index) { m_value |= 1 << (int)(object)index; }
	public void UnsetAt(int index) { m_value &= ~(1 << index); }
	public void UnsetAt<T>(T index) { m_value &= ~(1 << (int)(object)index); }
	public bool IsSet(int index) { return (m_value & 1 << index) != 0; }
	public bool IsSet<T>(T index) { return (m_value & 1 << (int)(object)index) != 0; }
	public void Clear() { m_value = 0; }
	
	// And some static functions if you don't wanna construt the bitmask  and just wanna pass in/out an int
	public static int SetAt(int mask, int index) { return mask | 1 << index; }
	public static int UnsetAt(int mask, int index)  { return mask & ~(1 << index); }
	public static bool IsSet(int mask, int index) { return (mask & 1 << index) != 0; }


	public static uint GetNumberOfSetBits(uint i)
	{
		// From http://stackoverflow.com/questions/109023/how-to-count-the-number-of-set-bits-in-a-32-bit-integer
		i = i - ((i >> 1) & 0x55555555);
		i = (i & 0x33333333) + ((i >> 2) & 0x33333333);
		return (((i + (i >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24;
	}

	int m_value;


}


public class ParallaxAttribute : PropertyAttribute 
{
    // Intentionally blank
}
 
public class BitMaskAttribute : PropertyAttribute
{
    public System.Type propType;
    public BitMaskAttribute(System.Type aType)
    {
        propType = aType;
    }
}


// A less confusing version of rect that resizes from the center by default
[System.Serializable]
public struct RectCentered
{
	// PRivate vars
	[SerializeField] Vector2 m_min;
	[SerializeField] Vector2 m_max;

	//public RectCentered() { }
	public RectCentered(float centerX, float centerY, float width, float height) 
	{ 
		m_min = Vector2.zero;
		m_max = Vector2.zero;
		Center = new Vector2(centerX,centerY);
		Size = new Vector2(width,height);
	}
	public RectCentered( Rect rect )
	{
		m_min = rect.min;
		m_max = rect.max;
	}
	public RectCentered( RectCentered rect )
	{
		m_min = rect.Min;
		m_max = rect.Max;
	}
	public RectCentered( Vector2 min, Vector2 max )
	{
		m_min = min;
		m_max = max;
	}

	public static bool operator ==(RectCentered lhs, RectCentered rhs) { return lhs.m_min == rhs.m_min && lhs.m_max == rhs.m_max; }
	public static bool operator !=(RectCentered lhs, RectCentered rhs) { return lhs.m_min != rhs.m_min || lhs.m_max != rhs.m_max; }
	public override bool Equals(object rhs) { return this == (RectCentered)rhs; }
	public override int GetHashCode() { return m_min.GetHashCode() + (m_max.GetHashCode()+int.MaxValue/2); }

	// Public properties
	public Vector2 Center 
	{ 
		get { return (m_min+m_max)*0.5f; } 
		set 
		{
			Vector2 offset = value - Center;
			m_min += offset;
			m_max += offset;
		}
	}
	public Vector2 Size 
	{ 
		get { return m_max-m_min; } 
		set 
		{
			Vector2 offset = (value - (m_max-m_min))*0.5f;
			m_min -= offset;
			m_max += offset;
		}
	}
	public float Width 
	{ 
		get { return m_max.x-m_min.x; } 
		set 
		{
			float offset = (value - (m_max.x-m_min.x))*0.5f;
			m_min.x -= offset;
			m_max.x += offset;
		}
	}
	public float Height 
	{ 
		get { return m_max.y-m_min.y; } 
		set 
		{
			float offset = (value - (m_max.y-m_min.y))*0.5f;
			m_min.y -= offset;
			m_max.y += offset;
		}
	}

	public Vector2 Min { get { return m_min; } set { m_min = value; } }
	public Vector2 Max { get { return m_max; } set { m_max = value; } }
	public float MinX { get { return m_min.x; } set { m_min.x = value; } }
	public float MaxX { get { return m_max.x; } set { m_max.x = value; } }
	public float MinY { get { return m_min.y; } set { m_min.y = value; } }
	public float MaxY { get { return m_max.y; } set { m_max.y = value; } }


	public void Encapsulate(Vector2 point)
	{
		m_min.x = Mathf.Min(m_min.x, point.x);
		m_min.y = Mathf.Min(m_min.y, point.y);
		m_max.x = Mathf.Max(m_max.x, point.x);
		m_max.y = Mathf.Max(m_max.y, point.y);			
	}

	public void Encapsulate(Vector2 point, float radius)
	{
		m_min.x = Mathf.Min(m_min.x, point.x-radius);
		m_min.y = Mathf.Min(m_min.y, point.y-radius);
		m_max.x = Mathf.Max(m_max.x, point.x+radius);
		m_max.y = Mathf.Max(m_max.y, point.y+radius);			
	}

	public void EncapsulateLerp(Vector2 point, float radius, RectCentered original, float ratio)
	{
		if ( ratio >= 1.0 )
		{
		    Encapsulate(point,radius);
		    return;
		}

		float offset = point.x-radius;
		if ( offset < original.MinX ) offset = Mathf.Lerp(original.MinX, offset, ratio );
		m_min.x = Mathf.Min(m_min.x, offset );

		offset = point.y-radius;
		if ( offset < original.MinY ) offset = Mathf.Lerp(original.MinY, offset, ratio );
		m_min.y = Mathf.Min(m_min.y, offset );

		offset = point.x+radius;
		if ( offset > original.MaxX ) offset = Mathf.Lerp(original.MaxX, offset, ratio );
		m_max.x = Mathf.Max(m_max.x, offset );

		offset = point.y+radius;
		if ( offset > original.MaxY ) offset = Mathf.Lerp(original.MaxY, offset, ratio );
		m_max.y = Mathf.Max(m_max.y, offset );			
	}




	public void Encapsulate(RectCentered rect)
	{
		m_min.x = Mathf.Min(m_min.x, rect.Min.x);
		m_min.y = Mathf.Min(m_min.y, rect.Min.y);
		m_max.x = Mathf.Max(m_max.x, rect.Max.x);
		m_max.y = Mathf.Max(m_max.y, rect.Max.y);			
	}
	
}



public class RunningAverage
{
	float m_total = 0.0f;
	float m_num = 0.0f;
	float m_average = 0.0f;

	public void SetAverage( float average ) 
	{ 
		m_total = 0; 
		m_num = 0; 
		m_average = 0; 
		AddValue(average); 
	}	
	
	public void AddValue(float value)
	{
		m_total += value;
		++m_num;		
		m_average = m_total / m_num;	
	}
	public float GetAverage() { return m_average; }
}

public class ColorX {
	
    static readonly string ALPHA_STRING = "0123456789abcdef";
	
    private static string GetHex(int num) {
        return ALPHA_STRING[num].ToString();
    }

    private static int HexToInt(char hexChar) {
        switch (hexChar) {
            case '0': return 0;
            case '1': return 1;
            case '2': return 2;
            case '3': return 3;
            case '4': return 4;
            case '5': return 5;
            case '6': return 6;
            case '7': return 7;
            case '8': return 8;
            case '9': return 9;
            case 'A': case 'a': return 10;
            case 'B': case 'b': return 11;
            case 'C': case 'c': return 12;
            case 'D': case 'd': return 13;
            case 'E': case 'e': return 14;
            case 'F': case 'f': return 15;
        }
        return -1;
    }

    public static string RGBToHex(Color color) {
        float red = color.r * 255;
        float green = color.g * 255;
        float blue = color.b * 255;

        string a = GetHex(Mathf.FloorToInt(red / 16));
        string b = GetHex(Mathf.RoundToInt(red) % 16);
        string c = GetHex(Mathf.FloorToInt(green / 16));
        string d = GetHex(Mathf.RoundToInt(green) % 16);
        string e = GetHex(Mathf.FloorToInt(blue / 16));
        string f = GetHex(Mathf.RoundToInt(blue) % 16);

        return a + b + c + d + e + f;
    }

    public static Color HexToRGB(string color) {
        float red = (HexToInt(color[1]) + HexToInt(color[0]) * 16f) / 255f;
        float green = (HexToInt(color[3]) + HexToInt(color[2]) * 16f) / 255f;
        float blue = (HexToInt(color[5]) + HexToInt(color[4]) * 16f) / 255f;
        Color finalColor = new Color { r = red, g = green, b = blue, a = 1 };
        return finalColor;
    }



}


public class ReadOnlyAttribute : PropertyAttribute
{

}




// Hacky slow bit memory intensive bool array thing, but i need it to save out the ints easily when mask is > 32 bits
public class BigBadBitMask
{

	public int[] m_masks = new int[1];	


	public BigBadBitMask() {}
	public BigBadBitMask( int[] masks ) { m_masks = masks; }

	public void Clear()
	{
		for (int i = 0; i < m_masks.Length; ++i )
			m_masks[i] = 0;
	}
	public void SetAt(int index, bool value)
	{
		int mask = GetMaskIdFromIndex(ref index);
		
		if ( value )
		{
			m_masks[mask] |= 1 << index;
		}
		else 
		{
			m_masks[mask] &= ~(1 << index);
		}
	}
	
	public bool GetAt(int index)
	{
		int mask = GetMaskIdFromIndex(ref index);
		
		
		return (m_masks[mask] & 1 << index) != 0;
	}

	public override string ToString()
	{
		string result = string.Empty;

		for( int i = 0; i < m_masks.Length * 32; ++i )
		{
			result += GetAt(i) ? '0':'1';
		}
		return result;
	}
	
	// returns the mask, increasing size of array if necessary
	int GetMaskIdFromIndex(ref int index)
	{		
		int mask = index/32;
		index = index%32;
		while (mask >= m_masks.Length)
		{
			System.Array.Resize(ref m_masks, mask+1);
		}
		return mask;
	}	
}

/// Util for hiding all renderers under an object, then showing them again. Only hides/re-shows currently enabled renderers
public class RendererHider
{
	Renderer[] m_hiddenRenderers = null;

	public void Hide(GameObject root)
	{
		if ( m_hiddenRenderers != null )
			return; // don't double hide

		m_hiddenRenderers = root.GetComponentsInChildren<Renderer>(false);
		System.Array.ForEach(m_hiddenRenderers, item=>item.enabled = false);
	}

	public void Show()
	{
		if ( m_hiddenRenderers == null )
			return;
		System.Array.ForEach(m_hiddenRenderers, item=>{if (item != null) item.enabled = true;});
		m_hiddenRenderers = null;
		
	}

}



/// For sorting strings by natural order (so, for example walk_9.png is sorted before walk_10.png)
public class NaturalComparer: Comparer<string>, System.IDisposable 
{
	// NaturalComparer function courtesy of Justin Jones http://www.codeproject.com/Articles/22517/Natural-Sort-Comparer

	Dictionary<string, string[]> m_table = null;

	public NaturalComparer() 
	{
		m_table = new Dictionary<string, string[]>();
	}

	public void Dispose() 
	{
		m_table.Clear();
		m_table = null;
	}

	public override int Compare(string x, string y) 
	{
		if (x == y) 
			return 0;

		string[] x1, y1;
		if (!m_table.TryGetValue(x, out x1)) 
		{
			x1 = Regex.Split(x.Replace(" ", ""), "([0-9]+)");
			m_table.Add(x, x1);
		}
		if (!m_table.TryGetValue(y, out y1)) 
		{
			y1 = Regex.Split(y.Replace(" ", ""), "([0-9]+)");
			m_table.Add(y, y1);
		}

		for (int i = 0; i < x1.Length && i < y1.Length; i++) 
		{
			if (x1[i] != y1[i]) 
			{
				return PartCompare(x1[i], y1[i]);
			}
		}

		if (y1.Length > x1.Length) 
		{
			return 1;
		} 
		else if (x1.Length > y1.Length) 
		{
			return -1;
		} 

		return 0;		
	}


	static int PartCompare(string left, string right) 
	{
		int x, y;
		if (!int.TryParse(left, out x)) 
		{
			return left.CompareTo(right);
		}

		if (!int.TryParse(right, out y)) 
		{
			return left.CompareTo(right);
		}

		return x.CompareTo(y);
	}

}

}
