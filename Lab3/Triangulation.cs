using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Triangulation : MonoBehaviour
{
    static readonly float edgeEnergyThreshold = 0.1f;
    static readonly float lineSignThreshold = 0.01f;

    private static List<int> _triangulationConvexVertices = new List<int>();
    private static HashSet<int> _triangulationReflexVertices = new HashSet<int>();
    private enum VertexType { CONVEX = 0, REFLEX = 1 };
    private struct VertexData
    {
        public int prevVertexIndex;
        public int nextVertexIndex;
        public VertexType vertexType;
        public Vector3 vertex;
    }

    private static Quaternion capOrientation(Vector3[] verts, List<int> edge)
    {
        Vector3 cross = Vector3.zero;
        for (int i = 1; i < edge.Count - 1; i++)
        {
            Vector3 prevVertex = verts[edge[i - 1]];
            Vector3 currVertex = verts[edge[i]];
            Vector3 nextVertex = verts[edge[i + 1]];
            cross += Vector3.Cross(nextVertex - currVertex, currVertex - prevVertex);
        }
        cross /= (edge.Count - 2);
        return Quaternion.LookRotation(cross);
    }

    public static void CapMesh(Mesh parent, List<int> edges, int outputSubMesh, bool useTriangulation = true)
    {
        if (parent == null)
            throw new System.ArgumentNullException("parent", "Input mesh cannot be null");
        if (edges == null)
            throw new System.ArgumentException("edges", "Input edges cannot be null");

        if (edges[0] == edges[edges.Count - 1])
        {
            edges.RemoveAt(edges.Count - 1);
        }

        if (edges.Count < 3)
        {
            return;
        }

        Vector3[] p_vertices = parent.vertices;
        int oldSize = p_vertices.Length;
        Vector2[] uvs = parent.uv;
        Vector3[] normals = parent.normals;
        Vector4[] tangents = parent.tangents;
        BoneWeight[] weights = parent.boneWeights;
        Color32[] colors = parent.colors32;
        bool calcBW = weights.Length != 0;
        bool calcCol = colors.Length != 0;

        List<int> vert_used = extractIndices(edges, false);

        Dictionary<int, int> reindex = mapIndices(vert_used, oldSize);

        Quaternion plane = capOrientation(p_vertices, edges);
        Quaternion plane_inverse = Quaternion.Inverse(plane);
        Vector3 v0 = plane_inverse * (p_vertices[vert_used[0]]);
        // calculate uv map limits
        float[] UVLimits_x = { v0.x, v0.x };
        float[] UVLimits_y = { v0.y, v0.y };
        float[] limits_z = { v0.z, v0.z };

        for (int a = 1; a < vert_used.Count; a++)
        {
            Vector3 v = plane_inverse * (p_vertices[vert_used[a]]);
            if (v.x < UVLimits_x[0]) UVLimits_x[0] = v.x;
            if (v.x > UVLimits_x[1]) UVLimits_x[1] = v.x;
            if (v.y < UVLimits_y[0]) UVLimits_y[0] = v.y;
            if (v.y > UVLimits_y[1]) UVLimits_y[1] = v.y;
            if (v.z < limits_z[0]) limits_z[0] = v.z;
            if (v.z > limits_z[1]) limits_z[1] = v.z;
        }
        bool smooth = limits_z[1] - limits_z[0] < edgeEnergyThreshold;

        // triangulation requires no additional vertices, if triangulation is not used and the cut is smooth only one center vertex is required
        int num_additional_vertices = useTriangulation ? 0 : (smooth ? 1 : (edges.Count));
        Array.Resize(ref p_vertices, oldSize + vert_used.Count + num_additional_vertices);
        Array.Resize(ref uvs, p_vertices.Length);
        Array.Resize(ref normals, p_vertices.Length);
        Array.Resize(ref tangents, p_vertices.Length);
        if (calcBW)
            Array.Resize(ref weights, p_vertices.Length);
        if (calcCol)
            Array.Resize(ref colors, p_vertices.Length);

        for (int i = 0; i < vert_used.Count; i++)
        {
            p_vertices[oldSize + i] = p_vertices[vert_used[i]];

            Vector3 v = plane_inverse * (p_vertices[vert_used[i]]);

            uvs[oldSize + i] = new Vector2(normalizeL(UVLimits_x, v.x), normalizeL(UVLimits_y, v.y));
            if (calcBW)
                weights[oldSize + i] = weights[vert_used[i]];
            if (calcCol)
                colors[oldSize + i] = colors[vert_used[i]];
        }

        List<int> triangles = new List<int>(edges.Count * 3);

        if (useTriangulation)
        {
            _triangulationConvexVertices.Clear();
            _triangulationReflexVertices.Clear();
            Dictionary<int, VertexData> vertexData = new Dictionary<int, VertexData>(vert_used.Count);
            List<int> properVertices = new List<int>(edges.Count);

            int numEarsRemoved = 0;

            for (int i = 0; i < edges.Count; i++)
            {
                int vertexIndex = edges[i];

                int prevVertexIndex = edges[i == 0 ? edges.Count - 1 : i - 1];
                int nextVertexIndex = edges[(i + 1) % edges.Count];
                Vector3 prevVertex = plane_inverse * p_vertices[prevVertexIndex];
                Vector3 currVertex = plane_inverse * p_vertices[vertexIndex];
                Vector3 nextVertex = plane_inverse * p_vertices[nextVertexIndex];

                float sign = Sign(nextVertex, currVertex, prevVertex);

                if (sign > 0)
                {
                    _triangulationConvexVertices.Add(vertexIndex);
                    vertexData[vertexIndex] = new VertexData() { vertexType = VertexType.CONVEX, vertex = currVertex };
                    properVertices.Add(vertexIndex);
                }
                //straight line
                else if (sign == 0)
                {
                }
                else
                {
                    _triangulationReflexVertices.Add(vertexIndex);
                    vertexData[vertexIndex] = new VertexData() { vertexType = VertexType.REFLEX, vertex = currVertex };
                    properVertices.Add(vertexIndex);
                }
            }

            if (properVertices.Count < 3)
            {
                return;
            }

            for (int i = 0; i < properVertices.Count; i++)
            {
                int vertexIndex = properVertices[i];

                int prevVertexIndex = properVertices[i == 0 ? properVertices.Count - 1 : i - 1];
                int nextVertexIndex = properVertices[(i + 1) % properVertices.Count];

                VertexData vData = vertexData[vertexIndex];
                vData.nextVertexIndex = nextVertexIndex;
                vData.prevVertexIndex = prevVertexIndex;
                vertexData[vertexIndex] = vData;
            }

            int cnt = 0;
            while (numEarsRemoved < properVertices.Count - 2)
            {
                if (cnt++ > 500) break;
                for (int i = 0; i < _triangulationConvexVertices.Count; i++)
                {
                    if (_triangulationConvexVertices[i] == -1)
                    {
                        continue;
                    }
                    VertexData cVertexData = vertexData[_triangulationConvexVertices[i]];
                    int v2Index = cVertexData.prevVertexIndex;
                    int v3Index = cVertexData.nextVertexIndex;
                    VertexData v2 = vertexData[v2Index];
                    VertexData v3 = vertexData[v3Index];

                    bool ear = true;
                    foreach (int reflexIndex in _triangulationReflexVertices)
                    {
                        if (reflexIndex == v2Index || reflexIndex == v3Index)
                        {
                            continue;
                        }

                        if (PointInTriangle(vertexData[reflexIndex].vertex, cVertexData.vertex, v2.vertex, v3.vertex))
                        {
                            ear = false;
                            break;
                        }
                    }

                    if (ear)
                    {
                        numEarsRemoved++;
                        triangles.Add(reindex[cVertexData.prevVertexIndex]);
                        triangles.Add(reindex[_triangulationConvexVertices[i]]);
                        triangles.Add(reindex[cVertexData.nextVertexIndex]);

                        if (numEarsRemoved == properVertices.Count - 2)
                        {
                            break;
                        }

                        _triangulationConvexVertices[i] = -1;
                        v2.nextVertexIndex = v3Index;
                        v3.prevVertexIndex = v2Index;
                        if (v2.vertexType == VertexType.REFLEX)
                        {
                            Vector3 v2PrevVertex = vertexData[v2.prevVertexIndex].vertex;
                            Vector3 v2NextVertex = vertexData[v2.nextVertexIndex].vertex;
                            if (Sign(v2NextVertex, v2.vertex, v2PrevVertex) >= 0)
                            {
                                _triangulationReflexVertices.Remove(v2Index);
                                _triangulationConvexVertices.Add(v2Index);

                                v2.vertexType = VertexType.CONVEX;
                            }
                        }
                        if (v3.vertexType == VertexType.REFLEX)
                        {
                            Vector3 v3PrevVertex = vertexData[v3.prevVertexIndex].vertex;
                            Vector3 v3NextVertex = vertexData[v3.nextVertexIndex].vertex;
                            if (Sign(v3NextVertex, v3.vertex, v3PrevVertex) >= 0)
                            {
                                _triangulationReflexVertices.Remove(v3Index);
                                _triangulationConvexVertices.Add(v3Index);
                                v3.vertexType = VertexType.CONVEX;
                            }
                        }
                        vertexData[v2Index] = v2;
                        vertexData[v3Index] = v3;
                    }
                }
            }
        }
        else
        {
            edges.Add(edges[0]);

            Vector3 center = new Vector3();
            foreach (int i in vert_used)
                center += p_vertices[i];
            center /= vert_used.Count;

            for (int i = oldSize + vert_used.Count; i < p_vertices.Length; i++)
            {
                p_vertices[i] = center;

                Vector3 vi = plane_inverse * center;

                uvs[i] = new Vector2(normalizeL(UVLimits_x, vi.x), normalizeL(UVLimits_y, vi.y));
                if (calcBW)
                    weights[i] = weights[vert_used[0]];
                if (calcCol)
                    colors[i] = colors[vert_used[0]];
            }

            int centers_start_index = oldSize + vert_used.Count;
            for (int a = 0; a < edges.Count - 1; a++)
            {
                triangles.Add(centers_start_index + (smooth ? 0 : a));
                triangles.Add(reindex[edges[a]]);
                triangles.Add(reindex[edges[a + 1]]);
            }
        }

        parent.vertices = p_vertices;
        parent.uv = uvs;
        if (calcBW)
            parent.boneWeights = weights;
        if (calcCol)
            parent.colors32 = colors;

        int[] old_tris = parent.GetTriangles(outputSubMesh);
        int[] all_tris = new int[old_tris.Length + triangles.Count];
        Array.Copy(old_tris, 0, all_tris, 0, old_tris.Length);
        //Array.Copy(triangles, 0, all_tris, old_tris.Length, triangles.Count);
        triangles.CopyTo(all_tris, old_tris.Length);
        parent.SetTriangles(all_tris, outputSubMesh);

        parent.RecalculateNormals();
        parent.RecalculateTangents();
        Vector3[] unityNormals = parent.normals;
        Vector4[] unityTangents = parent.tangents;
        for (int i = oldSize; i < normals.Length; i++)
        {
            normals[i] = unityNormals[i];
            tangents[i] = unityTangents[i];
        }

        parent.normals = normals;
        parent.tangents = tangents;
    }

    public static float Sign(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        Vector3 v13 = (p1 - p3).normalized;
        Vector3 v23 = (p2 - p3).normalized;
        //float sign = (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        float sign = v13.x * v23.y - v23.x * v13.y;
        return Mathf.Abs(sign) < lineSignThreshold ? 0 : sign;
    }

    public static bool PointInTriangle(Vector3 pt, Vector3 v1, Vector3 v2, Vector3 v3)
    {
        float d1, d2, d3;
        bool has_neg, has_pos;

        d1 = Sign(pt, v1, v2);
        d2 = Sign(pt, v2, v3);
        d3 = Sign(pt, v3, v1);

        has_neg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        has_pos = (d1 > 0) || (d2 > 0) || (d3 > 0);

        return !(has_neg && has_pos);
    }

    public static float normalizeL(float[] limits, float value)
    {
        return (value - limits[0]) / (limits[1] - limits[0]);
    }

    public static List<int> extractIndices(List<int> index_group, bool sort)
    {
        if (index_group == null)
            throw new System.ArgumentNullException("index_group", "Input index group cannot be null");

        HashSet<int> extract = new HashSet<int>(Enumerable.Range(0, index_group.Count));
        extract.Clear();

        foreach (int i in index_group)
            extract.Add(i);

        List<int> list = extract.ToList();
        if (sort)
            list.Sort();

        return list;
    }

    public static Dictionary<int, int> mapIndices(List<int> indices, int offset)
    {
        if (indices == null)
            throw new System.ArgumentNullException("indices", "Index list cannot be null");

        Dictionary<int, int> d = new Dictionary<int, int>(indices.Count);
        for (int i = 0; i < indices.Count; i++)
            d[indices[i]] = i + offset;
        return d;
    }
}
