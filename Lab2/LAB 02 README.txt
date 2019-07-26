Lab 02 README

Authors: Schutt & Fetterolf

Entity will choose a path that avoids observed nodes and a path that avoids going over hills.  Will still traverse to locations on both if explicitly ordered to go there.  

To ensure proper grid is cached perform the following steps:

1. Select the ScriptContainer object in the Hierarchy.
2. Click the Compute Observer Data button under the Analytic Planner component.
3. Click Recompute Costs button under same component.
4. (Optional) Click either Visualize Nodes or Visualize Fran Observers if viewing nodes is desired.
5. Select the A* object in the Hierarchy.
6. Click the Save & Load menu under the Astra Path component.
7. Click the Generate cache button.
8. Click the Don't Scan button.
9. Simulation is ready to run, click the play button, select the blue entity, and move it across the terrain.