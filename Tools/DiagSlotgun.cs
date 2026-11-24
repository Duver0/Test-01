using UnityEngine;
using UnityEngine.SceneManagement;

public static class DiagSlotgun
{
    public static string Execute()
    {
        var scene = SceneManager.GetActiveScene();
        var direct = Object.FindAnyObjectByType<Slotgun.GameDirector>();
        var player = Object.FindAnyObjectByType<Slotgun.PlayerController>();
        var cc = player != null ? player.GetComponent<CharacterController>() : null;
        var boss = Object.FindAnyObjectByType<Slotgun.BossController>();
        var cam = player != null ? player.cam : null;

        string playerInfo = player != null
            ? $"playerPos={player.transform.position} grounded={(cc != null && cc.isGrounded)} yaw={player.transform.eulerAngles.y:0}"
            : "player=null";
        string camInfo = cam != null ? $"camPos={cam.transform.position} camRot={cam.transform.eulerAngles}" : "cam=null";
        string bossInfo = boss != null
            ? $"boss={boss.DisplayName} hp={boss.Current:0}/{boss.MaxHealth:0} phase={boss.Phase} pos={boss.transform.position} parts={boss.transform.childCount}"
            : "boss=none";
        int proj = Object.FindObjectsByType<Slotgun.Projectile>(FindObjectsSortMode.None).Length;

        return string.Join("\n",
            $"isPlaying={Application.isPlaying} scene={scene.name}",
            $"directorInstance={(Slotgun.GameDirector.Instance != null)} phase={(direct != null ? direct.Phase.ToString() : "?")} round={(direct != null ? direct.Round : 0)} money={(direct != null ? direct.Money : 0)}",
            playerInfo,
            camInfo,
            bossInfo,
            $"projectiles={proj} playerHp={(direct != null && direct.playerHealth != null ? direct.playerHealth.Current : 0f):0}");
    }
}
