using UnityEngine;

public static class LookDown
{
    public static void Execute()
    {
        var player = GameObject.Find("Player");
        if (player == null) { Debug.Log("[LookDown] no player"); return; }

        var pivot = player.transform.Find("CameraPivot");
        if (pivot == null) { Debug.Log("[LookDown] no CameraPivot"); return; }

        var controller = player.GetComponent("PlayerController") as MonoBehaviour;
        if (controller != null) controller.enabled = false;

        pivot.localRotation = Quaternion.Euler(58f, 0f, 0f);

        var body = player.transform.Find("Body");
        int visible = 0;
        if (body != null)
            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
                if (r.enabled && r.gameObject.activeInHierarchy) visible++;

        // Freeze the run so a boss cannot land a hit between the pitch and the screenshot.
        Time.timeScale = 0f;

        Debug.Log("[LookDown] pivot 58 down, controller off, boss frozen, visible body renderers = " + visible);
    }
}
