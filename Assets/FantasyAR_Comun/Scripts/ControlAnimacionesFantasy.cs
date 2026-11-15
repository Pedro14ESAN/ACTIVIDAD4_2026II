using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// FantasyAR - Control de las 3 animaciones (Idle, Spell, Jump) de un personaje.
/// Va en el personaje (junto a su Animator). Sirve para los modelos que no usan botones virtuales:
///  - Ciclo automatico opcional (Idle -> Spell -> Jump) para mostrar las 3 animaciones en el video.
///  - Teclas 1, 2 y 3 en el Editor para probar.
///  - Metodos publicos Hechizo(), Reposo() y Saltar() para conectar a botones de UI.
/// El Animator Controller debe tener los Trigger "Idle", "Spell" y "Jump".
/// </summary>
[RequireComponent(typeof(Animator))]
public class ControlAnimacionesFantasy : MonoBehaviour
{
    [Tooltip("Cambia solo entre Idle, Spell y Jump cada cierto tiempo")]
    public bool cicloAutomatico = true;

    [Tooltip("Segundos que dura cada animacion en el ciclo automatico")]
    [Min(1f)] public float segundosPorAnimacion = 4f;

    [Tooltip("Particulas que se lanzan con el hechizo (opcional)")]
    public ParticleSystem vfxHechizo;

    private static readonly string[] Triggers = { "Idle", "Spell", "Jump" };
    private Animator animator;
    private int indice;
    private float tiempo;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        animator.applyRootMotion = false;
    }

    private void OnEnable()
    {
        indice = 0;
        tiempo = 0f;
    }

    private void Update()
    {
        if (cicloAutomatico)
        {
            tiempo += Time.deltaTime;
            if (tiempo >= segundosPorAnimacion)
            {
                tiempo = 0f;
                indice = (indice + 1) % Triggers.Length;
                Activar(Triggers[indice]);
            }
        }

#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
        var teclado = Keyboard.current;
        if (teclado == null) return;
        if (teclado.digit1Key.wasPressedThisFrame) Hechizo();
        if (teclado.digit2Key.wasPressedThisFrame) Reposo();
        if (teclado.digit3Key.wasPressedThisFrame) Saltar();
#endif
    }

    public void Hechizo() => Activar("Spell");
    public void Reposo() => Activar("Idle");
    public void Saltar() => Activar("Jump");

    private void Activar(string trigger)
    {
        foreach (string t in Triggers) animator.ResetTrigger(t);
        animator.SetTrigger(trigger);
        indice = System.Array.IndexOf(Triggers, trigger);
        tiempo = 0f;

        if (vfxHechizo == null) return;
        if (trigger == "Spell") vfxHechizo.Play(true);
        else vfxHechizo.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
