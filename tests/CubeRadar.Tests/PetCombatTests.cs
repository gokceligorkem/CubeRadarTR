using CubeRadar.Core.Pets;

namespace CubeRadar.Tests;

public class PetCombatTests
{
    static byte[] U(params int[] values)=>values.SelectMany(BitConverter.GetBytes).ToArray();
    static byte[] Spawn(int entity,int npc)=>[(byte)entity,0x0c,0x20,0,..U(npc),0,2,..BitConverter.GetBytes(12000f),..BitConverter.GetBytes(-85000f),..BitConverter.GetBytes(500f)];
    static byte[] Hit(byte actor,byte target)=>[target,4,0,actor,..U(11000001),1,0,..new byte[8],0xe8,7,100,1];
    [Fact] public void Only_deaths_of_mobs_we_damaged_count_once_and_never_add_souls()
    {
        var t=new PetTracker();var source=PetCatalog.Default.Sources.First();
        t.OnFrame(0x4136,Spawn(123,source.NpcId),100,1110,selfId:42);
        t.OnFrame(0x0438,Hit(42,123),200,1110,selfId:42);
        t.OnFrame(0x4236,[123,0,3],300,1110,selfId:42);
        t.OnFrame(0x4236,[123,0,3],301,1110,selfId:42);
        var p=t.Read(301).Progress[source.PetId];Assert.Equal(1,p.SessionKills);Assert.Equal(0,p.SessionSouls);Assert.Null(p.Remaining(3));
        t.OnFrame(0x4136,Spawn(123,source.NpcId),400,1110,selfId:42);
        t.OnFrame(0x0438,Hit(43,123),500,1110,selfId:42);
        t.OnFrame(0x4236,[123,0,3],600,1110,selfId:42);
        Assert.Equal(1,t.Read(600).Progress[source.PetId].SessionKills);
    }
    [Theory][InlineData(1,300)][InlineData(0,300)][InlineData(3,31000)]
    public void Corpse_despawn_and_old_damage_are_not_new_participating_kills(byte reason,long death)
    {
        var t=new PetTracker();var source=PetCatalog.Default.Sources.First();
        t.OnFrame(0x4136,Spawn(123,source.NpcId),100,1110,selfId:42);t.OnFrame(0x0438,Hit(42,123),200,1110,selfId:42);
        t.OnFrame(0x4236,[123,0,reason],death,1110,selfId:42);
        Assert.Equal(0,t.Read(death).Progress.GetValueOrDefault(source.PetId)?.SessionKills??0);
    }
    [Fact] public void Map_change_and_unknown_self_cannot_attribute_someone_elses_kill()
    {
        var t=new PetTracker();var source=PetCatalog.Default.Sources.First();
        t.OnFrame(0x4136,Spawn(123,source.NpcId),100,1110);t.OnFrame(0x0438,Hit(42,123),200,1110);
        t.OnFrame(0x4236,[123,0,3],300,1110);Assert.False(t.Read(300).CombatKnown);
        t.OnFrame(0x4136,Spawn(123,source.NpcId),400,1110,selfId:42);t.OnFrame(0x0438,Hit(42,123),500,1110,selfId:42);
        t.OnFrame(0x4236,[123,0,3],600,20,selfId:42);Assert.Equal(0,t.Read(600).Progress.GetValueOrDefault(source.PetId)?.SessionKills??0);
    }
    [Fact] public void Refreshing_the_same_collection_preserves_session_counts_and_gaps_mark_them_partial()
    {
        var t=new PetTracker();var source=PetCatalog.Default.Sources.First();var id=source.PetId;
        byte[] snapshot=[..U(id),1,..U(id,id,1),1,..U(id,13)];
        t.OnFrame(0x0090,snapshot,50,1110,1,1,42);
        t.OnFrame(0x4136,Spawn(123,source.NpcId),100,1110,1,2,42);t.OnFrame(0x0438,Hit(42,123),200,1110,1,3,42);t.OnFrame(0x4236,[123,0,3],300,1110,1,4,42);
        t.OnFrame(0x0D90,[1,..U(id,2)],400,1110,1,5,42);
        t.OnFrame(0x0090,[..U(id),1,..U(id,id,1),1,..U(id,15)],500,1110,1,6,42);
        Assert.Equal(1,t.Read(500).Progress[id].SessionKills);Assert.Equal(2,t.Read(500).Progress[id].SessionSouls);Assert.Equal(10,t.Read(500).Progress[id].Remaining(2));
        t.Invalidate();Assert.True(t.Read(500).CombatPartial);Assert.Equal(1,t.Read(500).Progress[id].SessionKills);Assert.Null(t.Read(500).Progress[id].Remaining(2));
    }
    [Fact] public void Respawn_appearance_and_chained_hits_keep_each_mob_separate()
    {
        var t=new PetTracker();var source=PetCatalog.Default.Sources.First();
        t.OnFrame(0x4236,Spawn(123,source.NpcId),100,1110,selfId:42);t.OnFrame(0x4136,Spawn(124,source.NpcId),110,1110,selfId:42);
        Assert.Equal(2,t.Read(110).Nearby.Count);
        t.OnFrame(0x0438,[..Hit(42,123),1,0,..Hit(42,124)],200,1110,selfId:42);
        t.OnFrame(0x4236,[123,0,3],300,1110,selfId:42);t.OnFrame(0x4236,[124,0,3],310,1110,selfId:42);
        Assert.Equal(2,t.Read(310).Progress[source.PetId].SessionKills);
    }
    [Fact] public void Own_hit_notice_can_attribute_participation_without_parsing_an_unsupported_damage_variant()
    {
        var t=new PetTracker();var source=PetCatalog.Default.Sources.First();t.OnFrame(0x4136,Spawn(123,source.NpcId),100,1110,selfId:42);
        t.OnFrame(0x0338,[42,0,123,..U(11000001)],200,1110,selfId:42);t.OnFrame(0x4236,[123,0,3],300,1110,selfId:42);
        Assert.Equal(1,t.Read(300).Progress[source.PetId].SessionKills);
    }
    [Fact] public void Session_total_does_not_depend_on_receiving_the_npcs_initial_spawn()
    {
        var t=new PetTracker();t.OnFrame(0x0438,Hit(42,123),100,1110,selfId:42);t.OnFrame(0x4236,[123,0,3],200,1110,selfId:42);
        t.OnFrame(0x4236,[123,0,3],201,1110,selfId:42);
        Assert.Equal(1,t.Read(201).SessionKills);Assert.Equal(1,t.Read(201).UnmatchedKills);Assert.Empty(t.Read(201).Progress);
    }
}
