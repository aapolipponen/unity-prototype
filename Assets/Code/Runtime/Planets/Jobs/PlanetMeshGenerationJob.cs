using JetBrains.Annotations;
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

        public float cube;
        public int MaxIteration;
        public int Radius;
        public Vector3 campos;
        public Vector3 selfposition;

        public float floorheight;
        public float strenght;
        public float roughtness;
        public float3 center;
        public float3 centerOffset;
        public int layernumber;
        public float roughtnesschange;
        public float strenghtchange;

        public bool doBissect(float distance, int iteration)
        {
            // I could use a better structure like a dictionnary, or maybe just a (mathematical) function
             return (                    iteration <= 5 ) || // Do 5 iteration for everything
                    (distance < 16    && iteration <= 6 ) || // Do 6 iteration for distance < 32
                    (distance < 8     && iteration <= 7 ) || // Do 7 iteration for distance < 16
                    (distance < 4     && iteration <= 8 ) ||
                    (distance < 2     && iteration <= 9 ) ||
                    (distance < 1     && iteration <= 10) ||
                    (distance < 0.500 && iteration <= 11) ||
                    (distance < 0.250 && iteration <= 12) ||
                    (distance < 0.125 && iteration <= 13) ||
                    (distance < 0.0625 && iteration <= 14);
        }
        public void Execute()
        {           

            for (int i=0; i < MaxIteration; i++) {
                int l = Triangles.Length;
                for (int index = 0; index < l; index++)
                {
                    float3 pa = Vertices[Triangles[index].Index0].Position;
                    float3 pb = Vertices[Triangles[index].Index1].Position;
                    float3 pc = Vertices[Triangles[index].Index2].Position;
                    // now that i think about it this normalization might not be usefull at all
                    float3 m = math.normalize((pa + pb + pc) / 3); // middle point / average (The first few itarations have points real far away , and taking the average is important (we might be able to skip this for higher iterations when triangles are smaller))
                    if ((math.dot(campos, m) < -0.1) && (i > 4)) { continue; }  // Dont divide back side (backside is  dot()<0 but we might want a little bit of the back side for the triangles near the back side that share verticies of the backside)

                    Vector3 newp = math.normalize((pa + pb) / 2); // Point that splits triangles     
                    newp = changeHeight(newp);

                    float d = (campos - newp).magnitude;// distance
                    // If the triangel is already devided devide it (if there is a point in the middle of the hypotenus) if not then create the new point and then divide it

                    // we check if point have been created , but if we create a new point that point might appear on a triangle that have already been looped on so that triangle wont take it in account 
                    // we could check for the triangle sharing Index0 and Index1 but we'd need a dict that assosiates points to multiple triangles .. but that seems to much. (need a lot of ressources and time)

                    // If we want to get rid of the T shaped emm... holes ? We could assign 1 interger to each vertices that correspond to the LOWEST level of iteration that this point is part of (between the 4 triangles that share him) , and we could make it so that we only divide a triangle if both of his points (we dont care about the 3rd one) have a level of iteration just one layer above the one currently doin' 
                    int p;
                    if(true) //(!VertexToIndex.TryGetValue(newp, out p))
                    {
                        // IMPORTANT TO DO
                        // I need a systeme where sometimes it ignores the dot() depending on how many levels it should be displaying 
                        // ADDING THIS CAN HELP A LOT PERFORMANCES BUT GOOD LUCK MAKING THIS WORK **WELL** (it's supose to detect if a point is before or  after the horizon)
                        //if (math.dot(campos - newp, newp) < v1 && (i > v2)) { continue; } //v1 default = 0 , v2 default somthing like 4 
                        if (!doBissect(distance: d, iteration: i)) { continue; } // If dont generate then dont

                        // if we dont care about camera distance and that ALL triangles are cut in half then we woudn't need an dict to find out if point have been created , we know if it have been or not depending if we are in the first or second half of the loop
                        // and the place of the point is also easy to find 
                        // ALSO if I used a structure that holds empty slots for places where there could be an point we could also find easily the point without a dict (something like a binary tree i think) - from (1.1,2,1.2) to (1,1,1,0) I just dont know if using much bigger varrialbe with lots of empty values is a good idea
                        p = Vertices.Length;
                        VertexToIndex[newp] = p;
                        Vertices.Add(new Vertex { Position = newp });
                    }
                    // We need to destroy (write over 1) the old triangle and add 2 new triangles (add 1) 
                    Triangles.Add(     new Triangle { Index0 = Triangles[index].Index1, Index1 = Triangles[index].Index2, Index2 = (short)p });
                    Triangles[index] = new Triangle { Index1 = Triangles[index].Index0, Index0 = Triangles[index].Index2, Index2 = (short)p };

                }
            }
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
            return (noiseToAdd + 1) * Vector3.Normalize(point) * 1;
        }
    }
}

