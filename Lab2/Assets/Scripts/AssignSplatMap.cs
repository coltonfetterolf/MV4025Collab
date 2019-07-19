using UnityEngine;
using System.Collections;
using System.Linq; // used for Sum of array


public class AssignSplatMap : MonoBehaviour
{
    // Assumes textures are, in order, sand, black, white, red, green, blue
    public enum Mode { Sand, Height, Steepness, SlowGoHeight };
    public Mode mode = Mode.Steepness;
    enum Tex { Sand=0, Black, White, Red, Green, Blue };
    float nogo_grade = 0.8f;
    float slowgo_grade = 0.6f;
    float boost_frac = 0.5f;

    void Start()
    {
        SetMode(mode);
    }


    void SetMode(Mode mode) {
        // Get the attached terrain component
        Terrain terr = GetComponent<Terrain>();

        // Get a reference to the terrain data
        TerrainData terrainData1 = terr.terrainData;

        // Get max and min height
        float minHeight = terrainData1.bounds.min.y;
        float maxHeight = terrainData1.bounds.max.y;

        float maxSteepness = float.NegativeInfinity;
        float minSteepness = float.PositiveInfinity;

        if (mode == Mode.Steepness || mode == Mode.SlowGoHeight)
        {
            for (int y = 0; y < terrainData1.alphamapHeight; y++)
            {
                for (int x = 0; x < terrainData1.alphamapWidth; x++)
                {
                    // Normalise x/y coordinates to range 0-1 
                    float y_01 = (float)y / (float)terrainData1.alphamapHeight;
                    float x_01 = (float)x / (float)terrainData1.alphamapWidth;

                    // Calculate the steepness of the terrain
                    float steepness = terrainData1.GetSteepness(y_01, x_01);

                    if (steepness > maxSteepness) maxSteepness = steepness;
                    if (steepness < minSteepness) minSteepness = steepness;
                }
            }
        }

        // Splatmap data is stored internally as a 3d array of floats, so declare a new empty array ready for your custom splatmap data:
        float[,,] splatmapData1 = new float[terrainData1.alphamapWidth, terrainData1.alphamapHeight, terrainData1.alphamapLayers];

        Debug.Log("terrainData1.alphamapHeight " + terrainData1.alphamapHeight + " terrainData1.alphamapWidth " + terrainData1.alphamapWidth);

        for (int y = 0; y < terrainData1.alphamapHeight; y++)
        {
            for (int x = 0; x < terrainData1.alphamapWidth; x++)
            {
                // Normalise x/y coordinates to range 0-1 
                float y_01 = (float)y / (float)terrainData1.alphamapHeight;
                float x_01 = (float)x / (float)terrainData1.alphamapWidth;

                // Sample the height at this location (note GetHeight expects int coordinates corresponding to locations in the heightmap array)
                float height = terrainData1.GetHeight(Mathf.RoundToInt(y_01 * terrainData1.heightmapHeight), Mathf.RoundToInt(x_01 * terrainData1.heightmapWidth));

                // Calculate the steepness of the terrain
                float steepness = terrainData1.GetSteepness(y_01, x_01);

                // Setup an array to record the mix of texture weights at this point
                float[] splatWeights = new float[terrainData1.alphamapLayers];


                if (mode == Mode.Sand)
                {
                    splatWeights[(int)Tex.Sand] = 1f;
                }

                else if (mode == Mode.Steepness)
                {
                    // Black texture
                    splatWeights[(int)Tex.Black] = Mathf.Clamp01(1f - (steepness - minSteepness) / (maxSteepness - minSteepness));

                    // White texture
                    splatWeights[(int)Tex.White] = Mathf.Clamp01((steepness - minSteepness) / (maxSteepness - minSteepness));
                }

                else if (mode == Mode.Height)
                {
                    // Black texture
                    splatWeights[(int)Tex.Black] = Mathf.Clamp01(1f - (height - minHeight) / (maxHeight - minHeight));

                    // White texture
                    splatWeights[(int)Tex.White] = Mathf.Clamp01((height - minHeight) / (maxHeight - minHeight));
                }

                else if (mode == Mode.SlowGoHeight)
                {
                    splatWeights[(int)Tex.Black] = Mathf.Clamp01(1f - (height - minHeight) / (maxHeight - minHeight));
                    splatWeights[(int)Tex.White] = Mathf.Clamp01((height - minHeight) / (maxHeight - minHeight));
                    float boost = boost_frac * splatWeights[(int)Tex.White];
                    splatWeights[(int)Tex.White] -= boost;

                    if (steepness/maxSteepness >= nogo_grade)
                    {
                        // Boost red
                        splatWeights[(int)Tex.Red] = boost; ;
                    }

                    else if (steepness / maxSteepness >= slowgo_grade)
                    {
                        // Boost yellow
                        splatWeights[(int)Tex.Red] = boost/2;
                        splatWeights[(int)Tex.Green] = boost/2;
                    }
                }


                /*
                else if (mode == Mode.SlowGoHeight)
                {
                    if (steepness / maxSteepness >= nogo_grade)
                    {
                        splatWeights[(int)Tex.Black] = Mathf.Clamp01(1f - (height - minHeight) / (maxHeight - minHeight));
                        splatWeights[(int)Tex.Red] = Mathf.Clamp01((height - minHeight) / (maxHeight - minHeight));
                    }
                    else if (steepness / maxSteepness >= slowgo_grade)
                    {
                        splatWeights[(int)Tex.Black] = Mathf.Clamp01(1f - (height - minHeight) / (maxHeight - minHeight));
                        splatWeights[(int)Tex.Green] = Mathf.Clamp01((height - minHeight) / (maxHeight - minHeight));
                        splatWeights[(int)Tex.Red] = Mathf.Clamp01((height - minHeight) / (maxHeight - minHeight));
                    }
                    else
                    {
                        splatWeights[(int)Tex.Black] = Mathf.Clamp01(1f - (height - minHeight) / (maxHeight - minHeight));
                        splatWeights[(int)Tex.White] = Mathf.Clamp01((height - minHeight) / (maxHeight - minHeight));
                    }
                }
                */

                // Loop through each terrain texture
                for (int i = 0; i < terrainData1.alphamapLayers; i++)
                {
                    // Assign this point to the splatmap array
                    splatmapData1[x, y, i] = splatWeights[i];
                }
            }
        }

        // Finally assign the new splatmap to the terrainData1:
        terrainData1.SetAlphamaps(0, 0, splatmapData1);
    }
}
