using UnityEngine;
using System.Collections.Generic;
using System.IO;

public static class OBJLoaderHelper
{
    public static Mesh LoadOBJ(string objText)
    {
        var mesh = new Mesh();
        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        var lines = objText.Split('\n');

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim(); // Remove leading/trailing spaces

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue; // Skip empty or comment lines

            if (line.StartsWith("v "))
            {
                var parts = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                vertices.Add(new Vector3(
                    float.Parse(parts[1]),
                    float.Parse(parts[2]),
                    float.Parse(parts[3])));
            }
            else if (line.StartsWith("f "))
            {
                var parts = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

                List<int> faceIndices = new List<int>();

                for (int i = 1; i < parts.Length; i++)
                {
                    string vertexPart = parts[i];
                    string[] tokens = vertexPart.Split('/');

                    int vertexIndex = int.Parse(tokens[0]) - 1; // OBJ indices start at 1
                    faceIndices.Add(vertexIndex);
                }

                // Triangulate any polygon face (triangle, quad, ngons)
                for (int i = 1; i < faceIndices.Count - 1; i++)
                {
                    triangles.Add(faceIndices[0]);
                    triangles.Add(faceIndices[i]);
                    triangles.Add(faceIndices[i + 1]);
                }
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();

        return mesh;
    }
}
