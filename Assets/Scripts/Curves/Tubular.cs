using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Curve;

/*
    This code was adapted from https://github.com/mattatz/unity-tubular and somewhat broken down to keep only what was necessary for this project
    Check out mattatz original repository for better generation of tubular meshes, with regular sampling along the curve.
 */

namespace Tubular {

    // public class Tubular {

    //     const float PI2 = Mathf.PI * 2f;

    //     public static Mesh Build(Curve.Curve curve, int tubularSegments, float radius, int radialSegments = 8, Color? baseColor = null, bool closed = false, bool variableWidth = false, bool variableOpacity = false)
    //     {
    //         Color color = baseColor ?? Color.white;

    //         var vertices = new List<Vector3>();
    //         var normals = new List<Vector3>();
    //         var tangents = new List<Vector4>();
    //         List<Color> colors = new List<Color>();
    //         var uvs = new List<Vector2>();
    //         var indices = new List<int>();

    //         var frames = curve.ComputeFrenetFrames(tubularSegments, Vector3.zero, closed);

    //         for (int i = 0; i < tubularSegments; i++)
    //         {
    //             GenerateSegment(curve, frames, tubularSegments, radius, radialSegments, color, variableWidth, variableOpacity, vertices, normals, tangents, colors, i);
    //         }
    //         GenerateSegment(curve, frames, tubularSegments, radius, radialSegments, color, variableWidth, variableOpacity, vertices, normals, tangents, colors, (!closed) ? tubularSegments : 0);

    //         for (int i = 0; i <= tubularSegments; i++)
    //         {
    //             for (int j = 0; j <= radialSegments; j++)
    //             {
    //                 float u = 1f * j / radialSegments;
    //                 float v = 1f * i / tubularSegments;
    //                 uvs.Add(new Vector2(u, v));
    //             }
    //         }

    //         for (int j = 1; j <= tubularSegments; j++)
    //         {
    //             for (int i = 1; i <= radialSegments; i++)
    //             {
    //                 int a = (radialSegments + 1) * (j - 1) + (i - 1);
    //                 int b = (radialSegments + 1) * j + (i - 1);
    //                 int c = (radialSegments + 1) * j + i;
    //                 int d = (radialSegments + 1) * (j - 1) + i;

    //                 // faces
    //                 indices.Add(a); indices.Add(d); indices.Add(b);
    //                 indices.Add(b); indices.Add(d); indices.Add(c);
    //             }
    //         }

    //         var mesh = new Mesh();
    //         mesh.vertices = vertices.ToArray();
    //         mesh.normals = normals.ToArray();
    //         mesh.tangents = tangents.ToArray();
    //         mesh.colors = colors.ToArray();
    //         mesh.uv = uvs.ToArray();
    //         mesh.SetIndices(indices.ToArray(), MeshTopology.Triangles, 0);
    //         return mesh;
    //     }

    //     static void GenerateSegment(
    //         Curve.Curve curve,
    //         List<FrenetFrame> frames,
    //         int tubularSegments,
    //         float maxRadius,
    //         int radialSegments,
    //         Color baseColor,
    //         bool variableWidth,
    //         bool variableOpacity,
    //         List<Vector3> vertices,
    //         List<Vector3> normals,
    //         List<Vector4> tangents,
    //         List<Color> colors,
    //         int i
    //     )
    //     {
    //         // TODO: we could probably do better than uniform sampling here,
    //         // but this works ok
    //         var u = 1f * i / tubularSegments;
    //         var p = curve.GetPoint(u);
    //         var fr = frames[i];

    //         var N = fr.Normal;
    //         var B = fr.Binormal;

    //         float radius = maxRadius;
    //         float opacity = 1.0f;

    //         if (variableWidth)
    //             radius *= curve.GetWeight(u);
    //         if (variableOpacity)
    //             opacity *= curve.GetWeight(u);

    //         for (int j = 0; j <= radialSegments; j++)
    //         {
    //             float v = 1f * j / radialSegments * PI2;
    //             var sin = Mathf.Sin(v);
    //             var cos = Mathf.Cos(v);

    //             Vector3 normal = (cos * N + sin * B).normalized;
    //             vertices.Add(p + radius * normal);
    //             normals.Add(normal);

    //             var tangent = fr.Tangent;
    //             tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, 0f));

    //             colors.Add(new Color(baseColor.r, baseColor.g, baseColor.b, opacity));
    //         }
    //     }

    // }
    // not using anywhere
    public class Ribbon {
        public static Mesh Build(Curve.Curve curve, int segments, float width, Color? baseColor = null)
        {
            Color test=new Color(0.0f, 0.0f, 1.0f);
            Color color = baseColor ?? test;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var indices = new List<int>();
            var uvs = new List<Vector2>();
            var frames = curve.ComputeFrenetFrames(segments, Vector3.zero, false);

            for (int i = 0; i < segments; i++)
            {
                float u = (float)i / segments;
                Vector3 p = curve.GetPoint(u);
                Vector3 normal = frames[i].Normal;
                
                Vector3 left = p - (normal * (width * 0.5f));
                Vector3 right = p + (normal * (width * 0.5f));
                
                vertices.Add(left);
                vertices.Add(right);
                normals.Add(normal);
                normals.Add(normal);
                
                uvs.Add(new Vector2(0, u));
                uvs.Add(new Vector2(1, u));
            }

            for (int i = 0; i < segments - 1; i++)
            {
                int a = i * 2;
                int b = a + 1;
                int c = a + 2;
                int d = a + 3;

                indices.Add(a); indices.Add(b); indices.Add(c);
                indices.Add(b); indices.Add(d); indices.Add(c);
            }

            var mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.normals = normals.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.SetIndices(indices.ToArray(), MeshTopology.Triangles, 0);
            return mesh;
        }
    }
}




