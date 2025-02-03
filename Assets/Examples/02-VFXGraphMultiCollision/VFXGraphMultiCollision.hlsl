//
// Updates one particle, for a given collider
//
void UpdatePositionAndVelocity(
                inout float3 position, 
                inout float3 velocity, 
                in float4 sphere,
                in float inheritVelocityRatio,
                float deltaTime
          )
{
    float3 spherePos = sphere.xyz;
    float sphereRadius = sphere.w;
    
    if(distance(position, spherePos) < sphereRadius)
    {
        float3 delta = position - spherePos;
        float sling = sphereRadius - length(delta);
        delta = normalize(delta);
        position = spherePos + delta * sphereRadius;
        velocity += inheritVelocityRatio * delta * sling / max(deltaTime, 0.01666f);
    }
}

// ENTRYPOINT OF THE CUSTOM HLSL 
// =============================
// Iterates through all colliders
// and Update Particles
void UpdateAllPositionsAndVelocity(
    inout VFXAttributes attributes , 
    in StructuredBuffer<float4> collisions, 
    in uint collisionCount, 
    in float inheritVelocityRatio,
    in float deltaTime)
{
    float3 position = attributes.position;
    float3 velocity = attributes.velocity;
    
    for (uint i = 0; i < collisionCount; i++)
    {
        float4 sphere = collisions[i];
        UpdatePositionAndVelocity(position, velocity, sphere, inheritVelocityRatio, deltaTime);
    }
    
    attributes.position = position;
    attributes.velocity = velocity;
}