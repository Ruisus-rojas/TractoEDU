using UnityEngine;

[DisallowMultipleComponent]
// Registra palancas del tractor y sus colliders al iniciar la escena de exploración.
public sealed class PrototypeRearControlSetup : MonoBehaviour
{
    // Grosor extra mínimo para que las cajas finas también se puedan seleccionar con el retículo.
    private const float MinimumWorldHitThickness = 0.05f;

    private void Start()
    {
        // Busca el tractor y registra sus controles con los nombres del modelo nuevo.
        Interactable tractor = FindTractorRoot();
        if (tractor == null)
        {
            Debug.LogWarning("No se encontró el modelo Tractor TractoEDU para registrar sus palancas.", this);
            return;
        }

        Transform[] modelTransforms = tractor.GetComponentsInChildren<Transform>(true);
        Collider seatCollider = FindSeatCollider(modelTransforms);
        RegisterNamedControl(modelTransforms, "Palanca_control_de_accionamiento",
            "Palanca del accionamiento delantero",
            "Acoplar o separar el accionamiento del eje delantero.",
            "Permite conectar o desconectar el accionamiento delantero del tractor.",
            "El manual indica que el sentido depende de la potencia: en tractores de 60–95 hp, levantar acopla y presionar separa; en 100–130 hp, ocurre al contrario. Antes de operar, pisar el embrague. No acoplarlo al conducir por carretera dura.",
            seatCollider);

        RegisterNamedControl(modelTransforms, "Palanca_de_control_del_eje_de_salida_de_potencia",
            "Palanca del eje de salida de potencia (PTO)",
            "Seleccionar el modo sincronizado, neutro o independiente del eje de salida de potencia.",
            "Controla el modo en que se habilita la salida de potencia para accionar implementos.",
            "Levantar selecciona sincronización, la posición media es neutro y presionar selecciona independiente. Arrancar con la palanca en neutro. Para remolque sincronizado, el tractor debe estar detenido; en modo independiente, usar la palanca del embrague del eje de salida para cortar la potencia del motor.",
            seatCollider);

        RegisterNamedControl(modelTransforms, "Palanca_de_control_del_freno_de_estacionamiento",
            "Palanca del freno de estacionamiento",
            "Aplicar o liberar el freno de estacionamiento.",
            "Mantiene el tractor frenado cuando está estacionado o trabaja en el lugar.",
            "Levantar la palanca aplica el freno. Para liberarlo, pulsar primero el botón de desbloqueo y bajar la palanca; al soltarla sin desbloquear puede permanecer autobloqueada.",
            seatCollider);

        RegisterNamedControl(modelTransforms, "Palanca_de_acerelador_a_mano",
            "Palanca del acelerador de mano",
            "Aumentar o reducir la aceleración del motor.",
            "Permite mantener una aceleración determinada durante periodos prolongados con carga constante.",
            "Empujar la palanca hacia delante aumenta el acelerador y tirar de ella hacia atrás lo reduce. El manual recomienda evitar su uso para controlar la velocidad al conducir en carretera.",
            seatCollider);

        // Registra también la palanca izquierda añadida al modelo para el embrague del PTO.
        RegisterNamedControl(modelTransforms, "Palanca_de_control_de_Embrague_del_Eje_de_salida_potencia",
            "Palanca del embrague del eje de salida de potencia (PTO)",
            "Interrumpe la transmisión de potencia del motor al eje PTO.",
            "Permite detener la potencia que llega al eje PTO sin cambiar el modo seleccionado en la palanca PTO.",
            "En el modo independiente del PTO, se utiliza para cortar la potencia del motor hacia el eje de salida.",
            seatCollider, true);

        RegisterNamedControl(modelTransforms, "Palanca_principal_de_cambios",
            "Palanca primaria de cambios",
            "Selecciona la marcha principal entre 1, 2, 3 y 4.",
            "Permite elegir una marcha siguiendo el patrón 1, 2, 3 y 4 marcado junto a la palanca.",
            "Se opera después de pisar el embrague; para arrancar, ambas palancas deben estar en punto muerto.",
            seatCollider);

        RegisterNamedControl(modelTransforms, "Palanca_secundaria_de_cambios",
            "Palanca secundaria de cambios",
            "Selecciona el rango indicado por el patrón M-L-R-H del modelo.",
            "Permite elegir un rango de transmisión según el patrón marcado junto a la palanca.",
            "Se opera después de pisar el embrague; para arrancar, ambas palancas deben estar en punto muerto.",
            seatCollider);
    }

    private void RegisterNamedControl(Transform[] modelTransforms, string exactName, string displayName, string function, string utility, string usage, Collider interactionOccluder = null, bool fitColliderToWorldBounds = false)
    {
        // Registra una pieza de una sola malla; puede autorizar una obstrucción concreta o recalcular el collider.
        Transform part = FindExactTransform(modelTransforms, exactName);
        if (part == null)
        {
            Debug.LogWarning("No se encontró la malla actual '" + exactName + "'; no se registró " + displayName + ".", this);
            return;
        }

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogWarning("La malla '" + exactName + "' no tiene Renderer; no se registró " + displayName + ".", this);
            return;
        }

        RegisterControl(part, displayName, function, utility, usage, interactionOccluder, new[] { renderer }, renderer);
        if (fitColliderToWorldBounds)
        {
            BoxCollider collider = part.GetComponent<BoxCollider>();
            if (collider != null) FitColliderToWorldBounds(collider, part, renderer);
        }
    }

    private static void RegisterControl(Transform colliderHost, string displayName, string function, string utility, string usage, Collider interactionOccluder, Renderer[] colliderRenderers, params Renderer[] renderers)
    {
        // Reutiliza/añade Interactable, guarda la ficha y asegura que haya una caja trigger seleccionable.
        Interactable interactable = colliderHost.GetComponent<Interactable>();
        if (interactable == null) interactable = colliderHost.gameObject.AddComponent<Interactable>();
        string description = "Función: " + function + "\n\nUtilidad: " + utility + "\n\nSituación de uso: " + usage;
        interactable.Configure(displayName, description, function, utility, usage);
        interactable.SetAdditionalRenderers(renderers);
        interactable.SetInteractionOccluder(interactionOccluder);

        BoxCollider collider = colliderHost.GetComponent<BoxCollider>();
        if (collider == null)
        {
            Collider existingCollider = colliderHost.GetComponent<Collider>();
            if (existingCollider != null)
            {
                Debug.LogWarning("La pieza '" + colliderHost.name + "' ya tiene un collider distinto a BoxCollider; se conserva para no alterar su física.", colliderHost);
                return;
            }
            collider = colliderHost.gameObject.AddComponent<BoxCollider>();
        }

        collider.isTrigger = true;
        FitColliderToRenderers(collider, colliderHost, colliderRenderers);
    }

    private static Collider FindSeatCollider(Transform[] modelTransforms)
    {
        // Recupera el collider del asiento para permitir únicamente las selecciones autorizadas a través de él.
        foreach (Transform candidate in modelTransforms)
        {
            Interactable interactable = candidate.GetComponent<Interactable>();
            if (interactable == null || interactable.DisplayName != "Asiento del conductor") continue;
            return candidate.GetComponent<Collider>();
        }

        return null;
    }

    private static void FitColliderToRenderers(BoxCollider collider, Transform host, Renderer[] renderers)
    {
        // Convierte las esquinas de límites locales de las mallas al espacio del objeto que porta la caja.
        Vector3 minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            Bounds rendererBounds = renderer.localBounds;
            for (int x = 0; x <= 1; x++)
            for (int y = 0; y <= 1; y++)
            for (int z = 0; z <= 1; z++)
            {
                Vector3 rendererCorner = rendererBounds.min + Vector3.Scale(rendererBounds.size, new Vector3(x, y, z));
                Vector3 localCorner = host.InverseTransformPoint(renderer.transform.TransformPoint(rendererCorner));
                minimum = Vector3.Min(minimum, localCorner);
                maximum = Vector3.Max(maximum, localCorner);
            }
        }

        Vector3 localPadding = new Vector3(
            MinimumWorldHitThickness / Mathf.Max(0.001f, Mathf.Abs(host.lossyScale.x)),
            MinimumWorldHitThickness / Mathf.Max(0.001f, Mathf.Abs(host.lossyScale.y)),
            MinimumWorldHitThickness / Mathf.Max(0.001f, Mathf.Abs(host.lossyScale.z)));
        collider.center = (minimum + maximum) * 0.5f;
        collider.size = maximum - minimum + localPadding;
        collider.enabled = true;
    }

    private static void FitColliderToWorldBounds(BoxCollider collider, Transform host, Renderer renderer)
    {
        // Variante que parte de límites globales, útil cuando la malla está muy rotada respecto al control.
        Bounds worldBounds = renderer.bounds;
        worldBounds.Expand(Vector3.one * MinimumWorldHitThickness);
        Vector3 minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        for (int x = 0; x <= 1; x++)
        for (int y = 0; y <= 1; y++)
        for (int z = 0; z <= 1; z++)
        {
            Vector3 worldCorner = new Vector3(
                x == 0 ? worldBounds.min.x : worldBounds.max.x,
                y == 0 ? worldBounds.min.y : worldBounds.max.y,
                z == 0 ? worldBounds.min.z : worldBounds.max.z);
            Vector3 localCorner = host.InverseTransformPoint(worldCorner);
            minimum = Vector3.Min(minimum, localCorner);
            maximum = Vector3.Max(maximum, localCorner);
        }

        collider.center = (minimum + maximum) * 0.5f;
        collider.size = maximum - minimum;
        collider.enabled = true;
    }

    private static Transform FindExactTransform(Transform[] transforms, string exactName)
    {
        // Busca una malla por el nombre exacto importado desde el modelo.
        foreach (Transform candidate in transforms)
            if (candidate.name == exactName) return candidate;
        return null;
    }

    private static Interactable FindTractorRoot()
    {
        // Encuentra la raíz interactuable del tractor usando su nombre configurado.
        foreach (Interactable candidate in FindObjectsByType<Interactable>(FindObjectsSortMode.None))
            if (candidate.DisplayName == "Tractor TractoEDU") return candidate;
        return null;
    }
}
