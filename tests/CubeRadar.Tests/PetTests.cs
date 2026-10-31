using System.Buffers.Binary;
using CubeRadar.Core.Pets;

namespace CubeRadar.Tests;

public class PetTests
{
    static byte[] U(params int[] values)=>values.SelectMany(BitConverter.GetBytes).ToArray();
    static byte[] Snapshot(int rank=1,int souls=13)=>[..U(1002),1,..U(1002,1002,rank),1,..U(1002,souls)];
    static byte[] Reward(int n)=>[1,..U(1002,n)];
    [Fact] public void Login_and_rewards_use_actual_points_and_distinct_frames_not_kills()
    {
        var tracker=new PetTracker();Assert.Null(tracker.Read(0).Progress.GetValueOrDefault(1002));
        tracker.OnFrame(0x0090,Snapshot(),1000,1110,1,1);
        Assert.Equal(12,tracker.Read(1000).Progress[1002].Remaining(2));
        tracker.OnFrame(0x0D90,Reward(3),2000,1110,1,2);
        tracker.OnFrame(0x0D90,Reward(3),2000,1110,1,2);
        Assert.Equal(16,tracker.Read(2000).Progress[1002].Souls);
        tracker.OnFrame(0x0D90,Reward(3),2100,1110,1,3);
        Assert.Equal(19,tracker.Read(2100).Progress[1002].Souls);
        Assert.Equal(81,tracker.Read(2100).Progress[1002].Remaining(3));
    }
    [Fact] public void Unknown_login_gap_and_invalid_batch_never_create_a_false_balance()
    {
        var tracker=new PetTracker();tracker.OnFrame(0x0D90,Reward(1),100,1110,1,1);
        Assert.Null(tracker.Read(100).Progress[1002].Remaining(2));
        tracker.OnFrame(0x0090,Snapshot(),200,1110,1,2);tracker.Invalidate();
        Assert.Null(tracker.Read(200).Progress[1002].Remaining(2));
        tracker.OnFrame(0x0090,Snapshot(),300,1110,1,3);
        tracker.OnFrame(0x0D90,[2,..U(1002,1,999999,1)],400,1110,1,4);
        Assert.Null(tracker.Read(400).Progress[1002].Remaining(2));
        tracker.OnFrame(0x0090,Snapshot(2,4),500,1110,1,5);
        Assert.Equal(71,tracker.Read(500).Progress[1002].Remaining(3));
    }
    [Theory][InlineData(true)][InlineData(false)]
    public void Upgrade_spend_and_rank_can_arrive_in_either_order(bool spendFirst)
    {
        var tracker=new PetTracker();tracker.OnFrame(0x0090,Snapshot(1,25),100,1110);
        byte[] rank=[0,0,..U(1002,2),..new byte[13]];
        tracker.OnFrame(spendFirst?(ushort)0x0E90:(ushort)0x1490,spendFirst?Reward(25):rank,200,1110);
        Assert.Null(tracker.Read(200).Progress[1002].Remaining(3));
        tracker.OnFrame(spendFirst?(ushort)0x1490:(ushort)0x0E90,spendFirst?rank:Reward(25),300,1110);
        Assert.Equal(75,tracker.Read(300).Progress[1002].Remaining(3));
        tracker.OnFrame(0x0D90,Reward(2),400,1110);Assert.Equal(73,tracker.Read(400).Progress[1002].Remaining(3));
    }
    [Fact] public void Nearby_mob_mapping_and_removal_do_not_advance_pet_souls()
    {
        var tracker=new PetTracker();var source=PetCatalog.Default.Sources.First();
        byte[] spawn=[123,0x0c,0x20,0,..U(source.NpcId),0,2,..BitConverter.GetBytes(12000f),..BitConverter.GetBytes(-85000f),..BitConverter.GetBytes(500f)];
        tracker.OnFrame(0x4136,spawn,100,1110);
        Assert.Single(tracker.Read(100).Nearby);Assert.Equal(source.PetId,tracker.Read(100).Nearby[0].PetId);
        tracker.OnFrame(0x4236,[123,0,3],200,1110);Assert.Empty(tracker.Read(200).Nearby);Assert.Empty(tracker.Read(200).Progress);
        tracker.OnFrame(0x4136,spawn,300,1110);tracker.OnFrame(0x9999,[],400,20);Assert.Empty(tracker.Read(400).Nearby);
        tracker.OnFrame(0x4136,spawn,500,20);Assert.Empty(tracker.Read(100000).Nearby);
        byte[] unrelated=[123,0x0c,0x20,0,..U(9999999),0,2];tracker.OnFrame(0x4136,unrelated,600,20);Assert.Empty(tracker.Read(600).Nearby);
    }
    [Fact] public void Completion_only_fires_after_a_real_gain_and_never_on_login_or_goal_selection()
    {
        var gate=new PetGoalGate();var tracker=new PetTracker();var goals=new Dictionary<int,int>{{1002,2}};
        tracker.OnFrame(0x0090,Snapshot(1,24),100,1110);Assert.Empty(gate.Take(tracker.Read(100),goals));
        tracker.OnFrame(0x0D90,Reward(1),200,1110);Assert.Equal([1002],gate.Take(tracker.Read(200),goals));
        Assert.Empty(gate.Take(tracker.Read(200),goals));
        tracker.OnFrame(0x0090,Snapshot(1,25),300,1110);Assert.Empty(gate.Take(tracker.Read(300),goals));
        goals[1002]=1;Assert.Empty(gate.Take(tracker.Read(300),goals));
    }
    [Fact] public void Truncated_login_and_overflowed_reward_wait_for_a_new_full_snapshot()
    {
        var tracker=new PetTracker();tracker.OnFrame(0x0090,Snapshot()[..^2],100,1110);Assert.False(tracker.Read(100).Synced);
        tracker.OnFrame(0x0090,Snapshot(1,24),200,1110);tracker.OnFrame(0x0D90,Reward(3),300,1110);
        Assert.Null(tracker.Read(300).Progress[1002].Remaining(2));
        tracker.OnFrame(0x0090,Snapshot(2,2),400,1110);Assert.Equal(73,tracker.Read(400).Progress[1002].Remaining(3));
    }
    [Fact] public void Another_flow_cannot_mutate_a_bound_collection_and_new_session_never_reuses_the_alert_epoch()
    {
        var tracker=new PetTracker();tracker.OnFrame(0x0090,Snapshot(),100,1110,1,1);var epoch=tracker.Read(100).Epoch;
        tracker.OnFrame(0x0D90,Reward(4),200,1110,2,1);Assert.Equal(13,tracker.Read(200).Progress[1002].Souls);
        var next=new PetTracker();next.OnFrame(0x0090,Snapshot(2,5),300,1110,3,1);Assert.NotEqual(epoch,next.Read(300).Epoch);
    }
    [Fact] public void An_unrelated_connection_cannot_reset_the_collection_by_changing_the_actor_or_map()
    {
        var tracker=new PetTracker();tracker.OnFrame(0x0090,Snapshot(),100,1110,1,1,42);
        tracker.OnFrame(0x0D90,Reward(2),200,1110,1,2,42);var before=tracker.Read(200);
        tracker.OnFrame(0x9999,[],300,20,2,1,43);
        var after=tracker.Read(300);
        Assert.True(after.Synced);Assert.Equal(before.Epoch,after.Epoch);
        Assert.Equal(before.Progress[1002],after.Progress[1002]);
        tracker.OnFrame(0x0D90,Reward(1),400,1110,1,3,42);
        Assert.Equal(16,tracker.Read(400).Progress[1002].Souls);
    }
    [Fact] public void Own_death_teleport_and_map_load_do_not_reset_verified_pet_souls_or_completion_baselines()
    {
        var tracker=new PetTracker();var gate=new PetGoalGate();var goals=new Dictionary<int,int>{{1002,2}};
        tracker.OnFrame(0x0090,Snapshot(1,23),100,1110,1,1,42);
        tracker.OnFrame(0x0D90,Reward(1),200,1110,1,2,42);var before=tracker.Read(200);
        Assert.Empty(gate.Take(before,goals));
        tracker.OnFrame(0x4236,[42,0,3],300,1110,1,3,42);
        tracker.OnFrame(0x2336,[..new byte[6],..BitConverter.GetBytes(12000f),..BitConverter.GetBytes(-85000f),..BitConverter.GetBytes(500f)],400,1110,1,4,42);
        tracker.OnFrame(0x2136,[..U(0,20,0,0),..BitConverter.GetBytes(12000f),..BitConverter.GetBytes(-85000f),..BitConverter.GetBytes(500f)],500,20,1,5,42);
        var revived=tracker.Read(500);Assert.True(revived.Synced);Assert.Equal(before.Epoch,revived.Epoch);
        Assert.Equal(before.Progress[1002],revived.Progress[1002]);
        tracker.OnFrame(0x0D90,Reward(1),600,20,1,6,42);
        Assert.Equal([1002],gate.Take(tracker.Read(600),goals));
        Assert.Empty(gate.Take(tracker.Read(600),goals));
    }
    [Theory][InlineData(true)][InlineData(false)]
    public void Login_collection_survives_actor_identity_arriving_before_or_after_it(bool collectionFirst)
    {
        var tracker=new PetTracker();tracker.OnFrame(0x0090,Snapshot(),100,1110,1,1,42);
        if(collectionFirst)tracker.OnFrame(0x0090,Snapshot(2,5),200,1110,1,2,42);
        tracker.OnFrame(0x3336,[43],300,1110,1,3,43);
        if(!collectionFirst)tracker.OnFrame(0x0090,Snapshot(2,5),400,1110,1,4,43);
        var synced=tracker.Read(400);
        Assert.True(synced.Synced);Assert.Equal(2,synced.Progress[1002].Rank);
        Assert.True(synced.Progress[1002].Verified);Assert.Equal(70,synced.Progress[1002].Remaining(3));
        tracker.OnFrame(0x0D90,Reward(1),500,1110,1,5,43);
        Assert.Equal(69,tracker.Read(500).Progress[1002].Remaining(3));
    }
    [Fact] public void Actor_recreation_keeps_balances_and_session_counts_but_clears_pending_combat_hits()
    {
        var tracker=new PetTracker();
        tracker.OnFrame(0x0090,Snapshot(1,23),100,1110,1,1,42);
        tracker.OnFrame(0x0D90,Reward(1),200,1110,1,2,42);
        tracker.OnFrame(0x0338,[42,0,123,..U(11000001)],300,1110,1,3,42);
        var before=tracker.Read(300);
        tracker.OnFrame(0x3336,[43],400,1110,1,4,43);
        tracker.OnFrame(0x4236,[123,0,3],500,1110,1,5,43);
        var after=tracker.Read(500);
        Assert.True(after.Synced);Assert.Equal(before.Epoch,after.Epoch);
        Assert.Equal(before.Progress[1002],after.Progress[1002]);Assert.Equal(0,after.SessionKills);
    }
    [Fact] public void Only_loss_on_the_bound_collection_connection_invalidates_its_souls()
    {
        var tracker=new PetTracker();tracker.OnFrame(0x0090,Snapshot(),100,1110,1,1,42);var before=tracker.Read(100);
        tracker.Invalidate(2,"Other connection gap");
        Assert.True(tracker.Read(200).Synced);Assert.Equal(before.Epoch,tracker.Read(200).Epoch);
        Assert.Equal(12,tracker.Read(200).Progress[1002].Remaining(2));
        tracker.Invalidate(1,"World connection gap");
        var lost=tracker.Read(300);Assert.False(lost.Synced);Assert.True(lost.CombatPartial);
        Assert.Null(lost.Progress[1002].Remaining(2));Assert.StartsWith("World connection gap",lost.Status);
        tracker.OnFrame(0x0090,Snapshot(2,5),400,1110,1,2,42);
        Assert.True(tracker.Read(400).Synced);Assert.Equal(70,tracker.Read(400).Progress[1002].Remaining(3));
    }
    [Fact] public void Full_collection_after_a_gap_starts_a_clean_session_and_clears_the_partial_warning()
    {
        var tracker=new PetTracker();tracker.OnFrame(0x0090,Snapshot(),100,1110,1,1,42);
        tracker.OnFrame(0x0D90,Reward(1),200,1110,1,2,42);
        tracker.OnFrame(0x0338,[42,0,123,..U(11000001)],300,1110,1,3,42);
        tracker.OnFrame(0x4236,[123,0,3],400,1110,1,4,42);
        Assert.Equal(1,tracker.Read(400).SessionKills);Assert.Equal(1,tracker.Read(400).UnmatchedKills);
        tracker.Invalidate(1,"TCP veri kaybı");
        tracker.OnFrame(0x0D90,Reward(1),500,1110,1,5,42);
        var missing=tracker.Read(500);Assert.False(missing.Synced);Assert.True(missing.CombatPartial);
        Assert.Null(missing.Progress[1002].Remaining(2));
        tracker.OnFrame(0x0090,Snapshot(1,21),600,1110,1,6,42);
        var restored=tracker.Read(600);
        Assert.True(restored.Synced);Assert.False(restored.CombatPartial);
        Assert.Equal(21,restored.Progress[1002].Souls);Assert.Equal(4,restored.Progress[1002].Remaining(2));
        Assert.Equal(0,restored.SessionKills);Assert.Equal(0,restored.UnmatchedKills);
        Assert.Equal(0,restored.Progress[1002].SessionSouls);Assert.Equal(0,restored.Progress[1002].SessionKills);
        tracker.OnFrame(0x0D90,Reward(1),700,1110,1,7,42);
        Assert.Equal(3,tracker.Read(700).Progress[1002].Remaining(2));Assert.Equal(1,tracker.Read(700).Progress[1002].SessionSouls);
    }
    [Fact] public void A_full_collection_without_a_gap_preserves_complete_session_counts()
    {
        var tracker=new PetTracker();tracker.OnFrame(0x0090,Snapshot(),100,1110,1,1,42);
        tracker.OnFrame(0x0D90,Reward(1),200,1110,1,2,42);
        tracker.OnFrame(0x0338,[42,0,123,..U(11000001)],300,1110,1,3,42);
        tracker.OnFrame(0x4236,[123,0,3],400,1110,1,4,42);
        tracker.OnFrame(0x0090,Snapshot(2,5),500,1110,1,5,42);
        var refreshed=tracker.Read(500);
        Assert.True(refreshed.Synced);Assert.False(refreshed.CombatPartial);
        Assert.Equal(1,refreshed.SessionKills);Assert.Equal(1,refreshed.Progress[1002].SessionSouls);
        Assert.Equal(70,refreshed.Progress[1002].Remaining(3));
    }
    [Fact] public void Position_updates_refresh_an_existing_mob_but_invalid_coordinates_never_supply_a_distance()
    {
        var tracker=new PetTracker();var source=PetCatalog.Default.Sources.First();
        byte[] spawn=[123,0x0c,0x20,0,..U(source.NpcId),0,2,..BitConverter.GetBytes(float.NaN),..BitConverter.GetBytes(-85000f),..BitConverter.GetBytes(500f)];
        tracker.OnFrame(0x4136,spawn,100,1110);Assert.Null(tracker.Read(100).Nearby.Single().X);
        byte[] cast=[1,0,..U(123),1,2,123,..BitConverter.GetBytes(0f),..BitConverter.GetBytes(12000f),..BitConverter.GetBytes(-85000f),..BitConverter.GetBytes(600f)];
        tracker.OnFrame(0x0238,cast,20000,1110);Assert.Equal(12000f,tracker.Read(20000).Nearby.Single().X);Assert.Equal(20000,tracker.Read(20000).Nearby.Single().PositionMs);
    }
}
