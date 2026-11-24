using System;
using System.Reflection;
using System.Text;
using UnityEngine;

public static class HudProbe
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    static object Read(object target, string name)
    {
        if (target == null) return "<no target>";
        var t = target.GetType();
        var f = t.GetField(name, Flags);
        if (f != null) return f.GetValue(target);
        var p = t.GetProperty(name, Flags);
        if (p != null) return p.GetValue(target);
        return "<missing " + name + ">";
    }

    static MonoBehaviour Find(string typeName)
    {
        foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (mb != null && mb.GetType().Name == typeName) return mb;
        return null;
    }

    public static void Execute()
    {
        var sb = new StringBuilder();
        sb.AppendLine("isPlaying=" + Application.isPlaying);

        var scenes = new StringBuilder();
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            scenes.Append(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).name).Append(" ");
        sb.AppendLine("scenes=" + scenes);

        var hud = Find("HUD");
        if (hud == null) sb.AppendLine("HUD: NOT FOUND");
        else
        {
            sb.AppendLine($"HUD go={hud.gameObject.name} enabled={hud.enabled} activeInHierarchy={hud.gameObject.activeInHierarchy}");
            sb.AppendLine($"  _director={(Read(hud, "_director") == null ? "NULL" : "ok")} _health={(Read(hud, "_health") == null ? "NULL" : "ok")} _weapon={(Read(hud, "_weapon") == null ? "NULL" : "ok")}");
            var healthText = Read(hud, "_healthText") as UnityEngine.UI.Text;
            sb.AppendLine("  healthText=" + (healthText == null ? "NULL" : healthText.text));
            var moneyText = Read(hud, "_moneyText") as UnityEngine.UI.Text;
            sb.AppendLine("  moneyText=" + (moneyText == null ? "NULL" : moneyText.text));
            var ammoText = Read(hud, "_ammoText") as UnityEngine.UI.Text;
            sb.AppendLine("  ammoText=" + (ammoText == null ? "NULL" : ammoText.text.Replace("\n", " | ")));
            var hf = Read(hud, "_healthFill") as UnityEngine.UI.Image;
            sb.AppendLine("  healthFill=" + (hf == null ? "NULL" : "anchorMax=" + hf.rectTransform.anchorMax.ToString("F2") + " color=" + hf.color));
            var bf = Read(hud, "_bossFill") as UnityEngine.UI.Image;
            sb.AppendLine("  bossFill=" + (bf == null ? "NULL" : "anchorMax=" + bf.rectTransform.anchorMax.ToString("F2")));
            var bossRoot = Read(hud, "_bossRoot") as GameObject;
            sb.AppendLine("  bossRoot=" + (bossRoot == null ? "NULL" : bossRoot.activeSelf.ToString()));
            var endRoot = Read(hud, "_endRoot") as GameObject;
            sb.AppendLine("  endRoot=" + (endRoot == null ? "NULL" : endRoot.activeSelf.ToString()));
            var loadingRoot = Read(hud, "_loadingRoot") as GameObject;
            sb.AppendLine("  loadingRoot=" + (loadingRoot == null ? "NULL" : loadingRoot.activeSelf.ToString()));
        }

        var director = Find("GameDirector");
        if (director == null) sb.AppendLine("GameDirector: NOT FOUND");
        else
        {
            sb.AppendLine($"GameDirector Instance={(Read(director, "Instance") == null ? "NULL" : "ok")} Phase={Read(director, "Phase")} Money={Read(director, "Money")} Round={Read(director, "Round")}");
            var boss = Read(director, "CurrentBoss");
            sb.AppendLine("  CurrentBoss=" + (boss == null ? "NULL" : boss.GetType().Name));
            if (boss != null) sb.AppendLine("  bossHealthFraction=" + Read(boss, "HealthFraction"));
        }

        var health = Find("PlayerHealth");
        if (health == null) sb.AppendLine("PlayerHealth: NOT FOUND");
        else sb.AppendLine($"PlayerHealth Current={Read(health, "Current")} max={Read(health, "maxHealth")} go={health.gameObject.name} active={health.gameObject.activeInHierarchy}");

        var cubes = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine("canvases=" + cubes.Length);
        foreach (var c in cubes)
            sb.AppendLine($"  canvas {c.name} enabled={c.enabled} active={c.gameObject.activeInHierarchy} scale={c.transform.lossyScale}");

        Debug.Log("[HudProbe]\n" + sb);
    }

    public static void Font()
    {
        var legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Debug.Log($"[HudProbe] font LegacyRuntime.ttf={(legacy == null ? "NULL" : legacy.name)}");

        var textType = Type.GetType("UnityEngine.UI.Text, UnityEngine.UI");
        var imgType = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI");
        Debug.Log($"[HudProbe] types text={(textType == null ? "NULL" : textType.FullName)} image={(imgType == null ? "NULL" : imgType.FullName)}");

        var go = new GameObject("Probe", typeof(RectTransform));
        var img = go.AddComponent(imgType);
        var txt = go.AddComponent(textType) as Component;
        Debug.Log($"[HudProbe] img={(img == null ? "NULL" : "ok")} txt={(txt == null ? "NULL" : "ok")}");
        UnityEngine.Object.Destroy(go);
    }

    public static void Prep()
    {
        var director = Find("GameDirector");
        if (director == null) { Debug.Log("[HudProbe] no director"); return; }

        var m = director.GetType().GetMethod("EnterPrepRoom", Flags);
        if (m == null) { Debug.Log("[HudProbe] no EnterPrepRoom"); return; }

        director.StartCoroutine((System.Collections.IEnumerator)m.Invoke(director, null));
        Debug.Log("[HudProbe] EnterPrepRoom started");
    }

    public static void PrepShow()
    {
        var director = Find("GameDirector");
        if (director == null) { Debug.Log("[HudProbe] no director"); return; }

        var hp = Find("PlayerHealth");
        if (hp != null) hp.GetType().GetMethod("ResetHealth", Flags).Invoke(hp, null);

        director.StartCoroutine(PrepThenShow(director));
    }

    static System.Collections.IEnumerator PrepThenShow(MonoBehaviour director)
    {
        var m = director.GetType().GetMethod("EnterPrepRoom", Flags);
        yield return director.StartCoroutine((System.Collections.IEnumerator)m.Invoke(director, null));

        float until = Time.realtimeSinceStartup + 8f;
        while (Time.realtimeSinceStartup < until && Read(director, "Phase").ToString() != "Prep") yield return null;

        PrepUi();
    }

    public static void PrepUi()
    {
        var director = Find("GameDirector");
        if (director == null) { Debug.Log("[HudProbe] no director"); return; }

        director.GetType().GetMethod("OpenBetting", Flags).Invoke(director, null);

        var sb = new StringBuilder();
        sb.AppendLine($"Phase={Read(director, "Phase")} PrepUiOpen={Read(director, "PrepUiOpen")}");

        var machine = Read(director, "machine");
        if (machine == null) sb.AppendLine("machine=NULL");
        else
            sb.AppendLine($"machine safe={Read(machine, "safeCost")} risky={Read(machine, "riskyCost")} " +
                          $"degen={Read(machine, "degenCost")} direct={Read(machine, "directCost")}");

        var hud = Find("HUD");
        if (hud == null) { Debug.Log("[HudProbe] no HUD"); return; }

        var prepRoot = Read(hud, "_prepRoot") as GameObject;
        sb.AppendLine("prepRootActive=" + (prepRoot == null ? "NULL" : prepRoot.activeSelf.ToString()));

        var result = Read(hud, "_resultText") as UnityEngine.UI.Text;
        sb.AppendLine("resultText=" + (result == null ? "NULL" : result.text));

        var spin = Read(hud, "_spinLabels") as UnityEngine.UI.Text[];
        if (spin != null)
            for (int i = 0; i < spin.Length; i++)
                sb.AppendLine($"spin[{i}]={(spin[i] == null ? "NULL" : spin[i].text)}");

        var direct = Read(hud, "_directLabels") as UnityEngine.UI.Text[];
        if (direct != null)
            for (int i = 0; i < direct.Length; i++)
                sb.AppendLine($"direct[{i}]={(direct[i] == null ? "NULL" : direct[i].text.Replace("\n", " "))}");

        Debug.Log("[HudProbe][PREP]\n" + sb);
    }

    public static void Hurt()
    {
        var health = Find("PlayerHealth");
        if (health == null) { Debug.Log("[HudProbe] no PlayerHealth"); return; }

        var takeDamage = health.GetType().GetMethod("TakeDamage", Flags);
        takeDamage.Invoke(health, new object[] { 40f, Vector3.zero, Vector3.zero });
        Debug.Log("[HudProbe] hurt 40 -> Current=" + Read(health, "Current"));
    }

    public static void Kill()
    {
        var health = Find("PlayerHealth");
        if (health == null) { Debug.Log("[HudProbe] no PlayerHealth"); return; }

        var takeDamage = health.GetType().GetMethod("TakeDamage", Flags);
        takeDamage.Invoke(health, new object[] { 500f, Vector3.zero, Vector3.zero });
        Debug.Log("[HudProbe] killed -> Current=" + Read(health, "Current"));
        Execute();
    }
}
