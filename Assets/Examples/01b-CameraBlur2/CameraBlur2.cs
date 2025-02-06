using UnityEngine;
using UnityEngine.Experimental.Rendering;
public class CameraBlur2 : MonoBehaviour
{
    [Range(1,50)]
    public int steps = 1;

    public ComputeShader blurCompute;

    public RenderTexture inputTexture;
    public RenderTexture outputTexture;
    public RenderTexture outputTexture2;

    public MeshRenderer previewMeshRenderer;


    static readonly int widthProperty = Shader.PropertyToID("width");
    static readonly int heightProperty = Shader.PropertyToID("height");

    static readonly int inTextureProperty = Shader.PropertyToID("Input");
    static readonly int outTextureProperty = Shader.PropertyToID("Result");

    static readonly int previewTextureProperty = Shader.PropertyToID("_BaseMap");

    int numX; 
    int numY;

    Material mat;

    private void OnEnable()
    {
        Debug.Assert(blurCompute != null);
        Debug.Assert(inputTexture != null);

        this.outputTexture = new RenderTexture(inputTexture.width,
            inputTexture.height,
            0,
            GraphicsFormat.R32G32B32A32_SFloat, 0);

        // Enables using this texture as UAV
        this.outputTexture.enableRandomWrite = true;
        this.outputTexture.name = $"Blur Output : {outputTexture.width}×{outputTexture.height}";

        this.outputTexture2 = new RenderTexture(this.outputTexture);

        numX = outputTexture.width / 4;
        numY = outputTexture.height / 4;

        blurCompute.SetInt(widthProperty, outputTexture.width);
        blurCompute.SetInt(heightProperty, outputTexture.height);

        // Backup material copy to prevent leaking at Update()
        mat = this.previewMeshRenderer?.material;
    }

    private void OnDisable()
    {
        this.outputTexture.Release();
    }

    private void LateUpdate()
    {
        Debug.Assert(blurCompute != null);
        Debug.Assert(inputTexture != null);
        Debug.Assert(outputTexture != null);

        for (int i = 0; i < steps; i++)
        {
            // Set Texture for kernel

            blurCompute.SetTexture(0, inTextureProperty, i == 0 ? this.inputTexture : (i % 2 == 0 ? this.outputTexture2 : this.outputTexture) );
            blurCompute.SetTexture(0, outTextureProperty, i % 2 == 0 ? this.outputTexture : this.outputTexture2);
            blurCompute.Dispatch(0, numX, numY, 1);
        }

        // Enable Correct display on Preview
        mat.SetTexture(previewTextureProperty, steps % 2 == 0 ? this.outputTexture2 : this.outputTexture);

    }
}
