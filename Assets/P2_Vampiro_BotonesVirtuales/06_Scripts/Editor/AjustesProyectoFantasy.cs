using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// FantasyAR - Ajuste de proyecto (se aplica una sola vez).
/// Vuforia 11 deja eventos de render registrados al salir de Play cuando Unity no recarga
/// el dominio ("Enter Play Mode Options" activado). Eso llena la consola con
/// MissingReferenceException en VideoBackgroundTextureUpdater.
/// Este ajuste vuelve a "Reload Domain and Scene" (Project Settings > Editor).
/// </summary>
[InitializeOnLoad]
public static class AjustesProyectoFantasy
{
    private const string Bandera = "Library/FantasyAR_ajuste_playmode.pendiente";

    static AjustesProyectoFantasy()
    {
        if (!File.Exists(Bandera)) return;
        File.Delete(Bandera);

        if (EditorSettings.enterPlayModeOptionsEnabled)
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            Debug.Log("[FantasyAR] Enter Play Mode Settings cambiado a 'Reload Domain and Scene' " +
                      "para evitar los errores de Vuforia al salir de Play.");
        }
    }
}
