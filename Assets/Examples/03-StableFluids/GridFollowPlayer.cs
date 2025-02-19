using StableFluids;
using UnityEngine;

public class GridFollowPlayer : MonoBehaviour
{
    public Transform target;
    public MeshRenderer previewQuadRenderer;

    [Header("Grid Settings")]
    public Fluid fluid;
    public float GridHeight = 0.4f;
    public float GridStepSize = 0.5f;

    Vector3 oldPosition;
    Vector3 position;
    float scale;

    private void Start()
    {
        Debug.Assert(fluid != null);

        position = Snap(target.position);
        scale = fluid.Resolution * GridStepSize;
        previewQuadRenderer.transform.localScale = new Vector3(scale, scale, scale);
    }


    void LateUpdate()
    {
        oldPosition = position;
        position = Snap(target.position);
        transform.position = position;

        Vector3 delta = position - oldPosition;
        fluid.SetOffset(Mathf.RoundToInt(delta.x/this.GridStepSize),Mathf.RoundToInt(delta.z/this.GridStepSize));
    }

    Vector3 Snap(Vector3 position)
    { 
        return new Vector3(Snap(position.x , this.GridStepSize), position.y + GridHeight, Snap(position.z ,this.GridStepSize)); 
    }

    float Snap(float value, float snap) { return value - value % snap ; }
}
