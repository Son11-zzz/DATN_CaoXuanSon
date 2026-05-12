using UnityEngine;

public class QuestUnlocker : MonoBehaviour
{
    [SerializeField] private QuestData questToUnlock;
    [SerializeField] private GameObject[] targetsToEnable;

    private void OnEnable()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestUpdated += Refresh;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestUpdated -= Refresh;
        }
    }

    private void Refresh()
    {
        if (questToUnlock == null || targetsToEnable == null || targetsToEnable.Length == 0) return;
        if (QuestManager.Instance == null) return;

        if (QuestManager.Instance.IsCompleted(questToUnlock))
        {
            for (int i = 0; i < targetsToEnable.Length; i++)
            {
                if (targetsToEnable[i] != null)
                {
                    targetsToEnable[i].SetActive(true);
                }
            }
        }
    }
}
