using UnityEngine;
using Vuforia;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// FantasyAR - Actividad 2 (Nivel 2) - Integrante 3: Orco del Dibujo 2.
/// El Dibujo 2 no tiene botones virtuales, asi que el Animator Controller (AC_Orco)
/// recorre solo las 3 animaciones: Idle -> Spell -> Jump -> Idle ...
/// Este script:
///  - Reinicia el ciclo desde Idle cada vez que Vuforia reconoce el dibujo
///    (asi en el video siempre se ven las 3 animaciones desde el inicio).
///  - Lanza el VFX de hechizo cada vez que el orco entra en la animacion Spell.
///  - En el Editor permite forzar animaciones con las teclas 4 (Spell), 5 (Idle) y 6 (Jump).
/// Va en el ImageTarget del Dibujo 2. Lo agrega y conecta el menu
/// FantasyAR > Configurar orco (Dibujo 2).
/// </summary>
public class CicloAnimacionesOrco : MonoBehaviour
{
    [Tooltip("Animator del orco. Si se deja vacio, se busca en los hijos.")]
    [SerializeField] private Animator animatorModelo;

    [Tooltip("Particulas que se reproducen al entrar en la animacion Spell.")]
    [SerializeField] private ParticleSystem vfxHechizo;

    [Tooltip("Aura que envuelve al orco: solo se ve durante Idle. Si se deja vacio, se busca VFX_AuraOrco.")]
    [SerializeField] private ParticleSystem vfxAura;

    [Tooltip("Luz verde del orco: destella al lanzar el hechizo.")]
    [SerializeField] private Light luzHechizo;
    [SerializeField] private float intensidadBase = 2f;
    [SerializeField] private float intensidadDestello = 12f;

    [Header("Salto")]
    [Tooltip("Anillo de polvo y chispas al despegar.")]
    [SerializeField] private ParticleSystem vfxDespegue;
    [Tooltip("Estela que sigue la cadera mientras el orco esta en el aire.")]
    [SerializeField] private ParticleSystem vfxEstela;
    [Tooltip("Onda, escombros y destello al aterrizar.")]
    [SerializeField] private ParticleSystem vfxAterrizaje;

    [SerializeField] private string estadoIdle = "Idle";
    [SerializeField] private string estadoSpell = "Spell";
    [SerializeField] private string estadoJump = "Jump";

    private ObserverBehaviour observer;
    private bool estabaEnSpell;
    private float destello;

    // Estado del salto
    private Transform cadera;
    private bool estabaEnJump, despego, aterrizo;
    private float alturaBase;

    private void Start()
    {
        if (animatorModelo == null)
            animatorModelo = GetComponentInChildren<Animator>(true);

        if (vfxAura == null)
        {
            foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps.name.StartsWith("VFX_AuraOrco")) { vfxAura = ps; break; }
            }
        }

        if (animatorModelo != null && animatorModelo.isHuman)
            cadera = animatorModelo.GetBoneTransform(HumanBodyBones.Hips);
        if (vfxEstela != null)
        {
            var em = vfxEstela.emission;
            em.enabled = false;
        }

        observer = GetComponent<ObserverBehaviour>();
        if (observer != null)
            observer.OnTargetStatusChanged += AlCambiarEstado;
    }

    private void OnDestroy()
    {
        if (observer != null)
            observer.OnTargetStatusChanged -= AlCambiarEstado;
    }

    private void AlCambiarEstado(ObserverBehaviour ob, TargetStatus estado)
    {
        bool visible = estado.Status == Status.TRACKED || estado.Status == Status.EXTENDED_TRACKED;
        if (visible && animatorModelo != null && animatorModelo.isActiveAndEnabled)
        {
            // Cada vez que aparece el dibujo, el ciclo empieza desde Idle
            animatorModelo.Play(estadoIdle, 0, 0f);
            estabaEnSpell = false;
        }
    }

    /// <summary>Altura de la cadera en el espacio del modelo (sin depender de la escala).</summary>
    private float AlturaCadera()
    {
        return cadera != null ? animatorModelo.transform.InverseTransformPoint(cadera.position).y : 0f;
    }

    private void Despegar()
    {
        despego = true;
        if (vfxDespegue != null) vfxDespegue.Play(true);
        if (vfxEstela != null)
        {
            var em = vfxEstela.emission;
            em.enabled = true;
        }
    }

    private void Aterrizar()
    {
        aterrizo = true;
        if (vfxEstela != null)
        {
            var em = vfxEstela.emission;
            em.enabled = false;
        }
        if (vfxAterrizaje != null) vfxAterrizaje.Play(true);
        destello = Mathf.Max(destello, 0.6f);
    }

    /// <summary>
    /// Detecta despegue y aterrizaje con la altura de la cadera (sube por encima de su altura
    /// inicial y luego vuelve a bajar). Si no se puede medir, usa el tiempo de la animacion.
    /// </summary>
    private void ActualizarSalto()
    {
        AnimatorStateInfo info = animatorModelo.GetCurrentAnimatorStateInfo(0);
        bool enJump = info.IsName(estadoJump);
        float t = info.normalizedTime;

        if (enJump && !estabaEnJump)
        {
            despego = aterrizo = false;
            alturaBase = AlturaCadera();
        }

        if (enJump && !aterrizo)
        {
            float h = AlturaCadera();
            float umbral = Mathf.Max(0.08f * Mathf.Abs(alturaBase), 0.05f);
            if (cadera != null)
            {
                if (!despego && h > alturaBase + umbral) Despegar();
                else if (despego && h < alturaBase + umbral * 0.4f) Aterrizar();
            }
            // Respaldo por tiempo (si la cadera no se mueve lo suficiente)
            if (!despego && t > 0.3f) Despegar();
            if (despego && !aterrizo && t > 0.85f) Aterrizar();
        }

        if (!enJump && estabaEnJump && despego && !aterrizo)
            Aterrizar();

        // La estela sigue a la cadera para dibujar la trayectoria del salto
        if (vfxEstela != null && cadera != null)
            vfxEstela.transform.position = cadera.position;

        estabaEnJump = enJump;
    }

    private void Update()
    {
        if (animatorModelo == null || !animatorModelo.isActiveAndEnabled) return;

        // Disparar el VFX al entrar en Spell
        bool enSpell = animatorModelo.GetCurrentAnimatorStateInfo(0).IsName(estadoSpell);
        if (enSpell && !estabaEnSpell)
        {
            if (vfxHechizo != null) vfxHechizo.Play(true);
            destello = 1f;
        }
        estabaEnSpell = enSpell;

        // El aura solo envuelve al orco durante Idle; en Spell y Jump deja de emitir y se desvanece
        if (vfxAura != null)
        {
            bool enIdle = animatorModelo.GetCurrentAnimatorStateInfo(0).IsName(estadoIdle) &&
                          !animatorModelo.IsInTransition(0);
            if (enIdle && !vfxAura.isEmitting)
                vfxAura.Play(true);
            else if (!enIdle && vfxAura.isEmitting)
                vfxAura.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        ActualizarSalto();

        // Luz: parpadeo suave como fuego + destello fuerte al lanzar el hechizo
        if (luzHechizo != null)
        {
            destello = Mathf.MoveTowards(destello, 0f, Time.deltaTime * 1.5f);
            float parpadeo = 1f + 0.25f * Mathf.PerlinNoise(Time.time * 6f, 0f);
            luzHechizo.intensity = intensidadBase * parpadeo + intensidadDestello * destello;
        }

#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
        // Solo para probar en el Editor: 4 = Spell, 5 = Idle, 6 = Jump
        var teclado = Keyboard.current;
        if (teclado == null) return;
        if (teclado.digit4Key.wasPressedThisFrame) animatorModelo.SetTrigger("Spell");
        if (teclado.digit5Key.wasPressedThisFrame) animatorModelo.SetTrigger("Idle");
        if (teclado.digit6Key.wasPressedThisFrame) animatorModelo.SetTrigger("Jump");
#endif
    }
}
