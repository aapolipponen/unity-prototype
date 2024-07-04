using Unity.Entities;
using Unity.Mathematics;
using UnityEditor.PackageManager;
using UnityEngine;
public class PlayerBodyTag : MonoBehaviour
{
    public GameObject BodyToAlignTo;
    // Baker
    private class Baker : Baker<PlayerBodyTag>
    {
        public override void Bake(PlayerBodyTag authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new PlayerBody() { AlignToBody = GetEntity(authoring.BodyToAlignTo, TransformUsageFlags.Dynamic) });
        }
    }
}
public struct PlayerBody : IComponentData { public float x;public float3 mouvement; public Entity AlignToBody; }