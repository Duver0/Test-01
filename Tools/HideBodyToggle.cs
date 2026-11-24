using UnityEngine;

public static class HideBodyToggle
{
    public static void Execute()
    {
        var player = GameObject.Find("Player");
        if (player == null) { Debug.LogError("[HideBody] no Player"); return; }

        var body = player.transform.Find("Body");
        if (body == null) { Debug.LogError("[HideBody] no Body"); return; }

        bool now = !body.gameObject.activeSelf;
        body.gameObject.SetActive(now);
        Debug.Log("[HideBody] Body active=" + now);
    }
}
