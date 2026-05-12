# SVSimulator - Day 6 Arcade Event

## 1. Gi?i thi?u
S? ki?n di?n ra vào sáng Ngày Th? 6 (Day 6), khi ng??i ch?i l?n ??u ra kh?i nhà sau 9:00 sáng.
Lê Toàn Th?ng s? ??n r? ng??i ch?i ?i ch?i ?i?n t?.

## 2. Lu?ng s? ki?n
- **Kích ho?t:** `GameTimeManager.Instance.DayInSemester == 6` và `Hour >= 9`, cùng v?i c?nh (scene) phù h?p (ví d?: `21_SchoolArea` ho?c `20_CampusOutdoors`).
- **H?i tho?i ch?n l?a:**
  - **??ng ý ("?i luôn!"):** 
    - Th?c hi?n màn hình ?en (Fade). Th?i gian t?i nhanh ??n 23:00.
    - Energy -70.
    - B?t c? khóa không cho dùng bàn h?c ? nhà làm bài t?p (`GlobalBlockForArcadeEvent = true`). Ng??i ch?i ph?i ?i ng?.
    - Ngày Th? 7 (Day 7) s? th?c d?y mu?n vào lúc 13:00 và m?t 0.3 GPA kèm thông báo.
  - **T? ch?i ("Thôi, mình còn vi?c khác."):** Không có gì x?y ra, th?i gian ti?p t?c ch?y bình th??ng.

## 3. Các c? tr?ng thái
Các c? này ???c l?u tr? trong `StoryEventManager`:
- `day6EventTriggered`: ?ánh d?u s? ki?n Day 6 ?ã ???c ch?y (không ch?y l?i l?n 2).
- `day6AcceptedArcadeInvite`: C? ?ánh d?u ng??i ch?i ?ã ??ng ý ?i ch?i.
- `forceSleepTonight`: Yêu c?u ng??i ch?i ph?i ?i ng? t?i nay.
- `wakeUpLateAfterArcade`: ?ánh d?u l?ch trình th?c d?y Day 7 s? b? tr?.
- `lateWakePenaltyApplied`: Ki?m tra xem ?ã áp d?ng ph?t ng? quên hay ch?a.

## 4. Các file ?ã ch?nh s?a
- `Assets\Scripts\Core\Events\StoryEventManager.cs`: Thêm x? lý `TryTriggerDay6ArcadeEvent` và `TryApplyDay7WakeUpPenalty`.
- `Assets\Scripts\Interaction\InteractableObject\HomeComputerInteractable.cs`: Thêm c? khóa `GlobalBlockForArcadeEvent` và ch?n ng??i ch?i không cho dùng bàn h?c.

## 5. C?u hình trong Unity Inspector
- M? `StoryEventManager` GameObject trong Scene kh?i t?o.
- (Tùy ch?n) G?n Prefab Lê Toàn Th?ng (g?i ho?c spawn) n?u m? r?ng ch?c n?ng xu?t hi?n ??i t??ng NPC b?ng Visual trong Inspector.
