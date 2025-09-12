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
            // --- Effective stats (kept from your code) ---
            var pStats = BuildPlayerEffectiveStats(state);
            var eStats = enemy.BaseStats ?? new Stats(8, 8, 8, 8, 8, 8, 8);

            // --- HP (kept) ---
            var playerHp = Math.Max(1, state.CurrentHp);
            var enemyHp = Math.Max(1, enemy.Hp ?? (eStats.MaxHp / 2 + 4));

            // --- Player equipment & defenses (kept) ---
            var (pWType, pDie, pAttr) = GetPlayerWeapon(state);
            var pAC = ComputePlayerAC(state, pStats);
            var pDR = ComputePlayerDR(state);

            // --- Enemy defenses (kept) ---
            var eAC = 10 + Mod(eStats.Dexterity) + enemy.Level;
            var eDR = Math.Max(0, eStats.Vitality / 5);

            // --- Attack bonuses (kept) ---
            var profBonus = IsProficient(state, pWType) ? 2 : 0;
            var pAtkBonus = profBonus + Mod(pAttr switch
            {
                "DEX" => pStats.Dexterity,
                "INT" => pStats.Intelligence,
                _ => pStats.Strength
            });
            var eAtkBonus = enemy.Level + Mod(eStats.Strength);

            // --- Champion features (NEW) ---
            // Flag your Warrior as Champion somewhere in seed: state.Flags.Add("spec.champion");
            bool isChampion = state.Flags.Contains("spec.champion", StringComparer.OrdinalIgnoreCase);

            // Champion crits on 19–20; others on 20
            int critMin = isChampion ? 19 : 20;

            // Extra crit chance from Luck (e.g., Luck 14 → +4%), capped; Champion gets +5% flat
            double extraCritChance = Math.Clamp(((pStats.Luck - 10) * 0.01) + (isChampion ? 0.05 : 0.0), 0.0, 0.30);

            // Second Wind: once per combat, auto when at/below 50% HP
            bool secondWindUsed = false;
            int ConMod(int v) => (v - 10) / 2;

            // --- Initiative (kept) ---
            bool playerTurn = RollD20() + Mod(pStats.Dexterity) >= RollD20() + Mod(eStats.Dexterity);

            // Hard cap to avoid stalemates (kept)
            int roundGuard = 0;
            while (playerHp > 0 && enemyHp > 0 && roundGuard++ < 100)
            {
                if (playerTurn)
                {
                    // Auto Second Wind safety valve (NEW)
                    if (!secondWindUsed && pStats.MaxHp > 0 && playerHp <= (pStats.MaxHp / 2))
                    {
                        int heal = Math.Max(1, RollDamage(8) + ConMod(pStats.Vitality)); // 1d8 + CON
                        playerHp = Math.Min(playerHp + heal, pStats.MaxHp);
                        secondWindUsed = true;
                    }

                    // Player attack (augmented crit logic)
                    var roll = RollD20();
                    if (roll != 1) // nat 1 misses
                    {
                        var total = roll + pAtkBonus;
                        bool crit = roll >= critMin;
                        bool hit = crit || total >= eAC;

                        if (hit)
                        {
                            if (!crit && _rng.NextDouble() < extraCritChance) crit = true;

                            int mod = pAttr switch
                            {
                                "DEX" => Mod(pStats.Dexterity),
                                "INT" => Mod(pStats.Intelligence),
                                _ => Mod(pStats.Strength)
                            };

                            int dmg = RollDamage(pDie) + mod;
                            if (dmg < 1) dmg = 1;
                            if (crit) dmg *= 2;

                            dmg -= eDR;
                            enemyHp -= Math.Max(1, dmg);
                        }
                    }
                }
                else
                {
                    // Enemy attack (kept simple; nat 20 crits)
                    var roll = RollD20();
                    if (roll != 1)
                    {
                        var total = roll + eAtkBonus;
                        bool crit = roll == 20;
                        bool hit = crit || total >= pAC;

                        if (hit)
                        {
                            int dmg = RollDamage(6) + Mod(eStats.Strength); // simple "claw/club"
                            if (dmg < 1) dmg = 1;
                            if (crit) dmg *= 2;

                            dmg -= pDR;
                            playerHp -= Math.Max(1, dmg);
                        }
                    }
                }

                playerTurn = !playerTurn;
            }

            bool playerWon = playerHp > 0 && enemyHp <= 0;
            var loot = (playerWon && enemy.LootTable is not null) ? enemy.LootTable : new List<string>();
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

