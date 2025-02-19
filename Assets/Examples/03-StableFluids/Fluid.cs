// StableFluids - A GPU implementation of Jos Stam's Stable Fluids on Unity
// https://github.com/keijiro/StableFluids

using System.IO.Enumeration;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.VFX;

namespace StableFluids
{
    public class Fluid : MonoBehaviour
    {
        // Read-Only Accessors
        public int Resolution => _resolution;

        #region Editable attributes
        [Header("Simulation Input")]
        [SerializeField] CharacterController characterController;
        [SerializeField] SphereCollider[] sphereColliders;
        [SerializeField] GridFollowPlayer gridFollowPlayer;

        [Header("Simulation Properties")]
        [SerializeField] int _resolution = 512;
        [SerializeField] float _viscosity = 1e-6f;
        [SerializeField] float _force = 300;
        [SerializeField] float _exponent = 200;

        [Header("Artistic Controls")]
        [SerializeField] float _radiusScale = 2.2f;
        [SerializeField] float _velocityScale = 1.4f;

        [Header("Preview")]
        [SerializeField] MeshRenderer _previewMeshRenderer;
        [SerializeField] PreviewType _previewType;

        [Header("VFX")]
        [SerializeField] VisualEffect _vfx;

        public enum PreviewType
        {
            V1,
            V2, V3,
            P1, P2,
        }

        #endregion

        #region Internal resources

        [SerializeField, HideInInspector] ComputeShader _compute;
        [SerializeField, HideInInspector] Shader _shader;

        int OffsetX = 0;
        int OffsetY = 0;

        #endregion

        #region Private members

        Material _shaderSheet;
        Vector2 _previousInput;

        static class Kernels
        {
            public const int Advect = 0;
            public const int Force = 1;
            public const int PSetup = 2;
            public const int PFinish = 3;
            public const int Jacobi1 = 4;
            public const int Jacobi2 = 5;
            public const int Offset = 6;
        }

        int ThreadCountX { get { return (_resolution + 7) / 8; } }
        int ThreadCountY { get { return (_resolution * Screen.height / Screen.width + 7) / 8; } }

        int ResolutionX { get { return ThreadCountX * 8; } }
        int ResolutionY { get { return ThreadCountY * 8; } }

        // Vector field buffers
        static class VFB
        {
            public static RenderTexture V1;
            public static RenderTexture V2;
            public static RenderTexture V3;
            public static RenderTexture P1;
            public static RenderTexture P2;
        }

        // Color buffers (for double buffering)
        // RenderTexture _colorRT1;
        // RenderTexture _colorRT2;

        #region INPUT BUFFER

        struct FluidInputData
        {
            public Vector2 position;
            public Vector2 velocity;
            public float radius;
        }

        static readonly FluidInputData defaultData = new FluidInputData()
        {
            position = Vector2.zero,
            velocity = Vector2.zero,
            radius = 0f
        };

        GraphicsBuffer _inputBuffer;
        FluidInputData[] _inputBufferData;

        void InitializeInputBuffer()
        {
            if (_inputBuffer == null)
            {
                //    Structure for input : stride sizeof(float) * 5
                //    struct FluidInputData
                //    {
                //        float2 position;
                //        float2 velocity;
                //        float radius;
                //    };

                _inputBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, sphereColliders.Length + 1, sizeof(float) * 5);
                _inputBufferData = new FluidInputData[sphereColliders.Length + 1];
                _previousPositions = new Vector3[sphereColliders.Length + 1];
            }

            UpdateInputBuffer(0f);
        }

        /// <summary>
        /// Updates Input Data
        /// </summary>
        /// <param name="deltaTime">Given Delta Time, zero for initialization</param>
        void UpdateInputBuffer(float deltaTime)
        {
            if (characterController == null) _inputBufferData[0] = defaultData;
            else
            {
                UpdateData(0, characterController, deltaTime);
            }

            for(int i = 0; i < sphereColliders.Length; i++)
            {
                UpdateData(i + 1, sphereColliders[i], deltaTime);
            }

            _inputBuffer.SetData(_inputBufferData);
        }

        Vector3[] _previousPositions;

        
        Vector2 ProjectTo2DGrid(Vector3 position, Vector3 gridPosition, float gridResolution, float gridStepSize)
        {
            return new Vector2(position.x - gridPosition.x, position.z - gridPosition.z) / (this._resolution * gridFollowPlayer.GridStepSize);
        }

        void UpdateData(int index, Collider collider, float deltaTime)
        {
            FluidInputData d = _inputBufferData[index];
            Vector3 position = collider.transform.position;

            if (deltaTime == 0f) _previousPositions[index] = position;

            // Compute position in 2D
            Vector2 pos2d = ProjectTo2DGrid(position,transform.position,this._resolution,gridFollowPlayer.GridStepSize);
            pos2d.x = pos2d.x + 0.5f;
            pos2d.y = pos2d.y + 0.5f;

            // Compute velocity in 2D
            Vector2 vel2d = ProjectTo2DGrid(_previousPositions[index] - position, Vector3.zero, this._resolution, gridFollowPlayer.GridStepSize);
            _previousPositions[index] = position;

            // Compute radius in 2D
            float radius = 0f;
            if (collider is CharacterController)
                radius = ((CharacterController)collider).radius;
            else if (collider is SphereCollider)
                radius = ((SphereCollider)collider).radius * collider.transform.localScale.x;

            d.radius = this._radiusScale * radius / (this._resolution * gridFollowPlayer.GridStepSize);

            // Only store velocity for update pass
            if (deltaTime > 0)
                d.velocity = vel2d * this._velocityScale;
            else
                d.velocity = Vector2.zero;

            // Store position
            d.position = pos2d;

            _inputBufferData[index] = d;

        }

        #endregion

        RenderTexture AllocateBuffer(int componentCount, int width = 0, int height = 0)
        {
            var format = RenderTextureFormat.ARGBHalf;
            if (componentCount == 1) format = RenderTextureFormat.RHalf;
            if (componentCount == 2) format = RenderTextureFormat.RGHalf;

            if (width  == 0) width  = ResolutionX;
            if (height == 0) height = ResolutionY;

            var rt = new RenderTexture(width, height, 0, format);
            rt.enableRandomWrite = true;
            rt.Create();
            return rt;
        }

        #endregion

        #region MonoBehaviour implementation

        void OnValidate()
        {
            _resolution = Mathf.Max(_resolution, 8);
        }

        void Start()
        {
            _shaderSheet = new Material(_shader);

            VFB.V1 = AllocateBuffer(2);
            VFB.V2 = AllocateBuffer(2);
            VFB.V3 = AllocateBuffer(2);
            VFB.P1 = AllocateBuffer(1);
            VFB.P2 = AllocateBuffer(1);

            //_colorRT1 = AllocateBuffer(4, Screen.width, Screen.height);
            //_colorRT2 = AllocateBuffer(4, Screen.width, Screen.height);
            //Graphics.Blit(_initial, _colorRT1);

            InitializeInputBuffer();

        
            Application.targetFrameRate = 60;
        }

        void OnDestroy()
        {
            Destroy(_shaderSheet);

            Destroy(VFB.V1);
            Destroy(VFB.V2);
            Destroy(VFB.V3);
            Destroy(VFB.P1);
            Destroy(VFB.P2);

            //Destroy(_colorRT1);
            //Destroy(_colorRT2);
        }
        public void SetOffset(int x, int y)
        {
            //Debug.Log($"Offset : {x},{y}");
            OffsetX = x; OffsetY = y;
        }

        void LateUpdate()
        {
            var dt = Time.deltaTime;
            var dx = 1.0f / ResolutionY;


            // Common variables
            _compute.SetFloat("Time", Time.time);
            _compute.SetFloat("DeltaTime", dt);

            // Offset Step (V1 is result from last frame)
            _compute.SetInt("offsetU", this.OffsetX);
            _compute.SetInt("offsetV", this.OffsetY);
            _compute.SetTexture(Kernels.Offset, "W_in", VFB.V1);
            _compute.SetTexture(Kernels.Offset, "W_out", VFB.V3);
            _compute.Dispatch(Kernels.Offset, ThreadCountX, ThreadCountY, 1);

            // Advection
            _compute.SetTexture(Kernels.Advect, "U_in", VFB.V3);
            _compute.SetTexture(Kernels.Advect, "W_out", VFB.V2);
            _compute.Dispatch(Kernels.Advect, ThreadCountX, ThreadCountY, 1);
            
            // Diffuse setup
            var dif_alpha = dx * dx / (_viscosity * dt);
            _compute.SetFloat("Alpha", dif_alpha);
            _compute.SetFloat("Beta", 4 + dif_alpha);
            Graphics.CopyTexture(VFB.V2, VFB.V1);
            _compute.SetTexture(Kernels.Jacobi2, "B2_in", VFB.V1);

            // Jacobi iteration
            for (var i = 0; i < 20; i++)
            {
                _compute.SetTexture(Kernels.Jacobi2, "X2_in", VFB.V2);
                _compute.SetTexture(Kernels.Jacobi2, "X2_out", VFB.V3);
                _compute.Dispatch(Kernels.Jacobi2, ThreadCountX, ThreadCountY, 1);

                _compute.SetTexture(Kernels.Jacobi2, "X2_in", VFB.V3);
                _compute.SetTexture(Kernels.Jacobi2, "X2_out", VFB.V2);
                _compute.Dispatch(Kernels.Jacobi2, ThreadCountX, ThreadCountY, 1);
            }

            // Add external force
            //_compute.SetVector("ForceOrigin", input);
            //_compute.SetFloat("ForceExponent", _exponent);

            //if (Input.GetMouseButton(1))
            //    // Random push
            //   _compute.SetVector("ForceVector", Random.insideUnitCircle * _force * 0.025f);
            //else if (Input.GetMouseButton(0))
            //    // Mouse drag
            //    _compute.SetVector("ForceVector", (input - _previousInput) * _force);
            //else
            //    _compute.SetVector("ForceVector", Vector4.zero);

            UpdateInputBuffer(Time.deltaTime);

            _compute.SetTexture(Kernels.Force, "W_in", VFB.V2);
            _compute.SetTexture(Kernels.Force, "W_out", VFB.V3);
            _compute.SetBuffer(Kernels.Force, "FluidInput", this._inputBuffer);
            _compute.SetInt("FluidInputCount", this._inputBufferData.Length);
            _compute.Dispatch(Kernels.Force, ThreadCountX, ThreadCountY, 1);

            // Projection setup
            _compute.SetTexture(Kernels.PSetup, "W_in", VFB.V3);
            _compute.SetTexture(Kernels.PSetup, "DivW_out", VFB.V2);
            _compute.SetTexture(Kernels.PSetup, "P_out", VFB.P1);
            _compute.Dispatch(Kernels.PSetup, ThreadCountX, ThreadCountY, 1);

            // Jacobi iteration
            _compute.SetFloat("Alpha", -dx * dx);
            _compute.SetFloat("Beta", 4);
            _compute.SetTexture(Kernels.Jacobi1, "B1_in", VFB.V2);

            for (var i = 0; i < 20; i++)
            {
                _compute.SetTexture(Kernels.Jacobi1, "X1_in", VFB.P1);
                _compute.SetTexture(Kernels.Jacobi1, "X1_out", VFB.P2);
                _compute.Dispatch(Kernels.Jacobi1, ThreadCountX, ThreadCountY, 1);

                _compute.SetTexture(Kernels.Jacobi1, "X1_in", VFB.P2);
                _compute.SetTexture(Kernels.Jacobi1, "X1_out", VFB.P1);
                _compute.Dispatch(Kernels.Jacobi1, ThreadCountX, ThreadCountY, 1);
            }

            // Projection finish
            _compute.SetTexture(Kernels.PFinish, "W_in", VFB.V3);
            _compute.SetTexture(Kernels.PFinish, "P_in", VFB.P1);
            _compute.SetTexture(Kernels.PFinish, "U_out", VFB.V1);
            _compute.Dispatch(Kernels.PFinish, ThreadCountX, ThreadCountY, 1);


            // Don't care anymore, we're advecting particles
            // Apply the velocity field to the color buffer.
            //var offs = Vector2.one * (Input.GetMouseButton(1) ? 0 : 1e+7f);
            //_shaderSheet.SetVector("_ForceOrigin", input + offs);
            //_shaderSheet.SetFloat("_ForceExponent", _exponent);
            //_shaderSheet.SetTexture("_VelocityField", VFB.V1);
            //Graphics.Blit(_colorRT1, _colorRT2, _shaderSheet, 0);

            //// Swap the color buffers.
            //var temp = _colorRT1;
            //_colorRT1 = _colorRT2;
            //_colorRT2 = temp;

            //_previousInput = input;

            Texture previewTexture;
            switch (_previewType)
            {
                case PreviewType.V1:
                    previewTexture= VFB.V1; 
                    break;
                case PreviewType.V2:
                    previewTexture= VFB.V2;
                    break;
                default:
                case PreviewType.V3:
                    previewTexture= VFB.V3; 
                    break;
                case PreviewType.P1:
                    previewTexture= VFB.P1; 
                    break;
                case PreviewType.P2:
                    previewTexture= VFB.P2;
                    break;
            }

            this._previewMeshRenderer.sharedMaterial.SetTexture("_PreviewTexture", previewTexture);


            if(_vfx != null)
            {
                _vfx.SetVector3("Position", transform.position);
                float s = _resolution * gridFollowPlayer.GridStepSize;
                _vfx.SetVector3("Size", new Vector3(s, 0.1f, s));
                _vfx.SetTexture("VelocityBuffer", VFB.V3);
            }

        }

        #endregion
    }
}
