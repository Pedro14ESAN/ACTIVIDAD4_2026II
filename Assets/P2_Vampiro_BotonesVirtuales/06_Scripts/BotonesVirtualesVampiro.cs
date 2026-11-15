using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// FantasyAR - Actividad 2 (Nivel 2) - Botones virtuales.
/// Controla las 3 animaciones del modelo 3D (Spell, Idle, Jump) con 3 botones virtuales.
/// Vuforia 11 ya no trae Virtual Buttons, por eso se usan BotonVirtualFantasy
/// (deteccion por oclusion con DetectorBotonesVirtuales).
///
/// Uso:
///  1. Menu FantasyAR > Crear 3 botones virtuales, con el ImageTarget del Dibujo 1 seleccionado
///     (crea los botones, el detector y este script, y los conecta).
///  2. Revisar que el campo Animator Modelo apunte al Animator del vampiro.
///  3. El Animator Controller debe tener 3 parametros Trigger: "Spell", "Idle" y "Jump".
/// </summary>
public class BotonesVirtualesVampiro : MonoBehaviour
{
    [Header("Modelo 3D (hijo del ImageTarget)")]
    [Tooltip("Animator del modelo de Mixamo. Si se deja vacio, se busca en los hijos.")]
    [SerializeField] private Animator animatorModelo;

    [Header("Botones virtuales (hijos del ImageTarget)")]
    [SerializeField] private BotonVirtualFantasy botonHechizo; // Boton 1 - Spell
    [SerializeField] private BotonVirtualFantasy botonReposo;  // Boton 2 - Idle
    [SerializeField] private BotonVirtualFantasy botonSalto;   // Boton 3 - Jump

    [Header("Parametros Trigger del Animator Controller")]
    [SerializeField] private string triggerHechizo = "Spell";
    [SerializeField] private string triggerReposo = "Idle";
    [SerializeField] private string triggerSalto = "Jump";

    [Header("Efecto visual del hechizo (opcional)")]
    [Tooltip("Particulas que se reproducen al presionar el boton Hechizo.")]
    [SerializeField] private ParticleSystem vfxHechizo;

    private string animacionActual;

    private void Start()
    {
        if (animatorModelo == null)
            animatorModelo = GetComponentInChildren<Animator>();

        // El modelo arranca en reposo (estado por defecto del Animator Controller).
        animacionActual = triggerReposo;

        Registrar(botonHechizo, OnBotonHechizo, "Hechizo");
        Registrar(botonReposo, OnBotonReposo, "Reposo");
        Registrar(botonSalto, OnBotonSalto, "Salto");
    }

    private void OnDestroy()
    {
        if (botonHechizo != null) botonHechizo.AlPresionar -= OnBotonHechizo;
        if (botonReposo != null) botonReposo.AlPresionar -= OnBotonReposo;
        if (botonSalto != null) botonSalto.AlPresionar -= OnBotonSalto;
    }

    private void Registrar(BotonVirtualFantasy boton, System.Action<BotonVirtualFantasy> accion, string nombre)
    {
        if (boton != null)
            boton.AlPresionar += accion;
        else
            Debug.LogWarning($"[FantasyAR] Falta asignar el boton virtual '{nombre}' en el Inspector.", this);
    }

    // ---------- Eventos de los botones virtuales ----------

    private void OnBotonHechizo(BotonVirtualFantasy boton)
    {
        ActivarAnimacion(triggerHechizo);
        if (vfxHechizo != null) vfxHechizo.Play(true);
    }

    private void OnBotonReposo(BotonVirtualFantasy boton)
    {
        ActivarAnimacion(triggerReposo);
        if (vfxHechizo != null) vfxHechizo.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void OnBotonSalto(BotonVirtualFantasy boton)
    {
        ActivarAnimacion(triggerSalto);
        if (vfxHechizo != null) vfxHechizo.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    /// <summary>
    /// Activa solo la animacion del boton presionado: limpia los otros triggers
    /// y dispara el elegido. La transicion suave la define el Animator Controller.
    /// </summary>
    private void ActivarAnimacion(string trigger)
    {
        if (animatorModelo == null)
        {
            Debug.LogWarning("[FantasyAR] No hay Animator asignado al modelo.", this);
            return;
        }

        if (trigger == animacionActual)
            return; // ya se esta reproduciendo

        animatorModelo.ResetTrigger(triggerHechizo);
        animatorModelo.ResetTrigger(triggerReposo);
        animatorModelo.ResetTrigger(triggerSalto);
        animatorModelo.SetTrigger(trigger);

        animacionActual = trigger;
        Debug.Log($"[FantasyAR] Boton virtual presionado -> animacion {trigger}");
    }

#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
    // Solo para probar en el Editor sin camara: teclas 1, 2 y 3.
    private void Update()
    {
        var teclado = Keyboard.current;
        if (teclado == null) return;
        if (teclado.digit1Key.wasPressedThisFrame) OnBotonHechizo(null);
        if (teclado.digit2Key.wasPressedThisFrame) OnBotonReposo(null);
        if (teclado.digit3Key.wasPressedThisFrame) OnBotonSalto(null);
    }
#endif
}
