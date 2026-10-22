
using UnityEngine;
using ROVVR;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CurvedVideoScreen : MonoBehaviour
{
    public float width = 4f;
    public float height = 2.25f;

    [Range(10f, 150f)]
    public float curveDegrees = 80f;

    [Range(8, 128)]
    public int segments = 48;

    [Header("Calibrated ROV projection (overrides legacy width / height)")]
    public bool calibratedProjection;
    public bool cylindricalProjection = true;
    [Range(-1f, 150f)] public float independentCurveDegrees = -1f;
    [Range(1f, 4f)] public float viewingDistance = 2f;
    [Range(50f, 280f)] public float horizontalViewDegrees = 80f;
    public int sourceWidth = 1920;
    public int sourceHeight = 1080;
    [Range(8, 96)] public int verticalSegments = 54;
    public bool flipHorizontal;
    public bool flipVertical = true;

    [SerializeField, HideInInspector] private Mesh generatedMesh;

    private void Awake()
    {
        RebuildMesh();
    }

    public void RebuildMesh()
    {
        Mesh mesh = calibratedProjection
            ? RovVideoProjection.Create(sourceWidth, sourceHeight, horizontalViewDegrees,
                viewingDistance, segments, verticalSegments, flipHorizontal, flipVertical, cylindricalProjection, independentCurveDegrees)
            : GenerateLegacyMesh();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        ReleaseGeneratedMesh();
        generatedMesh = mesh;
    }

    private Mesh GenerateLegacyMesh()
    {
        int count = segments + 1;

        Vector3[] vertices = new Vector3[count * 2];
        Vector3[] normals = new Vector3[count * 2];
        Vector2[] uvs = new Vector2[count * 2];
        int[] triangles = new int[segments * 6];

        float arc = curveDegrees * Mathf.Deg2Rad;
        float radius = width / arc;

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float angle = (t - 0.5f) * arc;

            float x = Mathf.Sin(angle) * radius;
            float z = radius * (Mathf.Cos(angle) - 1f);

            int index = i * 2;

            vertices[index] =
                new Vector3(x, -height / 2f, z);

            vertices[index + 1] =
                new Vector3(x, height / 2f, z);

            // Same front-facing orientation as Unity's Quad
            Vector3 normal = new Vector3(
                -Mathf.Sin(angle), 0f, -Mathf.Cos(angle)
            );

            normals[index] = normal;
            normals[index + 1] = normal;

            uvs[index] = new Vector2(1f-t, 0f);
            uvs[index + 1] = new Vector2(1f-t, 1f);

            if (i < segments)
            {
                int a = index;
                int b = index + 1;
                int c = index + 2;
                int d = index + 3;

                int ti = i * 6;

                triangles[ti] = a;
                triangles[ti + 1] = b;
                triangles[ti + 2] = c;

                triangles[ti + 3] = b;
                triangles[ti + 4] = d;
                triangles[ti + 5] = c;
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "Curved Video Screen";

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        mesh.RecalculateBounds();

        mesh.hideFlags = HideFlags.DontSave;
        return mesh;
    }

    private void ReleaseGeneratedMesh()
    {
        if (generatedMesh == null) return;
        if (Application.isPlaying) Destroy(generatedMesh);
        else DestroyImmediate(generatedMesh);
        generatedMesh = null;
    }

    private void OnDestroy() { ReleaseGeneratedMesh(); }
}
