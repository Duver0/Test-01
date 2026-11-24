using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public static class SprintStaminaProbe
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    static Component Find(string typeName)
    {
        foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (mb != null && mb.GetType().Name == typeName) return mb;
        return null;
    }

    public static void Execute()
    {
        var p = Find("PlayerController");
        if (p == null) { Debug.Log("[STAM] no PlayerController"); return; }

        var t = p.GetType();
        var fCharge = t.GetField("_sprintCharge", Flags);
        var fLocked = t.GetField("_sprintLocked", Flags);
        var fSprint = t.GetField("_sprint", Flags);
        var mUpdate = t.GetMethod("UpdateStamina", Flags);
        var fSeconds = t.GetField("sprintSeconds", Flags);
        var fRecovery = t.GetField("sprintRecoverySeconds", Flags);
        var fUnlock = t.GetField("sprintUnlockFraction", Flags);

        if (fCharge == null || mUpdate == null) { Debug.Log("[STAM] reflection failed"); return; }

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float seconds = (float)fSeconds.GetValue(p);
        float recovery = (float)fRecovery.GetValue(p);
        float unlock = (float)fUnlock.GetValue(p);

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"[STAM] sprintSeconds={seconds} recoverySeconds={recovery} unlockAt={unlock} dt={dt:F4}");

        // 1. drain rate: a full bar must lose exactly dt/6 per sprint frame
        fLocked.SetValue(p, false);
        fCharge.SetValue(p, 1f);
        mUpdate.Invoke(p, new object[] { true });
        sb.AppendLine($"[STAM] full bar, 1 sprint frame -> {(float)fCharge.GetValue(p):F5} (expected {1f - dt / seconds:F5})");

        // 2. refill rate: 0.9 must climb by dt/3 per rest frame
        fCharge.SetValue(p, 0.9f);
        mUpdate.Invoke(p, new object[] { false });
        sb.AppendLine($"[STAM] 0.9 bar, 1 rest frame -> {(float)fCharge.GetValue(p):F5} (expected {0.9f + dt / recovery:F5})");

        // 3. the frame the bar empties must latch the lockout and stop the sprint
        fLocked.SetValue(p, false);
        fCharge.SetValue(p, 0.0005f);
        mUpdate.Invoke(p, new object[] { true });   // the draining frame that runs the bar out
        bool lockedAfterEmpty = (bool)fLocked.GetValue(p);
        mUpdate.Invoke(p, new object[] { true });   // still holding shift on an empty bar
        bool sprintWhenEmpty = (bool)fSprint.GetValue(p);
        sb.AppendLine($"[STAM] bar ran out while held -> locked={lockedAfterEmpty} " +
                      $"sprintWhileEmpty={sprintWhenEmpty} (want locked=True, sprint=False)");

        // 4. the lock holds until the bar climbs back to the unlock fraction
        fCharge.SetValue(p, unlock - 0.02f);
        mUpdate.Invoke(p, new object[] { true });
        bool blocked = !(bool)fSprint.GetValue(p) && (bool)fLocked.GetValue(p);
        fCharge.SetValue(p, unlock + 0.02f);
        mUpdate.Invoke(p, new object[] { true });
        bool resumed = (bool)fSprint.GetValue(p) && !(bool)fLocked.GetValue(p);
        sb.AppendLine($"[STAM] below unlock blocked={blocked}; above unlock sprint resumes={resumed}");

        sb.AppendLine($"[STAM] budget: full bar = {seconds:F1}s of sprint, refill = {recovery:F1}s of rest");
        Debug.Log(sb.ToString());

        // 5. HUD bar follows SprintFraction (checked a frame later, once HUD.Update has refreshed)
        fLocked.SetValue(p, true);
        fCharge.SetValue(p, 0.4f);
        ((MonoBehaviour)p).StartCoroutine(HudCheck(p, fCharge, fLocked));
    }

    static IEnumerator HudCheck(Component p, FieldInfo fCharge, FieldInfo fLocked)
    {
        // Hold the charge at 0.4 for a few frames so the value HUD.Update reads is unambiguous.
        for (int i = 0; i < 3; i++)
        {
            fCharge.SetValue(p, 0.4f);
            yield return null;
        }

        var hud = Find("HUD");
        Image fill = null;
        if (hud != null)
        {
            var fFill = hud.GetType().GetField("_staminaFill", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            fill = fFill != null ? fFill.GetValue(hud) as Image : null;
        }

        float charge = (float)fCharge.GetValue(p);
        Debug.Log(fill == null
            ? "[STAM] HUD stamina fill NOT FOUND"
            : $"[STAM] charge={charge:F3} -> HUD fill anchorMax.x={fill.rectTransform.anchorMax.x:F3} color={fill.color}");

        fCharge.SetValue(p, 1f);
        fLocked.SetValue(p, false);
    }
}
