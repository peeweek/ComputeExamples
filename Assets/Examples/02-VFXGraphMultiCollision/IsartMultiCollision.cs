
using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.VFX.Utility;

[ExecuteAlways]
[RequireComponent(typeof(VisualEffect))]
public class IsartMultiCollision : MonoBehaviour
{
    public ExposedProperty BufferProperty = "SphereColliders";
    public ExposedProperty CountProperty = "Count";

    VisualEffect effect;
    GraphicsBuffer graphicsBuffer;

    public SphereCollider[] colliders;
    Vector4[] data;

    private void OnEnable()
    {
        this.effect = GetComponent<VisualEffect>();
        this.graphicsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, colliders.Length, 4 * sizeof(float));
        this.effect.SetGraphicsBuffer(BufferProperty, this.graphicsBuffer);
        this.effect.SetInt(CountProperty, colliders.Length);
        this.data = new Vector4[colliders.Length];
    }

    private void OnDisable()
    {
        this.graphicsBuffer.Dispose();
    }


    private void Update()
    {
        for (int i = 0; i < colliders.Length; i++)
        {
            var collider = colliders[i];
            var pos = collider.transform.position;
            Vector4 temp_data = data[i];
            temp_data.x = pos.x;
            temp_data.y = pos.y;
            temp_data.z = pos.z;
            temp_data.w = collider.radius * collider.transform.localScale.x;
            data[i] = temp_data;

        }
        this.graphicsBuffer.SetData(data);
    }
}
