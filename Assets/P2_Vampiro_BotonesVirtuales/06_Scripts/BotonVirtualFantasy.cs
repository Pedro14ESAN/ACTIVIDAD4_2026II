using System;
using UnityEngine;

/// <summary>
/// FantasyAR - Boton virtual por oclusion (compatible con Vuforia Engine 11).
/// Vuforia 11 elimino los Virtual Buttons; este componente cumple la misma funcion:
/// cuando la mano tapa la zona del boton sobre el Image Target, se dispara AlPresionar.
///
/// Va en un objeto hijo del Image Target, centrado sobre el circulo dibujado.
/// El area es un cuadrado de lado 'tamano' sobre el plano del Image Target
/// (se ve como un recuadro verde en la vista Scene).
/// La deteccion la hace el componente DetectorBotonesVirtuales del Image Target.
/// </summary>
[DisallowMultipleComponent]
public class BotonVirtualFantasy : MonoBehaviour
{
    [Tooltip("Nombre del boton (aparece en la vista Scene y en el diagnostico)")]
    public string nombreBoton = "Boton";

    [Tooltip("Lado del area cuadrada del boton, en unidades locales")]
    [Min(0.0001f)] public float tamano = 0.02f;

    /// <summary>Se dispara cuando la mano tapa el boton.</summary>
    public event Action<BotonVirtualFantasy> AlPresionar;

    /// <summary>Se dispara cuando la mano deja de tapar el boton.</summary>
    public event Action<BotonVirtualFantasy> AlSoltar;

    public bool Presionado { get; private set; }

    /// <summary>Parecido entre lo que ve la camara y la referencia (1 = igual, 0 = distinto).</summary>
    public float Similitud { get; internal set; } = 1f;

    // Datos internos usados por DetectorBotonesVirtuales
    internal float[] referencia;
    internal int lecturasTapado;

    /// <summary>Lado del area en unidades del mundo.</summary>
    public float LadoMundo => tamano * Mathf.Abs(transform.lossyScale.x);

    /// <summary>Punto del area en el mundo; u y v van de -0.5 a 0.5.</summary>
    public Vector3 PuntoMundo(float u, float v)
    {
        float lado = LadoMundo;
        return transform.position + transform.right * (u * lado) + transform.forward * (v * lado);
    }

    internal void Presionar()
    {
        if (Presionado) return;
        Presionado = true;
        AlPresionar?.Invoke(this);
    }

    internal void Soltar()
    {
        lecturasTapado = 0;
        if (!Presionado) return;
        Presionado = false;
        AlSoltar?.Invoke(this);
    }

    /// <summary>Simula una pulsacion completa (util para pruebas o un boton de UI).</summary>
    public void SimularPulsacion()
    {
        Presionar();
        Soltar();
    }

    private void OnDrawGizmos()
    {
        Vector3 a = PuntoMundo(-0.5f, -0.5f);
        Vector3 b = PuntoMundo(0.5f, -0.5f);
        Vector3 c = PuntoMundo(0.5f, 0.5f);
        Vector3 d = PuntoMundo(-0.5f, 0.5f);
        Gizmos.color = Presionado ? Color.red : new Color(0.1f, 1f, 0.3f);
        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d);
        Gizmos.DrawLine(d, a);
        Gizmos.DrawLine(a, c);
        Gizmos.DrawLine(b, d);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position, nombreBoton);
#endif
    }
}
