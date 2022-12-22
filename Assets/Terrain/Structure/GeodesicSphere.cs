using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

public class GeodesicSphere : MonoBehaviour
{
    public enum BaseShape
    {
        Tetrahedron, 
        Hexahedron, 
        Octahedron, 
        Icosahedron, 
    }

    public BaseShape baseShape = BaseShape.Icosahedron;

    [Range(1, 1000)]
    public int subdivision = 3;

    [SerializeField]
    [Range(0.01f, 100000.0f)]
    protected float radius = 1.0f;
    public float Radius { get { return radius; } }

    protected const float _8By9Sqrt = 0.94280904158f;
    protected const float _2By9Sqrt = 0.47140452079f;
    protected const float _2By3Sqrt = 0.81649658092f;
    protected const float _3Rec = 0.33333333333f;
    protected Vector3[] tetrahedronVertices = 
    {
        new Vector3( _8By9Sqrt,       0.0f, -_3Rec), 
        new Vector3(-_2By9Sqrt,  _2By3Sqrt, -_3Rec), 
        new Vector3(-_2By9Sqrt, -_2By3Sqrt, -_3Rec), 
        new Vector3(      0.0f,       0.0f,   1.0f) 
    };
    protected int[] tetrahedronTriangles =
    {
        0, 2, 1, 
        1, 2, 3, 
        0, 1, 3, 
        0, 3, 2
    };

    protected const float _3SqrRec = 0.57735026919f;
    protected Vector3[] hexahedronVertices = 
    {
        new Vector3( -_3SqrRec, -_3SqrRec,  _3SqrRec), 
        new Vector3(  _3SqrRec, -_3SqrRec,  _3SqrRec), 
        new Vector3( -_3SqrRec,  _3SqrRec,  _3SqrRec), 
        new Vector3(  _3SqrRec,  _3SqrRec,  _3SqrRec), 
        new Vector3( -_3SqrRec, -_3SqrRec, -_3SqrRec), 
        new Vector3(  _3SqrRec, -_3SqrRec, -_3SqrRec), 
        new Vector3( -_3SqrRec,  _3SqrRec, -_3SqrRec), 
        new Vector3(  _3SqrRec,  _3SqrRec, -_3SqrRec), 
    };
    protected int[] hexahedronTriangles =
    {
        0, 1, 2, 
        2, 1, 3, 
        1, 5, 3, 
        3, 5, 7, 
        5, 4, 6, 
        5, 6, 7, 
        4, 0, 6, 
        6, 0, 2, 
        1, 0, 5, 
        4, 5, 0, 
        2, 3, 6, 
        6, 3, 7
    };

    protected Vector3[] octahedronVertices = 
    {
        new Vector3(  1.0f,  0.0f,  0.0f), 
        new Vector3( -1.0f,  0.0f,  0.0f), 
        new Vector3(  0.0f,  1.0f,  0.0f), 
        new Vector3(  0.0f, -1.0f,  0.0f), 
        new Vector3(  0.0f,  0.0f,  1.0f), 
        new Vector3(  0.0f,  0.0f, -1.0f)
    };
    protected int[] octahedronTriangles =
    {
        2, 0, 5, 
        2, 5, 1, 
        2, 1, 4, 
        2, 4, 0, 
        3, 5, 0, 
        3, 1, 5, 
        3, 4, 1, 
        3, 0, 4 
    };

    protected const float _LongDistance = 0.85065216457f;
    protected const float _ShortDistance = 0.52573111211f;
    protected Vector3[] icosahedronVertices = 
    {
        new Vector3(           0.0f,  _ShortDistance,  -_LongDistance), 
        new Vector3( _ShortDistance,   _LongDistance,            0.0f), 
        new Vector3(-_ShortDistance,   _LongDistance,            0.0f), 
        new Vector3(           0.0f,  _ShortDistance,   _LongDistance), 
        new Vector3(  _LongDistance,            0.0f, -_ShortDistance), 
        new Vector3(  _LongDistance,            0.0f,  _ShortDistance), 
        new Vector3( -_LongDistance,            0.0f,  _ShortDistance), 
        new Vector3( -_LongDistance,            0.0f, -_ShortDistance), 
        new Vector3(           0.0f, -_ShortDistance,  -_LongDistance), 
        new Vector3( _ShortDistance,  -_LongDistance,            0.0f), 
        new Vector3(-_ShortDistance,  -_LongDistance,            0.0f), 
        new Vector3(           0.0f, -_ShortDistance,   _LongDistance) 
    };
    protected int[] icosahedronTriangles =
    {
        2, 1, 0, 
        3, 1, 2, 
        0, 1, 4, 
        4, 1, 5, 
        5, 1, 3, 
        5, 3, 11,
        11, 3, 6, 
        6, 3, 2, 
        6, 2, 7, 
        7, 2, 0, 
        6, 7, 10, 
        6, 10, 11, 
        10, 7, 8, 
        8, 7, 0, 
        8, 0, 4, 
        8, 4, 9, 
        9, 4, 5, 
        9, 5, 11, 
        11, 10, 9, 
        8, 9, 10
    };

    protected Vector3[][] baseShapeVertices;
    protected int[][] baseShapeTriangles;

    public MeshRenderer meshRenderer;
    public MeshFilter meshFilter;
    public Mesh originalMesh;

    public void UpdateMesh()
    {
        originalMesh = new Mesh();
        originalMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        baseShapeVertices = new Vector3[][]
        {
            tetrahedronVertices, 
            hexahedronVertices, 
            octahedronVertices, 
            icosahedronVertices
        };

        baseShapeTriangles = new int[][]
        {
            tetrahedronTriangles, 
            hexahedronTriangles, 
            octahedronTriangles, 
            icosahedronTriangles
        };

        // FIXME: Edge subdivision vertices are doubled, they are created on both triangles. Optimization possible.
        Vector3[] baseVertices = baseShapeVertices[(int)baseShape].Select(vertex => vertex * radius).ToArray();
        int vertexCount = baseVertices.Count();
        int[] baseShapeTriangle = baseShapeTriangles[(int)baseShape];
        int triangleCount = baseShapeTriangle.Count() / 3;
        int subdividedVertexCount = ((subdivision + 1) * (subdivision + 2) / 2);
        int subdividedTriangleCount = (subdivision * subdivision);
        int totalSubdividedVertexCount = triangleCount * subdividedVertexCount;
        int totalSubdividedTriangleCount = triangleCount * subdividedTriangleCount;
        Vector3[] vertices = new Vector3[totalSubdividedVertexCount];
        int[] triangles = new int[totalSubdividedTriangleCount * 3];
        // Debug.Log("UpdateMesh Start");
        // Debug.Log("vertexCount: " + vertexCount + " triangleCount: " + triangleCount + " subdividedVertexCount: " + subdividedVertexCount + " subdividedTriangleCount: " + subdividedTriangleCount + " totalSubdividedVertexCount: " + totalSubdividedVertexCount + " totalSubdividedTriangleCount: " + totalSubdividedTriangleCount);
        for (int triangleCounter = 0; triangleCounter < triangleCount; triangleCounter++)
        {
            int currentTriangleVertexOffset = triangleCounter * subdividedVertexCount;
            int currentTriangleTriangleOffset = triangleCounter * subdividedTriangleCount;
            int currentTriangleIndexOffset = currentTriangleTriangleOffset * 3;
            int vertex0Index = baseShapeTriangle[triangleCounter * 3];
            int vertex1Index = baseShapeTriangle[triangleCounter * 3 + 1];
            int vertex2Index = baseShapeTriangle[triangleCounter * 3 + 2];
            Vector3 vertex0 = baseVertices[vertex0Index];
            Vector3 vertex1 = baseVertices[vertex1Index];
            Vector3 vertex2 = baseVertices[vertex2Index];
            // Debug.Log("For triangle: " + triangleCounter);
            // Debug.Log("currentTriangleVertexOffset: " + currentTriangleVertexOffset + " currentTriangleTriangleOffset: " + currentTriangleTriangleOffset);
            // Debug.Log("vertex0Index: " + vertex0Index + " vertex1Index: " + vertex1Index + " vertex2Index: " + vertex2Index);
            // Debug.Log("vertices[" + currentTriangleVertexOffset + "]: " + vertex0);
            vertices[currentTriangleVertexOffset] = vertex0;
            for (int subdivisionU = 0; subdivisionU < subdivision; subdivisionU++)
            {
                int subdivisionEdgeVertexOffset = ((subdivisionU) * (subdivisionU + 1) / 2);
                int subdivisionNextEdgeVertexOffset = ((subdivisionU + 1) * (subdivisionU + 2) / 2);
                int subdivisionEdgeTriangleOffset = (subdivisionU * subdivisionU);
                int subdivisionEdgeIndexOffset = (subdivisionEdgeTriangleOffset * 3);
                float subdivisionURatio = ((subdivisionU + 1.0f) / subdivision);
                Vector3 subdivisionEdgeVertex0 = Vector3.Lerp(vertex0, vertex1, subdivisionURatio);
                Vector3 subdivisionEdgeVertex1 = Vector3.Lerp(vertex0, vertex2, subdivisionURatio);
                // Debug.Log("For subdivision: " + subdivisionU);
                // Debug.Log("subdivisionEdgeVertexOffset: " + subdivisionEdgeVertexOffset + " subdivisionNextEdgeVertexOffset: " + subdivisionNextEdgeVertexOffset + " subdivisionEdgeTriangleOffset: " + subdivisionEdgeTriangleOffset);

                // Calculate the vertices along the subdivisionEdge
                for (int subdivisionV = 0; subdivisionV <= subdivisionU + 1; subdivisionV++)
                {
                    int subdivisionVertexOffset = currentTriangleVertexOffset + subdivisionNextEdgeVertexOffset + subdivisionV;
                    float subdivisionVRatio = (subdivisionV / (subdivisionU + 1.0f));
                    Vector3 subdivisionVertex = Vector3.Lerp(subdivisionEdgeVertex0, subdivisionEdgeVertex1, subdivisionVRatio);
                    // Debug.Log("vertices[" + subdivisionVertexOffset + "]: " + subdivisionVertex);
                    vertices[subdivisionVertexOffset] = subdivisionVertex;
                }

                // Calculate the triangles along the subdivisionEdge
                // if (subdivisionU < subdivision)
                {
                    for (int subdivisionEdgeEvenTriangles = 0; subdivisionEdgeEvenTriangles <= subdivisionU; subdivisionEdgeEvenTriangles++)
                    {
                        int subdivisionEvenTriangleIndexOffset = currentTriangleIndexOffset + subdivisionEdgeIndexOffset + subdivisionEdgeEvenTriangles * 2 * 3;
                        int subdivisionEvenTriangleVertex0Index = currentTriangleVertexOffset + subdivisionEdgeVertexOffset + subdivisionEdgeEvenTriangles;
                        int subdivisionEvenTriangleVertex1Index = currentTriangleVertexOffset + subdivisionNextEdgeVertexOffset + subdivisionEdgeEvenTriangles;
                        int subdivisionEvenTriangleVertex2Index = currentTriangleVertexOffset + subdivisionNextEdgeVertexOffset + subdivisionEdgeEvenTriangles + 1;
                        // Debug.Log("subdivisionEvenTriangleIndexOffset: " + subdivisionEvenTriangleIndexOffset + " subdivisionEvenTriangleVertex0Index: " + subdivisionEvenTriangleVertex0Index + " subdivisionEvenTriangleVertex1Index: " + subdivisionEvenTriangleVertex1Index+ " subdivisionEvenTriangleVertex2Index: " + subdivisionEvenTriangleVertex2Index);
                        triangles[subdivisionEvenTriangleIndexOffset + 0] = subdivisionEvenTriangleVertex0Index;
                        triangles[subdivisionEvenTriangleIndexOffset + 1] = subdivisionEvenTriangleVertex1Index;
                        triangles[subdivisionEvenTriangleIndexOffset + 2] = subdivisionEvenTriangleVertex2Index;
                    }
                    for (int subdivisionEdgeOddTriangles = 0; subdivisionEdgeOddTriangles < subdivisionU; subdivisionEdgeOddTriangles++)
                    {
                        int subdivisionOddTriangleIndexOffset = currentTriangleIndexOffset + subdivisionEdgeIndexOffset + subdivisionEdgeOddTriangles * 2 * 3 + 3;
                        int subdivisionOddTriangleVertex0Index = currentTriangleVertexOffset + subdivisionEdgeVertexOffset + subdivisionEdgeOddTriangles + 1;
                        int subdivisionOddTriangleVertex1Index = currentTriangleVertexOffset + subdivisionEdgeVertexOffset + subdivisionEdgeOddTriangles;
                        int subdivisionOddTriangleVertex2Index = currentTriangleVertexOffset + subdivisionNextEdgeVertexOffset + subdivisionEdgeOddTriangles + 1;
                        // Debug.Log("subdivisionOddTriangleIndexOffset: " + subdivisionOddTriangleIndexOffset + " subdivisionOddTriangleVertex0Index: " + subdivisionOddTriangleVertex0Index + " subdivisionOddTriangleVertex1Index: " + subdivisionOddTriangleVertex1Index+ " subdivisionOddTriangleVertex2Index: " + subdivisionOddTriangleVertex2Index);
                        triangles[subdivisionOddTriangleIndexOffset + 0] = subdivisionOddTriangleVertex0Index;
                        triangles[subdivisionOddTriangleIndexOffset + 1] = subdivisionOddTriangleVertex1Index;
                        triangles[subdivisionOddTriangleIndexOffset + 2] = subdivisionOddTriangleVertex2Index;
                    }
                }
            }
        }

        originalMesh.vertices = vertices.Select(vertex => vertex.normalized * radius).ToArray();

        // mesh.vertices = vertices;
        originalMesh.triangles = triangles;
        originalMesh.normals = originalMesh.vertices;

        meshFilter.mesh = originalMesh;

        // Debug.Log("VertexCount: " + mesh.vertices.Count() + " TriangleCount: " + (mesh.triangles.Count() / 3));    
        // Debug.Log("UpdateMesh End");
    }

    // Start is called before the first frame update
    void Start()
    {
        UpdateMesh();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnValidate()
    {
        UpdateMesh();
    }

    [CustomEditor(typeof(GeodesicSphere))]
    public class GeodesicSphereEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GeodesicSphere geodesicSphere = (GeodesicSphere)target;
            if (GUILayout.Button("Update Mesh"))
            {
                geodesicSphere.UpdateMesh();
            }
        }
    }
}
