using UnityEngine;

public enum StoryObjective { Eliminate, Survive, Generators, Escort }

// แยกตัวนับจากภาพ/Photon เพื่อให้เงื่อนไขผ่านด่านตรวจสอบได้ตรง ๆ
public sealed class StoryObjectiveState
{
    public float Elapsed { get; private set; }
    public bool Failed { get; private set; }
    public void Tick(float dt, bool playerAlive, bool escortAlive, bool nearby, StoryObjective objective)
    {
        if (Failed) return;
        if (objective == StoryObjective.Escort && !escortAlive) { Failed=true;return; }
        if (playerAlive && (objective != StoryObjective.Escort || nearby)) Elapsed+=Mathf.Clamp(dt,0,.25f);
    }
    public bool Complete(CampaignStageDef def,int destroyed)
        => !Failed && (def.objective==StoryObjective.Generators ? destroyed>=def.generators
            : def.objective!=StoryObjective.Eliminate && Elapsed>=def.duration);
}
