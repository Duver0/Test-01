using System.Text;
using UnityEngine;
using Slotgun;

public static class NearProbe
{
    public static void Execute()
    {
        var arms = Object.FindAnyObjectByType<FirstPersonArms>();
        var grip = arms.grip.position;
        var sb = new StringBuilder("NP near grip:\n");

        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!r.enabled) continue;
            float d = Vector3.Distance(r.bounds.center, grip);
            if (d > 0.8f) continue;
            var m = r.sharedMaterial;
            string col = "?";
            if (m != null)
            {
                Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : (m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white);
                col = c.ToString("F2") + (m.IsKeywordEnabled("_EMISSION") ? "+emit" : "");
            }
            string path = r.name;
            var t = r.transform.parent;
            for (int i = 0; i < 3 && t != null; i++) { path = t.name + "/" + path; t = t.parent; }
            sb.AppendLine("  " + path + " d=" + d.ToString("F2") + " size=" + r.bounds.size.ToString("F2") + " mat=" + (m != null ? m.name : "null") + " col=" + col);
        }

        Debug.Log(sb.ToString());
    }
}
