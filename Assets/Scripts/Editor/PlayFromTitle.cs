using UnityEditor;
using UnityEditor.SceneManagement;

// Editor only: pressing Play always starts from the title screen, whichever scene is open,
// so the Title -> Select -> Fight flow (and the opponent choice) is never skipped.
// The open scene is restored when Play mode ends.
[InitializeOnLoad]
static class PlayFromTitle
{
    const string TitleScene = "Assets/Scenes/MainMenu.unity";

    static PlayFromTitle()
    {
        // On a fresh project import the scene may not be loadable yet; retry on the next editor tick.
        if (!TryApply())
            EditorApplication.delayCall += () =>
            {
                if (!TryApply())
                    UnityEngine.Debug.LogWarning($"PlayFromTitle: {TitleScene} not found, so Play starts from the open scene.");
            };
    }

    static bool TryApply()
    {
        SceneAsset title = AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScene);
        if (title == null) return false;
        EditorSceneManager.playModeStartScene = title;
        return true;
    }
}
