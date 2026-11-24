using System.Text;
using UnityEngine;

public static class BodyPartsProbe
{
    public static void Execute()
    {
        var sb = new StringBuilder();

        var player = GameObject.Find("Player");
        if (player == null) { Debug.Log("[Probe] no Player"); return; }

        var body = player.transform.Find("Body");
        if (body == null) { Debug.Log("[Probe] no Body"); return; }

        var cam = player.GetComponentInChildren<Camera>(true);
        if (cam != null)
            sb.AppendLine("[Probe] camera world y = " + cam.transform.position.y.ToString("F3") +
                          "  local y = " + player.transform.InverseTransformPoint(cam.transform.position).y.ToString("F3"));

        foreach (var r in body.GetComponentsInChildren<Renderer>(true))
        {
            var b = r.bounds;
            float minLocal = player.transform.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z)).y;
            float maxLocal = player.transform.InverseTransformPoint(new Vector3(b.center.x, b.max.y, b.center.z)).y;

            string mats = "";
            for (int i = 0; i < r.sharedMaterials.Length; i++)
                if (r.sharedMaterials[i] != null) mats += r.sharedMaterials[i].name + " ";

            sb.AppendLine(string.Format("{0,-34} enabled={1,-5} type={2,-20} yLocal={3:F2}..{4:F2} mats={5}",
                r.transform.name, r.enabled, r.GetType().Name, minLocal, maxLocal, mats));
        }

        Debug.Log(sb.ToString());
    }
}
