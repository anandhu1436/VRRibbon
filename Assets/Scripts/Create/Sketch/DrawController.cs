using UnityEngine;
using Curve;
using VRSketch;
using System.Collections.Generic;
using System.IO;


public class DrawController : MonoBehaviour
{
    [Header("Stroke prefabs")]
    // STROKE
    public GameObject inputStrokePrefab;
    public GameObject finalStrokePrefab;

    [Header("References")]

    [SerializeField]
    private CASSIEParametersProvider parameters = null;

    // BEAUTIFIER
    // public Beautifier beautifier;

    // SCENE DATA
    public DrawingCanvas canvas;
    public SurfaceManager surfaceManager;
    public Grid3D grid;
    public MirrorPlane mirrorPlane;
    public Material ReferenceMaterial;

    // Reference to check for collisions on stroke start
    public BrushCollisions collisionDetector;

    [Header("Parameters")]
    // PARAMETERS (systems)
    public bool Beautification;


    private int subdivisionsPerUnit;
    private float colliderRadius;


    private int finalStrokeID = 0;
    private InputStroke currentStroke = null;
    private int currentSelectedPatchID = -1;
    private GameObject loadedMeshObject = null;


    private void Start()
    {
        FinalStroke s = finalStrokePrefab.GetComponent<FinalStroke>();

        subdivisionsPerUnit = Mathf.CeilToInt(s.SubdivisionsPerUnit * 0.5f); // Reduce resolution by half
        colliderRadius = s.BaseCurveWidth * 0.5f; // The collider around the stroke is exactly the same radius as the stroke (BaseCurveWidth gives the diameter)
    }

    public void Init(bool surfacing)
    {
        finalStrokeID = 0;
        canvas.Init(surfacing);
    }

    public void SwitchSystem(bool surfacing)
    {
        // Set parameter
        canvas.SwitchSystem(surfacing);
    }
    public void LoadAndDisplayOBJ(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError("OBJ file not found at " + path);
            return;
        }

        string objText = File.ReadAllText(path);
        Mesh mesh = OBJLoaderHelper.LoadOBJ(objText);

        if (mesh == null)
        {
            Debug.LogError("Failed to load mesh from OBJ file.");
            return;
        }

        if (loadedMeshObject != null)
            Destroy(loadedMeshObject); // Remove previous mesh if exists

        loadedMeshObject = new GameObject("LoadedOBJ");
        loadedMeshObject.transform.SetParent(canvas.transform, worldPositionStays: false);
        MeshFilter filter = loadedMeshObject.AddComponent<MeshFilter>();
        filter.mesh = mesh;
        MeshRenderer renderer = loadedMeshObject.AddComponent<MeshRenderer>();
        renderer.material = ReferenceMaterial;
        canvas.referencemesh=mesh;
    }


    public void ShowMesh()
    {
        if (loadedMeshObject != null)
            loadedMeshObject.SetActive(true);
    }

    public void HideMesh()
    {
        if (loadedMeshObject != null)
            loadedMeshObject.SetActive(false);
    }


    public void NewStroke(Vector3 position)
    {
        GameObject strokeObject = canvas.Create(inputStrokePrefab, Primitive.Stroke);
        currentStroke = strokeObject.GetComponent<InputStroke>();

        // Check for stroke start point constraint
        TryAddGridConstraint(canvas.transform.InverseTransformPoint(position));
        collisionDetector.QueryCollision();

        if (currentSelectedPatchID != -1)
        {
            AddInSurfaceConstraint(currentSelectedPatchID, position);
        }
    }

    public void UpdateStroke(Vector3 position, Quaternion rotation, Vector3 velocity, float pressure)
    {
        if (!currentStroke.ShouldUpdate(position, parameters.Current.MinSamplingDistance))
            return;

        // Check if current selected patch is still nearby
        UpdateSelectedPatch(position);

        Vector3 brushNormal = canvas.transform.InverseTransformDirection(rotation * new Vector3(0, 0, -1)); // seems fine
        Vector3 brushtangent = canvas.transform.InverseTransformDirection(rotation * new Vector3(0, 1, 0)); // seems fine
        Vector3 relativePos = canvas.transform.InverseTransformPoint(position);

        //Debug.DrawLine(samplePos, samplePos + brushNormal * 0.1f, Color.white, 100f);

        Sample s = new Sample(relativePos, brushNormal, brushtangent, pressure, velocity);

        currentStroke.AddSample(s);

        TryAddGridConstraint(relativePos);

        if (currentStroke.Samples.Count > 1)
            currentStroke.SetMesh(currentStroke.Build(0.01f));
            RenderStroke(currentStroke); // Draw current stroke
    }


    
    public bool CommitStroke(Vector3 position, out SerializableStroke strokeData, bool mirror = false)
    {
        // Create final stroke game object and render the stroke
        /*
        GameObject strokeObject = canvas.Create(finalStrokePrefab, Primitive.Stroke);
        (Curve.Curve snappedCurve,
        IntersectionConstraint[] intersections,
        MirrorPlaneConstraint[] mirrorIntersections) =
                beautifier.Beautify(
                    currentStroke,
                    Beautification,
                    mirror,
                    out List<SerializableConstraint> appliedConstraints,
                    out List<SerializableConstraint> rejectedConstraints,
                    out bool planar,
                    out bool onSurface,
                    out bool onMirror,
                    out bool closedLoop);
        */

        GameObject strokeObject = canvas.Create(finalStrokePrefab, Primitive.Stroke);

        FinalStroke finalStroke = strokeObject.GetComponent<FinalStroke>();
        finalStroke.SetID(finalStrokeID);
        finalStrokeID++;
        //finalStroke.SetCurve(snappedCurve, closedLoop: closedLoop);
        finalStroke.SaveInputSamples(currentStroke.GetPoints().ToArray(),currentStroke.Samples);

        canvas.Add(finalStroke);

        finalStroke.SetMesh(currentStroke.GetGeneratedMesh());
        RenderStroke(finalStroke);



            strokeData = new SerializableStroke(
            finalStroke.ID
            );

        currentStroke.Destroy();
        currentStroke = null;

        return true; // success in creating final stroke
    }


    private void RenderStroke(Stroke s)
    {
        s.RenderAsLine(canvas.transform.localScale.x);
    }



    // Called only when stroke is done drawing, to generate its collider
    // soldify only for final stroke
    private void SolidifyStroke(FinalStroke s)
    {
        //Mesh strokeMesh = brush.Solidify(s.Curve, true);
        int tubularSegments = Mathf.CeilToInt(s.Curve.GetLength() * subdivisionsPerUnit);

        //Mesh strokeMesh = Tubular.Ribbon.Build(s.Curve, tubularSegments, 5*colliderRadius);

         // Assign the generated mesh
        //s.SetMesh(strokeMesh);

        //s.UpdateCollider(strokeMesh);
    }

    public bool AddConstraint(Vector3 collisionPos, GameObject collided)
    {
        // If is drawing, create a new constraint and add it to the current stroke
        if (currentStroke)
        {
            // Find constraint type and exact position
            Constraint constraint;
            Vector3 relativePos = canvas.transform.InverseTransformPoint(collisionPos); // position of collision in canvas space

            switch (collided.tag)
            {
                case "GridPoint":
                    constraint = new Constraint(collided.transform.localPosition); // Grid point pos in canvas space
                    break;
                case "FinalStroke":
                    FinalStroke stroke = collided.GetComponent<FinalStroke>();
                    // Check if the curve can serve to create constraints
                    if (stroke != null)
                    {
                        constraint = stroke.GetConstraint(relativePos, parameters.Current.SnapToExistingNodeThreshold);
                    }
                    // Otherwise give up this constraint
                    else
                    {
                        constraint = new Constraint(relativePos);
                    }
                    break;
                case "MirrorPlane":
                    //Debug.Log("[Mirror] collided with plane");
                    Vector3 onPlanePos = mirrorPlane.Project(relativePos);
                    constraint = new MirrorPlaneConstraint(onPlanePos, mirrorPlane.GetNormal());
                    break;
                default:
                    //constraint = new Constraint(relativePos);
                    constraint = null;
                    break;
            }

            if (constraint != null)
            {
                currentStroke.AddConstraint(constraint, parameters.Current.MergeConstraintsThreshold);

                // Constraint was successfully added
                return true;
            }
        }
        
        // Didn't add constraint
        return false;
    }

    public void OnPatchCollide(int patchID, Vector3 pos)
    {
        if (currentSelectedPatchID != patchID)
        {
            if (currentSelectedPatchID != -1)
            {
                // Check whether we should select new patch or keep old one
                if (
                    !surfaceManager.ProjectOnPatch(patchID, pos, out Vector3 posOnPatch, canvasSpace: true)
                    || Vector3.Distance(pos, posOnPatch) > parameters.Current.ProximityThreshold)
                {
                    currentSelectedPatchID = patchID;

                    // Add constraint of surface ingoing point, if currently drawing
                    if (currentStroke)
                    {
                        AddInSurfaceConstraint(currentSelectedPatchID, pos);
                    }
                }
            }
            else
            {
                currentSelectedPatchID = patchID;

                // Add constraint of surface ingoing point, if currently drawing
                if (currentStroke)
                {
                    AddInSurfaceConstraint(currentSelectedPatchID, pos);
                }
            }

        }

    }

    public void OnPatchDeselect(Vector3 pos)
    {
        if (currentStroke)
        {
            // If we're currently drawing, prevent patch deselect based on pure collision events
            // we want to keep drawing projected on the same patch as we started

        }
        else
        {
            DeselectPatch(pos);
        }
    }

    private void DeselectPatch(Vector3 pos)
    {
        // Add constraint of surface outgoing point to draw controller
        if (currentStroke)
        {
            AddOutSurfaceConstraint(currentSelectedPatchID, pos);
        }
        else
            surfaceManager.OnDetailDrawStop(currentSelectedPatchID); // still send stop event to surface patch (to have correct appearance)
        currentSelectedPatchID = -1;
    }

    private bool AddInSurfaceConstraint(int patchID, Vector3 onStrokePos)
    {
        if (currentStroke)
        {
            // Check whether we are actually still close to the patch
            if (
                !surfaceManager.ProjectOnPatch(patchID, onStrokePos, out Vector3 posOnPatch, canvasSpace: false)
                || Vector3.Distance(onStrokePos, posOnPatch) > parameters.Current.ProximityThreshold)
            {
                //Debug.Log("went too far from patch");
                currentSelectedPatchID = -1;
                return false;
            }
            //Debug.Log("add constraint to patch " + patchID);
            surfaceManager.OnDetailDrawStart(patchID);
            currentStroke.InConstrainToSurface(patchID, canvas.transform.InverseTransformPoint(onStrokePos));
            return true;
        }
        return false;
    }

    private bool AddOutSurfaceConstraint(int patchID, Vector3 onStrokePos)
    {
        if (currentStroke)
        {
            surfaceManager.OnDetailDrawStop(patchID);
            currentStroke.OutConstrainToSurface(patchID, canvas.transform.InverseTransformPoint(onStrokePos));
            return true;
        }
        return false;
    }

    private bool GetProjectedPos(Vector3 brushPos, out Vector3 projectedPos, float maxDist)
    {
        projectedPos = brushPos;

        if (currentSelectedPatchID == -1)
            return false;

        if (surfaceManager.ProjectOnPatch(currentSelectedPatchID, brushPos, out projectedPos))
        {
            if (Vector3.Distance(projectedPos, brushPos) < maxDist)
            {
                return true;
            }
        }
        return false;
    }

    private void UpdateSelectedPatch(Vector3 brushPos)
    {

        if (currentSelectedPatchID != -1)
        {
            // A patch is currently selected,
            // Check whether it should still be selected,
            // otherwise, explicitly deselect it

            Vector3 projectedPos;
            if (!GetProjectedPos(brushPos, out projectedPos, maxDist: parameters.Current.ProximityThreshold))
            {
                //if (currentStroke)
                //    Debug.Log("got too far from patch while drawing");
                DeselectPatch(brushPos);
            }
        }
    }

    private bool TryAddGridConstraint(Vector3 pos)
    {
        if (grid.TryFindConstraint(pos, parameters.Current.ProximityThreshold, out Vector3 gridPointPos))
        {
            // Check for potential stroke/stroke intersection
            Collider[] overlapped = Physics.OverlapSphere(canvas.transform.TransformPoint(gridPointPos), parameters.Current.ProximityThreshold);
            Constraint constraint = null;
            Collider prioritary = null;
            foreach (var obj in overlapped)
            {
                if (obj.CompareTag("FinalStroke") && obj.GetComponent<FinalStroke>() != null)
                {
                    // Add stroke/stroke constraint insteand
                    //constraint = obj.GetComponent<FinalStroke>().GetConstraint(pos, parameters.Current.SnapToExistingNodeThreshold);
                    prioritary = obj;
                }
                // Second prioritary is mirror plane
                if (obj.CompareTag("MirrorPlane") && prioritary == null)
                {
                    prioritary = obj;
                    // Add stroke/mirror constraint insteand
                    constraint = new MirrorPlaneConstraint(gridPointPos, mirrorPlane.GetNormal());
                }
            }
            if (constraint == null)
                constraint = new Constraint(gridPointPos); // Grid point pos is in canvas space

            currentStroke.AddConstraint(constraint, parameters.Current.MergeConstraintsThreshold);
            //Debug.Log("found grid constraint at " + pos.ToString("F3"));
            return true;
        }
        else
            return false;
    }

    private void TrimDanglingEndpoints(Curve.Curve curve, IntersectionConstraint[] intersections, MirrorPlaneConstraint[] mirrorIntersections)
    {
        Constraint firstIntersection;
        Constraint lastIntersection;

        // Initialize intersections
        if (intersections.Length > 0)
        {
            firstIntersection = intersections[0];
            lastIntersection = intersections[0];
        }
        else if (mirrorIntersections.Length > 0)
        {
            firstIntersection = mirrorIntersections[0];
            lastIntersection = mirrorIntersections[0];
        }
        else
        {
            // No intersections => won't trim endpoints
            return;
        }

        foreach (var intersection in intersections)
        {
            if (intersection.NewCurveData.t < firstIntersection.NewCurveData.t)
            {
                firstIntersection = intersection;
            }

            else if (intersection.NewCurveData.t > lastIntersection.NewCurveData.t)
            {
                lastIntersection = intersection;
            }

        }

        foreach (var intersection in mirrorIntersections)
        {
            if (intersection.NewCurveData.t < firstIntersection.NewCurveData.t)
            {
                firstIntersection = intersection;
            }

            else if (intersection.NewCurveData.t > lastIntersection.NewCurveData.t)
            {
                lastIntersection = intersection;
            }
        }

        float firstIntersectionParam = firstIntersection.NewCurveData.t;

        // Total length
        float length = curve.GetLength();

        // First segment length
        float firstSegmentLength = curve.LengthBetween(0f, firstIntersectionParam);

        // Cut if first segment is sufficiently small (yet still exists)
        if (firstSegmentLength > Constants.eps && firstSegmentLength < parameters.Current.MaxHookSectionStrokeRatio * length && firstSegmentLength < parameters.Current.MaxHookSectionLength)
        {
            //Debug.Log("cutting stroke at t = " + firstIntersectionParam);
            Reparameterization? r = curve.CutAt(firstIntersectionParam, throwBefore: true, snapToExistingAnchorThreshold: parameters.Current.SmallDistance * 0.1f);

            foreach (var intersection in intersections)
            {
                intersection.ReparameterizeNewCurve(r, curve);
            }

            foreach (var intersection in mirrorIntersections)
            {
                intersection.ReparameterizeNewCurve(r, curve);
            }

        }

        float lastIntersectionParam = lastIntersection.NewCurveData.t;

        length = curve.GetLength();
        float lastSegmentLength = curve.LengthBetween(lastIntersectionParam, 1f);

        // Cut if last segment is sufficiently small (yet still exists)
        if (lastSegmentLength > Constants.eps && lastSegmentLength < parameters.Current.MaxHookSectionStrokeRatio * length && lastSegmentLength < parameters.Current.MaxHookSectionLength)
        {
            //Debug.Log("cutting stroke at t = " + lastIntersectionParam);
            Reparameterization? r = curve.CutAt(lastIntersectionParam, throwBefore: false, snapToExistingAnchorThreshold: parameters.Current.SmallDistance * 0.1f);

            foreach (var intersection in intersections)
            {
                intersection.ReparameterizeNewCurve(r, curve);
            }


            foreach (var intersection in mirrorIntersections)
            {
                intersection.ReparameterizeNewCurve(r, curve);
            }
        }
    }

}

