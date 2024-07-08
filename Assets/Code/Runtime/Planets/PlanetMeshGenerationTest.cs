using System;
using PLE.Prototype.Runtime.Code.Runtime.Planets.Jobs;
using Unity.Jobs;
using UnityEngine;
using Unity.Entities;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;
using Unity.Collections;
using System.Linq;
using UnityEditor.Build;
using Unity.Mathematics;

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
        }
        private void Update()
        {
            if (!ConstantUpdate) { if (lastpos == cameraPosition.position) return; }
            lastpos = cameraPosition.position;
            mesh = new Mesh();
            var meshDataArray = Mesh.AllocateWritableMeshData(mesh);

            //EntityManager manager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var job = new PlanetMeshGenerationJob
            {
                MeshData = meshDataArray[0],
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

        private void OnDestroy()
        {
            if(mesh)
                Destroy(mesh);
            
            if(previewGameObject)
                Destroy(previewGameObject);
            
            //if(debugMaterial)
            //    Destroy(debugMaterial);
        }
    }
}
