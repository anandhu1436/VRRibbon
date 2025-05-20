using System;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Collections.Generic;

public class CircumSphereMeshInterface : MonoBehaviour
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Point3D
    {
        public double x, y, z;
    }

    [DllImport("circumsphereDLL")]
    public static extern int computeSurfaceMesh(
        [In] Point3D[] points,
        [In] Point3D[] normals,
        int numPoints,
        [Out] double[] outVertices,  // x0,y0,z0,x1,y1,z1,...
        [Out] int[] outFaces,        // triplets of vertex indices
        out int outVertexCount,      // <-- newly added
        out int outFaceCount,        // <-- newly added
        int maxVertices,
        int maxFaces
    );

    public static void ComputeCircumSphereMesh(Vector3[] inputPoints, Vector3[] inputNormals, out Vector3[] vertices, out int[] triangles)
    {
        if (inputPoints.Length != inputNormals.Length)
            throw new ArgumentException("Points and normals arrays must have the same length.");

        int numPoints = inputPoints.Length;
        Point3D[] points = new Point3D[numPoints];
        Point3D[] normals = new Point3D[numPoints];

        for (int i = 0; i < numPoints; i++)
        {
            points[i] = new Point3D { x = inputPoints[i].x, y = inputPoints[i].y, z = inputPoints[i].z };
            Vector3 n = inputNormals[i].normalized;
            normals[i] = new Point3D { x = n.x, y = n.y, z = n.z };
        }

        int maxVertices = 9000;
        int maxFaces = 9000;
        double[] outVerticesRaw = new double[maxVertices * 3];
        int[] outFacesRaw = new int[maxFaces * 3];

        int outVertexCount, outFaceCount;

        int result = computeSurfaceMesh(
            points,
            normals,
            numPoints,
            outVerticesRaw,
            outFacesRaw,
            out outVertexCount,
            out outFaceCount,
            maxVertices,
            maxFaces
        );

        // Fill vertices array
        vertices = new Vector3[outVertexCount];
        for (int i = 0; i < outVertexCount; i++)
        {
            vertices[i] = new Vector3(
                (float)outVerticesRaw[3 * i],
                (float)outVerticesRaw[3 * i + 1],
                (float)outVerticesRaw[3 * i + 2]
            );
        }

        // Fill triangles array
        triangles = new int[outFaceCount * 3];
        Array.Copy(outFacesRaw, triangles, outFaceCount * 3);
    }
}
