using System.ComponentModel;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

[ExecuteAlways]
public class CameraBlur : MonoBehaviour
{
    public MeshRenderer meshRenderer;
    public ComputeShader computeShader;
    public RenderTexture inTexture;
    public RenderTexture outTexture;

    readonly int inTextureNameID = Shader.PropertyToID("Input");
    readonly int outTextureNameID = Shader.PropertyToID("Result");

    readonly int inTextureWidthID = Shader.PropertyToID("TextureWidth");
    readonly int inTextureHeightID = Shader.PropertyToID("TextureHeight");


    void OnEnable()
    {
        outTexture = new RenderTexture(inTexture.width, inTexture.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        outTexture.enableRandomWrite = true;
        outTexture.name = $"CameraBlur buffer {outTexture.width}x{outTexture.height}";

        if(meshRenderer != null)
        {
            meshRenderer.sharedMaterial.SetTexture("_BaseMap", outTexture);
        }
    }

    public void LateUpdate()
    {
        Debug.Assert(inTexture != null);
        Debug.Assert(outTexture != null);
        Debug.Assert(computeShader != null);

        computeShader.SetTexture(0, inTextureNameID, inTexture);
        computeShader.SetTexture(0, outTextureNameID, outTexture);
        computeShader.SetInt(inTextureWidthID, inTexture.width);
        computeShader.SetInt(inTextureHeightID, inTexture.height);

        // We do 4x4 pixel blur inside a 8x8 pixel window, so we need to iterate on 4x4 blocks
        computeShader.Dispatch(0, inTexture.width / 4,  inTexture.height/ 4 ,1);
    }
}
