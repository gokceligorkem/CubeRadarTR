using CubeRadar.Core.Protocol;

namespace CubeRadar.Core.Pets;

public sealed record PetProgress(int? Rank,int? Souls,bool Verified,long GainSerial=0,int SessionSouls=0,int SpentCredit=0,int OwedSpend=0,int SessionKills=0)
{
    public int? Remaining(int goal)=>Verified&&Rank is {} rank&&Souls is {} souls&&goal is >=1 and <=3
        ?Math.Max(0,PetCatalog.CostBetween(rank,goal)-(rank>=goal?0:souls)):null;
}
public sealed record PetMob(uint EntityId,int NpcId,int PetId,string Name,float? X,float? Y,float? Z,long SeenMs,long PositionMs)
{
    public uint WireEntityId {get;init;}
    public long LifeId {get;init;}
    public long BirthMs {get;init;}
}
public sealed record PetMobRemoval(PetMob Mob,long Session,int MapId,long ObservedMs,uint Reason);
public sealed record PetSnapshot(long Epoch,long Version,bool Synced,string Status,IReadOnlyDictionary<int,PetProgress> Progress,IReadOnlyList<PetMob> Nearby,long LastRewardMs,bool CombatKnown=false,bool CombatPartial=false,int SessionKills=0,int UnmatchedKills=0)
{
    public IReadOnlyList<PetMobRemoval> Removals {get;init;}=[];
    public bool ConfirmedDeathSeen {get;init;}
}
public sealed record PetCombatDiagnostics(long HitFrames,long DecodedHits,long OwnHitFrames,long DeathFrames,long EligibleDeaths,int LastDeathTail,bool LastHitTargetKnown,long OwnHitNotices);

/// <summary>Session-local balances. A new login replaces all counts; stale/unknown totals never become zero.</summary>
public sealed class PetTracker
{
    static long _sessionSeed;
    readonly Dictionary<int,PetProgress> _progress=[];
    readonly Dictionary<uint,PetMob> _mobs=[];
    readonly Dictionary<uint,long> _ownHits=[];
    readonly Queue<PetMobRemoval> _removals=[];
    long _lifeSerial;bool _confirmedDeathSeen;
    uint? _selfId;bool _combatPartial;
    int _sessionKills,_unmatchedKills;
    long _hitFrames,_decodedHits,_ownHitFrames,_deathFrames,_eligibleDeaths,_ownHitNotices;
    int _lastDeathTail=-1;bool _lastHitTargetKnown;
    public PetCombatDiagnostics CombatDiagnostics()=>new(_hitFrames,_decodedHits,_ownHitFrames,_deathFrames,_eligibleDeaths,_lastDeathTail,_lastHitTargetKnown,_ownHitNotices);
    readonly HashSet<(int,long)> _seen=[];readonly Queue<(int,long)> _seenOrder=[];
    int? _map,_flow;long _epoch=Interlocked.Increment(ref _sessionSeed)*1000000,_version,_gainSerial,_lastReward;
    bool _synced;string _status="Pet eşitlemesi bekleniyor · radar açıkken karaktere giriş yap.";
    public void Invalidate(int? flow=null,string reason="Pet verisi eksik")
    {
        if(flow is not null&&_flow is not null&&flow!=_flow)return;
        _synced=false;foreach(var id in _progress.Keys.ToArray())_progress[id]=_progress[id] with {Verified=false};
        _mobs.Clear();_ownHits.Clear();_removals.Clear();_confirmedDeathSeen=false;_combatPartial=true;_epoch++;_version++;_status=reason+" · karaktere yeniden giriş yaparak eşitle.";
    }
    public void OnFrame(ushort op,ReadOnlySpan<byte> body,long time,int? map,int flow=0,long sequence=0,uint? selfId=null)
    {
        // Character/actor and nearby state belong to the collection's world flow too.
        if(_flow is not null&&_flow!=flow&&op!=0x0090)return;
        if(selfId is not null&&selfId!=_selfId)
        {
            // The world actor can be recreated after the login collection has arrived.
            // Balances belong to the collection flow, not that transient combat actor.
            _selfId=selfId;_ownHits.Clear();_version++;
        }
        if(map!=_map){_map=map;_mobs.Clear();_ownHits.Clear();_removals.Clear();_confirmedDeathSeen=false;_version++;}
        if(op==0x0338&&PetMessages.TryHitNotice(body,out var ownActor,out var ownTarget)&&_selfId==ownActor)
        {ownTarget=Normalize(ownTarget);_ownHitNotices++;RememberAttack(ownTarget,time);}
        if(op==0x0438)
        {
            _hitFrames++;
            foreach(var (actor,rawTarget) in PetMessages.ReadHits(body))
            {
                _decodedHits++;var target=Normalize(rawTarget);_lastHitTargetKnown=_mobs.ContainsKey(target);
                if(_selfId==actor){_ownHitFrames++;RememberAttack(target,time);}
            }
        }
        ObserveMob(op,body,time);
        if((op&255)!=0x90)return;
        if(sequence>0)
        {
            if(!_seen.Add((flow,sequence)))return;_seenOrder.Enqueue((flow,sequence));
            while(_seenOrder.Count>2048)_seen.Remove(_seenOrder.Dequeue());
        }
        if(op==0x0090)
        {
            if(!PetMessages.TryCollection(body,out var data)){Invalidate();return;}
            // A full collection restores exact balances, but cannot reconstruct missed kills/rewards.
            // Start a clean farming baseline after loss instead of carrying the partial warning forever.
            var newSession=_combatPartial||_flow is not null&&_flow!=flow;
            var previous=newSession?[]:new Dictionary<int,PetProgress>(_progress);
            if(newSession){_ownHits.Clear();_confirmedDeathSeen=false;_combatPartial=false;_sessionKills=_unmatchedKills=0;}
            if(_flow is not null&&_flow!=flow)_mobs.Clear();
            _flow=flow;_progress.Clear();_removals.Clear();_epoch++;_version++;_gainSerial=0;_lastReward=0;
            foreach(var pet in PetCatalog.Default.Pets.Values)
            {
                var rank=data.Ranks.GetValueOrDefault(pet.Id);var souls=rank==3?0:data.Souls.GetValueOrDefault(pet.Id);
                var old=previous.GetValueOrDefault(pet.Id);
                _progress[pet.Id]=new(rank,souls,true,SessionSouls:old?.SessionSouls??0,SessionKills:old?.SessionKills??0);
            }
            _synced=true;_status=newSession?"Pet koleksiyonu eşitlendi · yeni av oturumu başlatıldı.":"Pet koleksiyonu eşitlendi · ruh kazanımları izleniyor.";return;
        }
        if(op is 0x0D90 or 0x0E90)
        {
            if(!PetMessages.TryPoints(body,out var points)){Invalidate();return;}
            _flow??=flow;
            foreach(var point in points)
            {
                var p=_progress.GetValueOrDefault(point.PetId)??new(null,null,false);
                if(op==0x0D90)
                {
                    var souls=p.Souls is {} old?old+point.Amount:(int?)null;
                    p=p with {Souls=p.Rank==3?0:souls,GainSerial=++_gainSerial,SessionSouls=p.SessionSouls+point.Amount};_lastReward=time;
                }
                else
                {
                    var count=p.Souls is {} old&&old>=point.Amount?old-point.Amount:(int?)null;
                    var owed=p.OwedSpend;var credit=p.SpentCredit;
                    if(owed>=point.Amount)owed-=point.Amount;else credit+=point.Amount;
                    p=p with {Souls=count,OwedSpend=owed,SpentCredit=credit};
                }
                _progress[point.PetId]=Validate(p);
            }
            _version++;
        }
        else if(op==0x1490&&PetMessages.TryRank(body,out var id,out var rank))
        {
            var p=_progress.GetValueOrDefault(id)??new(null,null,false);
            if(p.Rank is {} old&&rank>old)
            {
                var cost=PetCatalog.CostBetween(old,rank);var credit=Math.Min(cost,p.SpentCredit);
                p=p with {Rank=rank,SpentCredit=p.SpentCredit-credit,OwedSpend=p.OwedSpend+cost-credit};
            }
            else if(p.Rank is null||rank<p.Rank)p=p with {Rank=rank,Souls=null,Verified=false};
            _progress[id]=Validate(p);_version++;
        }
    }
    PetProgress Validate(PetProgress p)=>p with {Verified=_synced&&p.Rank is {} rank&&p.Souls is >=0&&p.SpentCredit==0&&p.OwedSpend==0&&(rank is 0 or 3||p.Souls<=PetCatalog.Cost(rank))};
    /// <summary>Known server observations for explicitly marked visual history; removals/reset still own lifetime.</summary>
    public IReadOnlyList<PetMob> ReadRadarMobs()=>_mobs.Values.ToArray();
    public PetSnapshot Read(long now)=>new(_epoch,_version,_synced,_status,new Dictionary<int,PetProgress>(_progress),
        _mobs.Values.Where(m=>now>=m.SeenMs&&now-m.SeenMs<=90000).ToArray(),_lastReward,_selfId is not null,_combatPartial,_sessionKills,_unmatchedKills)
        {Removals=_removals.Where(r=>now>=r.ObservedMs&&now-r.ObservedMs<=3000).ToArray(),ConfirmedDeathSeen=_confirmedDeathSeen};
    static uint Normalize(uint id)=>id>1000000?(id&0x3fff)|0x4000:id;
    void RememberAttack(uint target,long time)
    {
        if(_ownHits.Count>=2048&&!_ownHits.ContainsKey(target))_ownHits.Remove(_ownHits.MinBy(p=>p.Value).Key);
        _ownHits[target]=time;
    }
    private void ObserveMob(ushort op,ReadOnlySpan<byte> body,long time)
    {
        if(PositionMessages.TryParse(op,body,out var fix)&&_mobs.TryGetValue(fix.EntityId,out var existing))
        {
            _mobs[fix.EntityId]=existing with {X=fix.X,Y=fix.Y,Z=fix.Z,PositionMs=time,SeenMs=time};_version++;return;
        }
        if(op==0x4236)
        {
            var at=0;if(Wire.TryVarint(body,ref at,out var entity)&&Wire.TryVarint(body,ref at,out var opaque)&&Wire.TryVarint(body,ref at,out var reason))
            {
                if(opaque==0&&reason==3){_deathFrames++;_lastDeathTail=body.Length-at;}
                if(at==body.Length)
                {
                var wireEntity=entity;entity=Normalize(entity);
                _mobs.Remove(entity,out var mob);
                if(mob is not null&&mob.WireEntityId==wireEntity&&opaque==0&&_map is >0)
                {
                    _removals.Enqueue(new(mob,_epoch,_map.Value,time,reason));while(_removals.Count>128)_removals.Dequeue();
                    if(reason==3)_confirmedDeathSeen=true;
                }
                if(reason==3&&_ownHits.TryGetValue(entity,out var hit)&&time>=hit&&time-hit<=30000)
                {
                    _sessionKills++;_eligibleDeaths++;
                    if(mob is not null)
                    {var progress=_progress.GetValueOrDefault(mob.PetId)??new(null,null,false);_progress[mob.PetId]=progress with {SessionKills=progress.SessionKills+1};}
                    else _unmatchedKills++;
                }
                _version++;
                _ownHits.Remove(entity);return;
                }
            }
        }
        if(op is not (0x4036 or 0x4136 or 0x4236))return;
        var p=0;if(!Wire.TryVarint(body,ref p,out var raw)||raw==0||p+3>body.Length||(body[p]&0x1f)==0x1f)return;
        p+=3;
        foreach(var at in new[]{p+2,p})
        {
            if(at+6>body.Length||body[at+5]!=2||!PetCatalog.Default.Npcs.TryGetValue((int)Wire.U32(body,at),out var source))continue;
            float? x=null,y=null,z=null;
            if(at+18<=body.Length)
            {
                var xx=BitConverter.ToSingle(body[(at+6)..]);var yy=BitConverter.ToSingle(body[(at+10)..]);var zz=BitConverter.ToSingle(body[(at+14)..]);
                if(PositionMessages.IsWorldPosition(xx,yy,zz)){x=xx;y=yy;z=zz;}
            }
            var entity=Normalize(raw);
            if(op==0x4136||_mobs.TryGetValue(entity,out var previous)&&previous.NpcId!=source.NpcId)_ownHits.Remove(entity);
            if(_mobs.Count>=2048&&!_mobs.ContainsKey(entity)){var old=_mobs.MinBy(m=>m.Value.SeenMs).Key;_mobs.Remove(old);_ownHits.Remove(old);}
            var previousLife=_mobs.GetValueOrDefault(entity);
            var newLife=op==0x4136||previousLife is null||previousLife.NpcId!=source.NpcId||previousLife.WireEntityId!=raw;
            _mobs[entity]=new(entity,source.NpcId,source.PetId,source.Name,x,y,z,time,x is null?0:time)
                {WireEntityId=raw,LifeId=newLife?++_lifeSerial:previousLife!.LifeId,BirthMs=newLife?time:previousLife!.BirthMs};_version++;return;
        }
        // A reused entity belonging to another NPC must not retain the old pet source.
        foreach(var at in new[]{p+2,p})
            if(at+6<=body.Length&&body[at+5]==2&&Wire.U32(body,at) is >=1000000 and <=10000000)
            {var entity=Normalize(raw);if(_mobs.Remove(entity))_version++;_ownHits.Remove(entity);break;}
    }
}
