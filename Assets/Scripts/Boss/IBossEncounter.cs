namespace Slotgun
{
    /// <summary>
    /// One boss encounter, whatever shape it takes: a single generated monster, or a squad that
    /// shares a health pool. The run state, the HUD bar and the door all speak to this, so adding a
    /// new encounter type never reaches into the flow code again.
    /// </summary>
    public interface IBossEncounter
    {
        BossConfig Config { get; }
        float HealthFraction { get; }

        /// <summary>Current top ground speed, including enrage and weak-point surge.</summary>
        float ChaseSpeed { get; }
        int Phase { get; }
        string DisplayName { get; }

        event System.Action<IBossEncounter> Defeated;
    }
}
