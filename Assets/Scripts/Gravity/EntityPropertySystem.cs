using Unity.Burst;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

public partial struct EntityPropertySystem : ISystem
{
    public void OnCreate(ref SystemState state) 
    {
        state.RequireForUpdate<EntityProperty>();
    }

    public void OnUpdate(ref SystemState state)
    {
        state.Enabled = false;

        foreach (var (entityProperty, renderBounds) in 
            SystemAPI.Query<RefRW<EntityProperty>, RefRO<RenderBounds>>())
        {
            var extents = renderBounds.ValueRO.Value.Extents;
            entityProperty.ValueRW.size = extents;
            entityProperty.ValueRW.radius = extents.x; // extents[0] should == extents[1]
            entityProperty.ValueRW.height = extents.y;
        }
    }
}
