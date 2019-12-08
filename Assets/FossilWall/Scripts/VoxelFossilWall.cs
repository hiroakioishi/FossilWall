using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;

using Malee;

using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace irishoak.VJ.VoxelFossilWall
{
    public class VoxelFossilWall : MonoBehaviour
    {
        public struct ParticleData
        {
            public float4 velocity;
            public float4 position;
            public float4 targetPosition;
            public float4 rotation;
            public float4 color;
            public float  scale;
            public int    id;
            public int    alive;
            public int    isFossil;
            public float  valueToPeel;
            public int    isPeeled;
            public float  timerFromPeeled;
        };

        const int MAX_PARTICLE_NUM = 524288;

        #region Params
        public int2 Resolution = int2(128, 72);
        public float VoxelSize = 0.05f;

        public int MaxDepthNum = 4;

        public float BrushSize = 0.125f;

        // x: base, y: speedacc z: Randomness
        public float3 SpinParams = float3(1.0f, 1.0f, 1.0f);
        #endregion

        #region Resources
        [SerializeField]
        ComputeShader _kernelCS = null;
        
        [SerializeField]
        Shader _mouseInteractionShader = null;
        
        [System.Serializable]
        public class TextureList : ReorderableArray<Texture> { };

        [Header("Texture List")]
        [SerializeField, Reorderable]
        TextureList _fossilTexList = new TextureList();
        #endregion

        #region Private Variables and Resources
        ComputeBuffer _particleBuffer;
        ComputeBuffer _particleDeadListBuffer;
        ComputeBuffer _particleIndirectArgsBuffer;

        int[] _particleIndirectArgs;

        bool _mouseLeftButtonPressed = false;
        float2 _prevMousePos = float2(0, 0);
        Material _mouseInteractionMat = null;
        
        [SerializeField]
        int _currentParticleCount = 0;

        int fossilTexId = 0;

        int _emitCount = 0;

        bool _isInit = false;
        
        #endregion

        #region Accessors
        public ComputeBuffer particleBuffer => _particleBuffer ?? null;
        public int particleCount => MAX_PARTICLE_NUM;
        #endregion

        void Update()
        {
            if (_isInit == false)
            {
                InitParticles();
               
                _isInit = true;
            }
            
            // 手前方向にボクセルパーティクルを移動
            if (Input.GetKeyUp("z"))
            {
                PushTargetPosition();
            }

            // ボクセルパーティクルを生成
            if (Input.GetKeyUp("x"))
            {
                PushNullPlane();
            }

            // 化石テクスチャを反映させたボクセルパーティクルを生成
            if (Input.GetKeyUp("c"))
            {
                PushFossilPlane();
            }
            
            // ブラシサイズ 小
            if (Input.GetKeyUp("i"))
            {
                BrushSize = 0.05f;
            }

            // ブラシサイズ 中
            if (Input.GetKeyUp("o"))
            {
                BrushSize = 0.1f;
            }

            // ブラシサイズ 大
            if (Input.GetKeyUp("p"))
            {
                BrushSize = 0.15f;
            }

            // マウス 左クリックの状態
            if (Input.GetMouseButtonDown(0))
                _mouseLeftButtonPressed = true;
            if (Input.GetMouseButtonUp(0))
                _mouseLeftButtonPressed = false;

            // --------------------------------------------------
            // Mouse Interaction
            // マウスで塗った部分をテクスチャに転写
            if (_mouseInteractionMat == null)
            {
                _mouseInteractionMat = new Material(_mouseInteractionShader);
                _mouseInteractionMat.hideFlags = HideFlags.DontSave;
            }
            
            RenderTexture mouseInteractionRT = RenderTexture.GetTemporary(Resolution.x, Resolution.y, 0, RenderTextureFormat.RHalf);

            RenderTexture store = RenderTexture.active;
            Graphics.SetRenderTarget(mouseInteractionRT);
            GL.Clear(false, true, Color.black);
            Graphics.SetRenderTarget(store);

            var mousePosition = (float3)Input.mousePosition;
            if (_mouseLeftButtonPressed)
            {                
                var dist = distance(mousePosition.xy, _prevMousePos.xy);
                var inputPointInterval = abs((abs(Screen.width + Screen.height) * 0.5f) / 100.0f);
                var inputPointNum = dist < inputPointInterval ? 1 : Mathf.CeilToInt(dist / inputPointInterval);
                inputPointNum = min(inputPointNum, 25);
                for (var i = 0; i < inputPointNum; i++)
                {
                    var mp = lerp(_prevMousePos.xy, mousePosition.xy, inputPointNum == 1 ? 1.0f : (float)i * (1.0f / (inputPointNum - 1)));

                    mp.x /= (float)Screen.width;
                    mp.y /= (float)Screen.height;

                    var rad   = Mathf.Clamp(BrushSize, 0.01f, 0.5f);
                    var power = 0.5f;
                    _mouseInteractionMat.SetTexture("_MainTex", mouseInteractionRT);
                    _mouseInteractionMat.SetFloat("_AspectRatio", (float)Resolution.x / Resolution.y);
                    _mouseInteractionMat.SetVector("_InputParams", new Vector4(mp.x, mp.y, rad, power));
                    Graphics.Blit(null, mouseInteractionRT, _mouseInteractionMat, 0);
                }                
            }
            _prevMousePos = float2(mousePosition.x, mousePosition.y);
            
            // ----------------------------------------------------
            // Update Voxel
            _currentParticleCount = GetCurrentParticleCount();

            var cs = _kernelCS;
            var id = cs.FindKernel("CSUpdate");

            var threadsX = Mathf.CeilToInt((float)MAX_PARTICLE_NUM / 32);

            cs.SetFloat("_DeltaTime", Time.deltaTime);
            cs.SetFloat("_Timer", Time.time);
            cs.SetVector("_AreaCenter", new Vector3(0, 0, 0));
            cs.SetVector("_AreaSize", new Vector3(
                (Resolution.x - 0) * VoxelSize,
                (Resolution.y - 0) * VoxelSize,
                0.0f
            ));
            cs.SetFloat("_MaxDepthNum", MaxDepthNum);
            cs.SetVector("_SpinParams", (Vector3)SpinParams);
            cs.SetTexture(id, "_MouseInteractionTex", mouseInteractionRT);
            cs.SetBuffer (id, "_ParticleBuffer", _particleBuffer);
            cs.SetBuffer(id, "_ParticleDeadListBufferAppend", _particleDeadListBuffer);
            cs.Dispatch(id, threadsX, 1, 1);

            RenderTexture.ReleaseTemporary(mouseInteractionRT);
        }

        void OnDestroy()
        {
            if (_particleBuffer != null)
                _particleBuffer.Release();
            _particleBuffer = null;

            if (_particleDeadListBuffer != null)
                _particleDeadListBuffer.Release();
            _particleDeadListBuffer = null;

            if (_particleIndirectArgsBuffer != null)
                _particleIndirectArgsBuffer.Release();
            _particleIndirectArgsBuffer = null;
            
            if (_mouseInteractionMat != null)
            {
                if (Application.isEditor)
                    Material.DestroyImmediate(_mouseInteractionMat);
                else
                    Material.Destroy(_mouseInteractionMat);
                _mouseInteractionMat = null;
            }
        }
       
        /// <summary>
        /// ボクセルパーティクルを初期化
        /// </summary>
        void InitParticles()
        {
            _particleBuffer = new ComputeBuffer(MAX_PARTICLE_NUM, Marshal.SizeOf(typeof(ParticleData)));

            _particleDeadListBuffer = new ComputeBuffer(MAX_PARTICLE_NUM, sizeof(int), ComputeBufferType.Append);
            _particleDeadListBuffer.SetCounterValue(0);

            _particleIndirectArgsBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
            _particleIndirectArgs = new int[] { 0, 1, 0, 0 };
            
            var cs = _kernelCS;
            var kernelId = cs.FindKernel("CSInit");
            cs.SetBuffer(kernelId, "_ParticleBuffer", _particleBuffer);
            cs.SetBuffer(kernelId, "_ParticleDeadListBufferAppend", _particleDeadListBuffer);
            cs.Dispatch(kernelId, Mathf.CeilToInt((float)MAX_PARTICLE_NUM / 32), 1, 1);
            
        }

        /// <summary>
        /// 化石テクスチャを参照し、ボクセルを生成
        /// 前: 空のプレーン
        /// 中: 化石テクスチャを参照し, 白い部分を化石としたプレーン
        /// 奥: 空のプレーン
        /// </summary>
        void PushFossilPlane()
        {
            if (_currentParticleCount - (Resolution.x * Resolution.y * 3) < 0)
                return;

            var tex = _fossilTexList[fossilTexId] ?? null;

            RenderTexture nullTex = RenderTexture.GetTemporary(Resolution.x, Resolution.y);
            RenderTexture store = RenderTexture.active;
            Graphics.SetRenderTarget(nullTex);
            GL.Clear(false, true, Color.black);
            Graphics.SetRenderTarget(store);

            // Null
            PushTexturePlane(nullTex);
            PushTargetPosition();
            // Texture
            PushTexturePlane(tex);
            PushTargetPosition();
            // Null
            PushTexturePlane(nullTex);
            PushTargetPosition();

            RenderTexture.ReleaseTemporary(nullTex);

            fossilTexId++;
            if (fossilTexId >= _fossilTexList.Length)
                fossilTexId = 0;
        }
       
        /// <summary>
        /// 空のプレーンを生成
        /// </summary>
        void PushNullPlane()
        {
            if (_currentParticleCount - (Resolution.x * Resolution.y * 1) < 0)
                return;

            RenderTexture nullTex = RenderTexture.GetTemporary(Resolution.x, Resolution.y);
            RenderTexture store = RenderTexture.active;
            Graphics.SetRenderTarget(nullTex);
            GL.Clear(false, true, Color.black);
            Graphics.SetRenderTarget(store);

            // Null
            PushTexturePlane(nullTex);
            PushTargetPosition();

            RenderTexture.ReleaseTemporary(nullTex);
        }

        /// <summary>
        /// 引数にセットしたテクスチャをもとにボクセルパーティクルを生成
        /// </summary>
        /// <param name="tex"></param>
        void PushTexturePlane(Texture tex)
        {
            var cs = _kernelCS;
            var id = _kernelCS.FindKernel("CSEmit");

            var threadsX = Mathf.CeilToInt((float)Resolution.x / 8);
            var threadsY = Mathf.CeilToInt((float)Resolution.y / 8);

            cs.SetInt("_EmitCount", _emitCount);
            cs.SetFloat("_VoxelSize", VoxelSize);
            cs.SetInts("_Resolution", new int[2] { Resolution.x, Resolution.y });

            cs.SetTexture(id, "_FossilTex", tex);
           
            cs.SetBuffer(id, "_ParticleBuffer", _particleBuffer);
            cs.SetBuffer(id, "_ParticleDeadListBufferConsume", _particleDeadListBuffer);

            cs.Dispatch(id, threadsX, threadsY, 1);

            _emitCount++;

        }

        /// <summary>
        /// 手前方向に 1ボクセル分 位置を押し出す
        /// </summary>
        void PushTargetPosition()
        {
            var cs = _kernelCS;
            var id = cs.FindKernel("CSPushTargetPosition");
            var threadsX = Mathf.CeilToInt((float)MAX_PARTICLE_NUM / 32);
            var threadsY = 1;
            cs.SetFloat("_VoxelSize", VoxelSize);
            cs.SetBuffer(id, "_ParticleBuffer", _particleBuffer);
            cs.Dispatch(id, threadsX, threadsY, 1);
        }

        /// <summary>
        /// 生成することができるボクセルパーティクルの数を取得
        /// </summary>
        /// <returns></returns>
        int GetCurrentParticleCount()
        {
            _particleIndirectArgsBuffer.SetData(_particleIndirectArgs);
            ComputeBuffer.CopyCount(_particleDeadListBuffer, _particleIndirectArgsBuffer, 0);
            _particleIndirectArgsBuffer.GetData(_particleIndirectArgs);
            return _particleIndirectArgs[0];
        }
    }
}