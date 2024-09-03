using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;
using Unity.Entities.UniversalDelegates;
using Unity.Physics;
using Unity.Transforms;
using Unity.VisualScripting;

public partial struct OnRailBodySystem : ISystem
{
    public void OnCreate(ref SystemState state) { state.RequireForUpdate<OnRailBodyComponent>(); }

    public void OnUpdate(ref SystemState state) {
        
        foreach ((RefRW< LocalTransform> localTransform, RefRW<OnRailBodyComponent> onRailBodyComponent) in SystemAPI.Query<RefRW<LocalTransform>, RefRW<OnRailBodyComponent>>())
        {
            // Some rescoures
            // https://space.stackexchange.com/questions/8911/determining-orbital-position-at-a-future-point-in-time
            // https://ssd.jpl.nasa.gov/planets/approx_pos.html

            float meanLongitudeAtEpoch = onRailBodyComponent.ValueRO.epochMeanLongitude; // Mean longitude at epoch T0
            float periapsisLongitude = onRailBodyComponent.ValueRO.periapsisLongitude; // Longitude of periapsis
            float ascendingNodeLongitude = onRailBodyComponent.ValueRO.ascendingNodeLongitude; // Longitude of the ascending node
            float eccentricity = onRailBodyComponent.ValueRO.eccentricity; // Orbital eccentricity
            float semiMajorAxis = onRailBodyComponent.ValueRO.semiMajorAxis; // Semi-major axis
            float inclination = onRailBodyComponent.ValueRO.inclination; // Orbital inclination

            // Mean motion: Revolution / Time
            float meanMotion = (2 * Mathf.PI) / onRailBodyComponent.ValueRO.years;

            // Update time and calculate mean longitude
            onRailBodyComponent.ValueRW.time += Time.deltaTime * onRailBodyComponent.ValueRO.multiplier;
            float meanLongitude = (meanLongitudeAtEpoch + meanMotion * (onRailBodyComponent.ValueRO.time / (3600 * 24 * 365.25f))) % 360;

            // Calculate mean anomaly
            float meanAnomaly = meanLongitude - periapsisLongitude;

            // Calculate argument of periapsis with mean anomaly, https://www.met.reading.ac.uk/~ross/Documents/OrbitNotes.pdf
            float argumentOfPeriapsis = periapsisLongitude - ascendingNodeLongitude;

            // Everything under here basically comes from https://space.stackexchange.com/questions/8911/determining-orbital-position-at-a-future-point-in-time

            // A common inital guess for eccentricity (A circular orbit)
            var eccentricAnomaly = meanAnomaly;
            
            // Numerically solve Kepler's equation using the Newton-Raphson method
            int iteration = 0;
            while (true)
            {
                float deltaEccentricAnomaly = (eccentricAnomaly - eccentricity * Mathf.Sin(eccentricAnomaly) - meanAnomaly) / (1 - eccentricity * Mathf.Cos(eccentricAnomaly));
                eccentricAnomaly -= deltaEccentricAnomaly;
                iteration++;
                if (Mathf.Abs(deltaEccentricAnomaly) < 1e-5) break;
                if (iteration > 10) break; // Prevent infinite loop
            }

            // Calculate position in the orbital plane (P and Q are 2D coordinates)
            float P = semiMajorAxis * (Mathf.Cos(eccentricAnomaly) - eccentricity);
            float Q = semiMajorAxis * Mathf.Sin(eccentricAnomaly) * Mathf.Sqrt(1 - Mathf.Pow(eccentricity, 2));

            // Rotate by argument of periapsis
            float xOrbitalPlane = Mathf.Cos(argumentOfPeriapsis) * P - Mathf.Sin(argumentOfPeriapsis) * Q;
            float yOrbitalPlane = Mathf.Sin(argumentOfPeriapsis) * P + Mathf.Cos(argumentOfPeriapsis) * Q;

            // Apply inclination rotation
            float z = Mathf.Sin(inclination) * yOrbitalPlane;
            float yInclination = Mathf.Cos(inclination) * yOrbitalPlane;

            // Rotate by longitude of ascending node
            float xTemp = xOrbitalPlane;
            float x = Mathf.Cos(ascendingNodeLongitude) * xTemp - Mathf.Sin(ascendingNodeLongitude) * yInclination;
            float y = Mathf.Sin(ascendingNodeLongitude) * xTemp + Mathf.Cos(ascendingNodeLongitude) * yInclination;

            // Update the position of the body
            localTransform.ValueRW.Position = new float3(x, y, z);
        }        
    }
}
