import json
import copy
import random
import sys

# Example list specification: 
#      "name" : "param_A", "levels" : [1, 7, 11]
# Example even spacing specification: 
#     "name" : "param_B", "begin" : 2, "end" : 10, "count" : 5

# Users should only change design_str
design_str = """
{
	"replications" : 10,
	"factors" : 
		[
			{
				"name" : "train_duration",
				"levels" : [ 24000 ]
			},
			{
				"name" : "test_duration",
				"levels" : [ 100 ]
			},
			{
				"name" : "reward_timeout",
				"levels" : [ 20 ]
			},
			{
				"name" : "load_brain",
				"levels" : [ false ]
			},
			{
				"name" : "respawnWidth",
				"levels" : [ 15 ]
			},
			{
				"name" : "learning_rate",
				"levels" : [ 0.05 ]
			},
			{
				"name" : "ranged_state",
				"levels" : [ true ]
			},
			{
				"name" : "num_hidden_units",
				"levels" : [ 80 ]
			},
			{
				"name" : "type_hidden_units",
				"levels" : [ "relu" ]
			},
			{
				"name" : "discount_factor",
				"levels" : [ 0.99 ]
			},
			{
				"name" : "loss_factor",
				"levels" : [ 1.1 ]
			},
			{
				"name" : "mobility_model",
				"levels" : [ "NoGradePenalty" ]
			}
		]
}
"""

def designPoints( designO ):
  points = []
  factors = designO["factors"]
  for factor in factors:
    if "levels" in factor.keys():
      continue;
    if factor["count"] == 1:
      step = 0
    else:
      step = ( factor["end"] - factor["begin"] ) / ( factor["count"] - 1 )
    values = [ ]
    value = factor["begin"]
    for i in range(factor["count"]):
      values.append( value );
      value += step
    factor["levels"] = values
  level_indices = []
  for factor in factors:
    level_indices.append( 0 )
  done = False
  while  not done:
    point = {}
    for i in range(len(factors)):
      name = factors[i]["name"]
      point[name] = factors[i]["levels"][ level_indices[i] ]
    points.append( point )
    
    cf = 0;
    level_indices[cf] += 1
    while ( level_indices[cf] == len(factors[cf]["levels"]) ):
      level_indices[cf] = 0
      cf += 1
      if cf == len(factors):
        done = True
        break
      level_indices[cf] += 1
  return points


design = json.loads( design_str )

replications = design["replications"]

design_points = designPoints( design )

# Create replications and add seeds
maxsize =  2147483647
replicationA = []
for point in design_points:
  for i in range(replications):
    rep = copy.copy(point)
    rep["seed"] = random.randint(0, maxsize)
    replicationA.append( rep )

# Convert all values to strings
stringValueRepA = []
for rep in replicationA:
  srep = copy.copy(rep)
  for key in rep.keys():
    srep[key] = str( rep[key] )
  stringValueRepA.append( srep )

# Print one replication per line
for srep in stringValueRepA:
  print( json.dumps(srep) )


