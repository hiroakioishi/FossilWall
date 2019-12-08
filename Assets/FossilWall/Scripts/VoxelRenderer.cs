using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace irishoak.VJ.VoxelFossilWall
{
    [RequireComponent(typeof(VoxelFossilWall))]
    public class VoxelRenderer : MonoBehaviour
    {
        public Color ColorA;
        public Color ColorB;
        public float ColorAIntensity = 3.0f;
        public float ColorBIntensity = 1.0f;

        [SerializeField]
        VoxelFossilWall _voxelFossilWall;
        
        [SerializeField]
        Mesh _instanceMesh = null;

        [SerializeField]
        Material _instanceRenderMaterial = null;

        uint[] _args = new uint[5] { 0, 0, 0, 0, 0 };

        ComputeBuffer _argsBuffer;

        void Start()
        {
            _argsBuffer = new ComputeBuffer(1, _args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        }

        void Update()
        {
            if (_voxelFossilWall == null)
            {
                _voxelFossilWall = GetComponent<VoxelFossilWall>();
            }

            RenderInstancedMesh();
        }

        void OnDestroy()
        {
            if (_argsBuffer != null)
                _argsBuffer.Release();
            _argsBuffer = null;
        }

        void RenderInstancedMesh()
        {
            uint numIndices = (_instanceMesh != null) ? (uint)_instanceMesh.GetIndexCount(0) : 0;
            _args[0] = numIndices;
            _args[1] = (uint)_voxelFossilWall.particleCount;
            _argsBuffer.SetData(_args);

            _instanceRenderMaterial.SetColor("_ColorA", ColorA);
            _instanceRenderMaterial.SetColor("_ColorB", ColorB);
            _instanceRenderMaterial.SetFloat("_ColorAIntensity", ColorAIntensity);
            _instanceRenderMaterial.SetFloat("_ColorBIntensity", ColorBIntensity);

            _instanceRenderMaterial.SetFloat("_VoxelSize", _voxelFossilWall.VoxelSize);
            _instanceRenderMaterial.SetBuffer("_ParticleBuffer", _voxelFossilWall.particleBuffer) ;

            var bounds = new Bounds(Vector3.zero, Vector3.one * 1000.0f);

            Graphics.DrawMeshInstancedIndirect
            (
                _instanceMesh,
                0,
                _instanceRenderMaterial,
                bounds,
                _argsBuffer
            );
        }

    }
}