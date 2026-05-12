using UnityEngine;

[DisallowMultipleComponent]
public class GameSettings : MonoBehaviour
{
    private static GameSettings instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = 60;
    }
}