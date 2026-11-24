using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Text;
using System.Collections.Generic;

public static class DarkPropProbe
{
    public static string Execute()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Slotgun_Sector_1.unity", OpenSceneMode.Additive);
        var sb = new StringBuilder();
        var report = new List<string>();

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null)
                    {
                        report.Add("NULLMAT   " + Full(r.transform) + "  shader=<none>");
                        continue;
                    }

                    string sh = m.shader != null ? m.shader.name : "<null>";
                    bool urp = sh.StartsWith("Universal Render Pipeline");
                    var color = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor")
                              : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                    float lum = Mathf.Max(color.r, Mathf.Max(color.g, color.b));

                    if (!urp || lum < 0.12f)
                    {
                        report.Add((urp ? "DARK  " : "NONURP") + " lum=" + lum.ToString("0.00") +
                                   "  " + Full(r.transform) + "  mat=" + m.name + "  shader=" + sh);
                    }
                }
            }
        }

        if (report.Count == 0) sb.AppendLine("no dark / non-URP renderers found");
        foreach (var line in report) sb.AppendLine(line);

        EditorSceneManager.CloseScene(scene, true);
        return sb.ToString();
    }

    static string Full(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}
