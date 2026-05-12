/// <summary>
/// Chuỗi hiển thị cho Quest panel / helper (tiếng Việt có dấu).
/// </summary>
public static class QuestUiLocalization
{
    public static string StatLabelVi(StatType stat) => stat switch
    {
        StatType.GPA => "GPA",
        StatType.Stress => "Căng thẳng",
        StatType.Money => "Tiền",
        StatType.Health => "Sinh lực",
        StatType.Energy => "Năng lượng",
        StatType.Social => "Xã giao",
        StatType.Skill => "Kỹ năng",
        _ => stat.ToString()
    };
}
