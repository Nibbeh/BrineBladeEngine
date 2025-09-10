// src/BrineBlade.Infrastructure/Services/CombatService.cs
using BrineBlade.Domain.Entities;
using BrineBlade.Domain.Game;
using BrineBlade.Infrastructure.Content;
using BrineBlade.Services.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BrineBlade.Infrastructure.Services
{
    public sealed class CombatService : ICombatService
    {
        private readonly IRandom _rng;
        private readonly ItemCatalog _items;

        public CombatService(IRandom rng, ItemCatalog items)
        {
            _rng = rng;
            _items = items;
        }

        public CombatResult StartCombat(GameState state, EnemyDef enemy)
        {
            var pStats = BuildPlayerEffectiveStats(state);
            var eStats = enemy.BaseStats ?? new Stats(8, 8, 8, 8, 8, 8, 8);

            var playerHp = Math.Max(1, state.CurrentHp);
            var enemyHp = Math.Max(1, enemy.Hp ?? (eStats.MaxHp / 2 + 4));

            var (pWType, pDie, pAttr) = GetPlayerWeapon(state);
            var pAC = ComputePlayerAC(state, pStats);
            var pDR = ComputePlayerDR(state);

            var eAC = 10 + Mod(eStats.Dexterity) + enemy.Level;
            var eDR = Math.Max(0, eStats.Vitality / 5);

            var profBonus = IsProficient(state, pWType) ? 2 : 0;
            var pAtkBonus = profBonus + Mod(pAttr switch
            {
                "DEX" => pStats.Dexterity,
                "INT" => pStats.Intelligence,
                _ => pStats.Strength
            });

            var eAtkBonus = enemy.Level + Mod(eStats.Strength);

            bool playerTurn = RollD20() + Mod(pStats.Dexterity) >= RollD20() + Mod(eStats.Dexterity);
            int roundGuard = 0;

            while (playerHp > 0 && enemyHp > 0 && roundGuard++ < 100)
            {
                if (playerTurn)
                {
                    var roll = RollD20();
                    if (roll != 1)
                    {
                        var total = roll + pAtkBonus;
                        if (roll == 20 || total >= eAC)
                        {
                            var dmg = RollDamage(pDie)
                                    + (pAttr == "DEX" ? Mod(pStats.Dexterity)
                                      : pAttr == "INT" ? Mod(pStats.Intelligence)
                                      : Mod(pStats.Strength));
                            if (roll == 20) dmg *= 2;
                            dmg -= eDR;
                            enemyHp -= Math.Max(1, dmg);
                        }
                    }
                }
                else
                {
                    var roll = RollD20();
                    if (roll != 1)
                    {
                        var total = roll + eAtkBonus;
                        if (roll == 20 || total >= pAC)
                        {
                            var dmg = RollDamage(6) + Mod(eStats.Strength); // simple enemy “claw/club”
                            if (roll == 20) dmg *= 2;
                            dmg -= pDR;
                            playerHp -= Math.Max(1, dmg);
                        }
                    }
                }

                playerTurn = !playerTurn;
            }

            bool playerWon = playerHp > 0 && enemyHp <= 0;
            var loot = playerWon && enemy.LootTable is not null ? enemy.LootTable : new List<string>();
            return new CombatResult(playerWon, Math.Max(playerHp, 0), Math.Max(enemyHp, 0), loot);
        }

        private int Mod(int stat) => (stat - 10) / 2;
        private int RollD20() => _rng.Next(1, 21);
        private int RollDamage(int sides) => _rng.Next(1, Math.Max(2, sides + 1));

        private (WeaponType? type, int die, string attr) GetPlayerWeapon(GameState s)
        {
            int die = 2; string attr = "STR"; WeaponType? type = null;
            if (s.Equipment.TryGetValue(EquipmentSlot.Weapon, out var id)
                && !string.IsNullOrWhiteSpace(id)
                && _items.TryGet(id, out var def))
            {
                type = def.WeaponType;
                switch (def.WeaponType)
                {
                    case WeaponType.Dagger: die = 4; attr = "DEX"; break;
                    case WeaponType.OneHanded: die = 6; attr = "STR"; break;
                    case WeaponType.TwoHanded: die = 10; attr = "STR"; break;
                    case WeaponType.Ranged: die = 8; attr = "DEX"; break;
                    case WeaponType.Staff: die = 6; attr = "INT"; break;
                    default: die = 6; break;
                }
            }
            return (type, die, attr);
        }

        private int ComputePlayerAC(GameState s, Stats st)
        {
            int ac = 10 + Mod(st.Dexterity);
            if (s.Equipment.TryGetValue(EquipmentSlot.Chest, out var chestId)
                && !string.IsNullOrWhiteSpace(chestId)
                && _items.TryGet(chestId, out var chest))
            {
                ac += chest.ArmorType switch
                {
                    ArmorType.Plate => 5,
                    ArmorType.Leather => 3,
                    ArmorType.Cloth => 1,
                    _ => 0
                };
            }
            if (s.Equipment.TryGetValue(EquipmentSlot.Offhand, out var offId)
                && !string.IsNullOrWhiteSpace(offId)
                && _items.TryGet(offId, out var off)
                && off.Type == ItemType.Shield)
            {
                ac += 2;
            }
            return ac;
        }

        private int ComputePlayerDR(GameState s)
        {
            int dr = 0;
            if (s.Equipment.TryGetValue(EquipmentSlot.Chest, out var chestId)
                && !string.IsNullOrWhiteSpace(chestId)
                && _items.TryGet(chestId, out var chest))
            {
                dr += chest.ArmorType switch
                {
                    ArmorType.Plate => 3,
                    ArmorType.Leather => 1,
                    ArmorType.Cloth => 0,
                    _ => 0
                };
            }
            if (s.Equipment.TryGetValue(EquipmentSlot.Offhand, out var offId)
                && !string.IsNullOrWhiteSpace(offId)
                && _items.TryGet(offId, out var off)
                && off.Type == ItemType.Shield)
            {
                dr += 1;
            }
            return dr;
        }

        private Stats BuildPlayerEffectiveStats(GameState s)
        {
            var baseStats = new Stats(12, 10, 8, 12, 8, 10, 10); // simple Warrior-ish seed
            var bonus = new StatDelta();
            foreach (var kv in s.Equipment)
                if (!string.IsNullOrWhiteSpace(kv.Value)
                    && _items.TryGet(kv.Value, out var def)
                    && def.Bonuses is not null)
                    bonus = bonus + def.Bonuses;

            return new Stats(
                baseStats.Strength + bonus.Strength,
                baseStats.Dexterity + bonus.Dexterity,
                baseStats.Intelligence + bonus.Intelligence,
                baseStats.Vitality + bonus.Vitality,
                baseStats.Charisma + bonus.Charisma,
                baseStats.Perception + bonus.Perception,
                baseStats.Luck + bonus.Luck
            );
        }

        private bool IsProficient(GameState s, WeaponType? wtype)
        {
            if (wtype is null) return true;
            var flag = s.Flags.FirstOrDefault(f => f.StartsWith("class.", StringComparison.Ordinal));
            string? classId = flag is null ? s.Player.Archetype?.ToUpperInvariant() switch
            {
                "WARRIOR" => "CLASS_WARRIOR",
                "MAGE" => "CLASS_MAGE",
                "ROGUE" => "CLASS_ROGUE",
                _ => null
            } : "CLASS_" + flag["class.".Length..].ToUpperInvariant();

            if (classId == "CLASS_WARRIOR")
                return wtype is WeaponType.OneHanded or WeaponType.TwoHanded or WeaponType.Dagger or WeaponType.Staff;

            return wtype is WeaponType.OneHanded or WeaponType.Dagger;
        }
    }
}
