using UnityEngine;
using System.Collections;
using System.Collections.Generic;
namespace PowerTools
{

// Horrible pathfinding hacked from old Crawl code and influenced by PolyNav2D
public class Pathfinder
{		
	public class PathPoly
	{	
		public PolygonCollider2D m_collider = null;
		public Transform m_transform = null; // usually the colliders transform, stored for efficiency. But can optionally only set this and not the polygon collider
		public Vector2[] m_verts = null;
		public Vector2[] m_vertsInflated = null;

		// For auto-detecting changes when transform moved
		public Vector2 m_positionCached = Vector2.zero;

		// Set enabled/disabled to turn the poly on/off temporarily
		public bool m_enabled = true;
		// Used to check if the poly needs to be updated when a path is calculated. Then is set to m_enabled.
		public bool m_wasEnabled = true;
	}

	public class PathNode
	{
		public PathPoly m_pathPoly = null;
		public Vector2 m_position = Vector2.zero;
		public List<PathLink> m_links = new List<PathLink>();		
		
		public bool m_visited = false;
		public float m_cost = float.MaxValue;
		public PathNode m_next = null;
		public PathNode m_previous = null;
		
	}
	public class PathLink
	{
		public PathNode m_node;
		public float m_cost;
	}

	List<PathPoly> m_pathPolys = new List<PathPoly>();
	List<PathNode> m_nodes = new List<PathNode>();

	PathPoly m_mainPoly = null;

	bool m_dirty = false;
		
	static readonly float INFLATE_AMOUNT = 0.01f;
	
	// Member for efficiency.
	LineIntersector m_lineIntersector = new LineIntersector();

	public bool GetValid() { return m_mainPoly != null; }

	// Set the main pathfinding poly. Set to null to disable pathfinding
	public void SetMainPolygon( PolygonCollider2D collider )
	{		
		RemovePolygon(m_mainPoly);
		m_mainPoly = AddPolygon(collider, true);
	}

	// Set the main pathfinding poly. Set to null to disable pathfinding
	public void SetMainPolygon( PolygonCollider2D collider, Vector2[] pointsoverride = null )
	{		
		RemovePolygon(m_mainPoly);
		m_mainPoly = AddPolygon(collider, true, pointsoverride );
	}

	public void AddObstacle( Transform transform, Vector2[] points )
	{
		// Add obstacle, if not already added
		if (  m_pathPolys.Exists( item=>item.m_transform == transform ) == false )
		{
			AddPolygon(transform, false, points);
		}
	}
	public void RemoveObstacle( Transform transform )
	{	
		if ( transform == null )
			return;			
		PathPoly obstacle = m_pathPolys.Find(item=>item.m_transform == transform);
		if ( obstacle == null )
			return;
		RemovePolygon(obstacle);
	}

	// Adds an obstacle. NB: If the collider's already been added it doesn't add it again
	public void AddObstacle( PolygonCollider2D collider, Vector2[] pointsoverride = null )
	{
		// Add obstacle, if not already added
		if (  m_pathPolys.Exists( item=>item.m_collider == collider ) == false )
		{
			AddPolygon(collider, false, pointsoverride);
		}
	}
	public void RemoveObstacle( PolygonCollider2D collider )
	{	
		if ( collider == null )
			return;			
		PathPoly obstacle = m_pathPolys.Find(item=>item.m_collider == collider);
		if ( obstacle == null )
			return;
		RemovePolygon(obstacle);
	}

	public void EnableObstacle(Transform trans)
	{
		if ( trans == null )
			return;
		PathPoly obstacle = m_pathPolys.Find(item=>item.m_transform == trans);
		if ( obstacle != null )
			obstacle.m_enabled = true;
	}
	public void DisableObstacle(Transform trans)
	{
		if ( trans == null )
			return;
		PathPoly obstacle = m_pathPolys.Find(item=>item.m_transform == trans);
		if ( obstacle != null )
			obstacle.m_enabled = false;
	}

	public bool IsPointInArea( Vector2 point )
	{		
		if ( m_mainPoly == null )
			return true;
			
		UpdateObstacles();

		foreach( PathPoly pathPoly in m_pathPolys )
		{			
			bool shouldBeInside = pathPoly == m_mainPoly;
			if ( pathPoly.m_enabled && IsPointInPoly( pathPoly.m_verts, point ) != shouldBeInside )
			{
				return false;
			}
		}
		return true;
	}
	
	public Vector2[] FindPath( Vector2 pointStart, Vector2 pointEnd )
	{
		if ( GetValid() == false )
			return new Vector2[]{pointStart,pointEnd};

		List<Vector2> result = new List<Vector2>();

		UpdateObstacles();

		// Check if we need to rebuild the map
		if ( m_dirty )
		{
			m_dirty = false;
			CalculateLinks(0);
		}

		// Add the starting point if it's not inside the map already
		if (!IsPointInArea(pointStart))
			pointStart = GetClosestPointToArea(pointStart);
		
		PathNode nodeStart = new PathNode() { m_position = pointStart };
		PathNode nodeEnd = new PathNode() { m_position = pointEnd };

		m_nodes.Add(nodeStart);
		m_nodes.Add(nodeEnd);

		// Calculate links (to the new nodes only)
		CalculateLinks( m_nodes.Count-2 );

		// Do dijkstra
		if ( EvaluateDijkstra(nodeStart, nodeEnd) )
		{	
			// Iterate backwards through to build list of points
			PathNode node = nodeEnd;
			result.Insert(0,node.m_position);
			while ( node != nodeStart )// iterate to node after the first
			{				
				node = node.m_previous;
				if ( (result[0] - node.m_position).sqrMagnitude > 1.0f ) // Don't insert positions that are too close (within 1.0f)
					result.Insert(0,node.m_position);
			}
		}
		else 
		{
			Debug.Log("Failed to find path");
		}

		RemoveNode(nodeStart);
		RemoveNode(nodeEnd);

		return result.ToArray();
	}

	// Just finds the next point to go to (was used for Crawl, but not for powerquest at the moment)
	public Vector2 FindNextPoint( Vector2 pointStart, Vector2 pointEnd )
	{
		if ( m_dirty )
		{
			m_dirty = false;
			CalculateLinks(0);
		}

		if (!IsPointInArea(pointStart))
			pointStart = GetClosestPointToArea(pointStart);
			
		PathNode nodeStart = new PathNode() { m_position = pointStart };
		PathNode nodeEnd = new PathNode() { m_position = pointEnd };

		m_nodes.Add(nodeStart);
		m_nodes.Add(nodeEnd);

		// Calculate links (to the new nodes only)
		CalculateLinks( m_nodes.Count-2 );

		// Do dijkstra
		Vector2 firstPoint = pointEnd; // if it fails, move towards end point i guess
		if ( EvaluateDijkstra(nodeStart, nodeEnd) )
		{
			PathNode node = nodeEnd;
			while ( node.m_previous != nodeStart // iterate to node after to the first
				&& (pointStart-node.m_previous.m_position).sqrMagnitude > 1.0f )// as long as you're not already too close
			{
				node = node.m_previous;
			}
			firstPoint = node.m_position;
		}

		RemoveNode(nodeStart);
		RemoveNode(nodeEnd);

		return firstPoint;
	}	

	// Update obstacles positions and whether they're enabled or disabled
	void UpdateObstacles()
	{
		foreach ( PathPoly pathPoly in m_pathPolys )
		{
			if ( pathPoly.m_enabled == pathPoly.m_wasEnabled )
				continue;
			pathPoly.m_wasEnabled = pathPoly.m_enabled;

			if ( pathPoly.m_enabled )
				AddPathPolyNodes(pathPoly);
			else
				RemovePathPolyNodes(pathPoly);

			// TODO: only calculate links for nodes we've changed, instead of marking whole thing as dirty
			m_dirty = true;			
		}

		m_pathPolys.ForEach(pathPoly => UpdatePolyNodePosition(pathPoly));
	}

	// for debugging (obviously)
	public void DrawDebugLines()
	{
		foreach( PathPoly poly in m_pathPolys )
		{
			DrawDebugPoly( poly.m_vertsInflated, ( poly == m_mainPoly ) ? Color.green : (poly.m_enabled?Color.red:Color.grey) );			
		}
		
		foreach( PathNode node in m_nodes )
		{			
			foreach ( PathLink link in node.m_links )
			{
				if ( link.m_node != null )
					Debug.DrawLine(node.m_position, link.m_node.m_position, Color.yellow);
			}
		}
	}

	void DrawDebugPoly(Vector2[] points, Color color)
	{
		for (int i = 0; i < points.Length; i++)
			Debug.DrawLine(points[i], points[(i + 1) % points.Length], color);
	}

	void RemovePolygon(PathPoly polygon)
	{		
		int index = m_pathPolys.FindIndex(item=>item == polygon);
		if ( index < 0 )
			return;
		PathPoly pathPoly = m_pathPolys[index];

		// Remove PathPoly from list
		m_pathPolys.RemoveAt(index);

		// Remove it's nodes
		RemovePathPolyNodes(pathPoly);
		m_dirty = true;

	}

	void RemovePathPolyNodes(PathPoly pathPoly)
	{
		for (int i = m_nodes.Count - 1; i >= 0; i--) 
		{
			if ( m_nodes[i].m_pathPoly == pathPoly )
				RemoveNode(m_nodes[i]);	
		}
	}

	PathPoly AddPolygon( PolygonCollider2D collider, bool isMain, Vector2[] pointsOverride = null )
	{
		if ( collider == null )
			return null;
		return AddPolygon( collider, collider.transform,isMain,pointsOverride);		
	}
	PathPoly AddPolygon( Transform transform, bool isMain, Vector2[] pointsOverride = null )
	{
		if ( transform == null )
			return null;
		return AddPolygon( null, transform,isMain,pointsOverride);		
	}

	PathPoly AddPolygon( PolygonCollider2D collider, Transform transform, bool isMain, Vector2[] pointsOverride = null )
	{
		m_dirty = true;
		//
		// Add basic pathpoly, with polygon
		//
		PathPoly pathPoly = new PathPoly();
		pathPoly.m_collider = collider;
		pathPoly.m_transform = transform;
		pathPoly.m_positionCached = transform.position;

		Vector2 positionOffset = pathPoly.m_positionCached;

		Vector2[] verts = null;
		if ( pointsOverride != null ) 
			verts = pointsOverride;
		else 
			verts = collider.GetPath(0).Clone() as Vector2[];
		for(int i = 0; i < verts.Length;++i)
			verts[i] = verts[i]+positionOffset;
		pathPoly.m_verts = verts;

		// The main polygon is reversed, since we wanna be inside it, not outside
		if ( isMain != CheckWindingClockwise(verts) )
			ReversePoly(verts);

		//
		// Create and Add inflated polygon
		//		
		Vector2[] vertsInflated = new Vector2[verts.Length];
		vertsInflated = InflatePoly(verts,INFLATE_AMOUNT);	
		pathPoly.m_vertsInflated = vertsInflated;

		// Add graph nodes (concave points)
		AddPathPolyNodes(pathPoly);

		m_pathPolys.Add(pathPoly);
		return pathPoly;	
	}

	// Returns true if the polygon is clockwise, otherwise false.
	bool CheckWindingClockwise(Vector2[] verts) 
	{
		// From https://stackoverflow.com/questions/1165647/how-to-determine-if-a-list-of-polygon-points-are-in-clockwise-order
		float signedArea = 0;
		for ( int i = 0; i < verts.Length; ++i )
		{
			int next = i == verts.Length-1 ? 0 : i+1;
			signedArea += (verts[i].x*verts[next].y) - (verts[next].x * verts[i].y);
		}
		// signedArea /= 2; // Don't need to actually find area, just whether its' +ve or -ve
		return signedArea > 0;
		
	}

	// Checks if the poly's transform has moved, and if so, updates the verts and nodes, and sets dirty flag
	void UpdatePolyNodePosition(PathPoly pathPoly)
	{
		if ( pathPoly.m_enabled == false )
			return;
		if ( (pathPoly.m_positionCached-(Vector2)pathPoly.m_transform.position).sqrMagnitude <= float.Epsilon )
			return;

		// Get position offset we need to make, and update cached position,
		Vector2 positionOffset = (Vector2)pathPoly.m_transform.position - pathPoly.m_positionCached;
		pathPoly.m_positionCached = pathPoly.m_transform.position;

		// Update vert and expanded vert positions
		for(int i = 0; i < pathPoly.m_verts.Length;++i)
			pathPoly.m_verts[i] = pathPoly.m_verts[i]+positionOffset;
		for(int i = 0; i < pathPoly.m_vertsInflated.Length;++i)
			pathPoly.m_vertsInflated[i] = pathPoly.m_vertsInflated[i]+positionOffset;

		// Remove nodes, and add them again.
		RemovePathPolyNodes(pathPoly);
		AddPathPolyNodes(pathPoly);

		m_dirty = true;

	}

	void AddPathPolyNodes(PathPoly pathPoly)
	{
		Vector2[] vertsInflated = pathPoly.m_vertsInflated;
		int count = vertsInflated.Length;
		for ( int i = 0; i < count; ++i )			
		{			
			// Find corner of that position

			bool concave = IsPointConcave(vertsInflated,i);			

			if ( concave == false ) // Don't add concave nodes
				// && IsPointInArea(vertsInflated[i]) ) // Previously only added nodes if they were in walkable area, but that creates a problem when removeing polygons, and they'd  they all have to be added again. So it's not really an effective optimisation.
			{
				m_nodes.Add( new PathNode() { m_position = vertsInflated[i], m_pathPoly = pathPoly } );	
			}
		}		
	}

	// Calculates links. Should only be done on new nodes, if fromNode == 0, then it clears them though.
	void CalculateLinks( int fromNode )
	{		
		if (  fromNode <= 0 )
		{
			// Clear all links
			for ( int i = 0; i < m_nodes.Count; ++i )
				m_nodes[i].m_links.Clear();
		}

		// Calculates links between nodes that don't hit a polygon.
		// NB: this is testing against the polygons and NOT the "expanded" polygons
		//   - this makes things a bit confusing, but works well for hero pathfinding (most of the time)
		for ( int i = 0; i < m_nodes.Count; ++i )
		{
			PathNode nodeA = m_nodes[i];						
			for ( int j = Mathf.Max(i+1, fromNode); j < m_nodes.Count; ++j )			
			{
				PathNode nodeB = m_nodes[j];	
				
				if ( HasLineOfSight(nodeA.m_position, nodeB.m_position) )
				{
					float cost = (nodeA.m_position - nodeB.m_position).magnitude;
					nodeA.m_links.Add( new PathLink() { m_node = nodeB, m_cost = cost } );
					nodeB.m_links.Add( new PathLink() { m_node = nodeA, m_cost = cost } );
				}				
			}
		}
	}


	void RemoveNode( PathNode node )
	{				
		for ( int i = 0; i < node.m_links.Count; ++i )			
		{
			PathNode linkerNode = node.m_links[i].m_node;
			linkerNode.m_links.RemoveAll(link=>link.m_node == node);
		}
		
		m_nodes.Remove(node);
	}

	// Same evaluate thing used on crawl, simpler than a* but fine for these purposes I reckon
	bool EvaluateDijkstra( PathNode startNode, PathNode endNode )
	{
		// Clear nodes
		for ( int i = 0; i < m_nodes.Count; ++i )			
		{
			PathNode node = m_nodes[i];
			node.m_visited = false;
			node.m_cost = float.MaxValue;
			node.m_next = null;
			node.m_previous = null;
		}
		startNode.m_cost = 0;
		
		List<PathNode> queue = new List<PathNode>();
		queue.Add(startNode);
		
		float tmpCost = 0;
		
		while ( queue.Count > 0 )
		{
			// Find element with smallest dist
			PathNode currNode = null;
			for ( int i = 0; i < queue.Count; ++i )
			{
				PathNode tmpNode = queue[i];
				if ( tmpNode.m_visited == false && ( currNode == null || tmpNode.m_cost < currNode.m_cost ) )
					currNode = tmpNode;
			}
			if ( currNode == endNode )
			{
				// finished
				return true;
			}
			if ( currNode == null )
				return false;
			queue.Remove(currNode);
			currNode.m_visited = true;
			
			for ( int i = 0; i < currNode.m_links.Count; ++i ) // Loop through links. Add unvisted ones to queue if cost is good
			{				
				PathLink link = currNode.m_links[i];				
				tmpCost = currNode.m_cost + link.m_cost;
				
				if ( tmpCost < link.m_node.m_cost )
				{
					link.m_node.m_cost = tmpCost;
					link.m_node.m_previous = currNode;
					if ( link.m_node.m_visited == false )
					{
						queue.Add(link.m_node);
					}
				}				
			}			
		}
		
		return false;	
	}
	
	// Check line of sight, using my intersector from crawl
	bool HasLineOfSight( Vector2 pointA, Vector2 pointB )
	{
		if ( (pointA-pointB).sqrMagnitude < float.Epsilon )
			return true;
		m_lineIntersector.SetFirstLine(pointA,pointB);
		for ( int iPoly = 0; iPoly < m_pathPolys.Count; ++iPoly )
		{
			if ( m_pathPolys[iPoly].m_enabled == false )
				continue;
			Vector2[] verts = m_pathPolys[iPoly].m_verts;
			int vertsLength = verts.Length;
			Vector2 prevPoint = verts[vertsLength-1];
			for ( int i = 0; i < vertsLength; ++i )			
			{
				if ( m_lineIntersector.Calculate( prevPoint, verts[i] ) )
					return false;
				prevPoint = verts[i];
			}
		}
		return true;
	}

	// Is a point inside a polygon? - Hacked up from PolyNav2D's method
	public static bool IsPointInPoly(Vector2[] polyPoints, Vector2 point)
	{

		float xMin = 0;
		for (int i = 0; i < polyPoints.Length; i++)
			xMin = Mathf.Min(xMin, polyPoints[i].x);

		Vector2 origin = new Vector2(xMin - 0.1f, point.y);
		int intersections = 0;

		for (int i = 0; i < polyPoints.Length; i++)
		{
			Vector2 pA = polyPoints[i];
			Vector2 pB = polyPoints[(i + 1) % polyPoints.Length];

			if (LineIntersector.HasIntersection(origin, point, pA, pB))
				intersections++;
		}

		return (intersections & 1) == 1;
	}


	// Finds the closer edge point to the navigation valid area - Hacked up from PolyNav2D's method
	public Vector2 GetClosestPointToArea( Vector2 point )
	{
	
		UpdateObstacles();

		List<Vector2> possiblePoints= new List<Vector2>();
		Vector2 closerVertex = Vector2.zero;
		float closerVertexDist = Mathf.Infinity;

		for (int p = 0; p < m_pathPolys.Count; p++)
		{
			if ( m_pathPolys[p].m_enabled == false  )
				continue;

			Vector2[] poly = m_pathPolys[p].m_verts;
			Vector2[] inflatedPoints = m_pathPolys[p].m_vertsInflated; 

			/* PolyNav2d inflates points here, since they're inconsistantly inflated for obstructions, etc. We don't need to do that though.
			Vector2[] inflatedPoints = new Vector2[m_pathPolys[p].m_vertsInflated.Length];
			m_pathPolys[p].m_vertsInflated.CopyTo(inflatedPoints,0);
			InflatePoly(inflatedPoints,0.01f);
			*/

			for (int i = 0; i < inflatedPoints.Length; i++){

				Vector2 a = inflatedPoints[i];
				Vector2 b = inflatedPoints[(i + 1) % inflatedPoints.Length];

				Vector2 originalA = poly[i];
				Vector2 originalB = poly[(i + 1) % poly.Length];

				Vector2 proj = (Vector2)Vector3.Project( (point - a), (b - a) ) + a;

				if (LineIntersector.HasIntersection(point, proj, originalA, originalB) && IsPointInArea(proj))
					possiblePoints.Add(proj);

				float dist = (point - inflatedPoints[i]).sqrMagnitude;
				if ( dist < closerVertexDist && IsPointInArea(inflatedPoints[i]))
				{
					closerVertexDist = dist;
					closerVertex = inflatedPoints[i];
				}
			}
		}

		possiblePoints.Add(closerVertex);

		var closerDist = Mathf.Infinity;
		var index = 0;
		for (int i = 0; i < possiblePoints.Count; i++){
			var dist = (point - possiblePoints[i]).sqrMagnitude;
			if (dist < closerDist){
				closerDist = dist;
				index = i;
			}
		}

		//Debug.DrawLine(point, possiblePoints[index]);
		return possiblePoints[index];
	}

	// Mostly from polynav2d's way of inflating. my previous attempt sucked.
	public static Vector2[] InflatePoly(Vector2[] poly, float amount)
	{
		Vector2[] result = new Vector2[poly.Length];

		for ( int i = 0; i < poly.Length; i++ )
		{
			Vector2 prev = poly[i == 0? poly.Length - 1 : i - 1];
			Vector2 curr = poly[i];
			Vector2 next = poly[(i + 1) % poly.Length];

			Vector2 prevDir = (prev-curr).normalized;
			Vector2 nextDir = (next-curr).normalized;
			Vector2 avgDir = (prevDir+nextDir);
			avgDir *= (IsPointConcave(poly, i) ? amount : -amount);

			result[i] = (poly[i] + avgDir);
		}

		return result;
	}

	// NB: if getting wierdness, maybe try polynav2d's way of checking for concave points, mine mihgt be bad?
	public static bool IsPointConcave(Vector2[] points, int point)
	{
		Vector2 current = points[point];
		Vector2 next = points[(point + 1) % points.Length];
		Vector2 previous =  points[point == 0? points.Length - 1 : point - 1];

		return Vector2.Dot( (current-previous).GetTangentR(), next-current ) <= 0;
	}


	public static void ReversePoly(Vector2[] poly)
	{
		for (int i = 0; i < poly.Length*0.5f; ++i )
			Quest.Utils.Swap(ref poly[i], ref poly[poly.Length-1-i]);
	}

}


// More efficient way of checking lots interseting lines of against a single line. Just set the vector that's changed
public class LineIntersector
{
	Vector2 m_start1;
	//Vector2 m_end1;
	Vector2 m_end1MinusStart1 = Vector2.zero;
	float m_end1MinusStart1Mag = 0;
	float m_resultRatioFromStart = 0;

	public Vector2 GetResult() { return m_start1 + (m_resultRatioFromStart * m_end1MinusStart1); }
	public float GetResultDistFromStart() { return m_resultRatioFromStart * m_end1MinusStart1Mag; }

	public void SetFirstLine(Vector2 start, Vector2 end)
	{
		m_start1 = start;	
		m_end1MinusStart1 = end - m_start1;
		m_end1MinusStart1Mag = m_end1MinusStart1.magnitude;
	}

	// Find intersection between the first line (set already) and the line passed in lines with start and end. Returns true if intersected. Can then use GetResult and GetResultDistFromStart().
	public bool Calculate( Vector2 secondLineStart, Vector2 secondLineEnd )
	{
		// Adapted from http://stackoverflow.com/questions/1119451/how-to-tell-if-a-line-intersects-a-polygon-in-c		

		Vector2 end2MinusStart2 = secondLineEnd - secondLineStart;

		float denom = (m_end1MinusStart1.x * end2MinusStart2.y) - (m_end1MinusStart1.y * end2MinusStart2.x);

		//  AB & CD are c 
		if (denom == 0)		
		{	
			return false;
		}
		denom = 1.0f/denom;

		float r = (((m_start1.y - secondLineStart.y) * end2MinusStart2.x) - ((m_start1.x - secondLineStart.x) * end2MinusStart2.y)) * denom;				
		float s = (((m_start1.y - secondLineStart.y) * m_end1MinusStart1.x) - ((m_start1.x - secondLineStart.x) * m_end1MinusStart1.y)) * denom;			

		/* use this if you want lines that touch but don't cross to intersect /
		if ((r < 0.0f || r > 1.0f) || (s < 0.0f || s > 1.0f))
		{
			return false;
		}
		/**/
		/* use this if you want lines that touch but don't cross to NOT intersect */
		if ((r <= 0 || r >= 1) || (s <= 0 || s >= 1))
		{
			return false;
		}
		/**/

		// Find intersection point
		m_resultRatioFromStart = r;
		//m_result = m_start1 + (r * m_end1MinusStart1);
		return true;
	}


	// Find intersection between two lines with start and end
	public static bool FindIntersection(Vector2 start1, Vector2 end1, Vector2 start2, Vector2 end2, out Vector2 result)
	{
		// Adapted from http://stackoverflow.com/questions/1119451/how-to-tell-if-a-line-intersects-a-polygon-in-c
		Vector2 end1MinusStart1 = (end1 - start1);
		Vector2 end2MinusStart2 = (end2 - start2);
		Vector2 start1MinusStart2 = (start1 - start2);
		float denom = (end1MinusStart1.x * end2MinusStart2.y) - (end1MinusStart1.y * end2MinusStart2.x);

		//  AB & CD are c 
		if (denom == 0)		
		{	
			result = Vector2.zero;
			return false;
		}
		denom = 1.0f/denom;

		float r = ((start1MinusStart2.y * end2MinusStart2.x) - (start1MinusStart2.x * end2MinusStart2.y)) * denom;		
		float s = ((start1MinusStart2.y * end1MinusStart1.x) - (start1MinusStart2.x * end1MinusStart1.y)) * denom;		

		// use this if you want lines that touch but don't cross to intersect
		//if ((r < 0 || r > 1) || (s < 0 || s > 1))
		//{
		//	result = Vector2.zero;
		//	return false;
		//}

		// use this if you want lines that touch but don't cross to NOT intersect
		if ((r <= 0 || r >= 1) || (s <= 0 || s >= 1))
		{
			result = Vector2.zero;
			return false;
		}

		// Find intersection point
		result = start1 + (r * end1MinusStart1);
		return true;
	}

	// Find intersection between two lines with start and end
	public static bool HasIntersection(Vector2 start1, Vector2 end1, Vector2 start2, Vector2 end2)
	{
		// Adapted from http://stackoverflow.com/questions/1119451/how-to-tell-if-a-line-intersects-a-polygon-in-c
		Vector2 end1MinusStart1 = (end1 - start1);
		Vector2 end2MinusStart2 = (end2 - start2);
		Vector2 start1MinusStart2 = (start1 - start2);
		float denom = (end1MinusStart1.x * end2MinusStart2.y) - (end1MinusStart1.y * end2MinusStart2.x);

		//  AB & CD are c 
		if (denom == 0)		
		{	
			return false;
		}
		denom = 1.0f/denom;

		float r = ((start1MinusStart2.y * end2MinusStart2.x) - (start1MinusStart2.x * end2MinusStart2.y)) * denom;		
		float s = ((start1MinusStart2.y * end1MinusStart1.x) - (start1MinusStart2.x * end1MinusStart1.y)) * denom;		

		// use this if you want lines that touch but don't cross to intersect
		//if ((r < 0 || r > 1) || (s < 0 || s > 1))
		//	return false;

		// use this if you want lines that touch but don't cross to NOT intersect
		if ((r <= 0 || r >= 1) || (s <= 0 || s >= 1))
			return false;

		// Find intersection point
		return true;
	}
}


public static class PathfinderExtentionMethods
{	

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
}

}