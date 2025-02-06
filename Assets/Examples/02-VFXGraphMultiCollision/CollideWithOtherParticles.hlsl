
void FillBufferWithParticles(inout VFXAttributes attributes, in RWStructuredBuffer<float4> buffer)
{
    int id = attributes.particleId;
    buffer[id] = float4(attributes.position, attributes.size * 0.5);
}



void CollideWithOtherParticles(inout VFXAttributes attributes, RWStructuredBuffer<float4> buffer, in float deltaTime, in int particleCount, in float restitution)
{
    //AllMemoryBarrierWithGroupSync();
    
    int id = attributes.particleId;
    float radius = attributes.size * 0.5;
    float3 position = attributes.position;
    float3 vel = attributes.velocity;
    for (int i = 0; i < particleCount; i++)
    {
        if(i == id)
            continue;
        
        float4 data = buffer[i];
        float3 tgtpos = data.xyz;
        float tgtradius = data.w + radius;
        
        float3 delta = position - tgtpos;
        float len = length(delta);
        if (len < tgtradius)
        {
            float3 dir = normalize(delta);
            vel += dir * (tgtradius - len) * restitution;
        }
    }
    
    attributes.velocity = vel;
}