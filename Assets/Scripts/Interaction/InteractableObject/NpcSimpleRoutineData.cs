using System;
using UnityEngine;

/// <summary>Giới hạn slot theo ngày đi học (Phone schedule hoặc override).</summary>
public enum NpcRoutineScheduleFilter
{
    Any,
    SchoolDayOnly,
    NoSchoolOnly
}

/// <summary>Hành động runtime cho slot routine.</summary>
public enum NpcSimpleRoutineAction
{
    /// <summary>Đi theo pathPointIds, có thể loop.</summary>
    MovePath,
    /// <summary>Đặt vị trí tức thì.</summary>
    Teleport,
    /// <summary>Hiện tại targetPointId, bỏ qua nếu resolve fail.</summary>
    AppearAtPoint,
    /// <summary>Ẩn thế giới (collider + sprite).</summary>
    Hide,
    /// <summary>Đứng tại targetPointId (lớp, văn phòng, quầy hàng, …).</summary>
    StationaryAtPoint
}

/// <summary>Một mốc lịch trong ngày (so khớp theo GameTimeManager.Hour).</summary>
[Serializable]
public class NpcRoutineSlot
{
    [Tooltip("Gợi ý trong Editor / debug")]
    public string label;

    [Range(0, 23)]
    public int startHour;
    [Tooltip("Kết thúc ngoài vòng (9 = giờ 8-8).")]
    [Range(1, 24)]
    public int endHour = 9;

    [Tooltip("Rỗng = wildcard: thị trấn (20_*), trường (21_*), cửa hàng (24_* vendor). Nên ghi rõ 20_Town, 21_SchoolArea; nhà bạn 23_* là scene riêng — giữ MovePath khi đổi scene xử lý ở NpcSimpleMover2D.")]
    public string sceneName;

    public NpcRoutineScheduleFilter scheduleFilter = NpcRoutineScheduleFilter.Any;
    public NpcSimpleRoutineAction action = NpcSimpleRoutineAction.MovePath;

    [Tooltip("Mảng tên điểm theo thứ tự, resolve qua NpcScenePointRegistry trong scene.")]
    public string[] pathPointIds = System.Array.Empty<string>();

    [Tooltip("Teleport, Appear, Stationary: một id điểm.")]
    public string targetPointId;

    public float moveSpeed = 1.5f;
    public bool loop;

    [Header("Một lượt path (thường dùng Town -> cổng trường)")]
    public bool hideWhenPathComplete = true;
}

/// <summary>Dữ liệu routine cho <see cref="NpcSimpleMover2D"/>. Gắn vào profile hoặc trực tiếp lên NPC.</summary>
[CreateAssetMenu(fileName = "NpcSimpleRoutine", menuName = "SVSimulator/NPC/Npc Simple Routine", order = 2)]
public class NpcSimpleRoutineData : ScriptableObject
{
    [Tooltip("Nếu rỗng: ngày đi học = PhoneSystem có block tiết. Nếu có phần tử: chỉ các DayInSemester này là đi học.")]
    public int[] activeSchoolDayOverride = System.Array.Empty<int>();

    [Header("Cơm trưa (School, ưu tiên hơn slot cùng giờ)")]
    [Tooltip("Dùng khi SchoolSchedulePhase == Lunch, trong scene trường, ngày đi học.")]
    public bool useDynamicLunchFromPhone = true;
    [Tooltip("Học sinh: canteen. Giáo viên: dùng lunchOfficePointId nếu archetype = Teacher (trên NpcSimpleMover2D từ profile).")]
    public string lunchCanteenPointId;
    public string lunchOfficePointId;

    [Header("Học sinh / giáo viên — slot theo giờ (thứ tự trên trước)")]
    public NpcRoutineSlot[] schoolDaySlots = System.Array.Empty<NpcRoutineSlot>();

    [Header("Ngày không đi học (cuối tuần, kỳ nghỉ)")]
    public NpcRoutineSlot[] noSchoolSlots = System.Array.Empty<NpcRoutineSlot>();

    [Header("Vendor (StationaryVendor)")]
    [Range(0, 23)]
    public int vendorShopOpenHour = 8;
    [Range(0, 24)]
    public int vendorShopCloseHour = 18;
    public string vendorStallPointId;
    [Tooltip("Sau giờ đóng, path về nhà; rỗng = Teleport/Ẩn tại homePoint nếu có.")]
    public string vendorHomePointId;
    [Tooltip("Rỗng = đóng: Ẩn. Có: đi path")]
    public string[] vendorPathHomePointIds = System.Array.Empty<string>();
}
