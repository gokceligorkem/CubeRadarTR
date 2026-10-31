// NPC/death layouts adapted from cyberbadger6969/aion2-dps-meter PacketParser.cs (GPL-3.0).
using CubeRadar.Core.Radar;

namespace CubeRadar.Core.Protocol;

public abstract record BossObservation(long TimeMs);
public sealed record BossListObservation(FieldBossList List):BossObservation(List.ObservedMs);
public sealed record BossKillObservation(int MapId,int SlotId,long AtMs):BossObservation(AtMs);

/// <summary>Only known field-boss NPCs; existing corpse records and despawns never mean a fresh kill.</summary>
public sealed class BossWorldTracker
{
    private readonly Dictionary<uint,BossBinding> _entities=[];
    private int? _map;
    public BossKillObservation? OnFrame(ushort opcode,ReadOnlySpan<byte> body,long timeMs,int? ownMap)
    {
        if(ownMap!=_map){_map=ownMap;_entities.Clear();}
        if(opcode==0x4136)
        {
            var at=0;if(!Wire.TryVarint(body,ref at,out var raw)||raw==0)return null;
            var entity=raw>1_000_000?(raw&0x3fff)|0x4000:raw;
            for(var i=at+3;i+15<=body.Length&&i<at+60;i++)
            {
                if(body[i]!=0||body[i+1] is not (0 or 0x40)||body[i+2]!=2)continue;
                var binding=BossCatalog.ForNpc((int)Wire.U24(body,i-3));
                if(binding is null||ownMap is {} map&&map!=binding.MapId)return null;
                if(_entities.Count>256)_entities.Clear();
                _entities[entity]=binding;break;
            }
        }
        if(opcode!=0x4236)return null;
        var offset=0;
        if(!Wire.TryVarint(body,ref offset,out var id)||!Wire.TryVarint(body,ref offset,out _)||!Wire.TryVarint(body,ref offset,out var flag)||flag!=3||!_entities.Remove(id,out var boss))return null;
        return new(boss.MapId,boss.SlotId,timeMs);
    }
}
