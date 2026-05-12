using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Đăng ký tên id → transform trong từng scene (Town, School, …) để <see cref="NpcSimpleMover2D"/> resolve.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-300)]
public class NpcScenePointRegistry : MonoBehaviour
{
    [Serializable]
    public class PointEntry
    {
        [Tooltip("Duy nhất trong scene, không phân biệt hoa thường khi tìm.")]
        public string id;
        public Transform point;
    }

    [SerializeField] private List<PointEntry> points = new List<PointEntry>();

    private readonly Dictionary<string, Transform> _map =
        new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        Rebuild();
    }

    public void Rebuild()
    {
        _map.Clear();
        if (points == null) return;
        for (int i = 0; i < points.Count; i++)
        {
            if (string.IsNullOrEmpty(points[i].id) || points[i].point == null) continue;
            _map[points[i].id.Trim()] = points[i].point;
        }
    }

    public bool TryGet(string id, out Transform t)
    {
        t = null;
        if (string.IsNullOrEmpty(id)) return false;
        if (_map.Count == 0) Rebuild();
        return _map.TryGetValue(id.Trim(), out t) && t != null;
    }

    /// <summary>Tìm trong mọi registry đang load; trả về nếu đúng 1, lỗi nếu trùng id giữa scene.</summary>
    public static bool TryGetPointInLoadedScenes(string id, out Transform t)
    {
        t = null;
        if (string.IsNullOrEmpty(id)) return false;
        var found = new List<Transform>(1);
        var regs = FindObjectsByType<NpcScenePointRegistry>(FindObjectsInactive.Include);
        for (int r = 0; r < regs.Length; r++)
        {
            if (regs[r] == null) continue;
            if (regs[r].TryGet(id, out var tr) && tr != null) found.Add(tr);
        }
        if (found.Count == 0) return false;
        t = found[0];
        return true;
    }
}
