using UnityEngine;

public class PingProbe
{
    public static void Execute()
    {
        var player = Object.FindFirstObjectByType<Slotgun.PlayerController>();
        Debug.Log("[PING] player=" + (player != null ? player.name : "none") + " pos=" + player.transform.position);
    }
}
