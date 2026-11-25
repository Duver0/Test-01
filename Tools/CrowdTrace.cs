using System.Text;
using UnityEngine;
using Slotgun;

/// <summary>
/// Samples the wave every half second while the game runs, so a single reading cannot be dismissed
/// as a lucky frame: the deepest pair, how many capsules intersect, and the frame rate the crowd
/// solver is being asked to keep up with.
/// </summary>
public static class CrowdTrace
{
    static float _until;
    static float _next;
    static float _frames;
    static float _time;
    static int _samples;
    static float _worst = float.MaxValue;
    static int _worstTouching;

    public static void Execute()
    {
        _until = Time.time + 25f;
        _next = 0f;
        _frames = 0f;
        _time = 0f;
        _samples = 0;
        _worst = float.MaxValue;
        _worstTouching = 0;

        UnityEditor.EditorApplication.update -= Tick;
        UnityEditor.EditorApplication.update += Tick;
        Debug.Log("[CrowdTrace] armed for 25 s");
    }

    static void Tick()
    {
        if (!Application.isPlaying) { Stop(); return; }

        _frames++;
        _time += Time.unscaledDeltaTime;

        if (Time.time < _next) return;
        _next = Time.time + 0.5f;

        var units = Object.FindObjectsByType<EnemyUnit>(FindObjectsSortMode.None);
        var zombies = Object.FindObjectsByType<ZombieEnemyUnit>(FindObjectsSortMode.None);

        var points = new System.Collections.Generic.List<Vector3>();
        foreach (var u in units)
            if (u != null && u.IsAlive) points.Add(u.transform.position);
        foreach (var z in zombies)
            if (z != null && z.IsAlive) points.Add(z.transform.position);

        float closest = float.MaxValue;
        int touching = 0;
        int packed = 0;

        for (int i = 0; i < points.Count; i++)
        {
            for (int j = i + 1; j < points.Count; j++)
            {
                Vector3 d = points[i] - points[j];
                d.y = 0f;
                float m = d.magnitude;
                if (m < closest) closest = m;
                if (m < 0.70f) touching++;
                if (m < 1.05f) packed++;
            }
        }

        if (points.Count < 2) closest = -1f;
        if (closest >= 0f && closest < _worst) _worst = closest;
        if (touching > _worstTouching) _worstTouching = touching;

        float fps = _time > 0.001f ? _frames / _time : 0f;
        _frames = 0f;
        _time = 0f;
        _samples++;

        var line = new StringBuilder("[CrowdTrace] t=");
        line.Append(Time.time.ToString("F1"))
            .Append(" fps=").Append(fps.ToString("F0"))
            .Append(" bodies=").Append(points.Count)
            .Append(" closest=").Append(closest.ToString("F2"))
            .Append(" touching<0.70=").Append(touching)
            .Append(" packed<1.05=").Append(packed)
            .Append(" | worst so far=").Append(_worst.ToString("F2"))
            .Append(" touching").Append(_worstTouching);

        if (Time.time >= _until)
        {
            line.Append("\n[CrowdTrace] DONE samples=").Append(_samples)
                .Append(" worst closest=").Append(_worst.ToString("F2"))
                .Append(" worst touching=").Append(_worstTouching);
        }

        Debug.Log(line.ToString());

        if (Time.time >= _until) Stop();
    }

    static void Stop()
    {
        UnityEditor.EditorApplication.update -= Tick;
    }
}
