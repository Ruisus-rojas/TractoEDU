using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class Interactable : MonoBehaviour
{
    [SerializeField] private string displayName = "Objeto";
    [SerializeField, TextArea] private string description = "";
    [FormerlySerializedAs("highlightMaterial")]
    [SerializeField] private Material outlineMaterial;

    public string DisplayName => displayName;
    public string Description => description;
    public bool IsHighlighted => isHighlighted;

    private readonly List<OutlinePart> outlineParts = new List<OutlinePart>();
    private bool outlinesCreated;
    private bool isHighlighted;

    private sealed class OutlinePart
    {
        public Renderer renderer;
        public Mesh generatedMesh;
        public SkinnedMeshRenderer sourceSkinnedRenderer;
        public SkinnedMeshRenderer outlineSkinnedRenderer;
    }

    private void Awake()
    {
        CreateOutlineVisuals();
    }

    private void LateUpdate()
    {
        if (!isHighlighted) return;
        foreach (OutlinePart part in outlineParts)
        {
            if (part.sourceSkinnedRenderer == null || part.outlineSkinnedRenderer == null) continue;
            int blendShapeCount = part.sourceSkinnedRenderer.sharedMesh.blendShapeCount;
            for (int i = 0; i < blendShapeCount; i++)
            {
                part.outlineSkinnedRenderer.SetBlendShapeWeight(i, part.sourceSkinnedRenderer.GetBlendShapeWeight(i));
            }
        }
    }

    private void OnDisable()
    {
        SetHighlighted(false);
    }

    private void OnDestroy()
    {
        foreach (OutlinePart part in outlineParts)
        {
            DestroyRuntimeObject(part.generatedMesh);
            if (part.renderer != null) DestroyRuntimeObject(part.renderer.gameObject);
        }
        outlineParts.Clear();
    }

    public void Configure(string objectName, string objectDescription)
    {
        displayName = objectName;
        description = objectDescription;
    }

    public void SetOutlineMaterial(Material material)
    {
        if (outlineMaterial == material) return;
        SetHighlighted(false);
        outlineMaterial = material;
        if (Application.isPlaying)
        {
            DestroyOutlineVisuals();
            CreateOutlineVisuals();
        }
    }

    public void SetHighlighted(bool highlighted)
    {
        if (highlighted && outlineMaterial == null) return;
        if (isHighlighted == highlighted) return;

        isHighlighted = highlighted;
        foreach (OutlinePart part in outlineParts)
        {
            if (part.renderer != null) part.renderer.enabled = highlighted;
        }
    }

    private void CreateOutlineVisuals()
    {
        if (outlinesCreated || outlineMaterial == null || !Application.isPlaying) return;
        outlinesCreated = true;

        Renderer[] sourceRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer source in sourceRenderers)
        {
            if (source is SkinnedMeshRenderer skinned)
                CreateSkinnedOutline(skinned);
            else if (source is MeshRenderer meshRenderer)
                CreateStaticOutline(meshRenderer);
        }
    }

    private void CreateStaticOutline(MeshRenderer sourceRenderer)
    {
        MeshFilter sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
        if (sourceFilter == null || sourceFilter.sharedMesh == null) return;

        Mesh outlineMesh = CreateSmoothedOutlineMesh(sourceFilter.sharedMesh);
        GameObject outlineObject = CreateOutlineObject(sourceRenderer.transform, sourceRenderer.gameObject.layer, sourceRenderer.name);
        outlineObject.AddComponent<MeshFilter>().sharedMesh = outlineMesh;
        MeshRenderer outlineRenderer = outlineObject.AddComponent<MeshRenderer>();
        ConfigureOutlineRenderer(outlineRenderer, sourceRenderer, outlineMesh.subMeshCount);

        outlineParts.Add(new OutlinePart { renderer = outlineRenderer, generatedMesh = outlineMesh });
    }

    private void CreateSkinnedOutline(SkinnedMeshRenderer sourceRenderer)
    {
        if (sourceRenderer.sharedMesh == null) return;

        Mesh outlineMesh = CreateSmoothedOutlineMesh(sourceRenderer.sharedMesh);
        GameObject outlineObject = CreateOutlineObject(sourceRenderer.transform, sourceRenderer.gameObject.layer, sourceRenderer.name);
        SkinnedMeshRenderer outlineRenderer = outlineObject.AddComponent<SkinnedMeshRenderer>();
        outlineRenderer.sharedMesh = outlineMesh;
        outlineRenderer.rootBone = sourceRenderer.rootBone;
        outlineRenderer.bones = sourceRenderer.bones;
        outlineRenderer.quality = sourceRenderer.quality;
        outlineRenderer.updateWhenOffscreen = sourceRenderer.updateWhenOffscreen;
        outlineRenderer.skinnedMotionVectors = sourceRenderer.skinnedMotionVectors;
        outlineRenderer.localBounds = sourceRenderer.localBounds;
        ConfigureOutlineRenderer(outlineRenderer, sourceRenderer, outlineMesh.subMeshCount);

        outlineParts.Add(new OutlinePart
        {
            renderer = outlineRenderer,
            generatedMesh = outlineMesh,
            sourceSkinnedRenderer = sourceRenderer,
            outlineSkinnedRenderer = outlineRenderer
        });
    }

    private GameObject CreateOutlineObject(Transform sourceTransform, int layer, string sourceName)
    {
        GameObject outlineObject = new GameObject(sourceName + "_Outline");
        outlineObject.transform.SetParent(sourceTransform, false);
        outlineObject.layer = layer;
        return outlineObject;
    }

    private void ConfigureOutlineRenderer(Renderer outlineRenderer, Renderer sourceRenderer, int subMeshCount)
    {
        Material[] materials = new Material[Mathf.Max(1, subMeshCount)];
        for (int i = 0; i < materials.Length; i++) materials[i] = outlineMaterial;
        outlineRenderer.sharedMaterials = materials;
        outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;
        outlineRenderer.lightProbeUsage = LightProbeUsage.Off;
        outlineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        outlineRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        outlineRenderer.sortingOrder = sourceRenderer.sortingOrder;
        outlineRenderer.enabled = false;
    }

    private Mesh CreateSmoothedOutlineMesh(Mesh source)
    {
        Mesh outlineMesh = Instantiate(source);
        outlineMesh.name = source.name + "_OutlineRuntime";
        outlineMesh.hideFlags = HideFlags.DontSave;

        Vector3[] vertices = outlineMesh.vertices;
        Vector3[] normals = outlineMesh.normals;
        if (normals.Length != vertices.Length)
        {
            outlineMesh.RecalculateNormals();
            normals = outlineMesh.normals;
        }

        Dictionary<Vector3Int, Vector3> normalSums = new Dictionary<Vector3Int, Vector3>();
        Dictionary<Vector3Int, int> normalCounts = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3Int key = PositionKey(vertices[i]);
            if (normalSums.TryGetValue(key, out Vector3 sum))
            {
                normalSums[key] = sum + normals[i];
                normalCounts[key]++;
            }
            else
            {
                normalSums.Add(key, normals[i]);
                normalCounts.Add(key, 1);
            }
        }

        for (int i = 0; i < normals.Length; i++)
        {
            Vector3 average = normalSums[PositionKey(vertices[i])] / normalCounts[PositionKey(vertices[i])];
            if (average.sqrMagnitude > 0.0001f) normals[i] = average.normalized;
        }
        outlineMesh.normals = normals;
        return outlineMesh;
    }

    private static Vector3Int PositionKey(Vector3 position)
    {
        const float precision = 10000f;
        return new Vector3Int(
            Mathf.RoundToInt(position.x * precision),
            Mathf.RoundToInt(position.y * precision),
            Mathf.RoundToInt(position.z * precision));
    }

    private static void DestroyRuntimeObject(Object objectToDestroy)
    {
        if (objectToDestroy == null) return;
        if (Application.isPlaying) Object.Destroy(objectToDestroy);
        else Object.DestroyImmediate(objectToDestroy);
    }

    private void DestroyOutlineVisuals()
    {
        foreach (OutlinePart part in outlineParts)
        {
            DestroyRuntimeObject(part.generatedMesh);
            if (part.renderer != null) DestroyRuntimeObject(part.renderer.gameObject);
        }
        outlineParts.Clear();
        outlinesCreated = false;
    }
}

