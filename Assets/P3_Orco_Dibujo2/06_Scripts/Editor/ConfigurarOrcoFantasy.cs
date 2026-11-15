using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vuforia;

/// <summary>
/// FantasyAR - Integrante 3 - Configura automaticamente el orco del Dibujo 2:
///  1. Modelo (externo, riggeado en Mixamo): Rig Humanoid, texturas y materiales URP.
///  2. Animaciones Idle / Spell / Jump de Mixamo: Humanoid con el avatar del orco, sin desplazamiento.
///  3. Animator Controller AC_Orco: ciclo automatico Idle -> Spell -> Jump -> Idle con transiciones suaves
///     (el Dibujo 2 no tiene botones virtuales, asi las 3 animaciones se ven solas en el video).
///  4. VFX: aura de fuego verde (siempre activa) y estallido al lanzar el hechizo.
///  5. Escena: coloca el orco sobre el Dibujo 2 y le agrega CicloAnimacionesOrco.
///
/// Los archivos se detectan solos, no hace falta cambiar nombres en este script:
///  - 02_Modelo3D_Texturas: el .fbx del orco (y sus texturas en Texturas/ si vienen aparte).
///  - 03_Animaciones: tres .fbx cuyo nombre contenga "Idle", "Spell" y "Jump"
///    (ej. Orco_Idle_BreathingIdle.fbx, Orco_Spell_MagicAttack.fbx, Orco_Jump_Jumping.fbx).
///
/// Uso: seleccionar el ImageTarget del Dibujo 2 y ir al menu FantasyAR > Configurar orco (Dibujo 2).
/// </summary>
public static class ConfigurarOrcoFantasy
{
    private const string Raiz = "Assets/P3_Orco_Dibujo2";
    private const string CarpetaModelo = Raiz + "/02_Modelo3D_Texturas";
    private const string CarpetaTexturas = CarpetaModelo + "/Texturas";
    private const string CarpetaMateriales = CarpetaModelo + "/Materiales";
    private const string CarpetaAnimaciones = Raiz + "/03_Animaciones";
    private const string RutaController = Raiz + "/04_Animator/AC_Orco.controller";
    private const string CarpetaVFX = Raiz + "/05_VFX";
    private const string RutaMaterialVFX = CarpetaVFX + "/M_VFX_Orco.mat";
    private const string RutaAura = CarpetaVFX + "/VFX_AuraOrco.prefab";
    private const string RutaHechizo = CarpetaVFX + "/VFX_HechizoOrco.prefab";
    private const string RutaSalto = CarpetaVFX + "/VFX_SaltoOrco.prefab";
    private const string NombreEnEscena = "Orco";

    /// <summary>Segundos minimos que el orco se queda en Idle antes de lanzar el hechizo.</summary>
    private const float SegundosIdle = 4f;

    [MenuItem("FantasyAR/Configurar orco (Dibujo 2)")]
    public static void ConfigurarTodo()
    {
        try
        {
            Debug.Log("[FantasyAR] Configurando orco del Dibujo 2...");

            GameObject target = BuscarImageTarget();
            if (target == null) return;

            string rutaModelo = BuscarModelo();
            if (rutaModelo == null) return;

            Avatar avatar = ConfigurarModelo(rutaModelo);
            if (avatar == null) return;

            AnimationClip idle = ConfigurarAnimacion(BuscarAnimacion("idle"), "Idle", avatar, true);
            AnimationClip spell = ConfigurarAnimacion(BuscarAnimacion("spell"), "Spell", avatar, false);
            AnimationClip jump = ConfigurarAnimacion(BuscarAnimacion("jump"), "Jump", avatar, false);
            if (idle == null || spell == null || jump == null)
            {
                Fallo("Faltan animaciones en " + CarpetaAnimaciones + ".\n\nDeben ser 3 .fbx de Mixamo cuyo nombre " +
                      "contenga Idle, Spell y Jump (ej. Orco_Idle_BreathingIdle.fbx).");
                return;
            }

            AnimatorController controller = CrearController(idle, spell, jump);
            ArmarEscena(target, rutaModelo, controller);

            AssetDatabase.SaveAssets();
            Debug.Log("[FantasyAR] Orco configurado: modelo, animaciones, Animator en ciclo y VFX.");
            EditorUtility.DisplayDialog("FantasyAR",
                "Listo: el orco ya esta sobre el Dibujo 2 con sus 3 animaciones en ciclo " +
                "(Idle -> Spell -> Jump), el Animator y los VFX.\n\n" +
                "Dale Play. En el Editor puedes forzar animaciones con las teclas 4, 5 y 6.", "OK");
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            Fallo("Ocurrio un error al configurar el orco. Revisa la consola.");
        }
    }

    // ------------------------------------------------------------ Busquedas

    private static GameObject BuscarImageTarget()
    {
        // 1) El objeto seleccionado, si es un ImageTarget
        GameObject sel = Selection.activeGameObject;
        if (sel != null)
        {
            var itb = sel.GetComponentInParent<ImageTargetBehaviour>();
            if (itb != null) return itb.gameObject;
        }

        // 2) Un ImageTarget cuyo nombre de target contenga "dibujo2" u "orco"
        var todos = Object.FindObjectsByType<ImageTargetBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (ImageTargetBehaviour itb in todos)
        {
            string nombre = (itb.TargetName ?? "").ToLowerInvariant();
            if (nombre.Contains("dibujo2") || nombre.Contains("orco"))
                return itb.gameObject;
        }

        Fallo("No encontre el ImageTarget del Dibujo 2.\n\n" +
              "1. GameObject > Vuforia Engine > Image Target.\n" +
              "2. En el Inspector elige la base de datos y el target del Dibujo 2.\n" +
              "3. Selecciona ese ImageTarget y vuelve a usar este menu.");
        return null;
    }

    private static string BuscarModelo()
    {
        string[] fbx = AssetDatabase.FindAssets("t:Model", new[] { CarpetaModelo })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(r => r.ToLowerInvariant().EndsWith(".fbx"))
            .ToArray();

        if (fbx.Length == 0)
        {
            Fallo("No hay ningun .fbx en " + CarpetaModelo + ".\n\nCopia ahi el orco descargado de Mixamo " +
                  "(T-Pose, With Skin).");
            return null;
        }
        if (fbx.Length > 1)
            Debug.LogWarning("[FantasyAR] Hay varios .fbx en " + CarpetaModelo + "; uso " + fbx[0]);
        return fbx[0];
    }

    private static string BuscarAnimacion(string palabra)
    {
        return AssetDatabase.FindAssets("t:Model", new[] { CarpetaAnimaciones })
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(r => r.ToLowerInvariant().EndsWith(".fbx") &&
                                 Path.GetFileNameWithoutExtension(r).ToLowerInvariant().Contains(palabra));
    }

    // ---------------------------------------------------------------- Modelo

    private static Avatar ConfigurarModelo(string rutaModelo)
    {
        var importador = (ModelImporter)AssetImporter.GetAtPath(rutaModelo);
        importador.animationType = ModelImporterAnimationType.Human;
        importador.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importador.importAnimation = false;
        importador.SaveAndReimport();

        CrearCarpeta(CarpetaTexturas);
        CrearCarpeta(CarpetaMateriales);

        importador = (ModelImporter)AssetImporter.GetAtPath(rutaModelo);
        if (importador.ExtractTextures(CarpetaTexturas))
            AssetDatabase.Refresh();
        MarcarMapasNormales();
        ExtraerMateriales(rutaModelo);
        ConvertirMaterialesAURP();

        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(rutaModelo).OfType<Avatar>().FirstOrDefault();
        if (avatar == null || !avatar.isHuman)
        {
            Fallo("El avatar del orco no es Humanoid valido.\n\nRevisa que el .fbx sea el que descargaste " +
                  "de Mixamo despues del auto-rig (With Skin), no el modelo original.");
            return null;
        }
        return avatar;
    }

    private static void MarcarMapasNormales()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { CarpetaTexturas }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            string nombre = Path.GetFileName(ruta).ToLowerInvariant();
            if (!nombre.Contains("normal") && !nombre.Contains("_nrm") && !nombre.Contains("_n.")) continue;
            var ti = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
        }
    }

    private static void ExtraerMateriales(string rutaModelo)
    {
        Material[] materiales = AssetDatabase.LoadAllAssetsAtPath(rutaModelo).OfType<Material>().ToArray();
        bool extraido = false;
        foreach (Material m in materiales)
        {
            string nombre = string.Concat(m.name.Split(Path.GetInvalidFileNameChars()));
            string destino = AssetDatabase.GenerateUniqueAssetPath(CarpetaMateriales + "/" + nombre + ".mat");
            string error = AssetDatabase.ExtractAsset(m, destino);
            if (string.IsNullOrEmpty(error))
            {
                extraido = true;
                AssetDatabase.WriteImportSettingsIfDirty(rutaModelo);
            }
        }
        if (extraido)
            AssetDatabase.ImportAsset(rutaModelo, ImportAssetOptions.ForceUpdate);
    }

    private static void ConvertirMaterialesAURP()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) return;

        string[] materialesGuid = AssetDatabase.FindAssets("t:Material", new[] { CarpetaMateriales });
        foreach (string guid in materialesGuid)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m == null) continue;

            // Texturas sueltas de la carpeta Texturas (Mixamo no las incrusta).
            // Con varios materiales (ej. Body y Accesories) se busca la textura cuyo nombre
            // comparta una palabra con el nombre del material.
            string clave = materialesGuid.Length > 1 ? ClaveMaterial(m.name) : null;
            Texture2D difusa = BuscarTextura(clave, "basecolor", "base_color", "albedo", "diff", "color", "col");
            Texture2D normalSuelta = BuscarTextura(clave, "normal", "_nrm");
            Texture2D ao = BuscarTextura(clave, "_ao", "occlusion");

            if (!m.shader.name.StartsWith("Universal Render Pipeline"))
            {
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
            }

            // Si quedo sin textura, asignar la difusa suelta de la carpeta Texturas
            if (m.GetTexture("_BaseMap") == null && difusa != null)
            {
                m.SetTexture("_BaseMap", difusa);
                m.SetColor("_BaseColor", Color.white);
                Debug.Log("[FantasyAR] Textura " + difusa.name + " asignada al material " + m.name);
            }
            if (m.GetTexture("_BumpMap") == null && normalSuelta != null)
            {
                m.SetTexture("_BumpMap", normalSuelta);
                m.EnableKeyword("_NORMALMAP");
            }
            if (m.HasProperty("_OcclusionMap") && m.GetTexture("_OcclusionMap") == null && ao != null)
            {
                m.SetTexture("_OcclusionMap", ao);
                m.EnableKeyword("_OCCLUSIONMAP");
            }
            m.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(m);
        }
    }

    /// <summary>Palabra distintiva del material, ej. "Orc_Body_MAT" -> "body".</summary>
    private static string ClaveMaterial(string nombreMaterial)
    {
        string[] comunes = { "orc", "orco", "mat", "material", "mtl", "default", "001", "002" };
        return nombreMaterial.ToLowerInvariant()
            .Split(new[] { '_', ' ', '.', '-', ':' }, System.StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(p => p.Length >= 3 && !comunes.Contains(p));
    }

    /// <summary>Busca en Texturas/ una textura que contenga 'clave' (si hay) y alguna de las palabras.</summary>
    private static Texture2D BuscarTextura(string clave, params string[] palabras)
    {
        if (!AssetDatabase.IsValidFolder(CarpetaTexturas)) return null;
        string[] rutas = AssetDatabase.FindAssets("t:Texture2D", new[] { CarpetaTexturas })
            .Select(AssetDatabase.GUIDToAssetPath).ToArray();

        string[] candidatas = rutas;
        if (!string.IsNullOrEmpty(clave))
        {
            string k = clave.Length > 5 ? clave.Substring(0, 5) : clave; // "accesories" / "accessories" -> "acces"
            string[] filtradas = rutas.Where(x => Path.GetFileNameWithoutExtension(x).ToLowerInvariant().Contains(k)).ToArray();
            if (filtradas.Length > 0) candidatas = filtradas;
        }
        else
        {
            // Un solo material: preferir las texturas del cuerpo
            string[] cuerpo = rutas.Where(x => Path.GetFileNameWithoutExtension(x).ToLowerInvariant().Contains("body")).ToArray();
            if (cuerpo.Length > 0) candidatas = cuerpo;
        }

        foreach (string palabra in palabras)
        {
            string r = candidatas.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x).ToLowerInvariant().Contains(palabra));
            if (r != null) return AssetDatabase.LoadAssetAtPath<Texture2D>(r);
        }
        return null;
    }

    // ---------------------------------------------------------- Animaciones

    private static AnimationClip ConfigurarAnimacion(string ruta, string nombre, Avatar avatar, bool enBucle)
    {
        if (ruta == null)
        {
            Debug.LogError("[FantasyAR] No hay un .fbx con '" + nombre + "' en el nombre dentro de " + CarpetaAnimaciones);
            return null;
        }

        var importador = (ModelImporter)AssetImporter.GetAtPath(ruta);
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
        clip.loopTime = enBucle;
        // Bake Into Pose: el orco no se sale del dibujo ni gira (el salto vertical se conserva)
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

    /// <summary>
    /// Ciclo automatico Idle -> Spell -> Jump -> Idle (por Exit Time, con fundido de 0.25 s)
    /// y ademas triggers Idle/Spell/Jump por si se quiere forzar una animacion.
    /// </summary>
    private static AnimatorController CrearController(AnimationClip idle, AnimationClip spell, AnimationClip jump)
    {
        CrearCarpeta(Raiz + "/04_Animator");
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(RutaController) != null)
            AssetDatabase.DeleteAsset(RutaController);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(RutaController);
        controller.AddParameter("Idle", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Spell", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState estadoIdle = sm.AddState("Idle", new Vector3(300f, 0f, 0f));
        estadoIdle.motion = idle;
        AnimatorState estadoSpell = sm.AddState("Spell", new Vector3(560f, 120f, 0f));
        estadoSpell.motion = spell;
        AnimatorState estadoJump = sm.AddState("Jump", new Vector3(300f, 240f, 0f));
        estadoJump.motion = jump;
        sm.defaultState = estadoIdle;

        // Idle se repite hasta cumplir SegundosIdle y luego pasa a Spell
        float vueltasIdle = idle.length > 0.01f ? Mathf.Max(1f, Mathf.Ceil(SegundosIdle / idle.length)) : 1f;
        PorTiempo(estadoIdle, estadoSpell, vueltasIdle);
        PorTiempo(estadoSpell, estadoJump, 0.92f);
        PorTiempo(estadoJump, estadoIdle, 0.92f);

        // Forzar una animacion con trigger (teclas 4/5/6 en el Editor)
        PorTrigger(sm, estadoIdle, "Idle");
        PorTrigger(sm, estadoSpell, "Spell");
        PorTrigger(sm, estadoJump, "Jump");

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static void PorTiempo(AnimatorState desde, AnimatorState hacia, float exitTime)
    {
        AnimatorStateTransition t = desde.AddTransition(hacia);
        t.hasExitTime = true;
        t.exitTime = exitTime;
        t.hasFixedDuration = true;
        t.duration = 0.25f;
    }

    private static void PorTrigger(AnimatorStateMachine sm, AnimatorState destino, string trigger)
    {
        AnimatorStateTransition t = sm.AddAnyStateTransition(destino);
        t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        t.hasExitTime = false;
        t.duration = 0.25f;
        t.canTransitionToSelf = false;
    }

    // --------------------------------------------------------------- Escena

    private static void ArmarEscena(GameObject target, string rutaModelo, AnimatorController controller)
    {
        if (Object.FindAnyObjectByType<VuforiaBehaviour>(FindObjectsInactive.Include) == null)
        {
            if (!EditorApplication.ExecuteMenuItem("GameObject/Vuforia Engine/AR Camera"))
                Debug.LogWarning("[FantasyAR] Agrega la AR Camera: GameObject > Vuforia Engine > AR Camera.");
        }

        Transform anterior = target.transform.Find(NombreEnEscena);
        if (anterior != null) Undo.DestroyObjectImmediate(anterior.gameObject);

        var prefabModelo = AssetDatabase.LoadAssetAtPath<GameObject>(rutaModelo);
        var orco = (GameObject)PrefabUtility.InstantiatePrefab(prefabModelo, target.scene);
        orco.name = NombreEnEscena;
        Undo.RegisterCreatedObjectUndo(orco, "Crear orco");
        orco.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        orco.transform.localScale = Vector3.one;
        float altoModelo = AltoDelModelo(orco);

        // Centro del dibujo, de pie y mirando hacia el borde de abajo (hacia la camara)
        Vector3 centro = target.transform.position;
        Quaternion rotacion = target.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
        float anchoDibujo = 0.15f;
        if (CrearBotonesVirtualesFantasy.BuscarEsquinas(target, out Vector3 supIzq, out Vector3 supDer, out Vector3 infIzq))
        {
            Vector3 ejeAncho = supDer - supIzq;
            Vector3 ejeAlto = infIzq - supIzq;
            Vector3 normal = Vector3.Cross(-ejeAlto, ejeAncho).normalized;
            anchoDibujo = ejeAncho.magnitude;
            centro = supIzq + ejeAncho * 0.5f + ejeAlto * 0.5f;
            rotacion = Quaternion.LookRotation(ejeAlto.normalized, normal);
        }
        else
        {
            Debug.LogWarning("[FantasyAR] No pude leer el tamano del dibujo; revisa la escala del orco a mano.");
        }

        Undo.SetTransformParent(orco.transform, target.transform, "Ubicar orco");
        orco.transform.SetPositionAndRotation(centro, rotacion);
        float escalaMundo = (0.8f * anchoDibujo) / altoModelo;
        float escalaPadre = Mathf.Abs(target.transform.lossyScale.x);
        orco.transform.localScale = Vector3.one * (escalaPadre > 1e-6f ? escalaMundo / escalaPadre : escalaMundo);

        Animator animator = orco.GetComponent<Animator>();
        if (animator == null) animator = orco.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        // VFX
        CrearCarpeta(CarpetaVFX);
        Material materialVFX = CrearMaterialVFX();
        GameObject prefabAura = GuardarPrefab(CrearAura(materialVFX, altoModelo), RutaAura);
        GameObject prefabHechizo = GuardarPrefab(CrearHechizo(materialVFX, altoModelo), RutaHechizo);

        var aura = (GameObject)PrefabUtility.InstantiatePrefab(prefabAura, target.scene);
        aura.transform.SetParent(orco.transform, false);
        aura.transform.localPosition = Vector3.zero;
        aura.transform.localRotation = Quaternion.identity;
        aura.transform.localScale = Vector3.one;

        var hechizo = (GameObject)PrefabUtility.InstantiatePrefab(prefabHechizo, target.scene);
        hechizo.transform.SetParent(orco.transform, false);
        hechizo.transform.localPosition = new Vector3(0f, 0.65f * altoModelo, 0.25f * altoModelo);
        hechizo.transform.localRotation = Quaternion.identity;
        hechizo.transform.localScale = Vector3.one;

        GameObject prefabSalto = GuardarPrefab(CrearSalto(materialVFX, altoModelo), RutaSalto);
        var salto = (GameObject)PrefabUtility.InstantiatePrefab(prefabSalto, target.scene);
        salto.transform.SetParent(orco.transform, false);
        salto.transform.localPosition = Vector3.zero;
        salto.transform.localRotation = Quaternion.identity;
        salto.transform.localScale = Vector3.one;

        Light luz = CrearLuz(orco.transform, altoModelo);

        CicloAnimacionesOrco ciclo = target.GetComponent<CicloAnimacionesOrco>();
        if (ciclo == null) ciclo = Undo.AddComponent<CicloAnimacionesOrco>(target);
        var so = new SerializedObject(ciclo);
        so.FindProperty("animatorModelo").objectReferenceValue = animator;
        so.FindProperty("vfxHechizo").objectReferenceValue = hechizo.GetComponent<ParticleSystem>();
        so.FindProperty("luzHechizo").objectReferenceValue = luz;
        so.FindProperty("vfxAura").objectReferenceValue = aura.GetComponent<ParticleSystem>();
        so.FindProperty("vfxDespegue").objectReferenceValue = salto.transform.Find("Despegue").GetComponent<ParticleSystem>();
        so.FindProperty("vfxEstela").objectReferenceValue = salto.transform.Find("Estela").GetComponent<ParticleSystem>();
        so.FindProperty("vfxAterrizaje").objectReferenceValue = salto.transform.Find("Aterrizaje").GetComponent<ParticleSystem>();
        // URP atenua las luces con la distancia al cuadrado: ajustar la intensidad a la escala de la escena
        float d = 0.6f * altoModelo * Mathf.Abs(orco.transform.lossyScale.x);
        so.FindProperty("intensidadBase").floatValue = 1.5f * d * d;
        so.FindProperty("intensidadDestello").floatValue = 10f * d * d;
        so.ApplyModifiedProperties();

        Selection.activeGameObject = orco;
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

    /// <summary>Material de particulas URP en modo aditivo (las particulas brillan y se notan sobre la camara).</summary>
    private static Material CrearMaterialVFX()
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialVFX);
        if (m == null)
        {
            var baseUrp = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
            m = baseUrp != null
                ? new Material(baseUrp)
                : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.name = "M_VFX_Orco";
            AssetDatabase.CreateAsset(m, RutaMaterialVFX);
        }

        // Transparente + aditivo
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>Luz verde que ilumina al orco (CicloAnimacionesOrco la hace destellar con el hechizo).</summary>
    private static Light CrearLuz(Transform orco, float alto)
    {
        var go = new GameObject("Luz_Orco");
        go.transform.SetParent(orco, false);
        go.transform.localPosition = new Vector3(0f, 0.6f * alto, 0.5f * alto);
        var luz = go.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = new Color(0.35f, 1f, 0.2f);
        luz.range = 2.5f * alto * orco.lossyScale.x;
        float d = 0.6f * alto * Mathf.Abs(orco.lossyScale.x);
        luz.intensity = 1.5f * d * d;
        luz.shadows = LightShadows.None;
        return luz;
    }

    private static GameObject GuardarPrefab(GameObject temporal, string ruta)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporal, ruta);
        Object.DestroyImmediate(temporal);
        return prefab;
    }

    /// <summary>
    /// Aura siempre activa: llamas verdes grandes que suben desde los pies
    /// + una espiral de chispas que gira alrededor del cuerpo.
    /// </summary>
    private static GameObject CrearAura(Material material, float alto)
    {
        var go = new GameObject("VFX_AuraOrco");

        // --- Llamas de los pies
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.duration = 5f;
        main.playOnAwake = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f * alto, 0.7f * alto);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f * alto, 0.38f * alto);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.45f, 1f, 0.15f, 1f), new Color(0.2f, 1f, 0.05f, 0.9f));
        main.maxParticles = 600;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var emision = ps.emission;
        emision.rateOverTime = 140f;

        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Circle;
        forma.radius = 0.38f * alto;
        forma.rotation = new Vector3(-90f, 0f, 0f);

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.85f, 1f, 0.5f), 0f),
                new GradientColorKey(new Color(0.25f, 1f, 0.1f), 0.4f),
                new GradientColorKey(new Color(0.05f, 0.5f, 0.02f), 1f),
            },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
        color.color = new ParticleSystem.MinMaxGradient(g);

        var tamano = ps.sizeOverLifetime;
        tamano.enabled = true;
        tamano.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        var ruido = ps.noise;
        ruido.enabled = true;
        ruido.strength = 0.15f * alto;
        ruido.frequency = 1.2f;

        var render = go.GetComponent<ParticleSystemRenderer>();
        render.sharedMaterial = material;
        render.renderMode = ParticleSystemRenderMode.Billboard;

        // --- Espiral de chispas alrededor del cuerpo
        var hijo = new GameObject("Espiral");
        hijo.transform.SetParent(go.transform, false);
        var esp = hijo.AddComponent<ParticleSystem>();
        var m2 = esp.main;
        m2.loop = true;
        m2.duration = 5f;
        m2.playOnAwake = true;
        m2.prewarm = true;
        m2.startLifetime = 2.2f;
        m2.startSpeed = 0f;
        m2.startSize = new ParticleSystem.MinMaxCurve(0.05f * alto, 0.1f * alto);
        m2.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.8f, 1f, 0.3f, 1f), new Color(1f, 0.75f, 0.1f, 1f));
        m2.maxParticles = 400;
        m2.simulationSpace = ParticleSystemSimulationSpace.Local;
        m2.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var e2 = esp.emission;
        e2.rateOverTime = 70f;

        var f2 = esp.shape;
        f2.shapeType = ParticleSystemShapeType.Circle;
        f2.radius = 0.5f * alto;
        f2.radiusThickness = 0f;
        f2.rotation = new Vector3(-90f, 0f, 0f);

        var vel = esp.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.orbitalX = 0f;
        vel.orbitalY = 3f;                       // gira alrededor del orco (eje vertical)
        vel.orbitalZ = 0f;
        vel.x = 0f;
        vel.y = new ParticleSystem.MinMaxCurve(0.45f * alto); // sube
        vel.z = 0f;

        var c2 = esp.colorOverLifetime;
        c2.enabled = true;
        var g2 = new Gradient();
        g2.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        c2.color = new ParticleSystem.MinMaxGradient(g2);

        var trail = esp.trails;
        trail.enabled = true;
        trail.lifetime = 0.25f;
        trail.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        trail.inheritParticleColor = true;

        var r2 = hijo.GetComponent<ParticleSystemRenderer>();
        r2.sharedMaterial = material;
        r2.trailMaterial = material;
        r2.renderMode = ParticleSystemRenderMode.Billboard;
        return go;
    }

    /// <summary>
    /// Hechizo: gran estallido de chispas verdes y naranjas, un destello central
    /// y una onda expansiva en el suelo. Se dispara al entrar en la animacion Spell.
    /// </summary>
    private static GameObject CrearHechizo(Material material, float alto)
    {
        var go = new GameObject("VFX_HechizoOrco");
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = false;
        main.duration = 1.5f;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.0f * alto, 2.4f * alto);
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f * alto, 0.18f * alto);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.5f, 1f, 0.1f, 1f), new Color(1f, 0.6f, 0.05f, 1f));
        main.gravityModifier = 0.3f;
        main.maxParticles = 1000;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var emision = ps.emission;
        emision.rateOverTime = 0f;
        emision.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)300),
            new ParticleSystem.Burst(0.3f, (short)200),
        });

        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Sphere;
        forma.radius = 0.1f * alto;

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
        color.color = new ParticleSystem.MinMaxGradient(g);

        var tamano = ps.sizeOverLifetime;
        tamano.enabled = true;
        tamano.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var trail = ps.trails;
        trail.enabled = true;
        trail.lifetime = 0.2f;
        trail.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        trail.inheritParticleColor = true;

        var render = go.GetComponent<ParticleSystemRenderer>();
        render.sharedMaterial = material;
        render.trailMaterial = material;
        render.renderMode = ParticleSystemRenderMode.Billboard;

        // --- Destello central grande
        var flash = new GameObject("Destello");
        flash.transform.SetParent(go.transform, false);
        var pf = flash.AddComponent<ParticleSystem>();
        var mf = pf.main;
        mf.loop = false;
        mf.duration = 0.5f;
        mf.playOnAwake = false;
        mf.startLifetime = 0.45f;
        mf.startSpeed = 0f;
        mf.startSize = 0.9f * alto;
        mf.startColor = new Color(0.6f, 1f, 0.3f, 1f);
        mf.simulationSpace = ParticleSystemSimulationSpace.Local;
        mf.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var ef = pf.emission;
        ef.rateOverTime = 0f;
        ef.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)3) });
        var sf = pf.shape;
        sf.enabled = false;
        var tf = pf.sizeOverLifetime;
        tf.enabled = true;
        tf.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1.4f));
        var cf = pf.colorOverLifetime;
        cf.enabled = true;
        var gf = new Gradient();
        gf.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        cf.color = new ParticleSystem.MinMaxGradient(gf);
        var rf = flash.GetComponent<ParticleSystemRenderer>();
        rf.sharedMaterial = material;
        rf.renderMode = ParticleSystemRenderMode.Billboard;

        // --- Onda expansiva en el suelo (anillo de particulas que se abre)
        var onda = new GameObject("OndaExpansiva");
        onda.transform.SetParent(go.transform, false);
        onda.transform.localPosition = new Vector3(0f, -0.62f * alto, -0.25f * alto); // a los pies
        var po = onda.AddComponent<ParticleSystem>();
        var mo = po.main;
        mo.loop = false;
        mo.duration = 1f;
        mo.playOnAwake = false;
        mo.startLifetime = 0.8f;
        mo.startSpeed = 1.6f * alto;
        mo.startSize = new ParticleSystem.MinMaxCurve(0.1f * alto, 0.18f * alto);
        mo.startColor = new Color(0.4f, 1f, 0.15f, 1f);
        mo.simulationSpace = ParticleSystemSimulationSpace.Local;
        mo.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var eo = po.emission;
        eo.rateOverTime = 0f;
        eo.SetBursts(new[] { new ParticleSystem.Burst(0.1f, (short)160) });
        var so = po.shape;
        so.shapeType = ParticleSystemShapeType.Circle;
        so.radius = 0.05f * alto;
        so.radiusThickness = 0f;
        so.rotation = new Vector3(-90f, 0f, 0f);
        var co = po.colorOverLifetime;
        co.enabled = true;
        co.color = new ParticleSystem.MinMaxGradient(gf);
        var ro = onda.GetComponent<ParticleSystemRenderer>();
        ro.sharedMaterial = material;
        ro.renderMode = ParticleSystemRenderMode.Billboard;

        return go;
    }

    /// <summary>Sistema de particulas basico (local, escala por jerarquia, sin emision continua).</summary>
    private static ParticleSystem NuevoSistema(Transform padre, string nombre, Material material, Vector3 posLocal)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = posLocal;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var em = ps.emission;
        em.rateOverTime = 0f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = material;
        r.trailMaterial = material;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        return ps;
    }

    private static Gradient Apagar()
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    /// <summary>Anillo de particulas que se abre sobre el suelo (onda expansiva).</summary>
    private static void Anillo(ParticleSystem ps, float alto, float velocidad, short cantidad, float tamano, Color color, float vida)
    {
        var main = ps.main;
        main.duration = vida;
        main.startLifetime = vida;
        main.startSpeed = velocidad * alto;
        main.startSize = new ParticleSystem.MinMaxCurve(tamano * 0.7f * alto, tamano * alto);
        main.startColor = color;
        main.maxParticles = cantidad + 10;
        var em = ps.emission;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, cantidad) });
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = 0.08f * alto;
        sh.radiusThickness = 0f;
        sh.rotation = new Vector3(-90f, 0f, 0f);
        var lim = ps.limitVelocityOverLifetime;
        lim.enabled = true;
        lim.limit = 0.05f * alto;
        lim.dampen = 0.08f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(Apagar());
    }

    /// <summary>
    /// VFX del salto: Despegue (anillo + chispas hacia arriba), Estela (sigue la cadera en el aire)
    /// y Aterrizaje (onda grande + escombros + destello). Los dispara CicloAnimacionesOrco.
    /// </summary>
    private static GameObject CrearSalto(Material material, float alto)
    {
        var raiz = new GameObject("VFX_SaltoOrco");
        Vector3 pie = new Vector3(0f, 0.02f * alto, 0f);

        // --- Despegue
        ParticleSystem despegue = NuevoSistema(raiz.transform, "Despegue", material, pie);
        Anillo(despegue, alto, 1.4f, 90, 0.12f, new Color(0.75f, 1f, 0.35f, 1f), 0.6f);
        ParticleSystem chispas = NuevoSistema(despegue.transform, "ChispasArriba", material, Vector3.zero);
        {
            var m = chispas.main;
            m.duration = 0.6f;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.8f * alto, 1.6f * alto);
            m.startSize = new ParticleSystem.MinMaxCurve(0.04f * alto, 0.09f * alto);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f, 1f), new Color(0.5f, 1f, 0.2f, 1f));
            m.maxParticles = 200;
            var em = chispas.emission;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)120) });
            var sh = chispas.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 25f;
            sh.radius = 0.25f * alto;
            sh.rotation = new Vector3(-90f, 0f, 0f);
            var col = chispas.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Apagar());
            var tr = chispas.trails;
            tr.enabled = true;
            tr.lifetime = 0.15f;
            tr.inheritParticleColor = true;
        }

        // --- Estela en el aire (CicloAnimacionesOrco la mueve a la cadera y activa la emision)
        ParticleSystem estela = NuevoSistema(raiz.transform, "Estela", material, new Vector3(0f, 0.5f * alto, 0f));
        {
            var m = estela.main;
            m.loop = true;
            m.playOnAwake = true;
            m.duration = 5f;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            m.startSpeed = 0f;
            m.startSize = new ParticleSystem.MinMaxCurve(0.15f * alto, 0.28f * alto);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.4f, 1f, 0.15f, 0.9f), new Color(0.9f, 1f, 0.5f, 0.9f));
            m.maxParticles = 500;
            var em = estela.emission;
            em.enabled = false;
            em.rateOverTime = 160f;
            var sh = estela.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.12f * alto;
            var col = estela.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Apagar());
            var sz = estela.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));
        }

        // --- Aterrizaje
        ParticleSystem aterrizaje = NuevoSistema(raiz.transform, "Aterrizaje", material, pie);
        Anillo(aterrizaje, alto, 2.6f, 180, 0.18f, new Color(0.45f, 1f, 0.15f, 1f), 0.9f);
        ParticleSystem escombros = NuevoSistema(aterrizaje.transform, "Escombros", material, Vector3.zero);
        {
            var m = escombros.main;
            m.duration = 1f;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.9f * alto, 2.0f * alto);
            m.startSize = new ParticleSystem.MinMaxCurve(0.05f * alto, 0.12f * alto);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.7f, 0.2f, 1f), new Color(0.6f, 1f, 0.25f, 1f));
            m.gravityModifier = 0f;
            m.maxParticles = 300;
            var em = escombros.emission;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)150) });
            var sh = escombros.shape;
            sh.shapeType = ParticleSystemShapeType.Hemisphere;
            sh.radius = 0.2f * alto;
            sh.rotation = new Vector3(-90f, 0f, 0f);
            var fuerza = escombros.forceOverLifetime;
            fuerza.enabled = true;
            fuerza.space = ParticleSystemSimulationSpace.Local;
            fuerza.x = 0f;
            fuerza.z = 0f;
            fuerza.y = -3.5f * alto;                 // caen de vuelta al suelo
            var col = escombros.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Apagar());
            var tr = escombros.trails;
            tr.enabled = true;
            tr.lifetime = 0.12f;
            tr.inheritParticleColor = true;
        }
        ParticleSystem golpe = NuevoSistema(aterrizaje.transform, "Destello", material, new Vector3(0f, 0.1f * alto, 0f));
        {
            var m = golpe.main;
            m.duration = 0.4f;
            m.startLifetime = 0.35f;
            m.startSpeed = 0f;
            m.startSize = 1.3f * alto;
            m.startColor = new Color(0.55f, 1f, 0.3f, 0.9f);
            var em = golpe.emission;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)2) });
            var sh = golpe.shape;
            sh.enabled = false;
            var sz = golpe.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.2f));
            var col = golpe.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Apagar());
        }

        return raiz;
    }

    // ----------------------------------------------------------- Utilidades

    private static void CrearCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(padre)) CrearCarpeta(padre);
        AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
    }

    private static void Fallo(string mensaje)
    {
        Debug.LogError("[FantasyAR] " + mensaje);
        EditorUtility.DisplayDialog("FantasyAR", mensaje, "OK");
    }
}
