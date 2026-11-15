using UnityEditor;
using UnityEngine;

/// <summary>
/// FantasyAR - Menu: FantasyAR > Crear 3 botones virtuales en el Image Target seleccionado.
/// Crea BV_1_Hechizo, BV_2_Reposo y BV_3_Salto encima de los 3 circulos del Dibujo 1 (Vampiro),
/// agrega DetectorBotonesVirtuales y BotonesVirtualesVampiro al Image Target y los conecta.
/// Se puede volver a ejecutar: reubica los botones que ya existen.
/// </summary>
public static class CrearBotonesVirtualesFantasy
{
    // Centros de los circulos en el dibujo (1200 x 1750 px):
    // u = fraccion del ancho desde la izquierda, v = fraccion del alto desde arriba.
    private static readonly string[] Nombres = { "BV_1_Hechizo", "BV_2_Reposo", "BV_3_Salto" };
    private static readonly string[] Etiquetas = { "Hechizo", "Reposo", "Salto" };
    private static readonly float[] CentroU = { 240f / 1200f, 600f / 1200f, 960f / 1200f };
    private const float CentroV = 1445f / 1750f;

    // Lado del area = cuadrado dentro del circulo (diametro 250 px de 1200 px de ancho)
    private const float LadoRelativo = (250f / 1200f) * 0.68f;

    [MenuItem("FantasyAR/Crear 3 botones virtuales en el Image Target seleccionado")]
    private static void Crear()
    {
        GameObject target = Selection.activeGameObject;
        if (target == null)
        {
            Avisar("Selecciona primero en la Hierarchy el ImageTarget del Dibujo 1 (Vampiro).");
            return;
        }

        if (!BuscarEsquinas(target, out Vector3 supIzq, out Vector3 supDer, out Vector3 infIzq))
        {
            Avisar("No encontre la imagen del Image Target.\n\nRevisa que el ImageTarget tenga asignada " +
                   "la base de datos y el dibujo (en el Inspector debe verse el vampiro sobre el plano).");
            return;
        }

        Vector3 ejeAncho = supDer - supIzq;  // izquierda -> derecha
        Vector3 ejeAlto = infIzq - supIzq;   // arriba -> abajo
        Vector3 adelante = (-ejeAlto).normalized;                         // hacia arriba del dibujo
        Vector3 normal = Vector3.Cross(-ejeAlto, ejeAncho).normalized;    // perpendicular al dibujo
        Quaternion rotacion = Quaternion.LookRotation(adelante, normal);
        float ladoMundo = LadoRelativo * ejeAncho.magnitude;

        Undo.SetCurrentGroupName("Crear botones virtuales FantasyAR");
        int grupo = Undo.GetCurrentGroup();

        var botones = new BotonVirtualFantasy[Nombres.Length];
        for (int i = 0; i < Nombres.Length; i++)
        {
            Transform existente = target.transform.Find(Nombres[i]);
            GameObject go;
            if (existente != null)
            {
                go = existente.gameObject;
                Undo.RecordObject(go.transform, "Reubicar boton virtual");
            }
            else
            {
                go = new GameObject(Nombres[i]);
                Undo.RegisterCreatedObjectUndo(go, "Crear boton virtual");
                Undo.SetTransformParent(go.transform, target.transform, "Crear boton virtual");
            }

            Vector3 centro = supIzq + ejeAncho * CentroU[i] + ejeAlto * CentroV;
            go.transform.SetPositionAndRotation(centro, rotacion);
            go.transform.localScale = Vector3.one;

            BotonVirtualFantasy boton = go.GetComponent<BotonVirtualFantasy>();
            if (boton == null) boton = Undo.AddComponent<BotonVirtualFantasy>(go);
            Undo.RecordObject(boton, "Configurar boton virtual");
            boton.nombreBoton = Etiquetas[i];
            float escala = Mathf.Abs(go.transform.lossyScale.x);
            boton.tamano = escala > 1e-6f ? ladoMundo / escala : ladoMundo;
            EditorUtility.SetDirty(boton);
            botones[i] = boton;
        }

        if (target.GetComponent<DetectorBotonesVirtuales>() == null)
            Undo.AddComponent<DetectorBotonesVirtuales>(target);

        BotonesVirtualesVampiro control = target.GetComponent<BotonesVirtualesVampiro>();
        if (control == null) control = Undo.AddComponent<BotonesVirtualesVampiro>(target);

        var so = new SerializedObject(control);
        so.FindProperty("botonHechizo").objectReferenceValue = botones[0];
        so.FindProperty("botonReposo").objectReferenceValue = botones[1];
        so.FindProperty("botonSalto").objectReferenceValue = botones[2];
        SerializedProperty animatorProp = so.FindProperty("animatorModelo");
        if (animatorProp.objectReferenceValue == null)
        {
            Animator animator = target.GetComponentInChildren<Animator>(true);
            if (animator != null) animatorProp.objectReferenceValue = animator;
        }
        so.ApplyModifiedProperties();

        Undo.CollapseUndoOperations(grupo);
        Selection.activeGameObject = target;
        Debug.Log("[FantasyAR] Botones virtuales creados sobre los 3 circulos del dibujo. " +
                  "Revisa en la vista Scene que los recuadros verdes queden encima de los circulos.", target);
    }

    /// <summary>
    /// Busca el plano que muestra la imagen del Image Target y devuelve sus esquinas
    /// (superior izquierda, superior derecha, inferior izquierda) usando las coordenadas UV.
    /// </summary>
    internal static bool BuscarEsquinas(GameObject target, out Vector3 supIzq, out Vector3 supDer, out Vector3 infIzq)
    {
        supIzq = supDer = infIzq = Vector3.zero;
        MeshFilter mejor = null;
        float mejorArea = 0f;

        foreach (MeshFilter mf in target.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            if (mf.GetComponentInParent<Animator>() != null) continue;           // ignora el modelo 3D
            if (mf.GetComponentInParent<BotonVirtualFantasy>() != null) continue;

            Vector3 s = Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale);
            float[] d = { Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z) };
            System.Array.Sort(d);
            if (d[2] <= 0f || d[0] > 0.01f * d[2]) continue;                     // no es un plano
            float area = d[1] * d[2];
            if (area > mejorArea)
            {
                mejorArea = area;
                mejor = mf;
            }
        }

        if (mejor == null) return false;

        Mesh malla = mejor.sharedMesh;
        Vector3[] vertices = malla.vertices;
        Vector2[] uvs = malla.uv;
        if (vertices.Length == 0 || uvs == null || uvs.Length != vertices.Length) return false;

        supIzq = mejor.transform.TransformPoint(VerticeMasCercano(vertices, uvs, new Vector2(0f, 1f)));
        supDer = mejor.transform.TransformPoint(VerticeMasCercano(vertices, uvs, new Vector2(1f, 1f)));
        infIzq = mejor.transform.TransformPoint(VerticeMasCercano(vertices, uvs, new Vector2(0f, 0f)));
        return (supDer - supIzq).sqrMagnitude > 1e-12f && (infIzq - supIzq).sqrMagnitude > 1e-12f;
    }

    private static Vector3 VerticeMasCercano(Vector3[] vertices, Vector2[] uvs, Vector2 objetivo)
    {
        int mejor = 0;
        float mejorDistancia = float.MaxValue;
        for (int i = 0; i < uvs.Length; i++)
        {
            float distancia = (uvs[i] - objetivo).sqrMagnitude;
            if (distancia < mejorDistancia)
            {
                mejorDistancia = distancia;
                mejor = i;
            }
        }
        return vertices[mejor];
    }

    private static void Avisar(string mensaje)
    {
        EditorUtility.DisplayDialog("FantasyAR - Botones virtuales", mensaje, "OK");
    }
}
