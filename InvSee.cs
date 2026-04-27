using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

[ApiVersion(2, 1)]
public class InvSee : TerrariaPlugin
{
    public override string Name => "InvSee Visualizer";
    public override string Author => "Gemini";
    public override Version Version => new Version(8, 6);

    private class AdminBackup
    {
        public Item[] Inv, Armor, Dye, Misc, B1, B2, B3, B4;
        public int TargetIndex;
        public int Mode = 1;
    }

    private readonly Dictionary<int, AdminBackup> _backups = new Dictionary<int, AdminBackup>();

    public InvSee(Main game) : base(game) { }

    public override void Initialize()
    {
        Commands.ChatCommands.Add(new Command("invsee.use", CmdInvSee, "invsee"));
        Commands.ChatCommands.Add(new Command("invsee.use", CmdInvStop, "invstop"));
    }

    private void CmdInvStop(CommandArgs args)
    {
        if (_backups.ContainsKey(args.Player.Index)) Restore(args.Player);
    }

    private void CmdInvSee(CommandArgs args)
    {
        var player = args.Player;
        if (_backups.TryGetValue(player.Index, out var backup))
        {
            if (args.Parameters.Count == 0)
            {
                backup.Mode = backup.Mode >= 4 ? 1 : backup.Mode + 1;
                var t = TShock.Players[backup.TargetIndex];
                if (t != null) ApplyMirror(player, t, backup.Mode);
                return;
            }
            Restore(player);
        }

        if (args.Parameters.Count == 0) return;
        var targetList = TSPlayer.FindByNameOrID(args.Parameters[0]);
        if (targetList.Count == 0) { player.SendErrorMessage("Игрок не найден."); return; }

        Save(player, targetList[0].Index);
        ApplyMirror(player, targetList[0], 1);
    }

    private void Save(TSPlayer admin, int tIdx)
    {
        var p = admin.TPlayer;
        _backups[admin.Index] = new AdminBackup {
            Inv = p.inventory.Select(i => i.Clone()).ToArray(),
            Armor = p.armor.Select(i => i.Clone()).ToArray(),
            Dye = p.dye.Select(i => i.Clone()).ToArray(),
            Misc = p.miscEquips.Select(i => i.Clone()).ToArray(),
            B1 = p.bank.item.Select(i => i.Clone()).ToArray(),
            B2 = p.bank2.item.Select(i => i.Clone()).ToArray(),
            B3 = p.bank3.item.Select(i => i.Clone()).ToArray(),
            B4 = p.bank4.item.Select(i => i.Clone()).ToArray(),
            TargetIndex = tIdx
        };
        admin.IgnoreSSCPackets = true;
    }

    private void ApplyMirror(TSPlayer admin, TSPlayer target, int mode)
    {
        var a = admin.TPlayer;
        var t = target.TPlayer;

        // Зеркалим инвентарь и снаряжение
        for (int i = 0; i < 59; i++) a.inventory[i] = t.inventory[i].Clone();
        for (int i = 0; i < 20; i++) a.armor[i] = t.armor[i].Clone();

        string modeName = "";
        Item[] sourceBank;

        switch (mode)
        {
            case 2: modeName = "СЕЙФ"; sourceBank = t.bank2.item; break;
            case 3: modeName = "КУЗНЯ"; sourceBank = t.bank3.item; break;
            case 4: modeName = "МЕШОК ПУСТОТЫ"; sourceBank = t.bank4.item; break;
            default: modeName = "СВИНЬЯ"; sourceBank = t.bank.item; break;
        }

        admin.SendSuccessMessage($"[InvSee] Режим: {modeName} ({target.Name})");

        // Подменяем только визуальный ряд Свиньи
        for (int i = 0; i < 40; i++) a.bank.item[i] = sourceBank[i].Clone();

        SyncAll(admin);
    }

    private void Restore(TSPlayer admin)
    {
        if (_backups.TryGetValue(admin.Index, out var b))
        {
            var a = admin.TPlayer;
            a.inventory = b.Inv; a.armor = b.Armor; a.dye = b.Dye; a.miscEquips = b.Misc;
            a.bank.item = b.B1; a.bank2.item = b.B2; a.bank3.item = b.B3; a.bank4.item = b.B4;
            
            admin.IgnoreSSCPackets = false;
            SyncAll(admin);

            _backups.Remove(admin.Index);
            admin.SendSuccessMessage("[InvSee] Режим просмотра выключен. Ваше снаряжение возвращено.");
        }
    }

    private void SyncAll(TSPlayer admin)
    {
        // Инвентарь (0-58)
        for (int i = 0; i < 59; i++) admin.SendData(PacketTypes.PlayerSlot, "", admin.Index, i);
        // Броня и аксессуары (59-78)
        for (int i = 0; i < 20; i++) admin.SendData(PacketTypes.PlayerSlot, "", admin.Index, 59 + i);
        // Красители (79-88)
        for (int i = 0; i < 10; i++) admin.SendData(PacketTypes.PlayerSlot, "", admin.Index, 79 + i);
        // Свинья (94-133)
        for (int i = 0; i < 40; i++) admin.SendData(PacketTypes.PlayerSlot, "", admin.Index, 94 + i);
        
        // Принудительное обновление визуала персонажа
        admin.SendData(PacketTypes.PlayerUpdate, "", admin.Index);
    }
}
