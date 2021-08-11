using UnityEngine;
using System.Collections;
using PowerTools.Quest;
using PowerScript;
using static GlobalScript;

public class RoomHome : RoomScript<RoomHome>
{
	// This area is where you can put variables you want to use for game logic in your room
	
	// Pump Part variables
	public enum handleType {small, medium, large};
	
	public enum hoseType {small, medium, large};
	
	private string[] sizeString  = {"Small", "Medium", "Large"};
	
	handleType currentHandle = handleType.small;
	hoseType currentHose = hoseType.small;
	
	// Water level variables
	public int waterLevelInt = (int)Globals.m_progressExample * 40;
	
	public void lowerWater(){
		Prop("Water").MoveTo(0, 0 - ((float)Globals.m_progressExample * 30), 50);
	}
	
	
	// enums like this are a nice way of keeping track of what's happened in a room
	enum eThingsYouveDone { Start, InsultedChimp, EatenSandwich, LoadedCrossbow, AttackedFlyingNun, PhonedAlbatross }
	eThingsYouveDone m_thingsDone = eThingsYouveDone.Start;
	public void OnEnterRoom()
	{
		// Put things here that you need to set up BEFORE the room fades in (but nothing "blocking")
		// Note, you can also just do this at the top of OnEnterRoomAfterFade
		
		// sets water level according to the stage of the game
		
		Prop("Water").SetPosition(0, 0 - ((float)Globals.m_progressExample * 30));
		
		if (C.Neighbor1.Room == R.Home) {
			C.Neighbor1.SetPosition(new Vector2(Point("HomeDoorPosition")[0] - 100, Point("HomeDoorPosition")[1]));
		}
		
		
		// C.Dave.WalkToBG(Point("EntryWalk"));
		
		
		// I.BilgePump.Add();
		
		
		
		
	}

	public IEnumerator OnEnterRoomAfterFade()
	{
		// Put things here that happen when you enter a room
		
		
		if ( (R.Current.FirstTimeVisited) && (Globals.m_progressExample == eProgress.None) ) // Only run this part the first time you visit
		{
		Prop("Pump").Disable();
		yield return C.Dave.Say("Oh no! My basement is flooded! ");
		yield return E.WaitSkip();
		yield return C.Dave.Say("Maybe I can get something at the local hardware store to help. ");
		yield return E.WaitSkip();
		yield return C.Display("Left Click to Walk & Interact\nRight Click to Look At");
		} else {
		C.Dave.Position = Point("HomeDoorPosition");
		}
	}





	
	IEnumerator OnLookAtHotspotDoor( IHotspot hotspot )
	{
		yield return C.Dave.Say(" It's a door to the outside.");
		yield return E.Break;
	}

	IEnumerator OnInteractHotspotDoor( IHotspot hotspot )
	{
		if (Globals.m_progressExample == eProgress.None) {
			yield return C.Dave.Say("Ok Here I go...");
		}
		C.Dave.ChangeRoomBG(R.InfoScreen);
		yield return E.Break;
	}

	IEnumerator OnUseInvHotspotDoor( IHotspot hotspot, IInventory item )
	{

		yield return E.Break;
	}


	IEnumerator OnUseInvPropWater( IProp prop, IInventory item )
	{
		// NB: You need to check they used the correct item!
		if ( item == I.BilgePump )
		{ 
			yield return C.Dave.WalkTo(Point("PumpPosition"));
			I.BilgePump.Remove();
			Prop("Pump").Enable();
			// FaceClicked
			yield return C.Display("Dave begins to try to pump out the water.");
			Globals.m_progressExample = eProgress.TriedPump1;
			lowerWater();
			// C.Dave.WalkTo(0,-400);
			yield return C.Display("Congratulations! You have recognized the problem, and the water level has decreased. However, it is not enough... ");
			yield return C.Dave.Say("This is too hard! I think the handle is too short and the diameter of the hose is too small, I need to go back to the hardware store. ");
			yield return E.Wait(1);
			yield return E.WaitSkip();
			yield return C.Dave.FaceDown();
		
				  
		}
		yield return E.Break;
		
	}

	IEnumerator OnLookAtPropWater( IProp prop )
	{
		yield return C.Dave.Say("That's a lot of water!");
		yield return E.Break;
	}

	IEnumerator OnInteractPropWater( IProp prop )
	{
		yield return C.Dave.Say(" I can't clean out all of this using only my hands.");
		yield return E.Break;
	}

	IEnumerator OnExitRoom( IRoom oldRoom, IRoom newRoom )
	{

		yield return E.Break;
	}

	IEnumerator UpdateBlocking()
	{

		yield return E.Break;
	}

	void Update()
	{
	}

	IEnumerator OnAnyClick()
	{

		yield return E.Break;
	}

	IEnumerator OnWalkTo()
	{

		yield return E.Break;
	}

	IEnumerator OnInteractPropPump( IProp prop )
	{

		if (currentHandle == handleType.large && currentHose == hoseType.large){
		
			Globals.m_progressExample = eProgress.RightParts;
			lowerWater();
			
			yield return C.Display("You've chosen the correct parts for the pump and the water level has decreased. Equivalent to afterload reduction.");
			yield return C.Dave.Say("Still not enough... I could use some extra hands.");
		} else {
			yield return C.Dave.Say(" This isn't any better. I should try different parts. ");
		}
		
		
		yield return E.Break;
	}

	IEnumerator OnLookAtPropPump( IProp prop )
	{
		yield return C.Display(sizeString[(int)currentHandle] + " Handle\n" + sizeString[(int)currentHose] + " Hose" );

		yield return E.Break;
	}

	IEnumerator OnUseInvPropPump( IProp prop, IInventory item )
	{
		if (item == I.SmallHandle) {
			string prevHandle = sizeString[(int)currentHandle];
			returnHandleToInv();
			currentHandle = handleType.small;
			I.SmallHandle.Remove();
			yield return C.Display(prevHandle + " Handle replaced with " + sizeString[(int)currentHandle] + " Handle");  
		} else if (item == I.MediumHandle) {
			string prevHandle = sizeString[(int)currentHandle];
			returnHandleToInv();
			currentHandle = handleType.medium;
			I.MediumHandle.Remove();
			yield return C.Display(prevHandle + " Handle replaced with " + sizeString[(int)currentHandle] + " Handle");  
		} else if (item == I.LargeHandle) {
			string prevHandle = sizeString[(int)currentHandle];
			returnHandleToInv();
			currentHandle = handleType.large;
			I.LargeHandle.Remove();
			yield return C.Display(prevHandle + " Handle replaced with " + sizeString[(int)currentHandle] + " Handle");  
		} else if (item == I.SmallHose) { 
			string prevHose = sizeString[(int)currentHose];
			returnHoseToInv();
			currentHose = hoseType.small;
			I.SmallHose.Remove();
			yield return C.Display(prevHose + " Hose replaced with " + sizeString[(int)currentHose] + " Hose");
		} else if (item == I.MediumHose) {
			string prevHose = sizeString[(int)currentHose];
			returnHoseToInv();
			currentHose = hoseType.medium;
			I.MediumHose.Remove();
			yield return C.Display(prevHose + " Hose replaced with " + sizeString[(int)currentHose] + " Hose");
		} else if (item == I.LargeHose) {
			string prevHose = sizeString[(int)currentHose];
			returnHoseToInv();
			currentHose = hoseType.large;
			I.LargeHose.Remove();
			yield return C.Display(prevHose + " Hose replaced with " + sizeString[(int)currentHose] + " Hose");
		} else {}
		
		
		yield return E.Break;
	}

	private void returnHandleToInv() {
		switch(currentHandle) {
			case handleType.small:
				I.SmallHandle.Add();
				break;
			case handleType.medium:
				I.MediumHandle.Add();
				break;
			case handleType.large:
				I.LargeHandle.Add();
				break;
			default:
				break;
		}
	}

	private void returnHoseToInv() {
		switch(currentHose) {
			case hoseType.small:
				I.SmallHose.Add();
				break;
			case hoseType.medium:
				I.MediumHose.Add();
				break;
			case hoseType.large:
				I.LargeHose.Add();
				break;
			default:
				break;
		}		
	}

	IEnumerator OnInteractCharacterNeighbor1( ICharacter character )
	{
		C.Neighbor1.WalkToBG(Point("PumpPosition"));
		
		yield return C.Dave.WalkTo(new Vector2(Point("PumpPosition")[0] - 150, Point("PumpPosition")[1]));
		yield return C.Dave.Face(eFace.Right);
		
		yield return C.Neighbor1.Say("Here goes nothing!");
		
		Globals.m_progressExample = eProgress.Friend2;
		lowerWater();
		
		yield return C.Display(" The recruited muscle has helped bring the water level down. Equivalent to using positive inotropes to improve heart muscle.");
		
		yield return E.Break;
	}

	IEnumerator OnInteractCharacterNeighbor2( ICharacter character )
	{
		Camera.SetCharacterToFollow(C.Neighbor2, 200);
		
		C.Neighbor2.WalkToBG(Point("WindowPosition"));
		yield return C.Dave.WalkTo(Point("PumpPosition"));
		
		yield return E.WaitUntil( ()=> C.Neighbor2.Position == Point("WindowPosition"));
		
		yield return C.Display("Jim helps get some more water out by scooping it out the window with his bucket. Equivalent to using a diuretic.");
		
		Globals.m_progressExample = eProgress.Friend1;
		lowerWater();
		
		yield return E.Wait(2);
		yield return E.FadeOut();
		yield return C.Display(" 30 minutes later...");
		yield return E.FadeIn();
		
		yield return C.Neighbor2.Say("Phew, I'm exhausted");
		
		yield return C.Dave.Say(" Me too. We could use some extra muscle.");
		
		Camera.SetCharacterToFollow(C.Dave, 200);
		
		yield return E.Break;
	}
}