MV4025 Lab 03
Schutt and Fetterolf
09 AUG 2019

Summary:

We implemented a basic version of the SE effort planning process.  Each "entity" (ME, SE1, SE2) chooses their position based on observation cost, distance from EN COM, cost to get to the position.  The main effort position is the basis for the supporting effort positions. The first supporting effort will look for a position within 45-90 degrees offset from the main effort based on the scoring.  Supporting effort two uses the same arc as supporting effort one, however is limited from being within 20m of SE1 and less than 80m from them.  This keeps the second supporting effort from positioning themselves on an opposite available arc than the first supporting effort.  

Setup:

The following steps are good practice to ensure proper functionality:
1. Select Script holder object in hierarchy.
2. Select the seed via the randomize button or via manual input
3. Click Place Forces button
2. Click the following buttons in order:
	a: Compute Observer Data
	b: Average Over Unit Size
	c: Recompute Cost

Once this is complete select the A* object in the Hierarchy and perform the following actions:

1. Select the Save & Load submenu
2. Click the Generate cache button
3. Click the don't scan button

Once this is complete you are ready to plan the mission.  Follow the next steps to complete this:

1. Select the MEsectorAxis (left, center, right)
2. In sequential order, from top to bottom, click the buttons starting with ME Analysis all the way down to SE2 Assault Position.
3. Once complete to view the planned assault positions for each click the Visualize Assault Positions button (This will show the markers for each position with the following color code: Blue - ME, Black - SE1, Gray - SE2.

Issues/Potential future work:

-Attempted to visualize the paths when visualizing the positions however was unable to figure out get this to work.
-Working out how to negate the field of fire of previously placed efforts and excluding those from viable sector nodes.
-Adding additional planning factors should be fairly easy based on how the code is implemented. 
-Implementing movement of the blue force to the enemy force.
