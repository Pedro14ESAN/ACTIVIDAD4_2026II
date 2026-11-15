using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// FantasyAR - Herramienta de Editor (sirve para los 4 modelos del grupo).
/// Menu: FantasyAR > Crear Animator (Idle - Spell - Jump)
///
/// Crea un Animator Controller con:
///  - Estados Idle (por defecto), Spell y Jump con sus clips de Mixamo.
///  - Parametros Trigger "Idle", "Spell" y "Jump".
///  - Transiciones desde Any State con fundido suave (sin Exit Time).
/// </summary>
public class CreadorAnimatorFantasy : EditorWindow
{
    private AnimationClip clipIdle;
    private AnimationClip clipSpell;
    private AnimationClip clipJump;
    private string nombreController = "AC_Vampiro";
    private float duracionTransicion = 0.25f;

    [MenuItem("FantasyAR/Crear Animator (Idle - Spell - Jump)")]
    private static void Abrir()
    {
        GetWindow<CreadorAnimatorFantasy>("Animator FantasyAR");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Arrastra los 3 clips de Mixamo. Dentro de cada FBX el clip se llama 'mixamo.com' " +
            "(despliega el FBX con la flechita para verlo).", MessageType.Info);

        clipIdle = (AnimationClip)EditorGUILayout.ObjectField("Idle (Reposo)", clipIdle, typeof(AnimationClip), false);
        clipSpell = (AnimationClip)EditorGUILayout.ObjectField("Spell (Hechizo)", clipSpell, typeof(AnimationClip), false);
        clipJump = (AnimationClip)EditorGUILayout.ObjectField("Jump (Saltar)", clipJump, typeof(AnimationClip), false);

        EditorGUILayout.Space();
        nombreController = EditorGUILayout.TextField("Nombre del controller", nombreController);
        duracionTransicion = EditorGUILayout.Slider("Transicion (segundos)", duracionTransicion, 0f, 1f);

        EditorGUILayout.Space();
        bool listo = clipIdle != null && clipSpell != null && clipJump != null;
        using (new EditorGUI.DisabledScope(!listo))
        {
            if (GUILayout.Button("Crear Animator Controller", GUILayout.Height(32)))
                Crear();
        }
    }

    private void Crear()
    {
        string ruta = EditorUtility.SaveFilePanelInProject(
            "Guardar Animator Controller", nombreController, "controller",
            "Elige la carpeta donde guardar el Animator Controller");
        if (string.IsNullOrEmpty(ruta))
            return;

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ruta);
        controller.AddParameter("Idle", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Spell", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimatorState idle = sm.AddState("Idle", new Vector3(320f, 0f, 0f));
        idle.motion = clipIdle;
        AnimatorState spell = sm.AddState("Spell", new Vector3(320f, 90f, 0f));
        spell.motion = clipSpell;
        AnimatorState jump = sm.AddState("Jump", new Vector3(320f, 180f, 0f));
        jump.motion = clipJump;

        sm.defaultState = idle;

        AgregarTransicion(sm, idle, "Idle");
        AgregarTransicion(sm, spell, "Spell");
        AgregarTransicion(sm, jump, "Jump");

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Selection.activeObject = controller;
        EditorGUIUtility.PingObject(controller);
        Debug.Log("[FantasyAR] Animator Controller creado en " + ruta);
    }

    private void AgregarTransicion(AnimatorStateMachine sm, AnimatorState destino, string trigger)
    {
        AnimatorStateTransition t = sm.AddAnyStateTransition(destino);
        t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        t.hasExitTime = false;
        t.duration = duracionTransicion;
        t.canTransitionToSelf = false;
    }
}
