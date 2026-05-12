# H??ng d?n thi?t l?p Dynamic Audio System cho SVSimulator
# H??ng d?n thi?t l?p Dynamic Audio System cho SVSimulator

Tài li?u này mô t? cách thi?t l?p h? th?ng âm thanh ??ng (nh?c n?n + ambient) theo scene và th?i gian trong ngày.

## 1) T?o `TimeOfDayAudioProfile`

1. Chu?t ph?i trong Project Window ? `Create` ? `SVSimulator/Audio/Time Of Day Audio Profile`.
2. ??t tên theo khung gi?, ví d?: `Morning`, `Afternoon`, `Evening`, `Night`.
3. C?u hình:
   - `timeRangeName`: tên hi?n th?.
   - `startHour` / `endHourExclusive`: gi? b?t ??u và gi? k?t thúc (exclusive).
   - `musicClip`: nh?c n?n cho khung gi?.
   - `ambientClips`: list ambient (chim, gió, côn trùng...).
   - `musicVolume` / `ambientVolume`: âm l??ng t??ng ??i (0..1).

Ví d? khung gi?:
- `06-12` Morning
- `12-18` Afternoon
- `18-22` Evening
- `22-06` Night

## 2) T?o `AreaAudioProfile`

1. Chu?t ph?i ? `Create` ? `SVSimulator/Audio/Area Audio Profile`.
2. ??t tên theo khu v?c (Dormitory, Campus, Library...).
3. Gán:
   - `areaName`: tên khu v?c.
   - `timeProfiles`: kéo các `TimeOfDayAudioProfile` vào m?ng.
   - `fadeDuration`: th?i gian fade in/out khi ??i nh?c.
   - `loopMusic`: b?t n?u nh?c n?n c?n l?p.
   - `useRandomAmbientClip`: b?t ?? ambient random khi có nhi?u clip.

## 3) Thêm `AudioManager` vào scene ??u tiên

1. T?o GameObject r?ng: `AudioManager`.
2. Add Component: `AudioManager`.
3. Gán `initialAreaProfile` (profile m?c ??nh khi vào game n?u scene ch?a c?u hình).
4. Trong `Scene Profiles`, t?o các ph?n t?:
   - `sceneName`: tên scene (?úng v?i `SceneAsset` ?ang dùng).
   - `areaProfile`: profile âm thanh t??ng ?ng v?i scene.
5. N?u không có `GameTimeManager` trong scene, gán `ManualTimeProvider` vào `Time Provider (Optional)` và ?i?u ch?nh gi? theo h? th?ng th?i gian c?a b?n.

## 4) Gán AudioClip

- Import nh?c n?n và ambient vào Unity (Assets).
- Kéo th? clip vào các profile.
- N?u có nhi?u ambient và `useRandomAmbientClip = true`, h? th?ng s? random t?ng clip.

## 5) Ví d? c?u hình theo scene

- `DormitoryScene` ? profile ký túc xá
- `CampusScene` ? profile sân tr??ng
- `LibraryScene` ? profile th? vi?n
- `ClassroomScene` ? profile l?p h?c
- `CafeteriaScene` ? profile c?n tin
- `EventHallScene` ? profile khu t? ch?c s? ki?n

## 6) Ki?m tra ho?t ??ng

1. Ch?y game t? scene ??u tiên.
2. Chuy?n scene và ki?m tra nh?c ??i m??t.
3. Ch?nh gi? (ho?c ?? th?i gian ch?y) ?? ki?m tra nh?c thay ??i theo khung gi?.
4. Console s? c?nh báo n?u:
   - Thi?u `AudioClip`.
   - Ch?ng l?n khung gi?.
   - Không có `TimeOfDayAudioProfile` phù h?p.
   - Scene ch?a gán `AreaAudioProfile`.

## 7) G?i ý c?u hình khung gi?

- `Morning`: `06-12`
- `Afternoon`: `12-18`
- `Evening`: `18-22`
- `Night`: `22-06`

## 8) G?i ý âm thanh UI

G?i t? code:
- `AudioManager.Instance.PlayUISound(clip)` cho hover/click/notification/popup.
- `AudioManager.Instance.SetMusicVolume(value)` (0..1).
- `AudioManager.Instance.SetSFXVolume(value)` (0..1).

Hoàn t?t, h? th?ng s? t? ??i nh?c theo scene và th?i gian trong ngày, có crossfade m??t và ambient ?a l?p.
