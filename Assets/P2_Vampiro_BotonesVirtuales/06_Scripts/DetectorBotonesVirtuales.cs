using System.Collections;
using System.Text;
using Unity.Collections;
using UnityEngine;
using Vuforia;

/// <summary>
/// FantasyAR - Detector de botones virtuales por oclusion (Vuforia Engine 11).
/// Va en el Image Target. Mientras el dibujo esta rastreado, cada 'intervalo' segundos
/// lee la imagen de la pantalla en la zona de cada BotonVirtualFantasy hijo y la compara
/// con una referencia tomada al reconocer el dibujo. Si una zona deja de parecerse
/// (la tapa la mano) mientras otra sigue visible, ese boton se considera presionado.
/// </summary>
public class DetectorBotonesVirtuales : MonoBehaviour
{
    [Tooltip("Camara AR (ARCamera). Si se deja vacio se usa Camera.main")]
    [SerializeField] private Camera camaraAR;

    [Tooltip("Segundos entre lecturas")]
    [SerializeField, Range(0.03f, 0.5f)] private float intervalo = 0.1f;

    [Tooltip("Lecturas seguidas con la zona tapada para contar como pulsacion")]
    [SerializeField, Range(1, 6)] private int lecturasParaPresionar = 2;

    [Tooltip("Similitud por debajo de la cual la zona se considera tapada (mas alto = mas sensible)")]
    [SerializeField, Range(0f, 1f)] private float umbralPresion = 0.45f;

    [Tooltip("Similitud por encima de la cual el boton se suelta")]
    [SerializeField, Range(0f, 1f)] private float umbralSoltar = 0.70f;

    [Tooltip("Muestra en pantalla la similitud de cada boton (para ajustar los umbrales)")]
    [SerializeField] private bool mostrarDiagnostico = false;

    private const int Celdas = 6;              // celdas por lado en cada boton
    private const int Sub = 4;                 // muestras por lado dentro de cada celda
    private const float EsperaInicial = 0.5f;  // segundos de rastreo estable antes de calibrar
    private const float Adaptacion = 0.1f;     // velocidad con que la referencia se adapta

    private ObserverBehaviour observer;
    private BotonVirtualFantasy[] botones;
    private Vector2[][] puntos;      // puntos proyectados en pantalla por boton
    private float[][] actuales;      // firma normalizada actual por boton
    private readonly float[] celdas = new float[Celdas * Celdas];
    private Texture2D lectura;
    private RenderTexture captura;   // copia de la pantalla (Game view en el editor)
    private float tiempoRastreado;
    private bool calibrado;
    private GUIStyle estilo;

    private void Start()
    {
        observer = GetComponent<ObserverBehaviour>();
        botones = GetComponentsInChildren<BotonVirtualFantasy>(true);
        if (camaraAR == null) camaraAR = Camera.main;
        if (camaraAR == null)
        {
            VuforiaBehaviour vuforia = FindAnyObjectByType<VuforiaBehaviour>();
            if (vuforia != null) camaraAR = vuforia.GetComponent<Camera>();
        }

        puntos = new Vector2[botones.Length][];
        actuales = new float[botones.Length][];
        for (int i = 0; i < botones.Length; i++)
        {
            puntos[i] = new Vector2[Celdas * Celdas * Sub * Sub];
            actuales[i] = new float[Celdas * Celdas];
        }

        if (observer == null)
            Debug.LogWarning("[FantasyAR] DetectorBotonesVirtuales debe ir en el Image Target.", this);
        if (botones.Length == 0)
            Debug.LogWarning("[FantasyAR] No hay BotonVirtualFantasy como hijos de este Image Target.", this);

        StartCoroutine(Bucle());
    }

    private void OnDestroy()
    {
        if (lectura != null) Destroy(lectura);
        if (captura != null) { captura.Release(); Destroy(captura); }
    }

    private IEnumerator Bucle()
    {
        var finDeFrame = new WaitForEndOfFrame();
        float siguiente = 0f;
        while (true)
        {
            yield return finDeFrame;
            if (Time.unscaledTime < siguiente) continue;
            siguiente = Time.unscaledTime + intervalo;
            Procesar();
        }
    }

    private void Procesar()
    {
        bool rastreado = observer != null && observer.TargetStatus.Status == Status.TRACKED;
        if (!rastreado || camaraAR == null || botones.Length == 0)
        {
            Reiniciar();
            return;
        }

        tiempoRastreado += intervalo;
        if (tiempoRastreado < EsperaInicial) return;

        // 1) Proyectar los puntos de muestreo de cada boton a la pantalla
        float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
        int lado = Celdas * Sub;
        for (int b = 0; b < botones.Length; b++)
        {
            int k = 0;
            for (int j = 0; j < lado; j++)
            {
                for (int i = 0; i < lado; i++)
                {
                    float u = (i + 0.5f) / lado - 0.5f;
                    float v = (j + 0.5f) / lado - 0.5f;
                    Vector3 p = camaraAR.WorldToScreenPoint(botones[b].PuntoMundo(u, v));
                    if (p.z <= 0f) return; // el boton esta detras de la camara
                    puntos[b][k++] = p;
                    if (p.x < xMin) xMin = p.x;
                    if (p.x > xMax) xMax = p.x;
                    if (p.y < yMin) yMin = p.y;
                    if (p.y > yMax) yMax = p.y;
                }
            }
        }

        int x0 = Mathf.FloorToInt(xMin), y0 = Mathf.FloorToInt(yMin);
        int x1 = Mathf.CeilToInt(xMax), y1 = Mathf.CeilToInt(yMax);
        if (x0 < 0 || y0 < 0 || x1 >= Screen.width || y1 >= Screen.height) return; // botones fuera de pantalla
        int ancho = x1 - x0 + 1, alto = y1 - y0 + 1;
        if (ancho < 8 || alto < 8 || ancho > 4096 || alto > 4096) return;

        // 2) Copiar la pantalla a una RenderTexture propia y leer solo ese rectangulo.
        //    (No se lee directo de la pantalla porque en el editor Vuforia deja activa
        //    la textura de la webcam al final del frame.)
        if (captura == null || captura.width != Screen.width || captura.height != Screen.height)
        {
            if (captura != null) { captura.Release(); Destroy(captura); }
            captura = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            captura.Create();
        }
        if (lectura == null || lectura.width < ancho || lectura.height < alto)
        {
            if (lectura != null) Destroy(lectura);
            lectura = new Texture2D(ancho + 32, alto + 32, TextureFormat.RGBA32, false);
        }

        ScreenCapture.CaptureScreenshotIntoRenderTexture(captura);

        // En DirectX/Metal/Vulkan la captura queda invertida verticalmente (comportamiento de Unity).
        bool invertida = SystemInfo.graphicsUVStartsAtTop;
        int yLectura = invertida ? captura.height - y0 - alto : y0;

        RenderTexture anterior = RenderTexture.active;
        RenderTexture.active = captura;
        lectura.ReadPixels(new Rect(x0, yLectura, ancho, alto), 0, 0, false);
        RenderTexture.active = anterior;

        NativeArray<Color32> pixeles = lectura.GetPixelData<Color32>(0);
        int anchoTex = lectura.width;

        // 3) Firma de cada boton: brillo promedio de una rejilla de celdas, normalizada
        for (int b = 0; b < botones.Length; b++)
        {
            System.Array.Clear(celdas, 0, celdas.Length);
            int k = 0;
            for (int j = 0; j < lado; j++)
            {
                for (int i = 0; i < lado; i++)
                {
                    Vector2 p = puntos[b][k++];
                    int px = Mathf.Clamp((int)p.x - x0, 0, ancho - 1);
                    int fila = (int)p.y - y0;
                    int py = Mathf.Clamp(invertida ? alto - 1 - fila : fila, 0, alto - 1);
                    Color32 c = pixeles[py * anchoTex + px];
                    celdas[(j / Sub) * Celdas + (i / Sub)] += 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
                }
            }
            Normalizar(celdas, actuales[b]);
        }

        // 4) Calibrar la referencia la primera vez que se ve el dibujo
        if (!calibrado)
        {
            for (int b = 0; b < botones.Length; b++)
            {
                botones[b].referencia = (float[])actuales[b].Clone();
                botones[b].Similitud = 1f;
            }
            calibrado = true;
            return;
        }

        // 5) Comparar con la referencia
        int visibles = 0;
        for (int b = 0; b < botones.Length; b++)
        {
            botones[b].Similitud = Correlacion(actuales[b], botones[b].referencia);
            if (botones[b].Similitud >= umbralSoltar) visibles++;
        }

        for (int b = 0; b < botones.Length; b++)
        {
            BotonVirtualFantasy boton = botones[b];
            if (!boton.Presionado)
            {
                // Solo cuenta como pulsacion si otro boton sigue claramente visible
                // (evita falsas pulsaciones por movimiento brusco o cambio de luz).
                bool tapado = boton.Similitud < umbralPresion && visibles >= 1;
                boton.lecturasTapado = tapado ? boton.lecturasTapado + 1 : 0;

                if (boton.lecturasTapado >= lecturasParaPresionar)
                {
                    boton.Presionar();
                }
                else if (boton.Similitud > 0.85f)
                {
                    // Adaptar la referencia lentamente a la luz y al angulo actuales
                    float[] r = boton.referencia;
                    float[] a = actuales[b];
                    for (int i = 0; i < r.Length; i++) r[i] = Mathf.Lerp(r[i], a[i], Adaptacion);
                    Normalizar(r, r);
                }
            }
            else if (boton.Similitud > umbralSoltar)
            {
                boton.Soltar();
            }
        }
    }

    private void Reiniciar()
    {
        tiempoRastreado = 0f;
        calibrado = false;
        if (botones == null) return;
        foreach (BotonVirtualFantasy boton in botones)
        {
            boton.Soltar();
            boton.Similitud = 1f;
        }
    }

    /// <summary>Resta la media y divide entre la desviacion (zona lisa = todo cero).</summary>
    private static void Normalizar(float[] origen, float[] destino)
    {
        int n = origen.Length;
        float media = 0f;
        for (int i = 0; i < n; i++) media += origen[i];
        media /= n;

        float varianza = 0f;
        for (int i = 0; i < n; i++)
        {
            float d = origen[i] - media;
            varianza += d * d;
        }
        float desviacion = Mathf.Sqrt(varianza / n);

        if (desviacion < 1e-3f)
        {
            for (int i = 0; i < n; i++) destino[i] = 0f;
            return;
        }
        for (int i = 0; i < n; i++) destino[i] = (origen[i] - media) / desviacion;
    }

    private static float Correlacion(float[] a, float[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return 1f;
        float suma = 0f;
        for (int i = 0; i < a.Length; i++) suma += a[i] * b[i];
        return suma / a.Length;
    }

    private void OnGUI()
    {
        if (!mostrarDiagnostico || botones == null) return;
        if (estilo == null)
        {
            estilo = new GUIStyle(GUI.skin.label) { fontSize = 32 };
            estilo.normal.textColor = Color.yellow;
        }

        var texto = new StringBuilder(calibrado ? "Botones virtuales\n" : "Buscando / calibrando...\n");
        foreach (BotonVirtualFantasy boton in botones)
        {
            texto.Append(boton.nombreBoton).Append(": ").Append(boton.Similitud.ToString("0.00"));
            if (boton.Presionado) texto.Append("  <- PRESIONADO");
            texto.Append('\n');
        }
        GUI.Label(new Rect(20, 20, 900, 300), texto.ToString(), estilo);
    }
}
