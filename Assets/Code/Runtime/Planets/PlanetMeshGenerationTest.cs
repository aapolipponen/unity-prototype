using System;
using PLE.Prototype.Runtime.Code.Runtime.Planets.Jobs;
using Unity.Jobs;
using UnityEngine;
using Unity.Entities;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;
using Unity.Collections;
using System.Linq;
using PLE.Prototype.Runtime.Code.Runtime.Planets.Data;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor.Build;
using Unity.Mathematics;
using UnityEngine.Rendering;
using static PLE.Prototype.Runtime.Code.Runtime.Planets.PlanetMeshGenerationTest;
using Codice.Client.BaseCommands.BranchExplorer;

namespace PLE.Prototype.Runtime.Code.Runtime.Planets
{
    // This class is only for testing, it isn't ECS and also not a proper unit test
    public class PlanetMeshGenerationTest : MonoBehaviour
    {
        [SerializeField]
        public Material Material;
        public Shader debugShader;
        public bool ShaderOrMaterial;

        public Transform cameraPosition;
        public bool ConstantUpdate; // If not then update only when cam pos changes
        [Range(0,3)]
        public int chunks = 0; // if this is changed while running the variables would need to be reset // so dont change it when running now
        private Mesh mesh;
        private GameObject previewGameObject;
        private GameObject[] previewGameObjects;
        private Mesh[] meshs;
        private NativeList<NativeList<Vertex>> verticess;
        private NativeList<NativeList<Triangle>> triangless;
        private NativeList<NativeHashMap<float3, int>> vertexToIndexs;

        [Range(0,15)]
        public int MaxIteration;
        public RenderDistance[] renderDistancesInput;

        public bool deleteRepetedVertex; // this dont work anymore

        [Range(0,15)]
        public int backsideIterations;
        public float backsideLimit;
        [Range(0, 15)]
        public int overhorizonIterations;
        public float overhorizonLimit;
        public float overhorizonLimit2;

        public float radius;
        public GameObject water;
        public parameters parameter;

        private NativeList<int> renderIterations;
        private NativeList<float> renderDistances;
        private NativeList<Vertex> vertices;
        private NativeList<Triangle> triangles;
        private NativeHashMap<float3, int> vertexToIndex;
        private Vector3 lastpos;

        public NativeList<int> iterationMinimumPerVertex;
        [System.Serializable]
        public class RenderDistance
        {
            public float distance;
            [Range(0, 15)]
            public int iteration;
        }


        [System.Serializable]
        public class parameters
        {
            public float floorheight;
            [Range(1, 10)]
            public int layernumber;

            public float strenght;
            public float roughtness;

            [Range(1, 10)]
            public float roughtnesschange;
            [Range(0, 1)]
            public float strenghtchange;

            public float3 center;
            public float3 centerOffset;
        }
        private void Start() {
            previewGameObject = new GameObject(nameof(PlanetMeshGenerationTest));
            previewGameObject.AddComponent<MeshFilter>();
            previewGameObject.AddComponent<MeshRenderer>();

            vertices = new NativeList<Vertex>(Allocator.Persistent);
            triangles = new NativeList<Triangle>(Allocator.Persistent);
            vertexToIndex = new NativeHashMap<float3, int>(23000, Allocator.Persistent);
            renderIterations = new NativeList<int>(Allocator.Persistent);
            renderDistances = new NativeList<float>(Allocator.Persistent);
            iterationMinimumPerVertex = new NativeList<int>(Allocator.Persistent);


            verticess = new NativeList<NativeList<Vertex>>(Allocator.Persistent);
            triangless = new NativeList<NativeList<Triangle>>(Allocator.Persistent);
            vertexToIndexs = new NativeList<NativeHashMap<float3, int>>(10000, Allocator.Persistent); // Idk what the value for 10k should be


            previewGameObjects = new GameObject[(int)(Math.Pow(2, chunks) * 12)];
            for (int i = 0; i < Math.Pow(2, chunks) *12 ; i++)
            {
                previewGameObjects[i] = new GameObject(nameof(PlanetMeshGenerationTest));
                previewGameObjects[i].AddComponent<MeshFilter>();
                previewGameObjects[i].AddComponent<MeshRenderer>();
                triangless.Add( new NativeList<Triangle>(Allocator.Persistent));
                verticess.Add(new NativeList<Vertex>(Allocator.Persistent));
                vertexToIndexs.Add( new NativeHashMap<float3, int>(10000, Allocator.Persistent));
            }
            meshs = new Mesh[(int)(Math.Pow(2, chunks) * 12)];
        }

        private void OnDestroy()
        {
            if(mesh)
                Destroy(mesh);
            
            if(previewGameObject)
                Destroy(previewGameObject);
            
            foreach (var gameo in previewGameObjects)
            {
                if(gameo)
                    Destroy(gameo);
            }
            //if(debugMaterial)
            //    Destroy(debugMaterial);

            vertices.Dispose();
            triangles.Dispose();
            vertexToIndex.Dispose();
            renderDistances.Dispose();
            renderIterations.Dispose();
        }

        private unsafe void Update()
        {
            if (!ConstantUpdate) { if (lastpos == cameraPosition.position) return; }
            lastpos = cameraPosition.position;

            water.GetComponent<Transform>().localScale = new Vector3(2.05f* radius, 2.05f * radius, 2.05f * radius);

            for (int i = 0; i < Math.Pow(2, chunks) * 12; i++)
            {
                verticess[i].Clear();
                triangless[i].Clear();
                vertexToIndexs[i].Clear();
            }
            vertices.Clear();
            triangles.Clear();
            vertexToIndex.Clear();
            renderDistances.Clear();
            renderIterations.Clear();

            foreach(var value in renderDistancesInput)
            {
                renderDistances.Add(value.distance);
                renderIterations.Add(value.iteration);
            }
            


            initializeVariables(triangles: ref triangles,vertices: ref vertices, vertexToIndex: ref vertexToIndex);
            
            // divide
            var job = new PlanetMeshGenerationJob
            {
                Vertices = vertices,
                Triangles = triangles,
                MaxIteration = chunks,
                campos = cameraPosition.position,
                roughtness = parameter.roughtness,
                strenght = parameter.strenght,
                center = parameter.center,
                layernumber = parameter.layernumber,
                roughtnesschange = parameter.roughtnesschange,
                strenghtchange = parameter.strenghtchange,
                centerOffset = parameter.centerOffset,
                VertexToIndex = vertexToIndex,
                floorheight = parameter.floorheight,
                deleteRepetedVertex = deleteRepetedVertex,
                renderDistances = renderDistances,
                renderIterations = renderIterations,
                backsideLimit = backsideLimit,
                backsideIterations = backsideIterations,
                overhorizonIterations = overhorizonIterations,
                overhorizonLimit = overhorizonLimit,
                overhorizonLimit2 = overhorizonLimit2,
                IterationMinimumPerVertex = iterationMinimumPerVertex,
                Radius = radius,
                mp = float3.zero

            };
            job.Run();
            

            for (int i = 0; i < Math.Pow(2, chunks) * 12; i++)
            {
                if (!previewGameObjects[i]) { continue; }

                triangless[i].Add(new Triangle { Index0 = 0, Index1 = 1, Index2 = 2 });
                verticess[i].Add(vertices[triangles[i].Index0]);
                verticess[i].Add(vertices[triangles[i].Index1]);
                verticess[i].Add(vertices[triangles[i].Index2]);

                float3 mp = (verticess[i][0].Position + verticess[i][1].Position + verticess[i][2].Position) / 3;

                bool thing = !(math.dot(cameraPosition.position, mp) < 0);
                previewGameObjects[i].SetActive(thing);
                if (!thing) { continue; }
                job = new PlanetMeshGenerationJob
                {
                    Vertices = verticess[i],
                    Triangles = triangless[i],
                    MaxIteration = MaxIteration,
                    campos = cameraPosition.position,
                    roughtness = parameter.roughtness,
                    strenght = parameter.strenght,
                    center = parameter.center,
                    layernumber = parameter.layernumber,
                    roughtnesschange = parameter.roughtnesschange,
                    strenghtchange = parameter.strenghtchange,
                    centerOffset = parameter.centerOffset,
                    VertexToIndex = vertexToIndexs[i],
                    floorheight = parameter.floorheight,
                    deleteRepetedVertex = deleteRepetedVertex,
                    renderDistances = renderDistances,
                    renderIterations = renderIterations,
                    backsideLimit = backsideLimit,
                    backsideIterations = backsideIterations,
                    overhorizonIterations = overhorizonIterations,
                    overhorizonLimit = overhorizonLimit,
                    overhorizonLimit2 = overhorizonLimit2,
                    IterationMinimumPerVertex = iterationMinimumPerVertex,
                    Radius = radius,
                    mp=mp

                };
                job.Run();
                /*
                if (deleteRepetedVertex)
                {
                    for (int k = 0; k < triangless[i].Length; k++)
                    {
                        int p;
                        var vj = triangless[i];
                        var v = vj[k];
                        if (vertexToIndexs[i].TryGetValue(verticess[i][v.Index0].Position, out p)) { v.Index0 = (short)p; }
                        if (vertexToIndexs[i].TryGetValue(verticess[i][v.Index1].Position, out p)) { v.Index1 = (short)p; }
                        if (vertexToIndexs[i].TryGetValue(verticess[i][v.Index2].Position, out p)) { v.Index2 = (short)p; }
                        vj[k] = v;
                        triangless[i] = vj;
                    }
                }*/

                if (!meshs[i]) // I've changed this so that the mesh is reused instead of recreated :) ~UnknownDude
                    meshs[i] = new Mesh();
                var meshDataArrayi = Mesh.AllocateWritableMeshData(meshs[i]);
                var meshDatai = meshDataArrayi[0];

                // Configure mesh data
                var vertexAttributeDescriptori = CreateVertexAttributeDescriptor();
                meshDatai.SetVertexBufferParams(verticess[i].Length, vertexAttributeDescriptori);
                meshDatai.SetIndexBufferParams(triangless[i].Length * 3, IndexFormat.UInt16);

                // Apply Vertices 
                var vertexBufferi = meshDatai.GetVertexData<Vertex>();
                UnsafeUtility.MemCpy(vertexBufferi.GetUnsafePtr(), verticess[i].GetUnsafeReadOnlyPtr(), (long)verticess[i].Length * UnsafeUtility.SizeOf<Vertex>());

                // Apply Indices
                var indexBufferi = meshDatai.GetIndexData<short>();
                UnsafeUtility.MemCpy(indexBufferi.GetUnsafePtr(), triangless[i].GetUnsafeReadOnlyPtr(), (long)triangless[i].Length * UnsafeUtility.SizeOf<Triangle>());

                // Configure sub mesh
                var subMeshi = new SubMeshDescriptor(0, triangless[i].Length * 3)
                {
                    topology = MeshTopology.Triangles,
                    vertexCount = verticess[i].Length
                };
                meshDatai.subMeshCount = 1;
                meshDatai.SetSubMesh(0, subMeshi);

                Mesh.ApplyAndDisposeWritableMeshData(meshDataArrayi, meshs[i]);


                meshs[i].RecalculateBounds();
                meshs[i].RecalculateNormals();
                meshs[i].RecalculateTangents();
                previewGameObjects[i].GetComponent<MeshFilter>().sharedMesh = meshs[i];
                if (ShaderOrMaterial)
                    previewGameObjects[i].GetComponent<MeshRenderer>().sharedMaterial = Material;
                else
                    previewGameObjects[i].GetComponent<MeshRenderer>().sharedMaterial = new Material(debugShader);
                previewGameObjects[i].GetComponent<Transform>().position = mp;
                
            }
            /*
                         if (deleteRepetedVertex)
            {
                for (int i = 0; i < triangles.Length; i++)
                {
                    int p;
                    var v = triangles[i];
                    if (vertexToIndex.TryGetValue(vertices[v.Index0].Position, out p)) { v.Index0 = (short)p; }
                    if (vertexToIndex.TryGetValue(vertices[v.Index1].Position, out p)) { v.Index1 = (short)p; }
                    if (vertexToIndex.TryGetValue(vertices[v.Index2].Position, out p)) { v.Index2 = (short)p; }
                    triangles[i] = v;
                }
            }
            */
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

        private void initializeVariables(ref NativeList<Triangle> triangles, ref NativeList<Vertex> vertices, ref NativeHashMap<float3,int> vertexToIndex) {


            setDefaultCube(ref triangles, ref vertices);

            for (int id = 0; id < vertices.Length; id++)
            {
                Vertex x = vertices[id];
                x.Position = changeHeight(x.Position);
                vertices[id] = x;
                vertexToIndex[x.Position] = id;
            }
        }

        private void setDefaultCube(ref NativeList<Triangle> triangles, ref NativeList<Vertex> vertices)
        {
            // TOP   FRONT  BACK    RIGHT   LEFT (Views)
            // 20    02     64      40      26  
            // 64    13     75      51      37

            vertices.Add(new Vertex { Position = math.normalize(new float3(+1, +1, +1)) });//0
            vertices.Add(new Vertex { Position = math.normalize(new float3(+1, -1, +1)) });//1

            vertices.Add(new Vertex { Position = math.normalize(new float3(-1, +1, +1)) });//2
            vertices.Add(new Vertex { Position = math.normalize(new float3(-1, -1, +1)) });//3

            vertices.Add(new Vertex { Position = math.normalize(new float3(+1, +1, -1)) });//4
            vertices.Add(new Vertex { Position = math.normalize(new float3(+1, -1, -1)) });//5

            vertices.Add(new Vertex { Position = math.normalize(new float3(-1, +1, -1)) });//6
            vertices.Add(new Vertex { Position = math.normalize(new float3(-1, -1, -1)) });//7
            /*
            // TOP
            triangles.Add(new Triangle { Index0 = (short)4, Index1 = (short)2, Index2 = (short)0 }); // top1
            triangles.Add(new Triangle { Index0 = (short)2, Index1 = (short)4, Index2 = (short)6 }); // top2
            // BOT
            triangles.Add(new Triangle { Index0 = (short)3, Index1 = (short)5, Index2 = (short)1 }); // bot1
            triangles.Add(new Triangle { Index0 = (short)5, Index1 = (short)3, Index2 = (short)7 }); // bot2 
            // FRONT
            triangles.Add(new Triangle { Index0 = (short)3, Index1 = (short)0, Index2 = (short)2 }); // front1
            triangles.Add(new Triangle { Index0 = (short)0, Index1 = (short)3, Index2 = (short)1 }); // front2
            // BACK
            triangles.Add(new Triangle { Index0 = (short)5, Index1 = (short)6, Index2 = (short)4 });
            triangles.Add(new Triangle { Index0 = (short)6, Index1 = (short)5, Index2 = (short)7 });
            // RIGHT
            triangles.Add(new Triangle { Index0 = (short)1, Index1 = (short)4, Index2 = (short)0 });
            triangles.Add(new Triangle { Index0 = (short)4, Index1 = (short)1, Index2 = (short)5 });
            // Left
            triangles.Add(new Triangle { Index0 = (short)7, Index1 = (short)2, Index2 = (short)6 });
            triangles.Add(new Triangle { Index0 = (short)2, Index1 = (short)7, Index2 = (short)3 });*/


            // This configuration (top1, bot1 , front1 .... left1, top2 , bot2 ... left2) will make it that the first half of the list have same "direction", later when cutting them in half (as long as we cut all of them in half (without worring about the camera distance)) the first half of the list will need the creation of a new point and the second half will not need to add a new point but will use the point created by the first half, (so if we run the code in parallele , the first half will edit the vertices list and the second half will not and will need the first half to have been ran first)
            triangles.Add(new Triangle { Index0 = (short)4, Index1 = (short)2, Index2 = (short)0 }); triangles.Add(new Triangle { Index0 = (short)3, Index1 = (short)5, Index2 = (short)1 }); triangles.Add(new Triangle { Index0 = (short)3, Index1 = (short)0, Index2 = (short)2 }); triangles.Add(new Triangle { Index0 = (short)5, Index1 = (short)6, Index2 = (short)4 }); triangles.Add(new Triangle { Index0 = (short)1, Index1 = (short)4, Index2 = (short)0 }); triangles.Add(new Triangle { Index0 = (short)7, Index1 = (short)2, Index2 = (short)6 });
            triangles.Add(new Triangle { Index0 = (short)2, Index1 = (short)4, Index2 = (short)6 }); triangles.Add(new Triangle { Index0 = (short)5, Index1 = (short)3, Index2 = (short)7 }); triangles.Add(new Triangle { Index0 = (short)0, Index1 = (short)3, Index2 = (short)1 }); triangles.Add(new Triangle { Index0 = (short)6, Index1 = (short)5, Index2 = (short)7 }); triangles.Add(new Triangle { Index0 = (short)4, Index1 = (short)1, Index2 = (short)5 }); triangles.Add(new Triangle { Index0 = (short)2, Index1 = (short)7, Index2 = (short)3 });
        }

        // idk if I should do this or if I should modify the function in the job and call it from here
        private float PerlinNoise3D(float3 point)
        {
            return (Mathf.PerlinNoise(point.x, point.y) + Mathf.PerlinNoise(point.y, point.x) + Mathf.PerlinNoise(point.z, point.y) + Mathf.PerlinNoise(point.y, point.z) + Mathf.PerlinNoise(point.z, point.x) + Mathf.PerlinNoise(point.x, point.z)) / 6;
        }

        private float3 changeHeight(float3 point)
        {

            float noiseToAdd = 0;
            float r = parameter.roughtness;
            float s = parameter.strenght;
            float3 c = parameter.center;
            for (int i = 0; i < parameter.layernumber; i++)
            {
                noiseToAdd += PerlinNoise3D(point * r + c) * s;
                r *= parameter.roughtnesschange;
                s *= parameter.strenghtchange;
                c += parameter.centerOffset;
            }
            noiseToAdd = Mathf.Max(0, noiseToAdd - parameter.floorheight);
            return (noiseToAdd + 1) * Vector3.Normalize(point) * radius;
        }
    }
}
