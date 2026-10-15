using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Hashtable=ExitGames.Client.Photon.Hashtable;

public partial class GameplayManager
{
    private readonly StoryObjectiveState storyState=new StoryObjectiveState();
    private readonly List<StoryGenerator> storyGenerators=new List<StoryGenerator>();
    private PlayerController rescueShip;
    private bool storySetupFailed;
    private void SetupStoryObjective()
    {
        if(campaign.objective==StoryObjective.Eliminate||campaign.objective==StoryObjective.Survive)return;
        // โหมดสตอรี่ถูกออกแบบให้เล่นคนเดียว ไม่สร้างวัตถุ local ในห้องออนไลน์
        if(!PhotonNetwork.OfflineMode){storySetupFailed=true;Debug.LogError("Story objectives require a solo room");return;}
        int map=GetCurrentMapIndex();
        if(campaign.objective==StoryObjective.Generators)
        {
            for(int i=0;i<campaign.generators;i++)
            {
                if(!TryFindSafeSpawn(GetPrefab(BattleLoadoutCatalog.PrefabName(0)),map,false,out Vector2 point))
                {storySetupFailed=true;return;}
                var go=new GameObject("StoryGenerator"+i);go.transform.position=point;
                var generator=go.AddComponent<StoryGenerator>();generator.Build(i+1);storyGenerators.Add(generator);
                Physics2D.SyncTransforms();
            }
        }
        else
        {
            string prefab=BattleLoadoutCatalog.PrefabName(0);
            if(!TryFindSafeSpawn(GetPrefab(prefab),map,true,out Vector2 point)){storySetupFailed=true;return;}
            int id=PlayerController.BotIdBase+botSequence++;
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable{[MatchRules.TeamPrefix+id]=0,[MatchRules.BotKillPrefix+id]=0});
            var go=PhotonNetwork.InstantiateRoomObject(prefab,point,Quaternion.identity,0,
                new object[]{"BOT",id,"RESCUE SHIP",0,0,0,0,false});
            rescueShip=go.GetComponent<PlayerController>();go.AddComponent<StoryRescueShip>();
            var image=go.GetComponent<SpriteRenderer>();if(image!=null)image.color=new Color(.35f,1,1);
        }
    }
    private int DestroyedStoryGenerators()
    {int count=0;foreach(var core in storyGenerators)if(core!=null&&core.Destroyed)count++;return count;}
    private void UpdateStoryObjective()
    {
        if(storySetupFailed){SetModeResult(-1);return;}
        bool nearby=localPlayer!=null&&rescueShip!=null&&Vector2.Distance(localPlayer.transform.position,rescueShip.transform.position)<=10;
        storyState.Tick(Time.deltaTime,localPlayer!=null&&!localPlayer.isDead,rescueShip!=null&&!rescueShip.isDead,nearby,campaign.objective);
        if(storyState.Failed)SetModeResult(-1);
        else if(storyState.Complete(campaign,DestroyedStoryGenerators()))SetModeResult(1);
    }
    private string StoryObjectiveLabel()
    {
        if(campaign==null)return "";
        if(campaign.objective==StoryObjective.Survive)return "SURVIVE "+campaign.duration+" SECONDS";
        if(campaign.objective==StoryObjective.Generators)return "DESTROY "+campaign.generators+" RED CORES";
        if(campaign.objective==StoryObjective.Escort)return "PROTECT RESCUE SHIP / STAY WITHIN 10m TO REPAIR";
        return campaign.boss?"DESTROY THE BOSS":"DESTROY "+campaign.kills+" ENEMIES";
    }
    private string StoryProgressLabel()
    {
        if(campaign.objective==StoryObjective.Survive)return "SURVIVAL "+Mathf.FloorToInt(storyState.Elapsed)+" / "+campaign.duration+"s";
        if(campaign.objective==StoryObjective.Generators)
        {
            StoryGenerator nearest=null;float distance=float.PositiveInfinity;
            if(localPlayer!=null)foreach(var core in storyGenerators)
                if(core!=null&&!core.Destroyed){float d=Vector2.Distance(localPlayer.transform.position,core.transform.position);if(d<distance){distance=d;nearest=core;}}
            return "CORES "+DestroyedStoryGenerators()+" / "+campaign.generators+(nearest!=null?" / "+StoryDirection(nearest.transform.position):"");
        }
        if(campaign.objective==StoryObjective.Escort)return "REPAIR "+Mathf.FloorToInt(storyState.Elapsed)+" / "+campaign.duration+"s / RESCUE HP "+(rescueShip!=null?Mathf.CeilToInt(rescueShip.currentHp):320)
            +(rescueShip!=null?" / "+StoryDirection(rescueShip.transform.position):"");
        return campaign.boss?(BossAlive()?"DESTROY THE BOSS":"BOSS DOWN!"):"KILLS "+MatchRules.LocalKills()+" / "+campaign.kills;
    }
    private string StoryDirection(Vector2 destination)
    {
        if(localPlayer==null)return "";
        Vector2 delta=destination-(Vector2)localPlayer.transform.position;
        string direction=Mathf.Abs(delta.x)>Mathf.Abs(delta.y)?(delta.x>0?"EAST":"WEST"):(delta.y>0?"NORTH":"SOUTH");
        return Mathf.CeilToInt(delta.magnitude)+"m "+direction;
    }
}
