using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using VRSketch;
using Valve.Newtonsoft.Json;

//public enum ExportMode
//{
//    OBJ,
//    Curves,
//}

public class ExportController : MonoBehaviour
{
    [SerializeField]
    private DrawingCanvas canvas = null;


    //public ExportMode exportMode = ExportMode.Curves;

    [Header("Export formats")]

    [SerializeField]
    private bool ExportFinalStrokes = true;

    [SerializeField]
    private bool ExportInputStrokes = true;

    [SerializeField]
    private bool ExportSketchOBJ = true;

    [SerializeField]
    private bool ExportGraphData = true;


    [Header("Export options")]

    public float StrokeWidth;
    public int SubdivisionsPerUnit;

    public bool ClearAfterExport = false;

    [Header("Export folder name")]

    [SerializeField]
    private string ExportFolderName = "SketchData~"; // Tilde makes Unity ignore this folder, so it doesn' t try to "import" the content as Assets (which breaks with the custom .curves format)

    public void ExportSketch(string fileName = null)
    {
        if (ExportSketchOBJ)
        {
            Debug.Log("[EXPORT] Exporting sketch as OBJ.");
            ExportToOBJ(fileName); // default file name
        }

        if (ExportFinalStrokes)
        {
            Debug.Log("[EXPORT] Exporting sketch as .curves (final strokes).");
            //ExportToCurves(fileName, finalStrokes: true); // default file name

        }

        if (ExportInputStrokes)
        {
            Debug.Log("[EXPORT] Exporting sketch as .curves (input strokes).");
            //ExportToCurves(fileName, finalStrokes: false); // default file name
        }

        if (ExportGraphData)
        {
            Debug.Log("[EXPORT] Exporting graph data as JSON file.");
            //ExportCurveNetwork(fileName);
        }

        if (ClearAfterExport)
        {
            Debug.Log("[EXPORT] Clearing the scene.");
            canvas.Clear();
        }
    }

    public void ExportToOBJ(string fileName=null)
    {
        string name = fileName ?? DefaultFileName();

        List<int> strokeIDs = new List<int>();
        List<int> patchIDs = new List<int>();

        // STROKES
        string path = Path.Combine(Application.dataPath, ExportFolderName);

        // Try to create the directory
        TryCreateDirectory(path);

        if (canvas.Strokes.Count > 0)
        {
            string fileNameStrokes = name + "_strokes" + ".obj";
            string fileNameStrokes2 = name + "_points" + ".obj";
            string fullfileNameStrokes = Path.Combine(path, fileNameStrokes);
            string fullfileNameStrokes2 = Path.Combine(path, fileNameStrokes2);
            File.Create(fullfileNameStrokes).Dispose();
            File.Create(fullfileNameStrokes2).Dispose();
            Debug.Log($"counting {canvas.Strokes.Count}");


            string curves = "";
            string curves2 = "";
            ObjExporterScript.Start();

            foreach (FinalStroke s in canvas.Strokes)
            {
                // First compute the mesh for the stroke
                //Mesh mesh = StrokeBrush.Solidify(s.Curve);
               

                Mesh mesh = s.GetGeneratedMesh();

                // Add stroke ID as a group name

                curves += string.Format("g SampleMesh_{0}\n", s.ID);
                string objString = ObjExporterScript.MeshToString(
                                        mesh,
                                        s.GetComponent<Transform>(),
                                        objectSpace: true);
                curves += objString;

                                //
                strokeIDs.Add(s.ID);

            }
             ObjExporterScript.End();

            
             File.WriteAllText(fullfileNameStrokes, curves);
             

             
             ObjExporterScript.Start();
            foreach (FinalStroke s in canvas.Strokes)
            {

                List<Vector3> vertices = new List<Vector3>();
                List<Vector3> normals = new List<Vector3>();
                List<int> triangles = new List<int>(); // Indices for mesh

                // Iterate over samples and extract position & normal
                List<Sample> samples = s.inputsampleSamples;
                for (int i = 0; i < samples.Count; i++)
                {
                    Sample sample = samples[i];

                    vertices.Add(sample.position); // Store position
                    normals.Add(sample.normal);   // Store normal
                }

                // Create a simple triangulation (e.g., connecting consecutive points)
               
                // Create a new mesh
                Mesh sampleMesh = new Mesh();
                sampleMesh.SetVertices(vertices);
                sampleMesh.SetNormals(normals);
                //sampleMesh.SetTriangles(triangles, 0);

                // Add stroke ID as a group name
                curves2 += string.Format("g SampleMesh_{0}\n", s.ID);

                // Convert the mesh to an OBJ string
                string objString2 = ObjExporterScript.MeshToString(
                                        sampleMesh,
                                        s.GetComponent<Transform>(),
                                        objectSpace: true);

                curves2 += objString2;
                                //
                strokeIDs.Add(s.ID);

            }

            ObjExporterScript.End();
            File.WriteAllText(fullfileNameStrokes2, curves2);


        }

        string referencemesh = name + "_final" + ".obj";
        string fullfileNameStrokes3 = Path.Combine(path, referencemesh);
        File.Create(fullfileNameStrokes3).Dispose();

        if (canvas.finalmesh != null)
        {
            ObjExporterScript.Start();
            string objString3 = ObjExporterScript.MeshToString(
                                    canvas.finalmesh,
                                    transform,
                                    objectSpace: true);

            ObjExporterScript.End();

            File.WriteAllText(fullfileNameStrokes3, objString3);
        }



        // PATCHES
        SurfacePatch[] allPatches = canvas.GetComponentsInChildren<SurfacePatch>();

        if (allPatches.Length > 0)
        {
            string fileNamePatches = name + "_patches" + ".obj";
            string fullfileNamePatches = Path.Combine(path, fileNamePatches);
            File.Create(fullfileNamePatches).Dispose();

            string patches = "";
            ObjExporterScript.Start();



            foreach (SurfacePatch s in allPatches)
            {
                // Add cycle ID as a group name
                patches += string.Format("g {0}\n", s.GetID());

                string objString = ObjExporterScript.MeshToString(
                                        s.GetComponent<MeshFilter>().sharedMesh,
                                        s.GetComponent<Transform>(),
                                        objectSpace: true);
                patches += objString;

                patchIDs.Add(s.GetID());
            }
            ObjExporterScript.End();
            File.WriteAllText(fullfileNamePatches, patches);
        }

        // SKETCH END DATA
        SketchEndData sketchData = new SketchEndData(strokeIDs, patchIDs);

        var sketchDataName = name + "_sketch.json";
        var fullfileNameSketch = Path.Combine(path, sketchDataName);

        File.WriteAllText(fullfileNameSketch, JsonConvert.SerializeObject(sketchData, new JsonSerializerSettings
        {
            Culture = new System.Globalization.CultureInfo("en-US")
        }));

    }

    public void ExportToCurves(string fileName = null, bool finalStrokes = true)
    {
        string name = fileName ?? DefaultFileName();

        string path = Path.Combine(Application.dataPath, ExportFolderName);

        // Try to create the directory
        TryCreateDirectory(path);

        string fileNameCurve = name;

        if (!finalStrokes)
            fileNameCurve += "-input";

        fileNameCurve += ".curves";
        string fullfileNameCurve = Path.Combine(path, fileNameCurve);

        File.Create(fullfileNameCurve).Dispose();

        StringBuilder curves = new StringBuilder();

        foreach (FinalStroke s in canvas.Strokes)
        {

            if (finalStrokes)
            {
                string curveString = CurvesExport.CurveToPolyline(s.Curve, SubdivisionsPerUnit);
                curves.Append(curveString);
            }
            else
            {
                string stroke = CurvesExport.SamplesToPolyline(s.inputSamples);
                curves.Append(stroke);
            }
        }

        File.WriteAllText(fullfileNameCurve, curves.ToString());
    }

    public void ExportCurveNetwork(string fileName=null)
    {
        string name = fileName ?? DefaultFileName();

        string path = Path.Combine(Application.dataPath, ExportFolderName);

        // Try to create the directory
        TryCreateDirectory(path);

        string fileNameNet = name + "_graph.json";

        List<SerializableStrokeInGraph> strokes = new List<SerializableStrokeInGraph>();
        List<SerializableSegment> segments = new List<SerializableSegment>();
        List<SerializableNode> nodes = canvas.Graph.GetNodesData();

        foreach (FinalStroke s in canvas.Strokes)
        {
            (SerializableStrokeInGraph strokeData, List<SerializableSegment> strokeSegments) = s.GetGraphData();
            strokes.Add(strokeData);
            segments.AddRange(strokeSegments);
        }


        CurveNetworkData graphData = new CurveNetworkData(strokes, segments, nodes);

        File.WriteAllText(Path.Combine(path, fileNameNet), JsonConvert.SerializeObject(graphData, new JsonSerializerSettings
        {
            Culture = new System.Globalization.CultureInfo("en-US")
        }));
    }

    private string DefaultFileName()
    {
        return (DateTime.Now).ToString("yyyyMMddHHmmss");
    }

    private void TryCreateDirectory(string path)
    {
        // Try to create the directory
        try
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

        }
        catch (IOException ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}
