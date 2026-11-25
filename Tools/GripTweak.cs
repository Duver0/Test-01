using UnityEngine;
using Slotgun;

/// <summary>
/// Live nudge of the two support-hand offsets, so a candidate can be rendered before it is written
/// into the scene. Play-mode only: nothing is saved.
/// </summary>
public static class GripTweak
{
    public static void Execute(Newtonsoft.Json.Linq.JObject args)
    {
        var player = GameObject.Find("Player");
        var body = player != null ? player.transform.Find("Body") : null;
        var arms = body != null ? body.GetComponent<FirstPersonArms>() : null;
        if (arms == null) { Debug.Log("[GripTweak] no arms"); return; }

        if (args["back"] != null) arms.foregripBack = (float)args["back"];
        if (args["down"] != null) arms.foregripDown = (float)args["down"];

        Debug.Log("[GripTweak] foregripBack=" + arms.foregripBack.ToString("F3")
                  + " foregripDown=" + arms.foregripDown.ToString("F3"));
    }
}
