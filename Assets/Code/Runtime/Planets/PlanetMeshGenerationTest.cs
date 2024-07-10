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

namespace PLE.Prototype.Runtime.Code.Runtime.Planets
{
    // This class is only for testing, it isn't ECS and also not a proper unit test
    public class PlanetMeshGenerationTest : MonoBehaviour
    {
        [SerializeField]
        public Material Material;
        public Shader debugShader;
        public bool ShaderOrMaterial;
        public bool ConstantUpdate; // If not then update only when cam pos changes

        private Mesh mesh;
        private GameObject previewGameObject;
        
        [Range(0,15)]
        public int MaxIteration;
        public Transform cameraPosition;
        private Vector3 lastpos;
        public float floorheight;
        public parameters parameter;

        private NativeList<Vertex> vertices;
        private NativeList<Triangle> triangles;
        private NativeHashMap<float3, int> vertexToIndex;

        [System.Serializable]
        public class parameters
        {
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
        }

        private void OnDestroy()
        {
            if(mesh)
                Destroy(mesh);
            
            if(previewGameObject)
                Destroy(previewGameObject);
            
            //if(debugMaterial)
            //    Destroy(debugMaterial);

            vertices.Dispose();
            triangles.Dispose();
            vertexToIndex.Dispose();
        }

        private unsafe void Update()
        {
            if (!ConstantUpdate) { if (lastpos == cameraPosition.position) return; }
            lastpos = cameraPosition.position;
            
            vertices.Clear();
            triangles.Clear();
            vertexToIndex.Clear();

            initializeVariables(triangles: ref triangles,vertices: ref vertices, vertexToIndex: ref vertexToIndex);

            //EntityManager manager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var job = new PlanetMeshGenerationJob
            {
                Vertices = vertices,
                Triangles = triangles,
                MaxIteration = MaxIteration,
                campos = cameraPosition.position,
                roughtness = parameter.roughtness,
                strenght =  parameter.strenght,
                center = parameter.center,  
                layernumber = parameter.layernumber,
                roughtnesschange = parameter.roughtnesschange,
                strenghtchange = parameter.strenghtchange,
                centerOffset = parameter.centerOffset,
                selfposition = float3.zero,
                VertexToIndex = vertexToIndex,
                floorheight = floorheight
            };
            job.Run();
            
            if(!mesh) // I've changed this so that the mesh is reused instead of recreated :) ~UnknownDude
                mesh = new Mesh();
            
            var meshDataArray = Mesh.AllocateWritableMeshData(mesh);
            var meshData = meshDataArray[0];
            
            // Configure mesh data
            var vertexAttributeDescriptor = CreateVertexAttributeDescriptor();
            meshData.SetVertexBufferParams(vertices.Length, vertexAttributeDescriptor);
            meshData.SetIndexBufferParams(triangles.Length * 3, IndexFormat.UInt16);
            
            // Apply Vertices 
            var vertexBuffer = meshData.GetVertexData<Vertex>();
            UnsafeUtility.MemCpy(vertexBuffer.GetUnsafePtr(), vertices.GetUnsafeReadOnlyPtr(), (long)vertices.Length * UnsafeUtility.SizeOf<Vertex>());

            // Apply Indices
            var indexBuffer = meshData.GetIndexData<short>();
            UnsafeUtility.MemCpy(indexBuffer.GetUnsafePtr(), triangles.GetUnsafeReadOnlyPtr(), (long)triangles.Length * UnsafeUtility.SizeOf<Triangle>());

            // Configure sub mesh
            var subMesh = new SubMeshDescriptor(0, triangles.Length * 3)
            {
                topology = MeshTopology.Triangles,
                vertexCount = vertices.Length
            };
            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, subMesh);
        
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
            
            
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            previewGameObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            if (ShaderOrMaterial)
                previewGameObject.GetComponent<MeshRenderer>().sharedMaterial = Material;
            else
                previewGameObject.GetComponent<MeshRenderer>().sharedMaterial = new Material(debugShader);
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
            noiseToAdd = Mathf.Max(0, noiseToAdd - floorheight);
            return (noiseToAdd + 1) * Vector3.Normalize(point) * 1;
        }
    }
}
