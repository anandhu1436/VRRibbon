using Curve;
// using MarchingCubesGPUProject;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRSketch;

public enum Primitive
{
    Stroke,
    Surface
}

public class DrawingCanvas : MonoBehaviour
{
    [SerializeField]
    private CASSIEParametersProvider parameters = null;

    // Display nodes
    [Header("Nodes display parameters")]
    public float NodesOpacity = 0.9f;
    [Tooltip("Value in pixels (so it may need to be changed depending on your resolution).")]
    public float NodesRadius = 10;

    public SurfaceManager surfaceManager;
    public MirrorPlane mirrorPlane;

    public List<FinalStroke> Strokes { get; private set; }
    public Graph Graph { get; private set; } = new Graph();

    private FinalStroke selectedStroke = null;
    private SurfacePatch selectedPatch = null;

    private GameObject StrokeContainer;
    private GameObject SurfaceContainer;
    private Grid3D grid;
    public Mesh referencemesh;

    // Display nodes stuff
    [Header("Node Display Shader")]
    public Shader shader;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Material material;

    // Shader uniforms indices
    private int _PointRadiusIdx;
    private int _ColorIdx;
    private int _OpacityIdx;
    private float pointRadius;

    private List<Vector3> sphereCenters = new List<Vector3>();
    private List<float> sphereRadii = new List<float>();

    private List<GameObject> sphereObjects = new List<GameObject>(); // Store sphere objects
    public GameObject spherePrefab; // Assign a sphere prefab in Unity Inspector


    public Vector3[] OrthoDirections { get; private set; }
    public bool displaySpheres=true;

    // public MarchingCubesGPU marchingCubes;


    private void Start()
    {
        Strokes = new List<FinalStroke>();
        OrthoDirections = new Vector3[] { Vector3.right, Vector3.up, Vector3.forward };
        grid = GetComponentInChildren<Grid3D>();

        StrokeContainer = new GameObject("Stroke Container");
        StrokeContainer.transform.parent = gameObject.transform;
        SurfaceContainer = new GameObject("Surface Container");



        // Display nodes stuff
        material = new Material(shader);
        meshRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();
        meshFilter.mesh = new Mesh();
        meshRenderer.material = material;

        pointRadius = NodesRadius;


        // Shader uniform indices
        _PointRadiusIdx = Shader.PropertyToID("_PointRadius");
        _ColorIdx = Shader.PropertyToID("_Color");
        _OpacityIdx = Shader.PropertyToID("_BaseOpacity");

        material.SetFloat(_PointRadiusIdx, pointRadius);

        material.SetColor(_ColorIdx, Color.black);
        material.SetFloat(_OpacityIdx, NodesOpacity);
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Return))
        {
            Debug.Log(Graph.Count() + " nodes currently in scene");
            foreach(var stroke in Strokes)
            {
                if (stroke as FinalStroke != null)
                {
                    ((FinalStroke)stroke).PrintSegments();
                }
            }
        }
#endif

        int N = Graph.Count();

        Mesh nodesMesh = new Mesh();

        if (N > 0)
        {
            Vector3[] nodes = Graph.GetNodes();
            int[] indices = new int[nodes.Length];

            for (int i = 0; i < nodes.Length; i++)
                indices[i] = i;

            nodesMesh.SetVertices(nodes);
            nodesMesh.SetIndices(indices, MeshTopology.Points, 0);

            material.SetFloat(_PointRadiusIdx, pointRadius * Mathf.Pow(transform.localScale.x, 1f));
        }
        else
        {
            nodesMesh.SetVertices(new Vector3[] { });
            nodesMesh.SetIndices(new int[] { }, MeshTopology.Points, 0);
        }
        meshFilter.mesh = nodesMesh;
    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        //int N = Graph.Count();
        int[] nodeIDs = Graph.GetNodeIDs();
        // Draw all nodes and associated normals and tangents
        foreach (int id in nodeIDs)
        {
            Gizmos.color = Color.yellow;

            Vector3 nodePos = this.transform.TransformPoint(Graph.Get(id));
            Vector3[] segments = Graph.GetNeighbors(id);

            Gizmos.DrawSphere(nodePos, 0.005f);

            int j = 0;

            foreach(var tan in segments)
            {
                Handles.color = FewColors.Get(j);
                j++;
                Vector3 dir = this.transform.TransformDirection(tan);
                Vector3 to = nodePos + 0.02f * dir;
                Handles.DrawAAPolyLine(4f, new Vector3[] { nodePos, to });
            }

            // Draw normal if there is one
            Vector3 normal = Graph.GetNormal(id);
            if (normal.magnitude > 0f)
            {
                Vector3 dir = this.transform.TransformDirection(normal);
                Vector3 to = nodePos + 0.05f * dir;
                Handles.color = Color.black;
                Handles.DrawAAPolyLine(10f, new Vector3[] { nodePos, to });
            }
        }
#endif
    }

    public void Init(bool surfacing)
    {
        Clear();
        Graph.Init(surfacing);
    }

    public void SwitchSystem(bool surfacing)
    {
        // Set parameter
        Graph.SwitchSystem(surfacing);
    }

    public void Scale(float newScale, Vector3 zoomCenter)
    {
        Vector3 originalPos = transform.localPosition;
        Vector3 translation = originalPos - zoomCenter;

        float RS = newScale / transform.localScale.x; // relative scale factor

        // calc final position post-scale
        Vector3 FP = zoomCenter + translation * RS;

        // finally, actually perform the scale/translation
        transform.localScale = new Vector3(newScale, newScale, newScale);
        transform.localPosition = FP;

        // Scale Small value
        parameters.Current.UpdateScale(newScale);

        // Scale stroke width
        foreach (var stroke in Strokes)
        {
            stroke.UpdateWidth(newScale);
        }

        surfaceManager.Scale(newScale);

        // Scale grid
        grid.Scale(newScale);
    }

    public void Add(FinalStroke s)
    {
        Strokes.Add(s);

        // Get graph update
        GraphUpdate();
    }

    public void Delete(FinalStroke s, bool mirror)
    {

        if (mirrorPlane.TryGetSymmetric(s, out FinalStroke mirroredToDelete))
        {
            // Clean up links before deleting one or both strokes
            mirrorPlane.Delete(s);
            mirrorPlane.Delete(mirroredToDelete);

            // If mirror is active, delete the mirrored stroke as well
            // Also verify that we're not in the case of a stroke that lies on the mirror (mirrored stroke == stroke)
            // In this case we prevent doing the deletion twice
            if (mirror && mirroredToDelete.ID != s.ID)
            {
                Strokes.Remove(mirroredToDelete);
                mirroredToDelete.Destroy();
            }
        }

        Strokes.Remove(s);
        s.Destroy();

        // Get graph update
        GraphUpdate();
    }

    [SerializeField] private Material surfaceMaterial;  // Assign in Inspector
    private GameObject currentSurfaceMesh;              // Track existing surface mesh

    private void ComputeAndRenderSurfaceMesh()
    {
        // Clear previous surface mesh if any
        if (currentSurfaceMesh != null)
        {
            Destroy(currentSurfaceMesh);
            currentSurfaceMesh = null;
        }

        // Clear previous stroke visuals
        foreach (var obj in sphereObjects)
            Destroy(obj);
        sphereObjects.Clear();

        sphereCenters.Clear();
        sphereRadii.Clear();

        List<CircumSphereMeshInterface.Point3D> pointsList = new List<CircumSphereMeshInterface.Point3D>();
        List<CircumSphereMeshInterface.Point3D> normalsList = new List<CircumSphereMeshInterface.Point3D>();

        foreach (var stroke in Strokes)
        {
            List<Sample> tempSamples = stroke.inputsampleSamples;
            foreach (var s in tempSamples)
            {
                pointsList.Add(new CircumSphereMeshInterface.Point3D { x = s.position.x, y = s.position.y, z = s.position.z });
                normalsList.Add(new CircumSphereMeshInterface.Point3D { x = s.normal.x, y = s.normal.y, z = s.normal.z });
            }
        }

        int numPoints = pointsList.Count;
        if (numPoints == 0)
        {
            Debug.LogWarning("No input points for surface mesh.");
            return;
        }

        int maxVertices = 50000;
        int maxFaces = 50000;

        double[] outVertices = new double[maxVertices * 3];
        int[] outFaces = new int[maxFaces * 3];

        int outVertexCount, outFaceCount;

        int result = CircumSphereMeshInterface.computeSurfaceMesh(
            pointsList.ToArray(),
            normalsList.ToArray(),
            numPoints,
            outVertices,
            outFaces,
            out outVertexCount,
            out outFaceCount,
            maxVertices,
            maxFaces
        );

        if (outVertexCount <= 0 || outFaceCount <= 0)
        {
            Debug.LogWarning("Surface mesh generation returned no data.");
            return;
        }

       // Convert raw vertex array into Vector3[]
        Vector3[] meshVertices = new Vector3[outVertexCount];
        for (int i = 0; i < outVertexCount; i++)
        {
            meshVertices[i] = new Vector3(
                (float)outVertices[3 * i],
                (float)outVertices[3 * i + 1],
                (float)outVertices[3 * i + 2]
            );
        }

        // Convert raw face indices and fix winding order
        int[] triangles = new int[outFaceCount * 3];
        for (int i = 0; i < outFaceCount; i++)
        {
            int idx = i * 3;
            int a = outFaces[idx];
            int b = outFaces[idx + 1];
            int c = outFaces[idx + 2];

            // Flip winding to clockwise (Unity expects clockwise for front face)
            triangles[idx] = a;
            triangles[idx + 1] = c;
            triangles[idx + 2] = b;
        }

        Mesh mesh = new Mesh();
        mesh.indexFormat = (outVertexCount > 65535) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = meshVertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        // Create and parent the surface mesh
        currentSurfaceMesh = Create(new GameObject("CircumSphereMesh"), Primitive.Stroke);
        MeshFilter mf = currentSurfaceMesh.AddComponent<MeshFilter>();
        MeshRenderer mr = currentSurfaceMesh.AddComponent<MeshRenderer>();

        mf.mesh = mesh;
        mr.material = surfaceMaterial != null ? surfaceMaterial : new Material(Shader.Find("Standard"));
    }



    public bool TryAddPatchAt(Vector3 worldPos, bool mirroring)
    {
        Vector3 pos = transform.InverseTransformPoint(worldPos);

        bool lookAtNonManifold = false;
        bool success = Graph.TryFindCycleAt(pos, lookAtNonManifold); 

        if (!success)
        {
            lookAtNonManifold = true;
            success = Graph.TryFindCycleAt(pos, lookAtNonManifold);
        }

        if (success)
        {
            if (mirroring)
            {
                Vector3 mirroredPos = mirrorPlane.Mirror(pos);
                Graph.TryFindCycleAt(mirroredPos, lookAtNonManifold);
            }

            GraphUpdate();
        }

        return success;
    }

    public bool DeleteSelected(Vector3 inputPos, out InteractionType type, out int elementID, bool mirror)
    {
        type = InteractionType.Idle;
        elementID = -1;

        if (selectedPatch != null)
        {
            Debug.Log("deleting patch");
            int toDestroyID = surfaceManager.DeletePatch(selectedPatch.GetID());

            if (toDestroyID != -1)
                Graph.ManualDeletePatch(toDestroyID);

            // Log info
            type = InteractionType.SurfaceDelete;
            elementID = selectedPatch.GetID();

            selectedPatch = null;

            return true;
        }
        else if (selectedStroke != null)
        {
            Delete(selectedStroke, mirror);

            // Log info
            if (selectedStroke.GetComponent<FinalStroke>() != null)
            {
                type = InteractionType.StrokeDelete;
                elementID = selectedStroke.GetComponent<FinalStroke>().ID;
            }
            selectedStroke = null;
            return true;
        }
        else
            return false;
    }


    public void Clear()
    {
        foreach (Stroke s in Strokes)
            s.Destroy();

        mirrorPlane.Clear();
        Strokes = new List<FinalStroke>();
        GraphUpdate();
        Graph.Clear();
    }

    public bool UpdateToDelete(FinalStroke stroke)
    {

        if (selectedStroke == null || !selectedStroke.Equals(stroke))
        {

            // Clear eventual selected things
            ClearToDelete(selectedPatch);
            ClearToDelete(selectedStroke);

            selectedStroke = stroke;
            selectedStroke.OnDeleteSelect();

            return true;
        }
        return false;
    }

    public bool UpdateToDelete(SurfacePatch patch)
    {

        // Stroke selection takes priority
        if (selectedStroke != null)
            return false;

        if (selectedPatch == null || !selectedPatch.Equals(patch))
        {

            // Clear eventual selected things
            ClearToDelete(selectedPatch);
            ClearToDelete(selectedStroke);

            selectedPatch = patch;
            selectedPatch.OnDeleteSelect();
            return true;
        }
        return false;
    }

    public bool ClearToDelete(Stroke toClear)
    {
        if (selectedStroke != null && selectedStroke.Equals(toClear))
        {
            selectedStroke.OnDeleteDeselect();
            selectedStroke = null;
            return true;
        }
        return false;
    }

    public bool ClearToDelete(SurfacePatch toClear)
    {
        if (selectedPatch != null && selectedPatch.Equals(toClear))
        {
            selectedPatch.OnDeleteDeselect();
            selectedPatch = null;
            return true;
        }
        return false;
    }

    // Create a game object as a child of the canvas,
    // reset its own transform so that only the canvas transform impacts world positions
    public GameObject Create(GameObject prefab, Primitive type)
    {
        Transform parent = type.Equals(Primitive.Stroke) ? StrokeContainer.transform
                         : type.Equals(Primitive.Surface) ? SurfaceContainer.transform
                         : gameObject.transform;
        GameObject newObject = Instantiate(prefab, Vector3.zero, Quaternion.identity, parent);
        newObject.transform.localPosition = Vector3.zero;
        newObject.transform.localRotation = Quaternion.identity;
        newObject.transform.localScale = Vector3.one;
        return newObject;
    }


    private void GraphUpdate()
    {
        // ComputeAndStoreCircumspheres();
        ComputeAndRenderSurfaceMesh();
        // Update cycles
        Graph.TryFindAllCycles();

        Graph.Update(out List<ICycle> toAdd, out List<ICycle> toRemove);
        foreach (var c in toRemove)
        {
            surfaceManager.DeletePatch(c.GetPatchID());
        }
        foreach (var c in toAdd)
        {
            surfaceManager.AddPatch(c);
        }

    }
     public void UpdateSpheres()
    {
        // ComputeAndStoreCircumspheres();
        ComputeAndRenderSurfaceMesh();
    }

    
}
