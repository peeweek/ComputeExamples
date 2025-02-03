using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.VFX.Utility;

[RequireComponent (typeof(VisualEffect))]
[ExecuteAlways]
public class GatherMultiCollisionSpheres : MonoBehaviour
{
    public ExposedProperty BufferProperty;
    public ExposedProperty CountProperty;


    GraphicsBuffer m_Buffer;
    Vector4[] m_Data;
    public SphereCollider[] colliders;

    void OnEnable()
    {
        m_Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, colliders.Length, sizeof(float) * 4);
        m_Data = new Vector4[colliders.Length];

        UpdateDataAndUpload();
        
        VisualEffect effect = GetComponent<VisualEffect>();
        effect.SetUInt(CountProperty, (uint)colliders.Length);
        effect.SetGraphicsBuffer(BufferProperty, m_Buffer);
    }

    void UpdateDataAndUpload()
    {
        for (int i = 0; i < colliders.Length; i++)
        {
            SphereCollider collider = colliders[i];
            Vector3 pos = collider.transform.position;
            m_Data[i] = new Vector4(pos.x, pos.y, pos.z, collider.radius * collider.transform.localScale.x);
        }
        m_Buffer.SetData(m_Data);
    }

    // Update is called once per frame
    void Update()
    {
        UpdateDataAndUpload();
    }
}
