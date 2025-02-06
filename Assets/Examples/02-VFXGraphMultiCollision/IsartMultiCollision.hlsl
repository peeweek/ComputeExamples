

void CollideWithManySpheres(inout VFXAttributes attributes, in StructuredBuffer<float4> buffer, in int count)
{
    for (int i = 0; i < count; i++)
    {
        // Collide with one sphere
        float3 pos = attributes.position;
        float4 data = buffer[i];
        
        float3 colliderPos = data.xyz;
        float colliderRadius = data.w + (attributes.size / 2.0);
        
        float3 delta = pos - colliderPos;
        float dist = length(delta);
        
        if(dist < colliderRadius)
        {
            float3 dir = normalize(delta);
            attributes.position = colliderPos + dir * colliderRadius;
        }
    }

}