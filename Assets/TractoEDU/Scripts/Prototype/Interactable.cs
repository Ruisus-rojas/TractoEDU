using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
// Datos y resaltado visual de una pieza que puede detectarse con el retículo.
public sealed class Interactable : MonoBehaviour
{
    // Ficha pedagógica editable en el Inspector o asignable en ejecución por los scripts Setup.
    [SerializeField] private string displayName = "Objeto";
    [SerializeField, TextArea] private string description = "";
    [SerializeField, TextArea] private string function = "";
    [SerializeField, TextArea] private string utility = "";
    [SerializeField, TextArea] private string usageSituation = "";
    [SerializeField] private bool dashboardComponent;
    // Renderers extra permiten que una sola pieza resalte también sus ejes o mallas asociadas.
    [SerializeField] private Renderer[] additionalRenderers = System.Array.Empty<Renderer>();
    [SerializeField] private Collider interactionOccluder;
    [FormerlySerializedAs("highlightMaterial")]
    [SerializeField] private Material outlineMaterial;

    // El resto del simulador lee estos datos, pero no modifica directamente sus campos privados.
    public string DisplayName => displayName;
    public string Description => description;
    public string Function => function;
    public string Utility => utility;
    public string UsageSituation => usageSituation;
    public bool IsDashboardComponent => dashboardComponent;
    public bool IsHighlighted => isHighlighted;

    public void SetDashboardComponent(bool isComponent)
    {
        // Marca controles accesibles con el cursor únicamente durante la inspección del tablero.
        dashboardComponent = isComponent;
    }

    // Autoriza una única obstrucción concreta (por ejemplo, el asiento) para selección controlada.
    public void SetInteractionOccluder(Collider occluder)
    {
        interactionOccluder = occluder;
    }

    public bool CanBeSelectedThrough(Collider occluder)
    {
        return occluder != null && interactionOccluder == occluder;
    }

    // Cada entrada empareja el renderer original con la copia que dibuja el contorno.
    private readonly List<OutlinePart> outlineParts = new List<OutlinePart>();
    private bool outlinesCreated;
    private bool isHighlighted;

    private sealed class OutlinePart
    {
        // generatedMesh solo existe cuando fue necesario clonar/suavizar la malla original.
        public Renderer renderer;
        public Mesh generatedMesh;
        public SkinnedMeshRenderer sourceSkinnedRenderer;
        public SkinnedMeshRenderer outlineSkinnedRenderer;
    }

    private void Awake()
    {
        // Hereda el material de contorno del Interactable padre si esta pieza no tiene uno propio.
        if (outlineMaterial == null)
        {
            for (Transform ancestor = transform.parent; ancestor != null; ancestor = ancestor.parent)
            {
                Interactable parentInteractable = ancestor.GetComponent<Interactable>();
                if (parentInteractable == null || parentInteractable.outlineMaterial == null) continue;
                outlineMaterial = parentInteractable.outlineMaterial;
                break;
            }
        }

        CreateOutlineVisuals();
    }

    private void LateUpdate()
    {
        // Copia las poses de blendshapes para que el contorno siga a una malla animada.
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
        // Al desactivar la pieza, apaga el contorno para no dejar copias visibles.
        SetHighlighted(false);
    }

    private void OnDestroy()
    {
        // Libera los objetos y mallas temporales creados exclusivamente para el contorno.
        foreach (OutlinePart part in outlineParts)
        {
            DestroyRuntimeObject(part.generatedMesh);
            if (part.renderer != null) DestroyRuntimeObject(part.renderer.gameObject);
        }
        outlineParts.Clear();
    }

    public void Configure(string objectName, string objectDescription)
    {
        // Variante sencilla para objetos que solo necesitan título y descripción.
        displayName = objectName;
        description = objectDescription;
    }

    public void Configure(string objectName, string objectDescription, string objectFunction, string objectUtility, string objectUsageSituation)
    {
        // Variante pedagógica completa: función, utilidad y situación se guardan por separado.
        displayName = objectName;
        description = objectDescription;
        function = objectFunction;
        utility = objectUtility;
        usageSituation = objectUsageSituation;
    }

    public void SetAdditionalRenderers(params Renderer[] renderers)
    {
        // Reemplaza las mallas asociadas y reconstruye el contorno si ya estamos en Play Mode.
        additionalRenderers = renderers ?? System.Array.Empty<Renderer>();
        if (!Application.isPlaying) return;

        SetHighlighted(false);
        DestroyOutlineVisuals();
        CreateOutlineVisuals();
    }

    public void SetOutlineMaterial(Material material)
    {
        // Cambiar el material requiere regenerar las copias visuales del contorno.
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
        // El resaltado se dibuja habilitando/deshabilitando los renderers duplicados.
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
        // Reúne renderers propios y adicionales sin duplicados y crea su contorno correspondiente.
        if (outlinesCreated || outlineMaterial == null || !Application.isPlaying) return;
        outlinesCreated = true;

        List<Renderer> sourceRenderers = new List<Renderer>(GetComponentsInChildren<Renderer>(true));
        HashSet<Renderer> includedRenderers = new HashSet<Renderer>(sourceRenderers);
        foreach (Renderer additionalRenderer in additionalRenderers)
        {
            if (additionalRenderer != null && includedRenderers.Add(additionalRenderer))
                sourceRenderers.Add(additionalRenderer);
        }

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
        // Para mallas estáticas copia MeshFilter y crea un renderer de contorno como hijo.
        MeshFilter sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
        if (sourceFilter == null || sourceFilter.sharedMesh == null) return;

        Mesh sourceMesh = sourceFilter.sharedMesh;
        Mesh outlineMesh = sourceMesh.isReadable ? CreateSmoothedOutlineMesh(sourceMesh) : sourceMesh;
        GameObject outlineObject = CreateOutlineObject(sourceRenderer.transform, sourceRenderer.gameObject.layer, sourceRenderer.name);
        outlineObject.AddComponent<MeshFilter>().sharedMesh = outlineMesh;
        MeshRenderer outlineRenderer = outlineObject.AddComponent<MeshRenderer>();
        ConfigureOutlineRenderer(outlineRenderer, sourceRenderer, outlineMesh.subMeshCount);

        outlineParts.Add(new OutlinePart
        {
            renderer = outlineRenderer,
            generatedMesh = outlineMesh != sourceMesh ? outlineMesh : null
        });
    }

    private void CreateSkinnedOutline(SkinnedMeshRenderer sourceRenderer)
    {
        // Para mallas deformables conserva huesos y parámetros para que la copia siga la animación.
        if (sourceRenderer.sharedMesh == null) return;

        Mesh sourceMesh = sourceRenderer.sharedMesh;
        Mesh outlineMesh = sourceMesh.isReadable ? CreateSmoothedOutlineMesh(sourceMesh) : sourceMesh;
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
            generatedMesh = outlineMesh != sourceMesh ? outlineMesh : null,
            sourceSkinnedRenderer = sourceRenderer,
            outlineSkinnedRenderer = outlineRenderer
        });
    }

    private GameObject CreateOutlineObject(Transform sourceTransform, int layer, string sourceName)
    {
        // Crea un hijo ocultable llamado *_Outline junto a la malla real.
        GameObject outlineObject = new GameObject(sourceName + "_Outline");
        outlineObject.transform.SetParent(sourceTransform, false);
        outlineObject.layer = layer;
        return outlineObject;
    }

    private void ConfigureOutlineRenderer(Renderer outlineRenderer, Renderer sourceRenderer, int subMeshCount)
    {
        // Asigna el material y evita sombras/reflejos adicionales; empieza desactivado.
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
        // Clona la malla y promedia normales de vértices coincidentes para que el borde sea uniforme.
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
        // Cuantiza posiciones para agrupar vértices casi idénticos con precisión de 0.0001 unidades.
        const float precision = 10000f;
        return new Vector3Int(
            Mathf.RoundToInt(position.x * precision),
            Mathf.RoundToInt(position.y * precision),
            Mathf.RoundToInt(position.z * precision));
    }

    private static void DestroyRuntimeObject(Object objectToDestroy)
    {
        // Usa Destroy durante el juego y DestroyImmediate fuera de Play Mode.
        if (objectToDestroy == null) return;
        if (Application.isPlaying) Object.Destroy(objectToDestroy);
        else Object.DestroyImmediate(objectToDestroy);
    }

    private void DestroyOutlineVisuals()
    {
        // Elimina copias actuales antes de reconstruirlas, y permite una futura creación.
        foreach (OutlinePart part in outlineParts)
        {
            DestroyRuntimeObject(part.generatedMesh);
            if (part.renderer != null) DestroyRuntimeObject(part.renderer.gameObject);
        }
        outlineParts.Clear();
        outlinesCreated = false;
    }
}

