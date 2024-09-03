using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;
public class EntityPropertyAuthoring : MonoBehaviour
{
    public float mass;
    public float radius;
    public float height;
    public float3 size;

    public class Baker : Baker<EntityPropertyAuthoring>
    {
        public override void Bake(EntityPropertyAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new EntityProperty 
            { 
                mass = authoring.mass, 
                radius = authoring.radius, 
                height = authoring.height,
                size = authoring.size
            });
        }
    }
}

public struct EntityProperty : IComponentData
{
    public float mass;
    public float radius;
    public float height;
    public float3 size;
}
