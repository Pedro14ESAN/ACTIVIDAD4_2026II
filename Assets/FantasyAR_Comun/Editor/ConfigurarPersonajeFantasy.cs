using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vuforia;

/// <summary>
/// FantasyAR - Configura cualquier personaje humanoide de Mixamo sobre un Image Target o un
/// Multi Target (Cuboid). Sirve para los 4 integrantes.
/// Menu: FantasyAR > Configurar personaje Mixamo...
///  1. Modelo Humanoid, texturas y materiales URP (con texturas asignadas).
///  2. Animaciones Idle / Spell / Jump en bucle y sin desplazamiento.
///  3. Animator Controller con transiciones suaves.
///  4. VFX: aura del color elegido (siempre activa) y estallido de hechizo.
///  5. Coloca el personaje: sobre el dibujo (Image Target) o cubriendo el cubo (Multi Target).
/// Si existe Library/FantasyAR_configurar_personaje.json se ejecuta solo al abrir Unity.
/// </summary>
[InitializeOnLoad]
public class ConfigurarPersonajeFantasy : EditorWindow
{
    [System.Serializable]
    public class Parametros
    {
        public string objetivo;          // nombre del Image Target o Multi Target en la escena
        public string modelo;            // FBX del personaje (With Skin)
        public string idle, spell, jump; // FBX de las 3 animaciones
        public string carpeta;           // carpeta del integrante, ej. Assets/P1_Cuboid
        public string nombre = "Personaje";
        public Color colorVFX = new Color(0.25f, 0.55f, 1f, 0.55f);
        public bool cubrirCubo;          // true para Multi Target
        public Vector3 correccion;       // giro del contenido (grados) si el cubo se subio girado
        public string datasetMulti;      // si no existe el target: crear Multi Target con esta base (ej. Vuforia/CubicTarget2.xml)
        public string trackableMulti;    // nombre del cuboid en la base (ej. cubo2)
    }

    private const string Bandera = "Library/FantasyAR_configurar_personaje.json";
    private static double inicio;

    // Campos de la ventana
    private GameObject objetivo;
    private GameObject modelo, idle, spell, jump;
    private string nombre = "Personaje";
    private Color colorVFX = new Color(0.25f, 0.55f, 1f, 0.55f);
    private bool cubrirCubo;
    private Vector3 correccion;

    static ConfigurarPersonajeFantasy()
    {
        if (!File.Exists(Bandera)) return;
        inicio = EditorApplication.timeSinceStartup;
        EditorApplication.update += EsperarYEjecutar;
    }

    private static void EsperarYEjecutar()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.timeSinceStartup - inicio < 2.0) return;
        EditorApplication.update -= EsperarYEjecutar;
        if (!File.Exists(Bandera)) return;
        string json = File.ReadAllText(Bandera);
        File.Delete(Bandera);
        Ejecutar(JsonUtility.FromJson<Parametros>(json));
    }

    [MenuItem("FantasyAR/Configurar personaje Mixamo...")]
    private static void Abrir()
    {
        GetWindow<ConfigurarPersonajeFantasy>("Personaje FantasyAR");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Arrastra el Image Target o Multi Target de la escena, el FBX del personaje (With Skin) " +
            "y los 3 FBX de animacion de Mixamo. Todo se guarda en la carpeta del personaje.", MessageType.Info);
        objetivo = (GameObject)EditorGUILayout.ObjectField("Target de la escena", objetivo, typeof(GameObject), true);
        modelo = (GameObject)EditorGUILayout.ObjectField("Personaje (FBX)", modelo, typeof(GameObject), false);
        idle = (GameObject)EditorGUILayout.ObjectField("Idle (FBX)", idle, typeof(GameObject), false);
        spell = (GameObject)EditorGUILayout.ObjectField("Spell (FBX)", spell, typeof(GameObject), false);
        jump = (GameObject)EditorGUILayout.ObjectField("Jump (FBX)", jump, typeof(GameObject), false);
        nombre = EditorGUILayout.TextField("Nombre", nombre);
        colorVFX = EditorGUILayout.ColorField("Color del aura", colorVFX);
        cubrirCubo = EditorGUILayout.Toggle("Cubrir el cubo (Multi Target)", cubrirCubo);
        correccion = EditorGUILayout.Vector3Field("Giro de correccion", correccion);

        bool listo = objetivo != null && modelo != null && idle != null && spell != null && jump != null;
        using (new EditorGUI.DisabledScope(!listo))
        {
            if (GUILayout.Button("Configurar", GUILayout.Height(32)))
            {
                string rutaModelo = AssetDatabase.GetAssetPath(modelo);
                string carpeta = Path.GetDirectoryName(Path.GetDirectoryName(rutaModelo)).Replace('\\', '/');
                Ejecutar(new Parametros
                {
                    objetivo = objetivo.name,
                    modelo = rutaModelo,
                    idle = AssetDatabase.GetAssetPath(idle),
                    spell = AssetDatabase.GetAssetPath(spell),
                    jump = AssetDatabase.GetAssetPath(jump),
                    carpeta = carpeta,
                    nombre = nombre,
                    colorVFX = colorVFX,
                    cubrirCubo = cubrirCubo,
                    correccion = correccion,
                });
            }
        }
    }

    // ================================================================ proceso

    public static void Ejecutar(Parametros p)
    {
        try
        {
            Debug.Log($"[FantasyAR] Configurando personaje '{p.nombre}'...");
            string carpetaModelo = p.carpeta + "/02_Modelo3D_Texturas";
            string carpetaTexturas = carpetaModelo + "/Texturas";
            string carpetaMateriales = carpetaModelo + "/Materiales";
            string rutaController = p.carpeta + "/04_Animator/AC_" + p.nombre + ".controller";
            string carpetaVFX = p.carpeta + "/05_VFX";
            foreach (string c in new[] { carpetaModelo, carpetaTexturas, carpetaMateriales,
                                         p.carpeta + "/04_Animator", carpetaVFX })
                CrearCarpeta(c);

            Avatar avatar = ConfigurarModelo(p.modelo, carpetaTexturas, carpetaMateriales);
            if (avatar == null) return;

            AnimationClip clipIdle = ConfigurarAnimacion(p.idle, "Idle", avatar);
            AnimationClip clipSpell = ConfigurarAnimacion(p.spell, "Spell", avatar);
            AnimationClip clipJump = ConfigurarAnimacion(p.jump, "Jump", avatar);
            if (clipIdle == null || clipSpell == null || clipJump == null)
            {
                Fallo("No se pudieron leer las 3 animaciones. Revisa la consola.");
                return;
            }

            AnimatorController controller = CrearController(rutaController, clipIdle, clipSpell, clipJump);
            ArmarEscena(p, controller, carpetaVFX);

            AssetDatabase.SaveAssets();
            Debug.Log($"[FantasyAR] Personaje '{p.nombre}' configurado.");
            EditorUtility.DisplayDialog("FantasyAR",
                $"Listo: '{p.nombre}' ya esta sobre su target con 3 animaciones, Animator y VFX.\n\n" +
                "Dale Play para probarlo.", "OK");
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            Fallo("Ocurrio un error al configurar el personaje. Revisa la consola.");
        }
    }

    // ---------------------------------------------------------------- Modelo

    private static Avatar ConfigurarModelo(string ruta, string carpetaTexturas, string carpetaMateriales)
    {
        var importador = AssetImporter.GetAtPath(ruta) as ModelImporter;
        if (importador == null)
        {
            Fallo("No encuentro el modelo en " + ruta);
            return null;
        }
        importador.animationType = ModelImporterAnimationType.Human;
        importador.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importador.importAnimation = false;
        importador.SaveAndReimport();

        importador = (ModelImporter)AssetImporter.GetAtPath(ruta);
        if (importador.ExtractTextures(carpetaTexturas))
            AssetDatabase.Refresh();
        MarcarMapasNormales(carpetaTexturas);
        ExtraerMateriales(ruta, carpetaMateriales);
        PrepararMateriales(carpetaMateriales, carpetaTexturas);

        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Avatar>().FirstOrDefault();
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
        {
            // Algunos personajes (ej. el Mutant de Mixamo, con una garra sin dedos) no se mapean solos
            Debug.Log("[FantasyAR] Mapeo automatico fallido; aplicando mapeo manual de huesos Mixamo.");
            AplicarMapeoMixamo(ruta);
            avatar = AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Avatar>().FirstOrDefault();
        }
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
        {
            Fallo("El personaje no tiene un esqueleto humanoide valido (usa un modelo de Mixamo o pasalo por el auto-rig de Mixamo).");
            return null;
        }
        return avatar;
    }

    private static readonly (string mixamo, HumanBodyBones hueso)[] MapaMixamo =
    {
        ("Hips", HumanBodyBones.Hips), ("Spine", HumanBodyBones.Spine), ("Spine1", HumanBodyBones.Chest),
        ("Spine2", HumanBodyBones.UpperChest), ("Neck", HumanBodyBones.Neck), ("Head", HumanBodyBones.Head),
        ("LeftShoulder", HumanBodyBones.LeftShoulder), ("LeftArm", HumanBodyBones.LeftUpperArm),
        ("LeftForeArm", HumanBodyBones.LeftLowerArm), ("LeftHand", HumanBodyBones.LeftHand),
        ("RightShoulder", HumanBodyBones.RightShoulder), ("RightArm", HumanBodyBones.RightUpperArm),
        ("RightForeArm", HumanBodyBones.RightLowerArm), ("RightHand", HumanBodyBones.RightHand),
        ("LeftUpLeg", HumanBodyBones.LeftUpperLeg), ("LeftLeg", HumanBodyBones.LeftLowerLeg),
        ("LeftFoot", HumanBodyBones.LeftFoot), ("LeftToeBase", HumanBodyBones.LeftToes),
        ("RightUpLeg", HumanBodyBones.RightUpperLeg), ("RightLeg", HumanBodyBones.RightLowerLeg),
        ("RightFoot", HumanBodyBones.RightFoot), ("RightToeBase", HumanBodyBones.RightToes),
        ("LeftHandThumb1", HumanBodyBones.LeftThumbProximal), ("LeftHandThumb2", HumanBodyBones.LeftThumbIntermediate),
        ("LeftHandThumb3", HumanBodyBones.LeftThumbDistal), ("LeftHandIndex1", HumanBodyBones.LeftIndexProximal),
        ("LeftHandIndex2", HumanBodyBones.LeftIndexIntermediate), ("LeftHandIndex3", HumanBodyBones.LeftIndexDistal),
        ("LeftHandMiddle1", HumanBodyBones.LeftMiddleProximal), ("LeftHandMiddle2", HumanBodyBones.LeftMiddleIntermediate),
        ("LeftHandMiddle3", HumanBodyBones.LeftMiddleDistal), ("LeftHandRing1", HumanBodyBones.LeftRingProximal),
        ("LeftHandRing2", HumanBodyBones.LeftRingIntermediate), ("LeftHandRing3", HumanBodyBones.LeftRingDistal),
        ("LeftHandPinky1", HumanBodyBones.LeftLittleProximal), ("LeftHandPinky2", HumanBodyBones.LeftLittleIntermediate),
        ("LeftHandPinky3", HumanBodyBones.LeftLittleDistal),
        ("RightHandThumb1", HumanBodyBones.RightThumbProximal), ("RightHandThumb2", HumanBodyBones.RightThumbIntermediate),
        ("RightHandThumb3", HumanBodyBones.RightThumbDistal), ("RightHandIndex1", HumanBodyBones.RightIndexProximal),
        ("RightHandIndex2", HumanBodyBones.RightIndexIntermediate), ("RightHandIndex3", HumanBodyBones.RightIndexDistal),
        ("RightHandMiddle1", HumanBodyBones.RightMiddleProximal), ("RightHandMiddle2", HumanBodyBones.RightMiddleIntermediate),
        ("RightHandMiddle3", HumanBodyBones.RightMiddleDistal), ("RightHandRing1", HumanBodyBones.RightRingProximal),
        ("RightHandRing2", HumanBodyBones.RightRingIntermediate), ("RightHandRing3", HumanBodyBones.RightRingDistal),
        ("RightHandPinky1", HumanBodyBones.RightLittleProximal), ("RightHandPinky2", HumanBodyBones.RightLittleIntermediate),
        ("RightHandPinky3", HumanBodyBones.RightLittleDistal),
    };

    /// <summary>Asigna a mano los huesos de un esqueleto Mixamo (prefijo mixamorig:) al avatar Humanoid.</summary>
    private static void AplicarMapeoMixamo(string ruta)
    {
        var modeloGO = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        var importador = AssetImporter.GetAtPath(ruta) as ModelImporter;
        if (modeloGO == null || importador == null) return;

        Transform[] huesos = modeloGO.GetComponentsInChildren<Transform>(true);
        string Buscar(string corto) => huesos.FirstOrDefault(t => t.name == corto || t.name.EndsWith(":" + corto))?.name;

        var humanos = new System.Collections.Generic.List<HumanBone>();
        foreach (var (mixamo, hueso) in MapaMixamo)
        {
            string nombre = Buscar(mixamo);
            if (nombre == null) continue;
            var hb = new HumanBone { boneName = nombre, humanName = HumanTrait.BoneName[(int)hueso] };
            hb.limit.useDefaultValues = true;
            humanos.Add(hb);
        }

        HumanDescription hd = importador.humanDescription;
        hd.human = humanos.ToArray();
        hd.skeleton = huesos.Select(t => new SkeletonBone
        {
            name = t.name,
            position = t.localPosition,
            rotation = t.localRotation,
            scale = t.localScale,
        }).ToArray();
        hd.upperArmTwist = 0.5f;
        hd.lowerArmTwist = 0.5f;
        hd.upperLegTwist = 0.5f;
        hd.lowerLegTwist = 0.5f;
        hd.armStretch = 0.05f;
        hd.legStretch = 0.05f;
        hd.feetSpacing = 0f;
        hd.hasTranslationDoF = false;

        importador.animationType = ModelImporterAnimationType.Human;
        importador.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importador.humanDescription = hd;
        importador.SaveAndReimport();
    }

    private static void MarcarMapasNormales(string carpeta)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { carpeta }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (!EsNormal(Path.GetFileNameWithoutExtension(ruta).ToLowerInvariant())) continue;
            var ti = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
        }
    }

    private static void ExtraerMateriales(string rutaModelo, string carpeta)
    {
        bool extraido = false;
        foreach (Material m in AssetDatabase.LoadAllAssetsAtPath(rutaModelo).OfType<Material>().ToArray())
        {
            string nombre = string.Concat(m.name.Split(Path.GetInvalidFileNameChars()));
            string destino = AssetDatabase.GenerateUniqueAssetPath(carpeta + "/" + nombre + ".mat");
            string error = AssetDatabase.ExtractAsset(m, destino);
            if (string.IsNullOrEmpty(error))
            {
                extraido = true;
                AssetDatabase.WriteImportSettingsIfDirty(rutaModelo);
            }
            else Debug.LogWarning("[FantasyAR] No se pudo extraer el material " + m.name + ": " + error);
        }
        if (extraido) AssetDatabase.ImportAsset(rutaModelo, ImportAssetOptions.ForceUpdate);
    }

    private static bool EsNormal(string n) => n.Contains("normal") || n.EndsWith("_n") || n.Contains("nrm");
    private static bool EsEmision(string n) => n.Contains("emiss") || n.Contains("glow");
    private static bool EsSecundaria(string n) =>
        n.Contains("spec") || n.Contains("metal") || n.Contains("rough") || n.Contains("gloss") ||
        n.Contains("_ao") || n.Contains("occlusion") || n.Contains("height") || n.Contains("opacity");

    /// <summary>URP Lit con textura base, normal y emision asignadas; transparencia recortada si aplica.</summary>
    private static void PrepararMateriales(string carpetaMateriales, string carpetaTexturas)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Texture2D[] texturas = AssetDatabase.FindAssets("t:Texture2D", new[] { carpetaTexturas })
            .Select(g => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(t => t != null).ToArray();

        Texture2D normal = texturas.FirstOrDefault(t => EsNormal(t.name.ToLowerInvariant()));
        Texture2D emision = texturas.FirstOrDefault(t => EsEmision(t.name.ToLowerInvariant()));
        Texture2D[] bases = texturas.Where(t =>
        {
            string n = t.name.ToLowerInvariant();
            return !EsNormal(n) && !EsEmision(n) && !EsSecundaria(n);
        }).ToArray();

        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { carpetaMateriales }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m == null) continue;
            string nombre = m.name.ToLowerInvariant();
            bool transparente = nombre.Contains("transparent") || nombre.Contains("alpha") ||
                                nombre.Contains("hair") || nombre.Contains("lash");

            if (lit != null && !m.shader.name.StartsWith("Universal Render Pipeline"))
            {
                Texture viejo = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                m.shader = lit;
                if (viejo != null) m.SetTexture("_BaseMap", viejo);
            }

            // Textura base: la que coincida con el tipo (transparente o no)
            if (m.GetTexture("_BaseMap") == null && bases.Length > 0)
            {
                Texture2D baseTex = bases.FirstOrDefault(t => t.name.ToLowerInvariant().Contains("transparent") == transparente)
                                    ?? bases[0];
                m.SetTexture("_BaseMap", baseTex);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", baseTex);
            }
            m.SetColor("_BaseColor", Color.white);

            if (normal != null && m.GetTexture("_BumpMap") == null)
            {
                m.SetTexture("_BumpMap", normal);
                m.EnableKeyword("_NORMALMAP");
            }

            // Emision: solo con mapa; sin mapa se apaga (si no, el modelo sale blanco)
            if (emision != null)
            {
                m.SetTexture("_EmissionMap", emision);
                m.SetColor("_EmissionColor", Color.white);
                m.EnableKeyword("_EMISSION");
            }
            else
            {
                m.SetColor("_EmissionColor", Color.black);
                m.DisableKeyword("_EMISSION");
            }

            if (transparente)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.5f);
                m.SetFloat("_Cull", 0f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.renderQueue = 2450;
            }
            EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();
    }

    // ----------------------------------------------------------- Animaciones

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

    private static AnimatorController CrearController(string ruta, AnimationClip idle, AnimationClip spell, AnimationClip jump)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ruta) != null)
            AssetDatabase.DeleteAsset(ruta);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ruta);
        controller.AddParameter("Idle", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Spell", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState sIdle = sm.AddState("Idle", new Vector3(320f, 0f, 0f));
        sIdle.motion = idle;
        AnimatorState sSpell = sm.AddState("Spell", new Vector3(320f, 90f, 0f));
        sSpell.motion = spell;
        AnimatorState sJump = sm.AddState("Jump", new Vector3(320f, 180f, 0f));
        sJump.motion = jump;
        sm.defaultState = sIdle;

        foreach (var (estado, trigger) in new[] { (sIdle, "Idle"), (sSpell, "Spell"), (sJump, "Jump") })
        {
            AnimatorStateTransition t = sm.AddAnyStateTransition(estado);
            t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            t.hasExitTime = false;
            t.duration = 0.25f;
            t.canTransitionToSelf = false;
        }
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    // ---------------------------------------------------------------- Escena

    private static void ArmarEscena(Parametros p, AnimatorController controller, string carpetaVFX)
    {
        GameObject target = EditorSceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .Select(t => t.gameObject).FirstOrDefault(g => g.name == p.objetivo);
        if (target == null && !string.IsNullOrEmpty(p.datasetMulti))
            target = CrearMultiTarget(p);
        if (target == null)
        {
            Fallo($"No encuentro '{p.objetivo}' en la escena abierta.");
            return;
        }

        // Contenedor con el giro de correccion (ejes = cubo fisico)
        string nombreContenido = "Contenido_" + p.nombre;
        Transform anterior = target.transform.Find(nombreContenido);
        if (anterior != null) Undo.DestroyObjectImmediate(anterior.gameObject);
        var contenido = new GameObject(nombreContenido);
        Undo.RegisterCreatedObjectUndo(contenido, "Crear personaje");
        Undo.SetTransformParent(contenido.transform, target.transform, "Crear personaje");
        contenido.transform.localPosition = Vector3.zero;
        contenido.transform.localRotation = Quaternion.Euler(p.correccion);
        contenido.transform.localScale = Vector3.one;

        // Instanciar y medir el modelo sin escala
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p.modelo);
        var personaje = (GameObject)PrefabUtility.InstantiatePrefab(prefab, target.scene);
        personaje.name = p.nombre;
        personaje.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        personaje.transform.localScale = Vector3.one;
        Bounds b = Medir(personaje);

        Undo.SetTransformParent(personaje.transform, contenido.transform, "Crear personaje");
        float escalaPadre = Mathf.Abs(contenido.transform.lossyScale.x);
        if (escalaPadre < 1e-6f) escalaPadre = 1f;

        if (p.cubrirCubo)
        {
            Vector3 cubo = TamanoCubo(target);
            // Escala para que el personaje tape el cubo visto de frente (ancho y alto) y lo supere en altura
            float escala = Mathf.Max(cubo.x * 1.15f / Mathf.Max(b.size.x, 1e-4f),
                                     cubo.y * 1.6f / Mathf.Max(b.size.y, 1e-4f));
            personaje.transform.localScale = Vector3.one * (escala / escalaPadre);
            // Pies en la base del cubo, centrado en X/Z, mirando al frente (+Z)
            personaje.transform.localRotation = Quaternion.identity;
            Vector3 centroModelo = new Vector3(b.center.x, b.min.y, b.center.z) * escala / escalaPadre;
            personaje.transform.localPosition = new Vector3(0f, -cubo.y * 0.5f / escalaPadre, 0f) - centroModelo;
        }
        else
        {
            float ancho = 100f;
            Vector3 centro = target.transform.position;
            Quaternion rot = target.transform.rotation;
            if (CrearBotonesVirtualesFantasy.BuscarEsquinas(target, out Vector3 si, out Vector3 sd, out Vector3 ii))
            {
                Vector3 ejeAncho = sd - si, ejeAlto = ii - si;
                ancho = ejeAncho.magnitude;
                centro = si + ejeAncho * 0.5f + ejeAlto * 0.45f;
                rot = Quaternion.LookRotation(ejeAlto.normalized, Vector3.Cross(-ejeAlto, ejeAncho).normalized);
            }
            float escala = 0.8f * ancho / Mathf.Max(b.size.y, 1e-4f);
            personaje.transform.SetPositionAndRotation(centro, rot);
            personaje.transform.localScale = Vector3.one * (escala / escalaPadre);
        }

        Animator animator = personaje.GetComponent<Animator>();
        if (animator == null) animator = personaje.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        // VFX
        float alto = b.size.y;
        Material mat = MaterialVFX(carpetaVFX + "/M_VFX_" + p.nombre + ".mat", p.colorVFX);
        GameObject prefabAura = GuardarPrefab(CrearAura(mat, alto, p.colorVFX, p.nombre), carpetaVFX + "/VFX_Aura" + p.nombre + ".prefab");
        GameObject prefabHechizo = GuardarPrefab(CrearHechizo(mat, alto, p.colorVFX, p.nombre), carpetaVFX + "/VFX_Hechizo" + p.nombre + ".prefab");

        var aura = (GameObject)PrefabUtility.InstantiatePrefab(prefabAura, target.scene);
        aura.transform.SetParent(personaje.transform, false);
        aura.transform.localPosition = Vector3.zero;
        var hechizo = (GameObject)PrefabUtility.InstantiatePrefab(prefabHechizo, target.scene);
        hechizo.transform.SetParent(personaje.transform, false);
        hechizo.transform.localPosition = new Vector3(0f, 0.6f * alto, 0.18f * alto);

        // Control de animaciones por teclado/boton (para el video y pruebas)
        var control = personaje.GetComponent<ControlAnimacionesFantasy>();
        if (control == null) control = Undo.AddComponent<ControlAnimacionesFantasy>(personaje);
        control.vfxHechizo = hechizo.GetComponent<ParticleSystem>();
        EditorUtility.SetDirty(control);

        Selection.activeGameObject = personaje;
        EditorSceneManager.MarkSceneDirty(target.scene);
        EditorSceneManager.SaveScene(target.scene);
    }

    /// <summary>Crea el Multi Target con el menu de Vuforia y le asigna la base de datos y el cuboid.</summary>
    private static GameObject CrearMultiTarget(Parametros p)
    {
        Selection.activeObject = null; // para que no se cree como hijo de otro objeto
        if (!EditorApplication.ExecuteMenuItem("GameObject/Vuforia Engine/Multi Target"))
            return null;

        MultiTargetBehaviour mt = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<MultiTargetBehaviour>()
            : null;
        if (mt == null)
        {
            mt = EditorSceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<MultiTargetBehaviour>(true))
                .FirstOrDefault(m => string.IsNullOrEmpty(new SerializedObject(m).FindProperty("mTrackableName")?.stringValue)
                                     || m.gameObject.name == "MultiTarget");
        }
        if (mt == null) return null;

        var so = new SerializedObject(mt);
        SerializedProperty dataset = so.FindProperty("mDataSetPath");
        SerializedProperty nombre = so.FindProperty("mTrackableName");
        SerializedProperty inicializado = so.FindProperty("mInitializedInEditor");
        if (dataset != null) dataset.stringValue = p.datasetMulti;
        if (nombre != null) nombre.stringValue = p.trackableMulti;
        if (inicializado != null) inicializado.boolValue = true;
        so.ApplyModifiedProperties();

        mt.gameObject.name = p.objetivo;
        mt.transform.position = new Vector3(250f, 0f, 0f); // al lado del dibujo en la vista Scene
        Debug.Log($"[FantasyAR] Multi Target '{p.objetivo}' creado con {p.datasetMulti} / {p.trackableMulti}.");
        return mt.gameObject;
    }

    private static Bounds Medir(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(new Vector3(0f, 0.9f, 0f), new Vector3(0.5f, 1.8f, 0.3f));
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    /// <summary>Tamano del cubo leyendo el XML de la base de datos de Vuforia (en unidades de la escena).</summary>
    private static Vector3 TamanoCubo(GameObject target)
    {
        var observer = target.GetComponent<ObserverBehaviour>();
        if (observer != null)
        {
            var so = new SerializedObject(observer);
            string dataset = so.FindProperty("mDataSetPath")?.stringValue;
            string nombre = so.FindProperty("mTrackableName")?.stringValue;
            string ruta = string.IsNullOrEmpty(dataset) ? null : Path.Combine(Application.streamingAssetsPath, dataset);
            if (ruta != null && File.Exists(ruta) && !string.IsNullOrEmpty(nombre))
            {
                var xml = new XmlDocument();
                xml.Load(ruta);
                Vector3 max = Vector3.zero;
                foreach (XmlElement parte in xml.SelectNodes($"//MultiTarget[@name='{nombre}']/Part"))
                {
                    string[] t = parte.GetAttribute("translation").Split(' ');
                    if (t.Length < 3) continue;
                    max = Vector3.Max(max, new Vector3(Mathf.Abs(Leer(t[0])), Mathf.Abs(Leer(t[1])), Mathf.Abs(Leer(t[2]))));
                }
                if (max != Vector3.zero) return max * 2f * Mathf.Abs(target.transform.lossyScale.x);
            }
        }
        Debug.LogWarning("[FantasyAR] No pude leer el tamano del cubo; uso 100.");
        return Vector3.one * 100f;
    }

    private static float Leer(string s) =>
        float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------- VFX

    private static Material MaterialVFX(string ruta, Color color)
    {
        var existente = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (existente != null) return existente;
        var baseUrp = AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        Material m = baseUrp != null ? new Material(baseUrp)
                                     : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    private static GameObject GuardarPrefab(GameObject temporal, string ruta)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporal, ruta);
        Object.DestroyImmediate(temporal);
        return prefab;
    }

    private static GameObject CrearAura(Material material, float alto, Color color, string nombre)
    {
        var go = new GameObject("VFX_Aura" + nombre);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.duration = 5f;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f * alto, 0.15f * alto);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f * alto, 0.28f * alto);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        Color oscuro = Color.Lerp(color, Color.black, 0.45f);
        oscuro.a = color.a * 0.8f;
        main.startColor = new ParticleSystem.MinMaxGradient(color, oscuro);
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
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(Desvanecer());
        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.3f));
        var ruido = ps.noise;
        ruido.enabled = true;
        ruido.strength = 0.08f * alto;
        ruido.frequency = 0.8f;
        var render = go.GetComponent<ParticleSystemRenderer>();
        render.sharedMaterial = material;
        render.renderMode = ParticleSystemRenderMode.Billboard;
        return go;
    }

    private static GameObject CrearHechizo(Material material, float alto, Color color, string nombre)
    {
        var go = new GameObject("VFX_Hechizo" + nombre);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.duration = 1.2f;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f * alto, 1.1f * alto);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f * alto, 0.09f * alto);
        Color claro = Color.Lerp(color, Color.white, 0.5f);
        claro.a = 1f;
        Color fuerte = color;
        fuerte.a = 1f;
        main.startColor = new ParticleSystem.MinMaxGradient(fuerte, claro);
        main.maxParticles = 400;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emision = ps.emission;
        emision.rateOverTime = 0f;
        emision.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)90), new ParticleSystem.Burst(0.35f, (short)60) });
        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Sphere;
        forma.radius = 0.08f * alto;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(Desvanecer());
        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
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

    // ------------------------------------------------------------- Utilidades

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
