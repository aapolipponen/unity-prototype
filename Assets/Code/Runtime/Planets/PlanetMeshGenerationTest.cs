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
        public parameters parameter;

        private NativeList<Vertex> vertices;
        private NativeList<Triangle> triangles;

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
        }
        private void Start() {
            previewGameObject = new GameObject(nameof(PlanetMeshGenerationTest));
            previewGameObject.AddComponent<MeshFilter>();
            previewGameObject.AddComponent<MeshRenderer>();

            vertices = new NativeList<Vertex>(Allocator.Persistent);
            triangles = new NativeList<Triangle>(Allocator.Persistent);
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
        }

        private unsafe void Update()
        {
            if (!ConstantUpdate) { if (lastpos == cameraPosition.position) return; }
            lastpos = cameraPosition.position;
            
            vertices.Clear();
            triangles.Clear();

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
                strenghtchange = parameter.strenghtchange
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
    }
}
