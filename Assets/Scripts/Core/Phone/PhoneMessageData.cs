using UnityEngine;

[CreateAssetMenu(fileName = "PhoneMessage", menuName = "Scriptable Objects/PhoneMessage")]
public class PhoneMessageData : ScriptableObject
{
    [Header("Gating")]
    [Min(1)] public int semester = 1;
    [Min(1)] public int dayInSemester = 1;

    [Tooltip("Gio (0..23) tin nhan xuat hien. -1 = xuat hien ngay tu dau ngay.")]
    public int hour = -1;

    [Header("Message")]
    public string sender = "NPC";

    [TextArea(2, 6)]
    public string content;

    [Header("Behavior")]
    [Tooltip("Neu bat, xuat hien popup thong bao khi nhan.")]
    public bool showPopup = true;

    [Header("Teleport (optional)")]
    [Tooltip("SpawnPoint.spawnId trong scene đang load. Để trống = không teleport.")]
    public string teleportSpawnPointId;

    [Tooltip("Chỉ teleport nếu scene hiện tại trùng tên (vd. 21_SchoolArea). Để trống = mọi scene có spawnId khớp.")]
    public string teleportOnlyIfSceneNamed;

    public string GetId()
    {
        return $"{name}_{semester}_{dayInSemester}_{hour}";
    }

    public bool IsAvailableNow(int curSemester, int curDayInSemester, int curHour)
    {
        if (semester != curSemester) return false;
        if (dayInSemester != curDayInSemester) return false;
        if (hour < 0) return true;
        return curHour >= hour;
    }
}
