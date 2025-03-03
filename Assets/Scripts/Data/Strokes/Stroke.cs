using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using VRSketch;
using Curve;

public abstract class Stroke : MonoBehaviour
{
    public bool CanvasSpaceConstantScale = true;
    public int SubdivisionsPerUnit = 150;
    public float BaseCurveWidth = 0.005f;

    private StrokeAppearance strokeAppearance;
    protected LineRenderer lineRenderer;
    protected MeshRenderer meshRenderer;
    protected MeshFilter meshFilter;
    protected Mesh generatedMesh;
    MeshCollider collider;

    protected virtual void Awake()
    {
        strokeAppearance = gameObject.GetComponent<StrokeAppearance>();
        lineRenderer = GetComponent<LineRenderer>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();
        // Ensure mesh collider is updated
        collider = gameObject.GetComponent<MeshCollider>();
        
    }

    public abstract void RenderAsLine(float scale);

    protected void RenderPoints(Vector3[] points, float scale)
    {
        lineRenderer.enabled = true;
        lineRenderer.widthMultiplier = CanvasSpaceConstantScale ? BaseCurveWidth * scale : BaseCurveWidth;
        lineRenderer.positionCount = points.Length;
        lineRenderer.SetPositions(points);
    }
    protected void RenderMesh()
    {
        meshRenderer.enabled=true;
        
    }

        public void SetMesh(Mesh mesh)
    {
    if (mesh == null)
    {
        Debug.LogError("SetMesh() was called with a null mesh!");
        return;
    }

    if (mesh.vertexCount == 0 || mesh.triangles.Length == 0)
    {
        Debug.LogError("SetMesh() -> Mesh has no vertices or triangles!");
        return;
    }

    generatedMesh = mesh;
    Debug.Log($"Mesh assigned successfully. Vertices: {mesh.vertexCount}, Triangles: {mesh.triangles.Length / 3}");

    // Ensure MeshFilter exists
    if (meshFilter == null)
    {
        meshFilter = gameObject.AddComponent<MeshFilter>();
        Debug.Log("MeshFilter component was missing and added.");
    }

    meshFilter.mesh.Clear();
    meshFilter.mesh = mesh;
    meshFilter.mesh.RecalculateNormals();
    meshFilter.mesh.RecalculateBounds();
    Debug.Log("MeshFilter updated successfully.");

    // Ensure MeshRenderer exists
    if (meshRenderer == null)
    {
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        Debug.Log("MeshRenderer component was missing and added.");
    }

    // Assign material if missing
    if (meshRenderer.material == null)
    {
        meshRenderer.material = new Material(Shader.Find("Standard"));
        Debug.LogWarning("MeshRenderer had no material, assigning default material.");
    }
    meshRenderer.material = lineRenderer.material;

    meshRenderer.enabled = true;
    Debug.Log("MeshRenderer enabled.");

    // Ensure the object is visible
    if (transform.localScale == Vector3.zero)
    {
        transform.localScale = Vector3.one;
        Debug.LogWarning("Object scale was zero! Resetting to (1,1,1).");
    }

    
    if (collider != null)
    {
        collider.sharedMesh = null;
        collider.sharedMesh = mesh;
        Debug.Log("MeshCollider updated.");
    }
    }



    public Mesh GetGeneratedMesh()
    {
        return generatedMesh;
    }


    public void UpdateWidth(float newScale)
    {
        if (CanvasSpaceConstantScale)
            lineRenderer.widthMultiplier = BaseCurveWidth * newScale;
    }

    public void UpdateCollider(Mesh colliderMesh)
    {
        // Generate collider
        MeshCollider collider = gameObject.GetComponent<MeshCollider>();
        collider.sharedMesh = null;
        collider.sharedMesh = colliderMesh;
    }

    public void OnDeleteSelect()
    {
        strokeAppearance.OnDeleteSelect();
    }

    public void OnDeleteDeselect()
    {
        strokeAppearance.OnDeleteDeselect();
    }


    public virtual void Destroy()
    {
        Destroy(gameObject);
    }
}
