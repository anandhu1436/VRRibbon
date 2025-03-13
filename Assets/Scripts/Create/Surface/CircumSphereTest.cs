using System.Collections.Generic;
using UnityEngine;
using VRSketch;
using System.Runtime.InteropServices;
using System;



public class CircumSphereTest : MonoBehaviour
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Point3D
    {
        public double x, y, z;
    }

    [DllImport("circumsphereDLL")]
    public static extern int computeCircumspheres(
        [In] Point3D[] points, [In] Point3D[] normals, int numPoints,
        [Out] Point3D[] outCenters, [Out] double[] outRadii, int maxOutputSize
    );

    public void Start()
    {
        TestCircumSphere();
    }

    void TestCircumSphere()
    {
        // Define 8 vertices of a unit cube centered at origin
        Point3D[] points = new Point3D[]
        {
            new Point3D { x = -0.5, y = -0.5, z = -0.5 },
            new Point3D { x =  0.5, y = -0.5, z = -0.5 },
            new Point3D { x = -0.5, y =  0.5, z = -0.5 },
            new Point3D { x =  0.5, y =  0.5, z = -0.5 },
            new Point3D { x = -0.5, y = -0.5, z =  0.5 },
            new Point3D { x =  0.5, y = -0.5, z =  0.5 },
            new Point3D { x = -0.5, y =  0.5, z =  0.5 },
            new Point3D { x =  0.5, y =  0.5, z =  0.5 }
        };

        // Define outward diagonal normals (cube's space diagonal direction)
        Point3D[] normals = new Point3D[]
        {
            new Point3D { x = -1, y = -1, z = -1 },
            new Point3D { x =  1, y = -1, z = -1 },
            new Point3D { x = -1, y =  1, z = -1 },
            new Point3D { x =  1, y =  1, z = -1 },
            new Point3D { x = -1, y = -1, z =  1 },
            new Point3D { x =  1, y = -1, z =  1 },
            new Point3D { x = -1, y =  1, z =  1 },
            new Point3D { x =  1, y =  1, z =  1 }
        };

        // Normalize normals (to unit length)
        for (int i = 0; i < normals.Length; i++)
        {
            double length = Math.Sqrt(normals[i].x * normals[i].x + normals[i].y * normals[i].y + normals[i].z * normals[i].z);
            normals[i].x /= length;
            normals[i].y /= length;
            normals[i].z /= length;
        }

        int numPoints = points.Length;
        Point3D[] outCenters = new Point3D[10];
        double[] outRadii = new double[10];

        int count = computeCircumspheres(points, normals, numPoints, outCenters, outRadii, 10);

        Debug.Log($"Circumspheres computed: {count}");

        for (int i = 0; i < count; i++)
        {
            Debug.Log($"Center {i}: ({outCenters[i].x}, {outCenters[i].y}, {outCenters[i].z}), Radius: {outRadii[i]}");
        }
    }
}
