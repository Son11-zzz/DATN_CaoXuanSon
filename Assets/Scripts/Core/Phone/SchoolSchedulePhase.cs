using UnityEngine;

/// <summary>
/// Pha trong ngày có lịch học (theo <see cref="ClassScheduleEntry"/>).
/// Dùng chung cho NPC và hệ thống khác.
/// </summary>
public enum SchoolSchedulePhase
{
    /// <summary>Ngày nghỉ, cuối tuần, hoặc asset lịch không có khối học hợp lệ.</summary>
    NoSchool,

    MorningClass,
    Lunch,
    AfternoonClass,

    /// <summary>Trước giờ vào lớp, sau hết buổi chiều, hoặc skipClassAttendanceToday.</summary>
    AfterSchoolHours
}

public static class SchoolSchedulePhaseHelper
{
    /// <param name="skipClassAttendanceToday">Từ <see cref="StoryEventDefinition.skipClassAttendanceToday"/>.</param>
    public static SchoolSchedulePhase GetPhase(int hour, ClassScheduleEntry schedule, bool skipClassAttendanceToday)
    {
        hour = Mathf.Clamp(hour, 0, 23);

        if (skipClassAttendanceToday)
        {
            return SchoolSchedulePhase.AfterSchoolHours;
        }

        if (schedule == null)
        {
            return SchoolSchedulePhase.NoSchool;
        }

        bool morningOk = schedule.morningEndHour > schedule.morningStartHour;
        bool afternoonOk = schedule.afternoonEndHour > schedule.afternoonStartHour;
        if (!morningOk && !afternoonOk)
        {
            return SchoolSchedulePhase.NoSchool;
        }

        if (morningOk && hour >= schedule.morningStartHour && hour < schedule.morningEndHour)
        {
            return SchoolSchedulePhase.MorningClass;
        }

        bool hasLunchGap = morningOk && afternoonOk && schedule.afternoonStartHour > schedule.morningEndHour;
        if (hasLunchGap && hour >= schedule.morningEndHour && hour < schedule.afternoonStartHour)
        {
            return SchoolSchedulePhase.Lunch;
        }

        if (afternoonOk && hour >= schedule.afternoonStartHour && hour < schedule.afternoonEndHour)
        {
            return SchoolSchedulePhase.AfternoonClass;
        }

        return SchoolSchedulePhase.AfterSchoolHours;
    }

    /// <summary>Đang trong khoảng phải có mặt trên sân trường (lớp hoặc nghỉ trưa tại trường).</summary>
    public static bool IsOnCampusSchoolHours(SchoolSchedulePhase phase)
    {
        return phase == SchoolSchedulePhase.MorningClass
               || phase == SchoolSchedulePhase.Lunch
               || phase == SchoolSchedulePhase.AfternoonClass;
    }

    /// <summary>Giờ bắt đầu buổi học đầu tiên trong ngày (sáng hoặc chỉ chiều).</summary>
    public static int GetFirstClassHour(ClassScheduleEntry schedule)
    {
        if (schedule == null) return 9;
        bool morningOk = schedule.morningEndHour > schedule.morningStartHour;
        bool afternoonOk = schedule.afternoonEndHour > schedule.afternoonStartHour;
        if (morningOk) return schedule.morningStartHour;
        if (afternoonOk) return schedule.afternoonStartHour;
        return 9;
    }

    /// <summary>Giờ kết thúc (exclusive) buổi học cuối trong ngày.</summary>
    public static int GetLastClassEndHour(ClassScheduleEntry schedule)
    {
        if (schedule == null) return 16;
        bool morningOk = schedule.morningEndHour > schedule.morningStartHour;
        bool afternoonOk = schedule.afternoonEndHour > schedule.afternoonStartHour;
        if (afternoonOk) return schedule.afternoonEndHour;
        if (morningOk) return schedule.morningEndHour;
        return 16;
    }

    public static bool HasClassBlocks(ClassScheduleEntry schedule)
    {
        if (schedule == null) return false;
        return schedule.morningEndHour > schedule.morningStartHour
               || schedule.afternoonEndHour > schedule.afternoonStartHour;
    }

    /// <summary>Trong ngày có buổi sáng: [morningCommuteStartHour, lịch morningStart) — sân trường trước giờ vào lớp (đồng bộ với <see cref="GameTimeManager"/>).</summary>
    public static bool IsOnCampusYardTimeBeforeFirstClass(
        int hour,
        ClassScheduleEntry schedule,
        int morningCommuteStartHour)
    {
        if (schedule == null) return false;
        bool morningOk = schedule.morningEndHour > schedule.morningStartHour;
        if (!morningOk) return false;

        return hour >= morningCommuteStartHour && hour < schedule.morningStartHour;
    }
}
