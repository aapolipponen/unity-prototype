using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

// TODO: Patched Conics

public struct Gravitable : IComponentData { }

public partial struct GravitySystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        // Ensure the system only updates if there are entities with the GravityTAGa component
        state.RequireForUpdate<GravityTAGa>();
    }

    public void OnUpdate(ref SystemState state)
    {
        double G = 6.67 * 10E-11;

        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        foreach ((RefRO<PhysicsMass> physicsMass, RefRO<LocalToWorld> localToWorld, RefRW<GravityTAGa> gravityComponent, RefRW<PhysicsVelocity> velocity) in SystemAPI.Query<RefRO<PhysicsMass>, RefRO<LocalToWorld>, RefRW<GravityTAGa>, RefRW<PhysicsVelocity>>())
        {
            // Apply initial force
            if (gravityComponent.ValueRO.t)
            {
                velocity.ValueRW.Linear = gravityComponent.ValueRO.force;
                gravityComponent.ValueRW.t = false;
            }

            float3 currentPosition = localToWorld.ValueRO.Position;
            float objectMass = 1 / physicsMass.ValueRO.InverseMass;

            // Check if the object is orbiting another body
            if (gravityComponent.ValueRO.gravitingBody != Entity.Null)
            {
                float3 targetPosition = entityManager.GetComponentData<LocalToWorld>(gravityComponent.ValueRO.gravitingBody).Position;
                float targetMass = 1 / entityManager.GetComponentData<PhysicsMass>(gravityComponent.ValueRO.gravitingBody).InverseMass;

                float3 direction = targetPosition - currentPosition;
                float distanceSquared = math.lengthsq(direction);
                float gravitationalForce = (float)G * objectMass * targetMass / distanceSquared;
                direction = math.normalize(direction);  // Normalize direction for accurate force application

                // Handle velocity matching if in CopyCat mode
                if (gravityComponent.ValueRO.copycat)
                {
                    float3 currentTargetVelocity = entityManager.GetComponentData<PhysicsVelocity>(gravityComponent.ValueRO.gravitingBody).Linear;
                    velocity.ValueRW.Linear += -gravityComponent.ValueRW.lastparentvelocity + currentTargetVelocity + SystemAPI.Time.DeltaTime * direction * gravitationalForce / objectMass;
                    gravityComponent.ValueRW.lastparentvelocity = currentTargetVelocity;  // Update last known velocity
                }
                else
                {
                    velocity.ValueRW.Linear += SystemAPI.Time.DeltaTime * direction * gravitationalForce / objectMass;
                }
            }
            else
            {
                float3 maximumForce = float3.zero;
                Entity closestGravitableBody = Entity.Null;
                float closestDistanceSquared = float.MaxValue;

                // Iterate over all gravitable bodies to find the closest one
                foreach ((RefRO<PhysicsMass> otherPhysicsMass, RefRO<LocalToWorld> otherLocalToWorld, RefRO<Gravitable> _) in SystemAPI.Query<RefRO<PhysicsMass>, RefRO<LocalToWorld>, RefRO<Gravitable>>())
                {
                    float3 otherPosition = otherLocalToWorld.ValueRO.Position;
                    if (math.all(currentPosition == otherPosition)) { continue; }  // Skip self

                    float3 direction = otherPosition - currentPosition;
                    float distanceSquared = math.lengthsq(direction);

                    // Check if this body is the closest one so far
                    if (distanceSquared < closestDistanceSquared)
                    {
                        closestDistanceSquared = distanceSquared;
                        closestGravitableBody = gravityComponent.ValueRO.gravitingBody;
                    }
                }

                // Calculate gravitational force from the closest body
                if (closestGravitableBody != Entity.Null)
                {
                    float3 closestPosition = entityManager.GetComponentData<LocalToWorld>(closestGravitableBody).Position;
                    float closestMass = 1 / entityManager.GetComponentData<PhysicsMass>(closestGravitableBody).InverseMass;

                    float3 direction = closestPosition - currentPosition;
                    float gravitationalForce = (float)G * objectMass * closestMass / closestDistanceSquared;
                    direction = math.normalize(direction);  // Normalize direction

                    maximumForce = gravitationalForce * direction;
                }

                // Apply the calculated gravitational force to the velocity
                velocity.ValueRW.Linear += SystemAPI.Time.DeltaTime * maximumForce / objectMass;
            }
        }
    }
}
