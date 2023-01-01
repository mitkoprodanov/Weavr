using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEditor;
using Fabric;
using Fabric.SerializationHelper;

public class TerrainGenerator : MonoBehaviour
{
    public GeodesicSphere structure;

    [Range(1, 100)]
    public int noiseSeed = 1;
    [Range(1, 20)]
    public int noiseFractalOctaves = 3;
    [Range(0.0f, 10.0f)]
    public float noiseFractalLacunarity = 2.0f;
    [Range(0.0f, 1.0f)]
    public float noiseFractalGain = 0.5f;
    [Range(0.0f, 1.0f)]
    public float noiseFrequency = 0.01f;
    [Range(0.0f, 1.0f)]
    public float noiseStrength = 0.1f;

    public Gradient colorGradient;
    public float colorNoiseStrength = 0.1f;

    protected MeshFilter meshFilter;

    protected Fabric.TerrainData terrainData;
    // protected TerrainVertex[] terrainVertices;
    // protected TerrainTriangle[] terrainTriangles;
    protected float[] terrainTriangleAreasAggregated;

    public BoundsOctree<TerrainTriangle> terrainTriangleOctree;
    public PointOctree<TerrainVertex> terrainVertexOctree;

    [Range(1, 1000)]
    public int treeCount = 200;
    public GameObject treePrefab;
    public GameObject vegetationObject;

    public string worldDirectoryName = "world";

    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
    }

    protected void GenerateNoise()
    {
        if (!structure)
            return;

        meshFilter = structure.meshFilter;
        if (!meshFilter)
            return;

        Mesh originalMesh = structure.originalMesh;
        if (!originalMesh)
            return;

        Vector3[] vertices = originalMesh.vertices;
        
        FastNoiseLite noise = new FastNoiseLite(noiseSeed);
        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2S);
        noise.SetFractalType(FastNoiseLite.FractalType.Ridged);
        noise.SetFractalOctaves(noiseFractalOctaves);
        noise.SetFractalLacunarity(noiseFractalLacunarity);
        noise.SetFractalGain(noiseFractalGain);
        noise.SetFrequency(noiseFrequency);

        // vertices.Select(vector => vector * (noise.GetNoise(vector.x, vector.y, vector.z))).ToArray();
        for (int vertexIndex = 0; vertexIndex < vertices.Count(); ++vertexIndex)
        {
            Vector3 originalVertex = vertices[vertexIndex];
            vertices[vertexIndex] = originalVertex.normalized * (1.0f + (noise.GetNoise(originalVertex.x, originalVertex.y, originalVertex.z) - 0.5f) * noiseStrength);
            // Debug.Log(originalVertex + " => " + vertices[vertexIndex]);
        }

        Mesh newMesh = new Mesh();
        newMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        newMesh.vertices = vertices;
        // FIXME: normals should be recalculated, at least at the very end
        newMesh.normals = originalMesh.normals;
        newMesh.triangles = originalMesh.triangles;

        meshFilter.mesh = newMesh;
    }

    public void Colorize()
    {
        if (!structure)
            return;

        meshFilter = structure.meshFilter;
        if (!meshFilter)
            return;

        FastNoiseLite colorNoise = new FastNoiseLite(noiseSeed + 1);

        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        Color[] colors = new Color[vertices.Count()];
 
        for (int vertexIndex = 0; vertexIndex < vertices.Count(); ++vertexIndex)
        {
            Vector3 vertex = vertices[vertexIndex];
            float colorNoiseValue = colorNoise.GetNoise(vertex.x, vertex.y, vertex.z);
            float colorHeight = vertex.magnitude + (colorNoiseValue - 0.5f) * colorNoiseStrength;
            colors[vertexIndex] = colorGradient.Evaluate(Mathf.Clamp01(colorHeight / (1.0f + noiseStrength)));
        }

        // Mesh uncolorizedMesh = meshFilter.sharedMesh;
        Mesh newMesh = new Mesh();
        newMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        newMesh.vertices = mesh.vertices;
        // // FIXME: normals should be recalculated, at least at the very end
        newMesh.normals = mesh.normals;
        newMesh.triangles = mesh.triangles;
        newMesh.colors = colors;

        meshFilter.mesh = newMesh;
    }

    public class VegetationInstance
    {
        public VegetationInstance(float radius)
        {
            this.radius = radius;
        }

        protected float radius;
        public float Radius { get { return radius; } }
    }

    // protected void GenerateVegetation()
    // {
    //     BoundsOctree<VegetationInstance> octree = new BoundsOctree<VegetationInstance>(2.0f, new Vector3(), 0.001f, 1.0f);

    //     List<Vector3> activePoints = new List<Vector3>();
    //     List<Vector3> inactivePoints = new List<Vector3>();

    //     Vector3 initialPoint = Random.onUnitSphere;
    //     activePoints.Add(initialPoint);

    //     float minRadius = 0.1f;
    //     float minRadiusSqr = minRadius * minRadius;
    //     float maxRadiusSqr = (minRadiusSqr * 4.0f); 
    //     int sampleCount = 10;
    //     // FIXME: circle of a sphere might be approximated by a simple circle
    //     do
    //     {
    //         int selectedActivePointIndex = Random.Range(0, activePoints.Count);
    //         Vector3 selectedActivePoint = activePoints[selectedActivePointIndex];
    //         for (int sampleIndex = 0; sampleIndex < sampleCount; ++sampleIndex)
    //         {
    //             float sampleDirection = Random.Range(0.0f, Mathf.PI * 2.0f);
    //             float sampleDistance = Mathf.Sqrt(Random.Range(minRadiusSqr, maxRadiusSqr));
    //             float sampleAngle = Mathf.Rad2Deg * (sampleDistance / structure.Radius);
    //             Vector3 sample = Quaternion.FromToRotation(Vector3.up, initialPoint) * Quaternion.AngleAxis(sampleDirection, Vector3.up) * Quaternion.AngleAxis(sampleAngle, Vector3.up) * Vector3.forward;
    //         }
    //         activePoints.RemoveAt(selectedActivePointIndex);
    //     } while (activePoints.Count > 0);
    // }

    public void GenerateVegetation()
    {
        if (!treePrefab || !vegetationObject)
            return;

        // Debug.Log("vegetationObject.transform.childCount " + vegetationObject.transform.childCount);
        for (int childIndex = vegetationObject.transform.childCount - 1; childIndex >= 0; --childIndex)
        {
            DestroyImmediate(vegetationObject.transform.GetChild(childIndex).gameObject);
        }
        // vegetationObject.transform.DetachChildren();

        for (int randomTreeIndex = 0; randomTreeIndex < treeCount; ++randomTreeIndex)
        {
            Vector3 randomTreePosition = FindRandomPointOnSurface();
            GameObject tree = PrefabUtility.InstantiatePrefab(treePrefab) as GameObject;
            tree.transform.SetParent(vegetationObject.transform);
            tree.transform.position = randomTreePosition;
            tree.transform.rotation = Quaternion.FromToRotation(Vector3.up, randomTreePosition.normalized);
        }
    }

    public Vector3 FindRandomPointOnSurface()
    {
        float totalArea = terrainTriangleAreasAggregated.Last();
        float randomAreaPoint = Random.Range(0.0f, totalArea);

        int fromIndex = 0;
        int toIndex = terrainTriangleAreasAggregated.Count() - 1;
        int count = toIndex - fromIndex;
        int step;
        int checkIndex;
        float checkValue;
        while (count > 0)
        {
            step = count / 2;
            checkIndex = fromIndex + step;
            checkValue = terrainTriangleAreasAggregated[checkIndex];
            if (checkValue < randomAreaPoint)
            {
                fromIndex = checkIndex++;
                count = step - 1;
            }
            else
            {
                count = step;
            }
        }

        int randomTriangleIndex = fromIndex;

        // Debug.Log("totalArea " + totalArea + " randomAreaPoint " + randomAreaPoint + " toIndex " + toIndex + " randomTriangleIndex " + randomTriangleIndex);

        return terrainData.GetTriangle(randomTriangleIndex).RandomPoint; 
        // return terrainTriangles[randomTriangleIndex].RandomPoint;
    }

    // public Vector3 FindRandomPointOnSurface()
    // {
    //     Vector3 randomDirectionOnSphere = Random.onUnitSphere;
    //     Ray randomDirectionOnSphereRay = new Ray(transform.position, randomDirectionOnSphere);

    //     List<TerrainTriangle> collidingTriangles = new List<TerrainTriangle>();
    //     terrainTriangleOctree.GetColliding(collidingTriangles, randomDirectionOnSphereRay);

    //     if (collidingTriangles.Count < 1)
    //     {
    //         Debug.LogError("The randomized unit direction did not collide with any surface triangles. Surface has holes, or what?");
    //         return Vector3.zero;
    //     }

    //     foreach (TerrainTriangle collidingTriangle in collidingTriangles)
    //     {
    //         Vector3 point =

    //         // FIXME: point on triangle check could be done more efficiently with barycentric coordinates  
    //         Vector3 side1 = collidingTriangle.Vertex0.Position - collidingTriangle.Vertex1.Position;
    //         Vector3 side2 = collidingTriangle.Vertex1.Position - collidingTriangle.Vertex2.Position;
    //         Vector3 side0 = collidingTriangle.Vertex2.Position - collidingTriangle.Vertex0.Position;
    //         Vector3 toPoint0 = 
    //     }

    //     return new Vector3();
    // }

    public void BuildTerrainData()
    {
        if (meshFilter && meshFilter.sharedMesh)
        {
            Mesh mesh = meshFilter.sharedMesh;

            terrainData = Fabric.TerrainData.FromMesh(mesh);

            terrainVertexOctree = new PointOctree<TerrainVertex>(2.0f, Vector3.zero, 0.00001f);

            Vector3[] meshVertices = mesh.vertices;
            Vector3[] meshNormals = mesh.normals;
            Color[] meshColors = mesh.colors;

            foreach (TerrainVertex terrainVertex in terrainData.Vertices)
            {
                terrainVertexOctree.Add(terrainVertex, terrainVertex.Position);
            }

            // int triangleMax = 10000;
            int[] meshTriangles = mesh.triangles;
            int triangleCount = meshTriangles.Length / 3;

            terrainTriangleAreasAggregated = new float[triangleCount];
            terrainTriangleOctree = new BoundsOctree<TerrainTriangle>(2.0f, Vector3.zero, 0.00001f, 1.2f);

            float terrainTriangleAreaAggregated = 0.0f;
            for (int triangleIndex = 0; triangleIndex < triangleCount; ++triangleIndex)
            {
                int vertexIndex0 = meshTriangles[triangleIndex * 3 + 0];
                int vertexIndex1 = meshTriangles[triangleIndex * 3 + 1];
                int vertexIndex2 = meshTriangles[triangleIndex * 3 + 2];

                ref readonly TerrainVertex terrainVertex0 = ref terrainData.GetVertex(vertexIndex0);
                ref readonly TerrainVertex terrainVertex1 = ref terrainData.GetVertex(vertexIndex1);
                ref readonly TerrainVertex terrainVertex2 = ref terrainData.GetVertex(vertexIndex2);

                // terrainTriangles[triangleIndex] = terrainData.GetTriangle(triangleIndex);
                // terrainTriangles[triangleIndex] = new TerrainTriangle(ref terrainVertex0, ref terrainVertex1, ref terrainVertex2, vertexIndex0, vertexIndex1, vertexIndex2);
            // //     // terrainVertex0.AddTriangle(terrainTriangle);
            // //     // terrainVertex1.AddTriangle(terrainTriangle);
            // //     // terrainVertex2.AddTriangle(terrainTriangle);

                ref readonly TerrainTriangle terrainTriangle = ref terrainData.GetTriangle(triangleIndex);

                terrainTriangleAreaAggregated += terrainTriangle.Area;
                terrainTriangleAreasAggregated[triangleIndex] = terrainTriangleAreaAggregated;

                float xMin = Mathf.Min(terrainVertex0.Position.x, Mathf.Min(terrainVertex1.Position.x, terrainVertex2.Position.x));
                float yMin = Mathf.Min(terrainVertex0.Position.y, Mathf.Min(terrainVertex1.Position.y, terrainVertex2.Position.y));
                float zMin = Mathf.Min(terrainVertex0.Position.z, Mathf.Min(terrainVertex1.Position.z, terrainVertex2.Position.z));
                float xMax = Mathf.Max(terrainVertex0.Position.x, Mathf.Max(terrainVertex1.Position.x, terrainVertex2.Position.x));
                float yMax = Mathf.Max(terrainVertex0.Position.y, Mathf.Max(terrainVertex1.Position.y, terrainVertex2.Position.y));
                float zMax = Mathf.Max(terrainVertex0.Position.z, Mathf.Max(terrainVertex1.Position.z, terrainVertex2.Position.z));
                Vector3 minVector = new Vector3(xMin, yMin, zMin);
                Vector3 maxVector = new Vector3(xMax, yMax, zMax);
                Bounds triangleBounds = new Bounds((minVector + maxVector) * 0.5f, (maxVector - minVector));
                terrainTriangleOctree.Add(terrainTriangle, triangleBounds);
            }
        }
    }

    public void Generate()
    {
        Debug.Log("GenerateNoise");
        GenerateNoise();

        Debug.Log("Colorize");
        Colorize();

        Debug.Log("BuildTerrainData");
        BuildTerrainData();

        Debug.Log("GenerateVegetation");
        GenerateVegetation();
    }

    // void OnValidate()
    // {
    //     Generate();
    // }

    private void UpdateMesh()
    {
        Debug.Log("UpdateMesh 0");

        if (!meshFilter)
            return;
        
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        int vertexCount = terrainData.Vertices.Count; 
        Vector3[] vertices = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        Color[] colors = new Color[vertexCount];
        for (int vertexIndex = 0; vertexIndex < vertexCount; ++vertexIndex)
        {
            ref readonly TerrainVertex terrainVertex = ref terrainData.GetVertex(vertexIndex); 
            vertices[vertexIndex] = terrainVertex.Position;
            normals[vertexIndex] = terrainVertex.Normal;
            colors[vertexIndex] = terrainVertex.Color;
        }

        Debug.Log("UpdateMesh 1");

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.colors = colors;

        Debug.Log("UpdateMesh 2");

        int triangleCount = terrainData.Triangles.Count; 
        int[] triangles = new int[triangleCount * 3];
        for (int triangleIndex = 0; triangleIndex < triangleCount; ++triangleIndex)
        {
            ref readonly TerrainTriangle terrainTriangle = ref terrainData.GetTriangle(triangleIndex); 
            triangles[triangleIndex * 3 + 0] = terrainTriangle.VertexIndex0;
            triangles[triangleIndex * 3 + 1] = terrainTriangle.VertexIndex1;
            triangles[triangleIndex * 3 + 2] = terrainTriangle.VertexIndex2;
        }

        Debug.Log("UpdateMesh 3");

        mesh.triangles = triangles;

        meshFilter.mesh = mesh;

        Debug.Log("UpdateMesh 4");
    }

    private void SaveWorldToFile()
    {
        string fileName = Application.persistentDataPath + "/" + worldDirectoryName + "/" + noiseSeed.ToString() + ".ftr";
        FileHelper.SaveToFile(fileName);
        Debug.Log("World saved to: " + fileName);
    }

    private void LoadWorldFromFile()
    {
        string fileName = Application.persistentDataPath + "/" + worldDirectoryName + "/" + noiseSeed.ToString() + ".ftr";
        FileHelper.LoadFromFile(fileName);
        UpdateMesh();        
        Debug.Log("World loaded from: " + fileName);
    }

    void OnDrawGizmos()
    {
        // DebugOctree();
    }

    protected List<TerrainTriangle> collidingTriangles = new List<TerrainTriangle>();
    protected List<TerrainVertex> collidingVertices = new List<TerrainVertex>();
    protected void DebugOctree()
    {
        Gizmos.color = Color.red;

        Gizmos.DrawLine(Vector3.up, Vector3.right);
        Gizmos.DrawLine(Vector3.forward, Vector3.right);

        Camera camera = UnityEditor.SceneView.lastActiveSceneView.camera;
        if (camera)
        {
            Vector2 mousePosition = Event.current.mousePosition;
            Ray mouseRay = HandleUtility.GUIPointToWorldRay(mousePosition);

            // Debug triangles
            {
                List<TerrainTriangle> newCollidingTriangles = new List<TerrainTriangle>();
                terrainTriangleOctree.GetColliding(newCollidingTriangles, mouseRay);
                if (newCollidingTriangles.Count > 0)
                {
                    collidingTriangles = newCollidingTriangles;
                }

                foreach (TerrainTriangle terrainTriangle in collidingTriangles)
                {
                    // Gizmos.DrawLine(terrainTriangle.Vertex0.Position, terrainTriangle.Vertex1.Position);
                    // Gizmos.DrawLine(terrainTriangle.Vertex1.Position, terrainTriangle.Vertex2.Position);
                    // Gizmos.DrawLine(terrainTriangle.Vertex2.Position, terrainTriangle.Vertex0.Position);
                }
            }

            // Debug vertices
            // {
            //     List<TerrainVertex> newCollidingVertices = new List<TerrainVertex>();
            //     if (terrainVertexOctree.GetNearbyNonAlloc(mouseRay, 0.1f, newCollidingVertices))
            //     {
            //         collidingVertices = newCollidingVertices;
            //     }

            //     // Debug.Log("collidingVertices.Count " + collidingVertices.Count);

            //     foreach (TerrainVertex terrainVertex in collidingVertices)
            //     {
            //         Gizmos.DrawLine(terrainVertex.Position, terrainVertex.Position + Vector3.up * 0.1f);
            //     }
            // }
        }

        // terrainTriangleOctree.DrawAllObjects();
        // terrainVertexOctree.DrawAllObjects();
    }

    [CustomEditor(typeof(TerrainGenerator))]
    public class TerrainGeneratorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            TerrainGenerator terrainGenerator = (TerrainGenerator)target;
            if (GUILayout.Button("Generate"))
            {
                terrainGenerator.Generate();
            }

            if (GUILayout.Button("Save World"))
            {
                terrainGenerator.SaveWorldToFile();
            }

            if (GUILayout.Button("Load World"))
            {
                terrainGenerator.LoadWorldFromFile();
            }
        }
    }
}
