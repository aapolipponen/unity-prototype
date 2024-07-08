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
        [WriteOnly]
        public Mesh.MeshData MeshData;

        public float cube;
        public int MaxIteration;
        public int Radius;
        public Vector3 campos;
        public Vector3 selfposition;

        public float strenght;
        public float roughtness;
        public float3 center;
        public int layernumber;
        public float roughtnesschange;
        public float strenghtchange;

        public bool doBissect(float distance,int iteration)
        {
            // I could use a better structure like a dictionnary defined in a start 
            if ((iteration <= 5 ||  // Do 5 iteration for everything
                (distance < 32 && iteration <= 6) || // Do 6 iteration for distance < 6
                (distance < 16 && iteration <= 7) || // Do 7 iteration for distance < 5
                (distance < 8 && iteration <= 8) || 
                (distance < 4 && iteration <= 9) ||
                (distance < 2 && iteration <= 10) || 
                (distance < 1.00 && iteration <= 11) || 
                (distance < 0.5 && iteration <= 12) || 
                (distance < 0.25 && iteration <= 13) || 
                (distance < 0.125 && iteration <= 14)))
            { return true; }else { return false; }
        }
        public unsafe void Execute()
        {
            selfposition = float3.zero;
            var vertices = new NativeList<Vertex>(Allocator.Temp);
            var triangles = new NativeList<Triangle>(Allocator.Temp);

            // TOP   FRONT  BACK    RIGHT   LEFT (Views)
            // 20    02     64      40      26  
            // 64    13     75      51      37
            vertices.Add(new Vertex { Position = math.normalize(new float3(+1,+1,+1)) });//0
            vertices.Add(new Vertex { Position = math.normalize(new float3(+1,-1,+1)) });//1

            vertices.Add(new Vertex { Position = math.normalize(new float3(-1,+1,+1)) });//2
            vertices.Add(new Vertex { Position = math.normalize(new float3(-1,-1,+1)) });//3

            vertices.Add(new Vertex { Position = math.normalize(new float3(+1,+1,-1)) });//4
            vertices.Add(new Vertex { Position = math.normalize(new float3(+1,-1,-1)) });//5

            vertices.Add(new Vertex { Position = math.normalize(new float3(-1,+1,-1)) });//6
            vertices.Add(new Vertex { Position = math.normalize(new float3(-1,-1,-1)) });//7

            for (int id = 0; id < vertices.Length; id++)
            {
                var x = vertices[id];
                x.Position = changeHeight(x.Position);
                vertices[id] = x;
            }

            var vertexDict = new NativeHashMap<float3, int>(23000, Allocator.TempJob); // This "dict" or whatever a hashmap is is connecting vertex positions to their index in verticies (so that I check for duplicated verticies and delete them)
            int T =0;
            foreach (var thing in vertices) { vertexDict[thing.Position] = T; T++; }
            // TOP
            triangles.Add(new Triangle { Index0 = (short)4, Index1 = (short)2, Index2 = (short)0});
            triangles.Add(new Triangle { Index0 = (short)2, Index1 = (short)4, Index2 = (short)6});
            // BOT
            triangles.Add(new Triangle { Index0 = (short)3, Index1 = (short)5, Index2 = (short)1});
            triangles.Add(new Triangle { Index0 = (short)5, Index1 = (short)3, Index2 = (short)7});
            // FRONT
            triangles.Add(new Triangle { Index0 = (short)3, Index1 = (short)0, Index2 = (short)2 });
            triangles.Add(new Triangle { Index0 = (short)0, Index1 = (short)3, Index2 = (short)1 });
            // BACK
            triangles.Add(new Triangle { Index0 = (short)5, Index1 = (short)6, Index2 = (short)4 });
            triangles.Add(new Triangle { Index0 = (short)6, Index1 = (short)5, Index2 = (short)7 });
            // RIGHT
            triangles.Add(new Triangle { Index0 = (short)1, Index1 = (short)4, Index2 = (short)0 });
            triangles.Add(new Triangle { Index0 = (short)4, Index1 = (short)1, Index2 = (short)5 });
            // Left
            triangles.Add(new Triangle { Index0 = (short)7, Index1 = (short)2, Index2 = (short)6 });
            triangles.Add(new Triangle { Index0 = (short)2, Index1 = (short)7, Index2 = (short)3 });

            

            

            for (int i=0; i < MaxIteration; i++) { 
                int l = triangles.Length;
                for (int index = 0; index < l; index++)
                {
                    float3 pa = vertices[triangles[index].Index0].Position;
                    float3 pb = vertices[triangles[index].Index1].Position;
                    float3 pc = vertices[triangles[index].Index2].Position;

                    float3 m = math.normalize((pa + pb + pc) / 3); // middle point / average (The first few itarations have points real far away , and taking the average is important (we might be able to skip this for higher iterations when triangles are smaller))
                    if ((math.dot(campos, m) < -0.1)&& (i > 4)) {continue; }  // Dont divide back side (backside is  dot()<0 but we might want a little bit of the back side for the triangles near the back side that share verticies of the backside)

                     
                    float3 newp = math.normalize((float3)((pa + pb) / 2)); // Point that splits triangles     
                    newp = changeHeight(newp);
                    float d = (campos - new Vector3(newp.x, newp.y, newp.z)).magnitude;// distance
                    // If the triangel is already devided devide it (if there is a point in the middle of the hypotenus) if not then create the new point and then divide it
                    int p;
                    if (!vertexDict.TryGetValue(newp, out p))
                    {
                        if (!doBissect(distance: d, iteration: i)) {continue;} // If dont generate then dont
                        p = vertices.Length;
                        vertexDict[newp] = p;
                        vertices.Add(new Vertex { Position = newp }) ;//6
                    }
                    // We need to destroy the old triangle and add 2 new triangles 
                    triangles.Add(new Triangle { Index0 = triangles[index].Index1, Index1 = triangles[index].Index2, Index2 = (short)p });
                    triangles[index] = new Triangle { Index1 = triangles[index].Index0, Index0 = triangles[index].Index2, Index2 = (short)p };
                }
            }

            

            // Configure mesh data
            var vertexAttributeDescriptor = CreateVertexAttributeDescriptor();
            MeshData.SetVertexBufferParams(vertices.Length, vertexAttributeDescriptor);
            MeshData.SetIndexBufferParams(triangles.Length * 3, IndexFormat.UInt16);
            
            // Apply vertices 
            var vertexBuffer = MeshData.GetVertexData<Vertex>();
            UnsafeUtility.MemCpy(vertexBuffer.GetUnsafePtr(), vertices.GetUnsafeReadOnlyPtr(), (long)vertices.Length * UnsafeUtility.SizeOf<Vertex>());

            // Apply Indices
            var indexBuffer = MeshData.GetIndexData<short>();
            UnsafeUtility.MemCpy(indexBuffer.GetUnsafePtr(), triangles.GetUnsafeReadOnlyPtr(), (long)triangles.Length * UnsafeUtility.SizeOf<Triangle>());

            // Configure sub mesh
            var subMesh = new SubMeshDescriptor(0, triangles.Length * 3)
            {
                topology = MeshTopology.Triangles,
                vertexCount = vertices.Length
            };
            MeshData.subMeshCount = 1;
            MeshData.SetSubMesh(0, subMesh);
        }
        private NativeArray<VertexAttributeDescriptor> CreateVertexAttributeDescriptor()
        {
            return new NativeArray<VertexAttributeDescriptor>(4, Allocator.Temp)
            {
                // ReSharper disable RedundantArgumentDefaultValue
                [0] = new(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                [1] = new(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                [2] = new(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
                [3] = new(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
            };
        }
        float noise(float3 point)
        {            
            return (Mathf.PerlinNoise(point.x, point.y) +Mathf.PerlinNoise(point.y, point.x) +Mathf.PerlinNoise(point.z, point.y) + Mathf.PerlinNoise(point.y, point.z) + Mathf.PerlinNoise(point.z, point.x) + Mathf.PerlinNoise(point.x, point.z)) / 6;
        }


        float3 changeHeight(float3 point)
        {
            float noiseToAdd = 0;
            float r = roughtness;
            float s = strenght;
            for (int i = 0; i < layernumber; i++)
            {
                noiseToAdd += noise(point * r + center) * s;
                r *= roughtnesschange;
                s *= strenghtchange;
            }
            return (noiseToAdd + 1) * Vector3.Normalize(point) * 1;
        }
        void SetHeights(int id,ref NativeList<Vertex> vertices)
        {
            var p = vertices[id];
            var pp = p.Position;

            /* Craters
            //p.Position = new float3(pp.x * (1+Mathf.Sin(pp.y*v1 + v2)*v3),pp.y ,pp.z * (1 + Mathf.Sin(pp.y * v1 +v2) * v3)) ;
            float3 ppp = v4 - pp;
            float3 pppp = (Vector3)v4 - (Vector3.Normalize(ppp) * v1);

            //while (Vector3.Magnitude(ppp) < v1) { pp *= 0.9f; ppp = v4 - pp; }
            if (Vector3.Magnitude(ppp) < v1) { p.Position =pppp; p.Normal = (Vector3.Normalize((Vector3)(v4 - p.Position))); p.UV = new float2(p.Position.x, p.Position.z); }
            */
            float noiseToAdd = 0;
            float r = roughtness;
            float s = strenght;
            for (int i = 0; i < layernumber; i++)
            {
                noiseToAdd += noise(pp * r + center) * s;
                r*= roughtnesschange;
                s *= strenghtchange;
            }
            p.Position = (noiseToAdd+1) * p.Position * 1; // 1= radius
            /*
            noiseToAdd +=noise(pp * varriable1,p1) * influences1; // 
            // if (multiplier>v3)
            noiseToAdd += Mathf.Pow((1-Mathf.Abs(noise(pp* varriable2,p2))),v2) * influences2; // Rigid Noise, not working

            p.Position = Vector3.Normalize(p.Position) * (Vector3.Magnitude(p.Position) + noiseToAdd); // Add Noise
            if (Vector3.Magnitude(p.Position) < v1) { p.Position = Vector3.Normalize(p.Position) * v1; } // Minimum floor
            */
            //p.UV = new Vector2(Vector3.Magnitude(p.Position)-0.75f, Vector3.Magnitude(p.Position) - 0.75f); // This isnt real well done -> Setting uv according to height
            vertices[id] = p;
        }
    }
}