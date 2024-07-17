using JetBrains.Annotations;
using PlasticPipe.PlasticProtocol.Messages;
using PLE.Prototype.Runtime.Code.Runtime.Planets.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Rendering;

using static Unity.Entities.EntitiesJournaling;
using static UnityEditor.MaterialProperty;
using static UnityEngine.UIElements.UxmlAttributeDescription;

namespace PLE.Prototype.Runtime.Code.Runtime.Planets.Jobs
{
    [BurstCompile]
    public struct PlanetMeshGenerationJob : IJob
    {
        public NativeList<Vertex> Vertices;
        public NativeList<Triangle> Triangles;
        public NativeHashMap<float3, int> VertexToIndex;
        [ReadOnly] public NativeList<int> renderIterations;
        [ReadOnly] public NativeList<float> renderDistances;
        

        [ReadOnly] public int MaxIteration;
        [ReadOnly] public Vector3 campos;

        [ReadOnly] public float floorheight;
        [ReadOnly] public float strenght;
        [ReadOnly] public float roughtness;
        [ReadOnly] public float3 center;
        [ReadOnly] public float3 centerOffset;
        [ReadOnly] public int layernumber;
        [ReadOnly] public float roughtnesschange;
        [ReadOnly] public float strenghtchange;
        [ReadOnly] public bool deleteRepetedVertex;

        [ReadOnly] public int backsideIterations;
        [ReadOnly] public float backsideLimit;


        [ReadOnly] public int overhorizonIterations;
        [ReadOnly] public float overhorizonLimit;
        [ReadOnly] public float overhorizonLimit2;

        [ReadOnly] public float Radius;
        [ReadOnly] public float3 mp;

        public NativeList<int> IterationMinimumPerVertex; // which index should correspond to the ones in "Vertices"
        
        public void Execute()
        {
            for (int i = 0; i < 3; i++)
            {
                var v = Vertices[i];
                v.Position -= mp;
                Vertices[i] = v;
            }
            // This is for the uv texture (will color different iterations)
            var t = new NativeList<float2>(Allocator.TempJob){new float2(0, 0), new float2(0.125f, 0), new float2(0.25f, 0), new float2(0.325f, 0), new float2(0.5f, 0), new float2(0.625f, 0), new float2(0.75f, 0), new float2(0.825f, 0f), new float2(1f, 0f), new float2(1f, 0.125f), new float2(1, 0.25f), new float2(1, 0.325f), new float2(1, 0.5f), new float2(1, 0.625f), new float2(1, 0.75f), new float2(1, 0.825f) };
            
            for (int i=0; i < MaxIteration; i++) {
                int l = Triangles.Length;
                for (int index = 0; index < l; index++)
                {
                    Vector3 newPoint = CreateMiddlePoint(Vertices[Triangles[index].Index0].Position+mp, Vertices[Triangles[index].Index1].Position+mp);
                    int p;
                    // If the triangle is already devided devide it (if there is a point in the middle of the hypotenus) if not then create the new point and then divide it
                    if (true)//!VertexToIndex.TryGetValue(newp, out p))// || !deleteRepetedVertex) // this seems like to work somewhat but i moved this part that delete repeted vertex in the monobehaviour script for it to be more clear (bringing it back here could help perf)
                    {
                        if (doNotBissect(distance: (campos - newPoint).magnitude, iteration: i, point:newPoint)) { continue; } // If dont generate then dont

                        // if we dont care about camera distance and that ALL triangles are cut in half then we woudn't need an dict to find out if point have been created , we know if it have been or not depending if we are in the first or second half of the loop
                        // and the place of the point is also easy to find 
                        // ALSO if I used a structure that holds empty slots for places where there could be an point we could also find easily the point without a dict (something like a binary tree i think) - from (1.1,2,1.2) to (1,1,1,0) I just dont know if using much bigger varrialbe with lots of empty values is a good idea
                        p = Vertices.Length;
                        VertexToIndex[newPoint] = p;
                        
                        Vertices.Add(new Vertex { Position = newPoint-(Vector3)mp, UV = t[i] }) ; // this colors the planet according to iteration count (debug purpurse) // actually no it didnt help me yet and i dont see how it will but it is cool to see so ....
                        //Vertices.Add(new Vertex { Position = newp , UV = new float2(0, Mathf.Pow(Vector3.Magnitude(newp) - floorheight, 3)) }); // this is for debug purpurse colors the uv map according to height
                    }
                    // We need to destroy (write over 1) the old triangle and add 2 new triangles (add 1) 
                    Triangles.Add(     new Triangle { Index0 = Triangles[index].Index1, Index1 = Triangles[index].Index2, Index2 = (short)p });
                    Triangles[index] = new Triangle { Index1 = Triangles[index].Index0, Index0 = Triangles[index].Index2, Index2 = (short)p };

                }
            }
            
        }

        private bool doNotBissect(float distance, int iteration, Vector3 point)
        {
            // Return True if point is on the backside of the planet
            if (((math.dot(campos, point) < backsideLimit) && (iteration >= backsideIterations))) { return true; }  // backsideLimit default is 0 or maybe something like -0.1 

            // Return True if point is over the horizon 
            if (math.dot(campos - point, point) < overhorizonLimit && (iteration >= overhorizonIterations) && (overhorizonLimit2 <= distance)) { return true; } //overhorizonLimit default = 0 , overhorizonIterations default somthing like 4 

            // Return True if according the renderdistance and iteration lists this point should be created (/ rendered ?) // Insted of using this lists i could use a mathematical function ? 
            for (int k = 0; k < renderDistances.Length; k++)
            {
                if (iteration <= renderIterations[k])
                {
                    if (distance <= renderDistances[k]*Radius)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private Vector3 CreateMiddlePoint(float3 a, float3 b)
        {
            return changeHeight(math.normalize((a + b) / 2));
        }

        private float PerlinNoise3D(float3 point)
        {
            return (Mathf.PerlinNoise(point.x, point.y) + Mathf.PerlinNoise(point.y, point.x) + Mathf.PerlinNoise(point.z, point.y) + Mathf.PerlinNoise(point.y, point.z) + Mathf.PerlinNoise(point.z, point.x) + Mathf.PerlinNoise(point.x, point.z)) / 6;
        }

        private float3 changeHeight(float3 point)
        {
            float noiseToAdd = 0;
            float r = roughtness;
            float s = strenght;
            float3 c = center;
            for (int i = 0; i < layernumber; i++)
            {
                noiseToAdd += PerlinNoise3D(point * r + c) * s;
                r *= roughtnesschange;
                s *= strenghtchange;
                c += centerOffset; // change of center -> its acttualy more of a sample point , this change could be done in other ways
            }
            noiseToAdd = Mathf.Max(0, noiseToAdd - floorheight); // set changes to 0 if they are not big enoth (result in a "ground" layer , an ocean floor)
            return (noiseToAdd + 1) * Vector3.Normalize(point) * Radius;
        }
    }
}

