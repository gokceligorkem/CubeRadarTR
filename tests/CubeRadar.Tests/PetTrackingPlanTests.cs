using CubeRadar.Core.Pets;

namespace CubeRadar.Tests;

public class PetTrackingPlanTests
{
    static PetSnapshot View(bool synced=true)=>new(1,1,synced,"",new Dictionary<int,PetProgress>
    {
        [1002]=new(1,13,true),[1003]=new(2,0,true),[1004]=new(3,0,true),[1005]=new(1,0,false),[1006]=new(2,2,true)
    },Enumerable.Range(1002,4).Select((id,i)=>new PetMob((uint)i+1,2000000+i,id,"",null,null,null,100,0)).ToArray(),0);
    [Fact] public void Nearby_verified_missing_pets_are_automatic_but_max_unknown_and_distant_pets_are_not()
    {
        var plan=new Dictionary<int,int>();Assert.True(PetTrackingPlan.Update(plan,new HashSet<int>(),View()));
        Assert.Equal(2,plan.Count);Assert.Equal(2,plan[1002]);Assert.Equal(3,plan[1003]);Assert.DoesNotContain(1004,plan.Keys);Assert.DoesNotContain(1005,plan.Keys);Assert.DoesNotContain(1006,plan.Keys);
        Assert.False(PetTrackingPlan.Update(plan,new HashSet<int>(),View()));
    }
    [Fact] public void An_opt_out_overrides_nearby_and_saved_manual_tracking_until_explicitly_reselected()
    {
        var manual=new Dictionary<int,int>{{1002,3}};var excluded=new HashSet<int>{1002};
        PetTrackingPlan.Update(manual,excluded,View());Assert.DoesNotContain(1002,manual.Keys);
        PetTrackingPlan.Update(manual,new HashSet<int>(excluded),View());Assert.DoesNotContain(1002,manual.Keys);
        excluded.Remove(1002);PetTrackingPlan.Update(manual,excluded,View());Assert.Equal(2,manual[1002]);
    }
    [Fact] public void A_tracked_pet_remains_after_its_last_nearby_mob_dies_so_the_reward_and_goal_are_not_lost()
    {
        var manual=new Dictionary<int,int>{{1006,3}};var view=View();
        PetTrackingPlan.Update(manual,new HashSet<int>(),view);Assert.Equal(3,manual.Count);
        Assert.False(PetTrackingPlan.Update(manual,new HashSet<int>(),view with {Nearby=[]}));Assert.Equal(3,manual.Count);Assert.Equal(3,manual[1006]);
    }
    [Fact] public void No_automatic_tracking_is_created_before_collection_sync()
    {
        var manual=new Dictionary<int,int>{{1006,3}};
        Assert.False(PetTrackingPlan.Update(manual,new HashSet<int>(),View(false)));Assert.Single(manual);Assert.Equal(3,manual[1006]);
    }
    [Fact] public void Opting_out_stops_completion_alerts_without_changing_the_soul_balance()
    {
        var goals=new Dictionary<int,int>();var excluded=new HashSet<int>();var view=View();var gate=new PetGoalGate();
        PetTrackingPlan.Update(goals,excluded,view);gate.Take(view,goals);excluded.Add(1002);PetTrackingPlan.Update(goals,excluded,view);
        var progress=view.Progress.ToDictionary();progress[1002]=new(1,25,true,GainSerial:1);
        Assert.Empty(gate.Take(view with {Progress=progress},goals));Assert.Equal(12,view.Progress[1002].Remaining(2));
    }
    [Fact] public void A_pet_ready_for_an_upgrade_stays_tracked_until_the_player_removes_it_or_hides_max()
    {
        var goals=new Dictionary<int,int>{{1003,3}};var view=View();var progress=view.Progress.ToDictionary();progress[1003]=new(2,75,true);
        PetTrackingPlan.Update(goals,new HashSet<int>(),view with {Progress=progress,Nearby=[]});Assert.Equal(3,goals[1003]);
    }
}
