using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vuforia;

/// <summary>
/// FantasyAR - Quita targets de Vuforia duplicados (mismo dataset y mismo nombre) o sin configurar.
/// Vuforia no puede activar dos observadores del mismo target: el segundo falla con
/// "Failed to activate observer" y eso detiene la camara.
/// Menu: FantasyAR > Quitar targets duplicados. Tambien se ejecuta una vez si existe la bandera.
/// </summary>
[InitializeOnLoad]
public static class LimpiezaEscenaFantasy
{
    private const string Bandera = "Library/FantasyAR_limpiar_duplicados.pendiente";

    static LimpiezaEscenaFantasy()
    {
        if (File.Exists(Bandera)) EditorApplication.update += Esperar;
    }

    private static void Esperar()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.update -= Esperar;
        if (!File.Exists(Bandera)) return;
        File.Delete(Bandera);
        Limpiar();
    }

    [MenuItem("FantasyAR/Quitar targets duplicados")]
    public static void Limpiar()
    {
        var escena = EditorSceneManager.GetActiveScene();
        var observadores = escena.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<ObserverBehaviour>(true))
            .ToList();

        string Clave(ObserverBehaviour o)
        {
            var so = new SerializedObject(o);
            return (so.FindProperty("mDataSetPath")?.stringValue ?? "") + "|" +
                   (so.FindProperty("mTrackableName")?.stringValue ?? "");
        }

        int borrados = 0;
        foreach (var grupo in observadores.GroupBy(Clave))
        {
            var lista = grupo.ToList();
            bool sinConfigurar = grupo.Key.EndsWith("|");
            // Se conserva el que tiene contenido (mas hijos); los demas se borran.
            var conservar = sinConfigurar ? null : lista.OrderByDescending(o => o.transform.childCount).First();
            foreach (var o in lista)
            {
                if (o == conservar) continue;
                if (sinConfigurar && o.transform.childCount > 0) continue; // por si tiene algo adentro
                Debug.Log($"[FantasyAR] Eliminado target duplicado o sin configurar: '{o.gameObject.name}' ({grupo.Key})");
                Undo.DestroyObjectImmediate(o.gameObject);
                borrados++;
            }
        }

        if (borrados > 0)
        {
            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
        }
        Debug.Log($"[FantasyAR] Limpieza de targets terminada: {borrados} eliminado(s).");
    }
}
