using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

#pragma warning disable 162

namespace MarchingCubesGPUProject
{
    public class MarchingCubesGPU : MonoBehaviour
    {
        const int N = 64;
        const int SIZE = N * N * N * 3 * 5;

        public Material m_drawBuffer;
        public ComputeShader m_marchingCubes;
        public ComputeShader m_normals;

        ComputeBuffer m_noiseBuffer, m_meshBuffer;
        RenderTexture m_normalsBuffer;
        ComputeBuffer m_cubeEdgeFlags, m_triangleConnectionTable;

        List<Vector3> sphereCenters = new List<Vector3>();
        List<float> sphereRadii = new List<float>();

        List<GameObject> generatedObjects = new List<GameObject>();

        void Start()
        {
            InitializeBuffers();
        }

        void InitializeBuffers()
        {
            if (N % 8 != 0)
                throw new System.ArgumentException("N must be divisible by 8");

            m_noiseBuffer = new ComputeBuffer(N * N * N, sizeof(float));

            m_normalsBuffer = new RenderTexture(N, N, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                dimension = TextureDimension.Tex3D,
                enableRandomWrite = true,
                useMipMap = false,
                volumeDepth = N
            };
            m_normalsBuffer.Create();

            m_meshBuffer = new ComputeBuffer(SIZE, sizeof(float) * 7);
            float[] val = new float[SIZE * 7];
            for (int i = 0; i < val.Length; i++) val[i] = -1.0f;
            m_meshBuffer.SetData(val);

            m_cubeEdgeFlags = new ComputeBuffer(256, sizeof(int));
            m_cubeEdgeFlags.SetData(MarchingCubesTables.CubeEdgeFlags);

            m_triangleConnectionTable = new ComputeBuffer(256 * 16, sizeof(int));
            m_triangleConnectionTable.SetData(MarchingCubesTables.TriangleConnectionTable);
        }

        public void SetSpheres(List<Vector3> centers, List<float> radii)
        {
            if (centers.Count != radii.Count)
            {
                Debug.LogError("Centers and Radii lists must be of same length.");
                return;
            }

            sphereCenters = centers;
            sphereRadii = radii;

            GenerateSpheresVoxelGrid();

            m_normals.SetInt("_Width", N);
            m_normals.SetInt("_Height", N);
            m_normals.SetBuffer(0, "_Noise", m_noiseBuffer);
            m_normals.SetTexture(0, "_Result", m_normalsBuffer);
            m_normals.Dispatch(0, N / 8, N / 8, N / 8);

            m_marchingCubes.SetInt("_Width", N);
            m_marchingCubes.SetInt("_Height", N);
            m_marchingCubes.SetInt("_Depth", N);
            m_marchingCubes.SetInt("_Border", 1);
            m_marchingCubes.SetFloat("_Target", 0.0f);
            m_marchingCubes.SetBuffer(0, "_Voxels", m_noiseBuffer);
            m_marchingCubes.SetTexture(0, "_Normals", m_normalsBuffer);
            m_marchingCubes.SetBuffer(0, "_Buffer", m_meshBuffer);
            m_marchingCubes.SetBuffer(0, "_CubeEdgeFlags", m_cubeEdgeFlags);
            m_marchingCubes.SetBuffer(0, "_TriangleConnectionTable", m_triangleConnectionTable);

            m_marchingCubes.Dispatch(0, N / 8, N / 8, N / 8);

            ClearPreviousMesh();
            generatedObjects = ReadBackMesh(m_meshBuffer);
        }

        void ClearPreviousMesh()
        {
            foreach (var go in generatedObjects)
                Destroy(go);
            generatedObjects.Clear();
        }

        void GenerateSpheresVoxelGrid()
        {
            float[] voxelData = new float[N * N * N];

            // Fill all voxels with positive distance (outside the surface)
            for (int i = 0; i < voxelData.Length; i++)
                voxelData[i] = 1.0f;

            // Define a small cube in the center with negative values (inside the surface)
            int cubeSize = 20;
            int start = (N - cubeSize) / 2;
            int end = start + cubeSize;

            for (int x = start; x < end; x++)
            {
                for (int y = start; y < end; y++)
                {
                    for (int z = start; z < end; z++)
                    {
                        int index = x + y * N + z * N * N;
                        voxelData[index] = -1.0f; // Inside the implicit surface
                    }
                }
            }

            m_noiseBuffer.SetData(voxelData);
        }

        bool IsVoxelInsideSpheres(Vector3 voxelPos)
        {
            for (int i = 0; i < sphereCenters.Count; i++)
            {
                if (Vector3.Distance(voxelPos, sphereCenters[i]) <= sphereRadii[i])
                    return true;
            }
            return false;
        }

        void OnRenderObject()
        {
            m_drawBuffer.SetBuffer("_Buffer", m_meshBuffer);
            m_drawBuffer.SetPass(0);
            Graphics.DrawProceduralNow(MeshTopology.Triangles, SIZE);
        }

        void OnDestroy()
        {
            m_noiseBuffer?.Release();
            m_meshBuffer?.Release();
            m_cubeEdgeFlags?.Release();
            m_triangleConnectionTable?.Release();
            m_normalsBuffer?.Release();
        }

        struct Vert
        {
            public Vector4 position;
            public Vector3 normal;
        };

        List<GameObject> ReadBackMesh(ComputeBuffer meshBuffer)
        {
            Vert[] verts = new Vert[SIZE];
            meshBuffer.GetData(verts);

            List<Vector3> positions = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<int> index = new List<int>();

            List<GameObject> objects = new List<GameObject>();
            int idx = 0;

            for (int i = 0; i < SIZE; i++)
            {
                if (verts[i].position.w != -1)
                {
                    positions.Add(verts[i].position);
                    normals.Add(verts[i].normal);
                    index.Add(idx++);
                }

                if (idx >= 65000 / 3)
                {
                    objects.Add(MakeGameObject(positions, normals, index));
                    positions.Clear();
                    normals.Clear();
                    index.Clear();
                    idx = 0;
                }
            }
            Debug.Log("first marching cube vertices count = " + positions.Count);
            Debug.Log("the sizeee = " + SIZE);
            if (positions.Count > 0)
                objects.Add(MakeGameObject(positions, normals, index));

            return objects;
        }

        GameObject MakeGameObject(List<Vector3> positions, List<Vector3> normals, List<int> index)
        {
            Mesh mesh = new Mesh();
            mesh.vertices = positions.ToArray();
            Debug.Log("marching cube vertices count = " + positions.Count);
            mesh.normals = normals.ToArray();
            mesh.bounds = new Bounds(new Vector3(0, N / 2, 0), new Vector3(N, N, N));
            mesh.SetTriangles(index.ToArray(), 0);
            mesh.RecalculateNormals(); // only if you're not assigning them


            GameObject go = new GameObject("Voxel Mesh");
            go.AddComponent<MeshFilter>().mesh = mesh;
            go.AddComponent<MeshRenderer>().material = new Material(Shader.Find("Standard"));
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.isStatic = true;

            // go.transform.parent = transform;
            go.transform.localPosition = new Vector3(N+2, 0, 0);

            return go;
        }
    }
}
