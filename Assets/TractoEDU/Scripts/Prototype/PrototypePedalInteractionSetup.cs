using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
// Registra los pedales del modelo al iniciar y ajusta sus colliders al volumen visible.
public sealed class PrototypePedalInteractionSetup : MonoBehaviour
{
    // Nombres exactos del modelo; deben coincidir con los Transform de la jerarquía importada.
    private const string TractorDisplayName = "Tractor TractoEDU";
    private const string SeatObjectName = "Silla";
    private const string SteeringWheelObjectName = "Volante";
    private const string DashboardObjectName = "consola";
    private const string DifferentialLockObjectName = "Pedal_de_control_de_bloqueo_de_diferencial";
    private const string ClutchObjectName = "Pedal_Embrague_Principal";
    private const string LeftBrakeObjectName = "Pedal_de_control_del_freno_de_conducción";
    private const string RightBrakeObjectName = "Pedal_de_control_del_freno_de_conducción.001";
    private const float MinimumHitThickness = 0.025f;

    private void Start()
    {
        // Busca el tractor, ajusta el asiento y registra embrague y frenos por separado.
        Interactable tractor = FindTractorRoot();
        if (tractor == null)
        {
            Debug.LogWarning("No se encontró el modelo Tractor TractoEDU para registrar el embrague y los frenos.", this);
            return;
        }

        Transform[] transforms = tractor.GetComponentsInChildren<Transform>(true);
        RegisterNamedPart(transforms, SeatObjectName, RegisterSeat,
            "No se encontró el asiento del modelo nuevo; no se registró.");
        RegisterNamedPart(transforms, SteeringWheelObjectName, RegisterSteeringWheel,
            "No se encontró el volante del modelo nuevo; no se registró.");
        RegisterNamedPart(transforms, DashboardObjectName, RegisterDashboard,
            "No se encontró el tablero del modelo nuevo; no se registró.");
        RegisterDashboardComponents(transforms);
        RegisterNamedPart(transforms, DifferentialLockObjectName, RegisterDifferentialLock,
            "No se encontró el pedal del bloqueo del diferencial; no se registró.");
        RefineSeatCollider(transforms);
        RegisterNamedPart(transforms, ClutchObjectName, RegisterClutch,
            "No se encontró el objeto de modelo del pedal de embrague; no se registró.");
        RegisterNamedPart(transforms, LeftBrakeObjectName,
            part => RegisterBrake(part, "Pedal de freno izquierdo"),
            "No se encontró el objeto de modelo del pedal de freno izquierdo; no se registró.");
        RegisterNamedPart(transforms, RightBrakeObjectName,
            part => RegisterBrake(part, "Pedal de freno derecho"),
            "No se encontró el objeto de modelo del pedal de freno derecho; no se registró.");
    }

    private static void RegisterNamedPart(Transform[] transforms, string exactName, Action<Transform> register, string missingMessage)
    {
        // Centraliza la búsqueda por nombre y emite un aviso claro si cambió la jerarquía.
        Transform part = FindExactTransform(transforms, exactName);
        if (part == null)
        {
            Debug.LogWarning(missingMessage + " Nombre buscado: " + exactName);
            return;
        }

        register(part);
    }

    private static void RegisterClutch(Transform part)
    {
        // Asigna al embrague su ficha específica y ajusta la zona seleccionable al pedal.
        Interactable interactable = GetOrAddInteractable(part);
        const string description = "Función: pisar el pedal separa el embrague y soltarlo lo acopla.\n\nUtilidad: permite accionar el embrague principal.\n\nSituación de uso: se pisa para arrancar el motor. Al usarlo, se pisa rápidamente para separar y se levanta lentamente para acoplar. Durante la conducción no se deben mantener los pies sobre los pedales.";
        const string function = "Separar o acoplar el embrague principal.";
        const string utility = "Permite accionar el embrague principal con el pedal.";
        const string usage = "Se pisa para arrancar el motor. Al usarlo, se pisa rápidamente para separar y se levanta lentamente para acoplar; durante la conducción no se deben mantener los pies sobre los pedales.";
        interactable.Configure("Pedal de embrague principal", description, function, utility, usage);
        FitColliderToVisiblePart(part);
    }

    private static void RegisterSeat(Transform part)
    {
        // El nombre que ve el jugador se conserva aunque el modelo nuevo llame "Silla" a la malla.
        Interactable interactable = GetOrAddInteractable(part);
        interactable.Configure("Asiento del conductor",
            "Asiento ubicado detrás del volante, en el puesto de conducción.",
            "Aloja al operador en el puesto de conducción.",
            "Permite al operador permanecer sentado durante la operación.",
            "El manual indica sentarse en el asiento al conducir el tractor.");
    }

    private static void RegisterSteeringWheel(Transform part)
    {
        // Se conserva explícitamente la limitación documental ya mostrada para el volante.
        Interactable interactable = GetOrAddInteractable(part);
        interactable.Configure("Volante", "Aro del volante ubicado frente al conductor.",
            "El manual proporcionado no describe específicamente el funcionamiento del volante.",
            "No especificada para este componente en el manual proporcionado.",
            "El manual proporcionado no especifica una situación de uso para este componente.");
        FitColliderToVisiblePart(part);
    }

    private static void RegisterDashboard(Transform part)
    {
        // Conserva la ficha pedagógica existente del tablero.
        Interactable interactable = GetOrAddInteractable(part);
        interactable.Configure("Tablero",
            "Panel frontal de la cabina donde se encuentran mandos e instrumentos.",
            "Reúne instrumentos y mandos visibles en el puesto del operador.",
            "El manual indica comprobar el funcionamiento de luces, bocina e instrumentos.",
            "Durante las revisiones de mantenimiento, comprobar que luces, bocina e instrumentos funcionen normalmente.");
        FitColliderToVisiblePart(part);
    }

    private static void RegisterDashboardComponents(Transform[] transforms)
    {
        // Mantiene la correspondencia con los seis botones funcionales indicados para las dos columnas.
        RegisterDashboardControl(transforms, "Btn_Luz_Cabina", "Botón de luces",
            "Control de luces indicado para el botón superior de la columna izquierda.",
            "Activa o desactiva las luces asociadas a este mando.",
            "Permite al operador manejar la iluminación del tractor.",
            "Úselo cuando necesite encender o apagar las luces durante la operación.");
        RegisterDashboardControl(transforms, "Btn_Luz_Trabajo", "Interruptor de luces bajas",
            "Control de luces bajas identificado para el segundo botón de la columna izquierda.",
            "Activa o desactiva las luces bajas.",
            "Permite iluminar el entorno con el haz bajo.",
            "Úselo cuando corresponda trabajar o circular con las luces bajas.");
        RegisterDashboardControl(transforms, "Btn_Bocina", "Bocina",
            "Botón de la bocina del tractor.",
            "Activa la señal sonora de la bocina.",
            "Permite advertir sonoramente a otras personas cercanas.",
            "Úsela para emitir una señal sonora cuando sea necesario.");
        RegisterDashboardControl(transforms, "Btn_Emergencia", "Luces de parqueo",
            "Control de luces de parqueo indicado para el botón superior de la columna derecha.",
            "Activa o desactiva las luces de parqueo.",
            "Permite señalizar el tractor cuando se encuentra estacionado.",
            "Úselas cuando el tractor deba quedar señalizado mientras está estacionado.");
        Transform highBeamButton = FindExactTransform(transforms, "Btn_Luces_altas");
        if (highBeamButton == null) highBeamButton = FindExactTransform(transforms, "Btn_vacio");
        if (highBeamButton != null)
            ConfigureDashboardControl(highBeamButton, "Luces altas",
            "Control de luces altas de la columna derecha.",
            "Activa o desactiva las luces altas.",
            "Proporciona el haz de iluminación alta del tractor.",
            "Úselas cuando el campo de trabajo requiera iluminación alta y no se deslumbre a otras personas.");
        else
            Debug.LogWarning("No se encontró el botón de luces altas ('Btn_Luces_altas' ni su posición de reserva 'Btn_vacio').");
        RegisterDashboardControl(transforms, "Btn_Direccionales", "Direccionales",
            "Control de las luces direccionales del tractor.",
            "Activa las luces direccionales para señalar un cambio de dirección.",
            "Permite comunicar hacia qué lado se orientará el tractor.",
            "Actívelas antes de realizar un giro o cambio de dirección.");

        RegisterDashboardControl(transforms, "Palanca_de_cable_de_apagado", "Palanca de cable de apagado",
            "Palanca conectada al mecanismo de apagado del motor.",
            "Al extraerla, apaga el motor y queda bloqueada en la posición de apagado.",
            "Permite detener el motor mediante el cable de apagado.",
            "Para volver a arrancar, el manual indica levantar primero la palanca y empujarla hasta su posición.");
        RegisterDashboardControl(transforms, "Cerradura_llave", "Cerradura eléctrica de arranque",
            "Interruptor de encendido accionado con llave.",
            "Posición 0: apaga la fuente; posición 1: enciende la fuente; posición 2: arranque.",
            "Permite conectar la fuente eléctrica y accionar el motor de arranque.",
            "Después de arrancar, suelte la llave: vuelve a la posición 1. Cada intento dura 2–3 segundos y nunca más de 15; espere al menos 2 minutos entre intentos. En frío, si el tractor tiene precalentamiento, el manual indica mantener el interruptor A durante 20–30 segundos antes de arrancar.");

        Transform marker = FindExactTransform(transforms, "Marcador_Kilometraje");
        if (marker == null)
        {
            Debug.LogWarning("No se encontró la malla de los instrumentos para crear sus zonas de selección.", transforms.Length > 0 ? transforms[0] : null);
            return;
        }

        Renderer markerRenderer = marker.GetComponent<Renderer>();
        RegisterInstrumentZone(marker, markerRenderer, "Instrumento_Bateria", "Indicador de carga de la batería",
            new Vector3(0.65f, -0.29f, -0.21f), new Vector3(0.17f, 0.20f, 0.20f),
            "Testigo de carga de la batería.",
            "Indica el estado del sistema de carga. El manual señala que, después de arrancar el motor, este testigo debe permanecer apagado.",
            "Permite comprobar visualmente el estado indicado por el sistema de carga.",
            "Revíselo después de arrancar; el manual indica que debe estar apagado. Para el tractor de 130 HP, el voltímetro debe mostrar más de 24 V; en los demás modelos indicados, más de 12 V. (Manual, p. 27)");
        RegisterInstrumentZone(marker, markerRenderer, "Instrumento_PresionAceite", "Luz de advertencia de presión de aceite",
            new Vector3(-0.78f, -0.29f, -0.21f), new Vector3(0.17f, 0.20f, 0.20f),
            "Testigo que advierte sobre la presión del aceite del motor.",
            "El manual indica que, después de arrancar, la luz debe permanecer apagada y el manómetro de aceite debe mostrar más de 0,1 MP.",
            "Permite revisar la condición de presión de aceite indicada en el tablero.",
            "Compruébelo después de arrancar el motor. (Manual, p. 27)");
        RegisterInstrumentZone(marker, markerRenderer, "Instrumento_Freno", "Indicador del sistema de frenos",
            new Vector3(0.96f, -0.29f, -0.21f), new Vector3(0.17f, 0.20f, 0.20f),
            "El manual enumera la advertencia del líquido de freno y la luz del freno de estacionamiento. En el modelo actual ambas referencias corresponden a una única zona gráfica de advertencia, por eso se mantienen en una sola interacción.",
            "Advierte sobre el sistema de frenos; el manual no describe aquí el circuito eléctrico ni las condiciones exactas de encendido de la luz.",
            "Permite consultar el nivel de líquido de freno y el estado del freno de estacionamiento.",
            "El nivel del depósito de líquido de freno debe estar en la marca superior; si está por debajo, el manual indica agregar líquido. El freno de estacionamiento se levanta para frenar y debe quedar levantado cuando el tractor está estacionado. (Manual, pp. 17, 25 y 27)");
        RegisterInstrumentZone(marker, markerRenderer, "Instrumento_ContadorHoras", "Contador de horas",
            new Vector3(0.00f, -0.27f, -0.21f), new Vector3(0.24f, 0.18f, 0.20f),
            "Contador que registra las horas de funcionamiento del tractor.",
            "Muestra el tiempo de trabajo acumulado que utiliza el programa de mantenimiento.",
            "Permite consultar cuándo corresponde realizar los mantenimientos por horas de uso.",
            "Consúltelo para seguir los intervalos del plan de mantenimiento; el manual toma como referencia el tiempo que muestra este contador. (Manual, p. 37)");
        RegisterInstrumentZone(marker, markerRenderer, "Instrumento_Combustible", "Indicador de combustible y luz de advertencia",
            new Vector3(-0.78f, 0.08f, -0.18f), new Vector3(0.52f, 0.43f, 0.20f),
            "Indicador que muestra el nivel de combustible e incluye una luz de advertencia.",
            "Informa visualmente sobre el combustible según la indicación del tablero. El manual proporcionado no especifica el umbral exacto de la advertencia.",
            "Permite vigilar el nivel y reconocer la advertencia de combustible del tractor.",
            "Revíselo durante el uso. Consulte la indicación del modelo si aparece la luz de advertencia. (Manual, p. 27)");
        RegisterInstrumentZone(marker, markerRenderer, "Instrumento_TemperaturaAgua", "Termómetro de agua",
            new Vector3(0.80f, 0.04f, -0.18f), new Vector3(0.52f, 0.43f, 0.20f),
            "Indicador de temperatura del agua del sistema de refrigeración del motor.",
            "Muestra la temperatura indicada por la aguja del termómetro.",
            "Permite vigilar la temperatura del sistema de refrigeración.",
            "Durante el funcionamiento normal, la aguja debe permanecer dentro de la zona verde. No abra la tapa del radiador mientras el sistema esté caliente. (Manual, pp. 27 y 58)");
    }

    private static void RegisterDashboardControl(Transform[] transforms, string objectName, string displayName,
        string description, string function, string utility, string usage)
    {
        RegisterNamedPart(transforms, objectName, part =>
        {
            ConfigureDashboardControl(part, displayName, description, function, utility, usage);
        }, "No se encontró el control del tablero; no se registró: " + objectName);
    }

    private static void ConfigureDashboardControl(Transform part, string displayName,
        string description, string function, string utility, string usage)
    {
        Interactable interactable = GetOrAddInteractable(part);
        interactable.Configure(displayName,
            description + "\n\nFunción: " + function + "\n\nUtilidad: " + utility + "\n\nSituación de uso: " + usage,
            function, utility, usage);
        interactable.SetDashboardComponent(true);
        FitColliderToVisiblePart(part);
    }

    private static void RegisterInstrumentZone(Transform marker, Renderer markerRenderer, string zoneName,
        string displayName, Vector3 localPoint, Vector3 colliderSize, string description,
        string function, string utility, string usage)
    {
        MeshFilter meshFilter = marker.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogWarning("No se pudo crear la zona '" + zoneName + "': falta la malla de instrumentos.", marker);
            return;
        }

        Transform zoneTransform = marker.Find(zoneName);
        GameObject zoneObject = zoneTransform != null ? zoneTransform.gameObject : new GameObject(zoneName);
        zoneTransform = zoneObject.transform;
        zoneTransform.SetParent(marker, false);
        // La geometría no se lee en runtime; estas posiciones se midieron sobre la malla del prefab.
        // El eje Z local positivo apunta hacia el operador en este panel.
        zoneTransform.localPosition = localPoint + Vector3.forward * 0.18f;
        zoneTransform.localRotation = Quaternion.identity;
        zoneTransform.localScale = Vector3.one;

        BoxCollider box = zoneObject.GetComponent<BoxCollider>();
        if (box == null) box = zoneObject.AddComponent<BoxCollider>();
        box.center = Vector3.zero;
        box.size = colliderSize;
        box.isTrigger = true;
        box.enabled = true;

        Interactable interactable = GetOrAddInteractable(zoneTransform);
        interactable.Configure(displayName,
            description + "\n\nFunción: " + function + "\n\nUtilidad: " + utility + "\n\nSituación de uso: " + usage,
            function, utility, usage);
        interactable.SetDashboardComponent(true);
        if (markerRenderer != null) interactable.SetAdditionalRenderers(markerRenderer);
    }


    private static void RegisterDifferentialLock(Transform part)
    {
        // El manual disponible no describe el procedimiento, por lo que la ficha limita su afirmación al nombre del control.
        Interactable interactable = GetOrAddInteractable(part);
        interactable.Configure("Pedal de bloqueo del diferencial",
            "El modelo identifica esta pieza como el control de bloqueo del diferencial. El manual disponible no detalla aquí el procedimiento ni la situación de uso.",
            "Control asociado al bloqueo del diferencial.",
            "Permite accionar el control identificado como bloqueo del diferencial.",
            "El manual disponible no especifica la situación de uso de este pedal.");
        FitColliderToVisiblePart(part);
    }

    private static void RegisterBrake(Transform part, string displayName)
    {
        // Cada pedal conserva nombre lateral y comparte información basada en el manual.
        Interactable interactable = GetOrAddInteractable(part);
        string side = displayName.EndsWith("izquierdo", StringComparison.Ordinal) ? "izquierdo" : "derecho";
        string description = "Función: pisar este pedal de freno de conducción aplica el frenado; soltarlo lo libera.\n\nParte del tractor: el manual identifica pedales izquierdo y derecho, pero no especifica qué rueda frena cada pedal.\n\nUtilidad: permite aplicar o liberar el freno de conducción desde el pedal " + side + ".\n\nSituación de uso: evitar el frenado de emergencia y no apoyar el pie sobre el pedal durante la conducción. Para circular en carretera, el manual indica bloquear juntos los pedales izquierdo y derecho para que trabajen al mismo tiempo.";
        string function = "Aplicar o liberar el freno de conducción con el pedal " + side + ".";
        string utility = "Permite frenar el tractor mediante el pedal " + side + ".";
        const string usage = "Se pisa para frenar y se suelta para liberar el frenado. Durante la conducción, evitar el frenado de emergencia y no apoyar el pie sobre los pedales. En carretera, bloquear juntos los pedales izquierdo y derecho.";
        interactable.Configure(displayName, description, function, utility, usage);
        FitColliderToVisiblePart(part);
    }

    private static Interactable GetOrAddInteractable(Transform part)
    {
        // Reutiliza el componente existente para no duplicar el sistema de interacción.
        Interactable interactable = part.GetComponent<Interactable>();
        return interactable != null ? interactable : part.gameObject.AddComponent<Interactable>();
    }

    private static void RefineSeatCollider(Transform[] transforms)
    {
        // Sustituye un collider de caja impreciso por el collider de la malla del asiento.
        Transform seat = null;
        foreach (Transform candidate in transforms)
        {
            Interactable interactable = candidate.GetComponent<Interactable>();
            if (interactable != null && interactable.DisplayName == "Asiento del conductor")
            {
                seat = candidate;
                break;
            }
        }

        if (seat == null) return;
        MeshFilter meshFilter = seat.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogWarning("No se pudo ajustar el collider del asiento: falta la malla visual.", seat);
            return;
        }

        MeshCollider meshCollider = seat.GetComponent<MeshCollider>();
        if (meshCollider == null)
        {
            BoxCollider boxCollider = seat.GetComponent<BoxCollider>();
            if (boxCollider != null)
            {
                boxCollider.enabled = false;
                Destroy(boxCollider);
            }
            meshCollider = seat.gameObject.AddComponent<MeshCollider>();
        }

        meshCollider.sharedMesh = meshFilter.sharedMesh;
        meshCollider.convex = false;
        meshCollider.enabled = true;
    }

    private static void FitColliderToVisiblePart(Transform host)
    {
        // Calcula límites locales de todos los renderers hijos y crea una caja seleccionable ajustada.
        Renderer[] renderers = host.GetComponentsInChildren<Renderer>(true);
        Vector3 minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        bool hasBounds = false;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled) continue;
            Bounds rendererBounds = renderer.localBounds;
            for (int x = 0; x <= 1; x++)
            for (int y = 0; y <= 1; y++)
            for (int z = 0; z <= 1; z++)
            {
                Vector3 rendererCorner = rendererBounds.min + Vector3.Scale(rendererBounds.size, new Vector3(x, y, z));
                Vector3 localCorner = host.InverseTransformPoint(renderer.transform.TransformPoint(rendererCorner));
                minimum = Vector3.Min(minimum, localCorner);
                maximum = Vector3.Max(maximum, localCorner);
                hasBounds = true;
            }
        }

        if (!hasBounds)
        {
            Debug.LogWarning("El objeto '" + host.name + "' no tiene renderers activos; no se pudo ajustar su collider.", host);
            return;
        }

        // Volante viene reflejado desde el modelo; una caja bajo esa escala negativa produce una advertencia y geometría impredecible.
        Vector3 worldScale = host.lossyScale;
        if (worldScale.x < 0f || worldScale.y < 0f || worldScale.z < 0f)
        {
            MeshFilter meshFilter = host.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                Collider existing = host.GetComponent<Collider>();
                if (existing != null && !(existing is MeshCollider)) Destroy(existing);
                MeshCollider meshCollider = host.GetComponent<MeshCollider>();
                if (meshCollider == null) meshCollider = host.gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = meshFilter.sharedMesh;
                meshCollider.convex = true;
                meshCollider.isTrigger = true;
                meshCollider.enabled = true;
                return;
            }
        }

        Collider existingCollider = host.GetComponent<Collider>();
        BoxCollider collider = existingCollider as BoxCollider;
        if (existingCollider != null && collider == null)
        {
            Debug.LogWarning("El objeto '" + host.name + "' ya tiene un collider distinto a BoxCollider; se conserva para no alterar su física.", host);
            return;
        }
        if (collider == null) collider = host.gameObject.AddComponent<BoxCollider>();

        Vector3 padding = new Vector3(
            MinimumHitThickness / Mathf.Max(0.001f, Mathf.Abs(host.lossyScale.x)),
            MinimumHitThickness / Mathf.Max(0.001f, Mathf.Abs(host.lossyScale.y)),
            MinimumHitThickness / Mathf.Max(0.001f, Mathf.Abs(host.lossyScale.z)));
        collider.center = (minimum + maximum) * 0.5f;
        collider.size = maximum - minimum + padding;
        collider.enabled = true;
        collider.isTrigger = true;
    }

    private static Transform FindExactTransform(Transform[] transforms, string exactName)
    {
        // Ignora espacios accidentales al inicio/final, pero conserva la comparación exacta del nombre restante.
        foreach (Transform candidate in transforms)
            if (string.Equals(candidate.name.Trim(), exactName.Trim(), StringComparison.Ordinal)) return candidate;
        return null;
    }

    private static Interactable FindTractorRoot()
    {
        // Localiza el objeto principal por su nombre de interacción, no por una ruta fija de jerarquía.
        foreach (Interactable candidate in FindObjectsByType<Interactable>(FindObjectsSortMode.None))
            if (candidate.DisplayName == TractorDisplayName) return candidate;
        return null;
    }
}
