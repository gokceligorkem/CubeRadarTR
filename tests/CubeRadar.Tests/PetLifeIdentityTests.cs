using CubeRadar.Core.Pets;

namespace CubeRadar.Tests;

public class PetLifeIdentityTests
{
    static byte[] U(params int[] values)=>values.SelectMany(BitConverter.GetBytes).ToArray();
    static byte[] Spawn(byte entity,int npc)=>[entity,0x0c,0x20,0,..U(npc),0,2,..BitConverter.GetBytes(12000f),..BitConverter.GetBytes(-85000f),..BitConverter.GetBytes(500f)];
    static byte[] Var(uint value)
    {var bytes=new List<byte>();do{var b=(byte)(value&127);value>>=7;bytes.Add((byte)(b|(value>0?128:0)));}while(value>0);return bytes.ToArray();}
    [Theory][InlineData(1048699u,true)][InlineData(1065083u,false)]
    public void Normalized_id_collision_cannot_become_exact_death(uint deathId,bool exact)
    {
        var tracker=new PetTracker();var source=PetCatalog.Default.Sources.First();
        tracker.OnFrame(0x4136,[..Var(1048699),..Spawn(123,source.NpcId).Skip(1)],100,1110);
        tracker.OnFrame(0x4236,[..Var(deathId),0,3],200,1110);
        var snapshot=tracker.Read(200);Assert.Equal(exact,snapshot.ConfirmedDeathSeen);Assert.Equal(exact,snapshot.Removals.Count==1);
    }
    [Fact] public void Mob_lifetimes_survive_position_updates_but_respawn_gets_a_new_identity()
    {
        var tracker=new PetTracker();var source=PetCatalog.Default.Sources.First();
        tracker.OnFrame(0x4136,Spawn(123,source.NpcId),100,1110);var first=tracker.Read(100).Nearby.Single();
        Assert.True(first.LifeId>0);Assert.Equal(123u,first.WireEntityId);Assert.Equal(100,first.BirthMs);
        tracker.OnFrame(0x4036,Spawn(123,source.NpcId),200,1110);var refreshed=tracker.Read(200).Nearby.Single();
        Assert.Equal(first.LifeId,refreshed.LifeId);Assert.Equal(first.BirthMs,refreshed.BirthMs);
        tracker.OnFrame(0x4136,Spawn(123,source.NpcId),300,1110);Assert.NotEqual(first.LifeId,tracker.Read(300).Nearby.Single().LifeId);
    }
    [Fact] public void Exact_known_death_is_exported_once_without_inventing_player_kill_credit()
    {
        var tracker=new PetTracker();var source=PetCatalog.Default.Sources.First();
        tracker.OnFrame(0x4136,Spawn(123,source.NpcId),100,1110);var mob=tracker.Read(100).Nearby.Single();
        tracker.OnFrame(0x4236,[123,0,3],200,1110);tracker.OnFrame(0x4236,[123,0,3],201,1110);
        var snapshot=tracker.Read(201);var removal=Assert.Single(snapshot.Removals);
        Assert.Equal(mob.LifeId,removal.Mob.LifeId);Assert.Equal(1110,removal.MapId);Assert.Equal(snapshot.Epoch,removal.Session);
        Assert.Equal(3u,removal.Reason);Assert.Equal(200,removal.ObservedMs);Assert.Equal(0,snapshot.SessionKills);
    }
    [Fact] public void Corruption_and_map_changes_do_not_leave_old_target_deaths_in_the_new_world()
    {
        var tracker=new PetTracker();var source=PetCatalog.Default.Sources.First();
        tracker.OnFrame(0x4136,Spawn(123,source.NpcId),100,1110);tracker.OnFrame(0x4236,[123,0,3,99],200,1110);
        Assert.Empty(tracker.Read(200).Removals);
        tracker.OnFrame(0x4236,[123,0,3],300,1110);Assert.Single(tracker.Read(300).Removals);
        tracker.OnFrame(0x4136,Spawn(124,source.NpcId),400,20);Assert.Empty(tracker.Read(400).Removals);
        tracker.OnFrame(0x4236,[124,0,3],500,20);tracker.Invalidate();Assert.Empty(tracker.Read(500).Removals);
    }
}
