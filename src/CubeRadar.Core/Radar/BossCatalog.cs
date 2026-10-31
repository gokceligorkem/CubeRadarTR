namespace CubeRadar.Core.Radar;

public sealed record BossBinding(int MapId,int SlotId,int NpcCode,string Name);

public static class BossCatalog
{
    // Altgard: source NPC-code order and count (24); verified with BABA list, 2026-10-30.
    // Abyss: UI order verified against the eight-slot map-20 list; it is NOT NPC-code order.
    private static readonly (int Code,string Name)[] Altgard=[
        (2400017,"Melted Danar"),(2400074,"Black Warrior Aed"),(2400140,"Faithful Rajit"),(2400141,"Berserker Vargor"),
        (2400212,"Predator Garsan"),(2400223,"Blood Warrior Lannar"),(2400274,"Deceiver Trid"),(2400335,"Blue Wave Kelpina"),
        (2400353,"High Overseer Nutah"),(2400358,"Advisor Resana"),(2400419,"Special Operations Leader Linx"),(2400424,"Desecrator Newbold"),
        (2400425,"Specter Archon Axios"),(2400474,"Addicted Hardirun"),(2400504,"Executioner Barthien"),(2400593,"Drakan Battalion Weapon Guruta"),
        (2400607,"Veteran Shujakan"),(2400608,"Visionary Karuka"),(2400659,"Dark Shadow Vishwada"),(2400709,"Sharp Shylak"),
        (2400800,"Immortal Gartua"),(2400853,"High Commander Lagta"),(2400854,"Soul Ruler Kashapa"),(2400855,"Silent Dartan")];
    private static readonly (int Code,string Name)[] Abyss=[
        (2600068,"Argo, the Spirit King"),(2600089,"Watcher Kaira"),(2600084,"Guardian Lord Nahma · Nokta 1"),
        (2600093,"Guardian Lord Nahma · Nokta 2"),(2600094,"Guardian Lord Nahma · Nokta 3"),
        (2600096,"Executor Tamasa"),(2600097,"Executor Argo"),(2600098,"Executor Kaira")];
    private static readonly BossBinding[] AltgardBindings=Altgard.Select((b,i)=>new BossBinding(1110,111001+i,b.Code,b.Name)).ToArray();
    private static readonly BossBinding[] AbyssBindings=Abyss.Select((b,i)=>new BossBinding(20,2001+i,b.Code,b.Name)).ToArray();
    private static readonly Dictionary<int,BossBinding> Npcs=AltgardBindings.Concat(AbyssBindings)
        // The three Nahma NPC codes cannot yet be assigned to distinct live positions from a dead list.
        .Where(b=>!b.Name.StartsWith("Guardian Lord Nahma",StringComparison.Ordinal)).ToDictionary(b=>b.NpcCode);
    public static IReadOnlyList<BossBinding> ForMap(int map,int count)
    {
        return map==1110&&count==24?AltgardBindings:map==20&&count==8?AbyssBindings:[];
    }
    public static BossBinding? ForNpc(int code)=>Npcs.GetValueOrDefault(code);
    public static BossBinding? Match(BossEvent e,int map,int count)=>ForMap(map,count).FirstOrDefault(b=>
        e.Live?.MapId==map&&e.Live.SlotId==b.SlotId||e.Name.Equals(b.Name,StringComparison.OrdinalIgnoreCase)||e.Id=="baba-altgard-"+b.NpcCode);
}
