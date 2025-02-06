using System.Drawing;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.VFX;

[ExecuteAlways]
[RequireComponent(typeof(VisualEffect))]
public class MakeParticleBuffer : MonoBehaviour
{
    [Range(16, 65536)]
    public int particleCount = 256;

    VisualEffect effect;
    static readonly int bufferProperty = Shader.PropertyToID("ParticleBuffer");

    GraphicsBuffer buffer;
    static readonly int particleCountProperty = Shader.PropertyToID("ParticleCount");

    private void OnEnable()
    {
        this.effect = GetComponent<VisualEffect>();
        UpdateBuffer(particleCount);
    }

    void UpdateBuffer(int size)
    {
        this.buffer?.Dispose();
        this.buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, size, 4 * sizeof(float));
        this.effect.SetGraphicsBuffer(bufferProperty, this.buffer);
        this.effect.SetInt(particleCountProperty, size);
    }

    private void OnValidate()
    {
        UpdateBuffer(particleCount);
        this.effect.Reinit();
    }


    private void OnDisable()
    {
        this.buffer.Dispose();
    }

    Vector4[] data = new Vector4[256];

    [ContextMenu("Dump")]
    void DumpData()
    {
        this.buffer.GetData(data);
        for (int i = 0; i < 32; i++)
        {
            Debug.Log(data[i]);
        }
    }
}
