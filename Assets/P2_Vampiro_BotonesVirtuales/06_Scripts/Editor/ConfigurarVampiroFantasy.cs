using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vuforia;

/// <summary>
/// FantasyAR - Configura automaticamente el vampiro de Mixamo:
///  1. Modelo: Rig Humanoid, extrae texturas y materiales (URP).
///  2. Animaciones Idle / Spell / Jump: Humanoid (avatar del vampiro), Loop Time, sin desplazamiento.
///  3. Animator Controller AC_Vampiro con transiciones suaves.
///  4. VFX: aura de niebla carmesi (siempre activa) y estallido de hechizo (boton 1).
///  5. Escena: coloca al vampiro sobre el dibujo, conecta los botones virtuales y agrega la AR Camera.
/// Se ejecuta solo una vez al abrir Unity (si existe la bandera en Library) y tambien desde
/// el menu FantasyAR > Configurar vampiro.
/// </summary>
[InitializeOnLoad]
public static class ConfigurarVampiroFantasy
{
    private const string Raiz = "Assets/P2_Vampiro_BotonesVirtuales";
    private const string RutaModelo = Raiz + "/02_Modelo3D_Texturas/Vampiro_Lusth.fbx";
    private const string CarpetaTexturas = Raiz + "/02_Modelo3D_Texturas/Texturas";
    private const string CarpetaMateriales = Raiz + "/02_Modelo3D_Texturas/Materiales";
    private const string RutaIdle = Raiz + "/03_Animaciones/Vampiro_Idle_ActionIdleToFightIdle.fbx";
    private const string RutaSpell = Raiz + "/03_Animaciones/Vampiro_Spell_Standing2HCastSpell01.fbx";
    private const string RutaJump = Raiz + "/03_Animaciones/Vampiro_Jump_JumpingDown.fbx";
    private const string RutaController = Raiz + "/04_Animator/AC_Vampiro.controller";
    private const string CarpetaVFX = Raiz + "/05_VFX";
    private const string RutaMaterialVFX = CarpetaVFX + "/M_VFX_Vampiro.mat";
    private const string RutaAura = CarpetaVFX + "/VFX_AuraVampiro.prefab";
    private const string RutaHechizo = CarpetaVFX + "/VFX_HechizoVampiro.prefab";
    private const string Bandera = "Library/FantasyAR_configurar_vampiro.pendiente";

    private static double inicio;

    static ConfigurarVampiroFantasy()
    {
        if (!File.Exists(Bandera)) return;
        inicio = EditorApplication.timeSinceStartup;
        EditorApplication.update += EsperarYConfigurar;
    }

    private static void EsperarYConfigurar()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.timeSinceStartup - inicio < 2.0) return;

        EditorApplication.update -= EsperarYConfigurar;
        if (!File.Exists(Bandera)) return;
        File.Delete(Bandera);
        ConfigurarTodo();
    }

    [MenuItem("FantasyAR/Configurar vampiro (modelo, animaciones, Animator, VFX y escena)")]
    public static void ConfigurarTodo()
    {
        try
        {
            Debug.Log("[FantasyAR] Configurando vampiro...");

            // 1) Modelo
            Avatar avatar = ConfigurarModelo();
            if (avatar == null) return;

            // 2) Animaciones
            AnimationClip idle = ConfigurarAnimacion(RutaIdle, "Idle", avatar);
            AnimationClip spell = ConfigurarAnimacion(RutaSpell, "Spell", avatar);
            AnimationClip jump = ConfigurarAnimacion(RutaJump, "Jump", avatar);
            if (idle == null || spell == null || jump == null)
            {
                Fallo("No se pudieron leer las 3 animaciones. Revisa la consola.");
                return;
            }

            // 3) Animator Controller
            AnimatorController controller = CrearController(idle, spell, jump);

            // 4) Escena + VFX
            ArmarEscena(controller);

            AssetDatabase.SaveAssets();
            Debug.Log("[FantasyAR] Vampiro configurado: modelo, animaciones, Animator, VFX y botones virtuales.");
            EditorUtility.DisplayDialog("FantasyAR",
                "Listo: el vampiro ya esta sobre el dibujo con sus 3 animaciones, el Animator, " +
                "los VFX y los botones virtuales conectados.\n\nDale Play y prueba con las teclas 1, 2 y 3.", "OK");
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            Fallo("Ocurrio un error al configurar el vampiro. Revisa la consola.");
        }
    }

    // ---------------------------------------------------------------- Modelo

    private static Avatar ConfigurarModelo()
    {
        var importador = AssetImporter.GetAtPath(RutaModelo) as ModelImporter;
        if (importador == null)
        {
            Fallo("No encuentro el modelo en " + RutaModelo);
            return null;
        }

        importador.animationType = ModelImporterAnimationType.Human;
        importador.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importador.importAnimation = false;
        importador.SaveAndReimport();

        CrearCarpeta(CarpetaTexturas);
        CrearCarpeta(CarpetaMateriales);

        importador = (ModelImporter)AssetImporter.GetAtPath(RutaModelo);
        if (importador.ExtractTextures(CarpetaTexturas))
            AssetDatabase.Refresh();
        MarcarMapasNormales();
        ExtraerMateriales();
        ConvertirMaterialesAURP();

        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(RutaModelo).OfType<Avatar>().FirstOrDefault();
        if (avatar == null || !avatar.isHuman)
        {
            Fallo("El avatar del vampiro no es Humanoid valido.");
            return null;
        }
        return avatar;
    }

    private static void MarcarMapasNormales()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { CarpetaTexturas }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (!Path.GetFileName(ruta).ToLowerInvariant().Contains("normal")) continue;
            var ti = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
        }
    }

    private static void ExtraerMateriales()
    {
        Material[] materiales = AssetDatabase.LoadAllAssetsAtPath(RutaModelo).OfType<Material>().ToArray();
        bool extraido = false;
        foreach (Material m in materiales)
        {
            string nombre = string.Concat(m.name.Split(Path.GetInvalidFileNameChars()));
            string destino = AssetDatabase.GenerateUniqueAssetPath(CarpetaMateriales + "/" + nombre + ".mat");
            string error = AssetDatabase.ExtractAsset(m, destino);
            if (string.IsNullOrEmpty(error))
            {
                extraido = true;
                AssetDatabase.WriteImportSettingsIfDirty(RutaModelo);
            }
            else
            {
                Debug.LogWarning("[FantasyAR] No se pudo extraer el material " + m.name + ": " + error);
            }
        }
        if (extraido)
            AssetDatabase.ImportAsset(RutaModelo, ImportAssetOptions.ForceUpdate);
    }

    private static void ConvertirMaterialesAURP()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) return;

        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { CarpetaMateriales }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m == null || m.shader.name.StartsWith("Universal Render Pipeline")) continue;

            Texture principal = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            Color color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            Texture normal = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;

            m.shader = lit;
            if (principal != null) m.SetTexture("_BaseMap", principal);
            m.SetColor("_BaseColor", color);
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(m);
        }
    }

    // ---------------------------------------------------------- Animaciones

    private static AnimationClip ConfigurarAnimacion(string ruta, string nombre, Avatar avatar)
    {
        var importador = AssetImporter.GetAtPath(ruta) as ModelImporter;
        if (importador == null)
        {
            Debug.LogError("[FantasyAR] No encuentro la animacion " + ruta);
            return null;
        }

        importador.animationType = ModelImporterAnimationType.Human;
        importador.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
        importador.sourceAvatar = avatar;
        importador.importAnimation = true;
        importador.materialImportMode = ModelImporterMaterialImportMode.None;
        importador.SaveAndReimport();

        importador = (ModelImporter)AssetImporter.GetAtPath(ruta);
        ModelImporterClipAnimation[] clips = importador.defaultClipAnimations;
        if (clips == null || clips.Length == 0)
        {
            Debug.LogError("[FantasyAR] El archivo no tiene animacion: " + ruta);
            return null;
        }

        ModelImporterClipAnimation clip = clips[0];
        clip.name = nombre;
        clip.loopTime = true;
        // Bake Into Pose: el personaje no se desplaza ni gira sobre el dibujo
        clip.lockRootRotation = true;
        clip.keepOriginalOrientation = true;
        clip.lockRootHeightY = true;
        clip.keepOriginalPositionY = true;
        clip.lockRootPositionXZ = true;
        clip.keepOriginalPositionXZ = true;
        importador.clipAnimations = new[] { clip };
        importador.SaveAndReimport();

        return AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
    }

    private static AnimatorController CrearController(AnimationClip idle, AnimationClip spell, AnimationClip jump)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(RutaController) != null)
            AssetDatabase.DeleteAsset(RutaController);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(RutaController);
        controller.AddParameter("Idle", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Spell", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState estadoIdle = sm.AddState("Idle", new Vector3(320f, 0f, 0f));
        estadoIdle.motion = idle;
        AnimatorState estadoSpell = sm.AddState("Spell", new Vector3(320f, 90f, 0f));
        estadoSpell.motion = spell;
        AnimatorState estadoJump = sm.AddState("Jump", new Vector3(320f, 180f, 0f));
        estadoJump.motion = jump;
        sm.defaultState = estadoIdle;

        Transicion(sm, estadoIdle, "Idle");
        Transicion(sm, estadoSpell, "Spell");
        Transicion(sm, estadoJump, "Jump");

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static void Transicion(AnimatorStateMachine sm, AnimatorState destino, string trigger)
    {
        AnimatorStateTransition t = sm.AddAnyStateTransition(destino);
        t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        t.hasExitTime = false;
        t.duration = 0.25f;
        t.canTransitionToSelf = false;
    }

    // --------------------------------------------------------------- Escena

    private static void ArmarEscena(AnimatorController controller)
    {
        BotonesVirtualesVampiro control = Object.FindAnyObjectByType<BotonesVirtualesVampiro>(FindObjectsInactive.Include);
        GameObject target = control != null ? control.gameObject : null;
        if (target == null)
        {
            ImageTargetBehaviour itb = Object.FindAnyObjectByType<ImageTargetBehaviour>(FindObjectsInactive.Include);
            if (itb != null) target = itb.gameObject;
        }
        if (target == null)
        {
            Fallo("No hay ningun ImageTarget en la escena abierta.");
            return;
        }

        // Camara AR de Vuforia
        if (Object.FindAnyObjectByType<VuforiaBehaviour>(FindObjectsInactive.Include) == null)
        {
            if (!EditorApplication.ExecuteMenuItem("GameObject/Vuforia Engine/AR Camera"))
                Debug.LogWarning("[FantasyAR] Agrega la AR Camera: GameObject > Vuforia Engine > AR Camera.");
        }

        // Quitar un vampiro anterior
        Transform anterior = target.transform.Find("Vampiro");
        if (anterior != null) Undo.DestroyObjectImmediate(anterior.gameObject);

        // Instanciar el modelo y medir su altura sin escalar
        var prefabModelo = AssetDatabase.LoadAssetAtPath<GameObject>(RutaModelo);
        var vampiro = (GameObject)PrefabUtility.InstantiatePrefab(prefabModelo, target.scene);
        vampiro.name = "Vampiro";
        Undo.RegisterCreatedObjectUndo(vampiro, "Crear vampiro");
        vampiro.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        vampiro.transform.localScale = Vector3.one;
        float altoModelo = AltoDelModelo(vampiro);

        // Ubicar sobre la parte superior del dibujo, mirando hacia el borde de abajo
        Vector3 centro = target.transform.position;
        Quaternion rotacion = target.transform.rotation;
        float anchoDibujo = 0.15f;
        if (CrearBotonesVirtualesFantasy.BuscarEsquinas(target, out Vector3 supIzq, out Vector3 supDer, out Vector3 infIzq))
        {
            Vector3 ejeAncho = supDer - supIzq;
            Vector3 ejeAlto = infIzq - supIzq;
            Vector3 normal = Vector3.Cross(-ejeAlto, ejeAncho).normalized;
            anchoDibujo = ejeAncho.magnitude;
            centro = supIzq + ejeAncho * 0.5f + ejeAlto * 0.45f;
            rotacion = Quaternion.LookRotation(ejeAlto.normalized, normal);
        }

        Undo.SetTransformParent(vampiro.transform, target.transform, "Ubicar vampiro");
        vampiro.transform.SetPositionAndRotation(centro, rotacion);
        float escalaMundo = (0.8f * anchoDibujo) / altoModelo;
        float escalaPadre = Mathf.Abs(target.transform.lossyScale.x);
        vampiro.transform.localScale = Vector3.one * (escalaPadre > 1e-6f ? escalaMundo / escalaPadre : escalaMundo);

        // Animator
        Animator animator = vampiro.GetComponent<Animator>();
        if (animator == null) animator = vampiro.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        // VFX (prefabs en 05_VFX, instanciados como hijos del vampiro)
        Material materialVFX = CrearMaterialVFX();
        GameObject prefabAura = GuardarPrefab(CrearAura(materialVFX, altoModelo), RutaAura);
        GameObject prefabHechizo = GuardarPrefab(CrearHechizo(materialVFX, altoModelo), RutaHechizo);

        var aura = (GameObject)PrefabUtility.InstantiatePrefab(prefabAura, target.scene);
        aura.transform.SetParent(vampiro.transform, false);
        aura.transform.localPosition = Vector3.zero;
        aura.transform.localRotation = Quaternion.identity;
        aura.transform.localScale = Vector3.one;

        var hechizo = (GameObject)PrefabUtility.InstantiatePrefab(prefabHechizo, target.scene);
        hechizo.transform.SetParent(vampiro.transform, false);
        hechizo.transform.localPosition = new Vector3(0f, 0.6f * altoModelo, 0.18f * altoModelo);
        hechizo.transform.localRotation = Quaternion.identity;
        hechizo.transform.localScale = Vector3.one;

        // Conectar con los botones virtuales
        if (control == null) control = Undo.AddComponent<BotonesVirtualesVampiro>(target);
        if (target.GetComponent<DetectorBotonesVirtuales>() == null) Undo.AddComponent<DetectorBotonesVirtuales>(target);

        var so = new SerializedObject(control);
        so.FindProperty("animatorModelo").objectReferenceValue = animator;
        so.FindProperty("vfxHechizo").objectReferenceValue = hechizo.GetComponent<ParticleSystem>();
        so.ApplyModifiedProperties();

        Selection.activeGameObject = vampiro;
        EditorSceneManager.MarkSceneDirty(target.scene);
        EditorSceneManager.SaveScene(target.scene);
    }

    private static float AltoDelModelo(GameObject modelo)
    {
        Renderer[] renderers = modelo.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return 1.8f;
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        return b.size.y > 0.01f ? b.size.y : 1.8f;
    }

    // ------------------------------------------------------------------ VFX

    private static Material CrearMaterialVFX()
    {
        var existente = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialVFX);
        if (existente != null) return existente;

        var baseUrp = AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        Material m = baseUrp != null
            ? new Material(baseUrp)
            : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        m.name = "M_VFX_Vampiro";
        AssetDatabase.CreateAsset(m, RutaMaterialVFX);
        return m;
    }

    private static GameObject GuardarPrefab(GameObject temporal, string ruta)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporal, ruta);
        Object.DestroyImmediate(temporal);
        return prefab;
    }

    /// <summary>Niebla carmesi que sube alrededor del vampiro (siempre activa).</summary>
    private static GameObject CrearAura(Material material, float alto)
    {
        var go = new GameObject("VFX_AuraVampiro");
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = true;
        main.duration = 5f;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f * alto, 0.15f * alto);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f * alto, 0.28f * alto);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.75f, 0.02f, 0.12f, 0.55f), new Color(0.35f, 0f, 0.45f, 0.45f));
        main.maxParticles = 250;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var emision = ps.emission;
        emision.rateOverTime = 40f;

        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 12f;
        forma.radius = 0.28f * alto;
        forma.rotation = new Vector3(-90f, 0f, 0f);

        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(Desvanecer());

        var tamano = ps.sizeOverLifetime;
        tamano.enabled = true;
        tamano.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.3f));

        var ruido = ps.noise;
        ruido.enabled = true;
        ruido.strength = 0.08f * alto;
        ruido.frequency = 0.8f;

        var render = go.GetComponent<ParticleSystemRenderer>();
        render.sharedMaterial = material;
        render.renderMode = ParticleSystemRenderMode.Billboard;
        return go;
    }

    /// <summary>Estallido de chispas magicas al lanzar el hechizo (boton 1).</summary>
    private static GameObject CrearHechizo(Material material, float alto)
    {
        var go = new GameObject("VFX_HechizoVampiro");
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = false;
        main.duration = 1.2f;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f * alto, 1.1f * alto);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f * alto, 0.09f * alto);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.12f, 0.25f, 1f), new Color(0.75f, 0.25f, 1f, 1f));
        main.maxParticles = 400;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var emision = ps.emission;
        emision.rateOverTime = 0f;
        emision.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)90),
            new ParticleSystem.Burst(0.35f, (short)60),
        });

        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Sphere;
        forma.radius = 0.08f * alto;

        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(Desvanecer());

        var tamano = ps.sizeOverLifetime;
        tamano.enabled = true;
        tamano.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var ruido = ps.noise;
        ruido.enabled = true;
        ruido.strength = 0.15f * alto;
        ruido.frequency = 1.2f;

        var render = go.GetComponent<ParticleSystemRenderer>();
        render.sharedMaterial = material;
        render.renderMode = ParticleSystemRenderMode.Billboard;
        return go;
    }

    private static Gradient Desvanecer()
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    // ----------------------------------------------------------- Utilidades

    private static void CrearCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
        AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
    }

    private static void Fallo(string mensaje)
    {
        Debug.LogError("[FantasyAR] " + mensaje);
        EditorUtility.DisplayDialog("FantasyAR", mensaje, "OK");
    }
}
