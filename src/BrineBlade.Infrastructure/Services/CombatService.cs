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

        // -------------------- AUTO-RESOLVE (unchanged behavior) --------------------
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

            // Safe Champion check (case-insensitive)
            bool isChampion = state.Flags.Any(f => string.Equals(f, "spec.champion", StringComparison.OrdinalIgnoreCase));
            int critMin = isChampion ? 19 : 20;
            double extraCritChance = Math.Clamp(((pStats.Luck - 10) * 0.01) + (isChampion ? 0.05 : 0.0), 0.0, 0.30);

            bool secondWindUsed = false;
            int ConMod(int v) => (v - 10) / 2;

            bool playerTurn = RollD20() + Mod(pStats.Dexterity) >= RollD20() + Mod(eStats.Dexterity);

            int guard = 0;
            while (playerHp > 0 && enemyHp > 0 && guard++ < 100)
            {
                if (playerTurn)
                {
                    if (!secondWindUsed && pStats.MaxHp > 0 && playerHp <= (pStats.MaxHp / 2))
                    {
                        int healSw = Math.Max(1, RollDamage(8) + ConMod(pStats.Vitality));
                        playerHp = Math.Min(playerHp + healSw, pStats.MaxHp);
                        secondWindUsed = true;
                    }

                    int roll = RollD20();
                    if (roll != 1)
                    {
                        int total = roll + pAtkBonus;
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
                            int dmg = Math.Max(1, RollDamage(pDie) + mod);
                            if (crit) dmg *= 2;
                            enemyHp -= Math.Max(1, dmg - eDR);
                        }
                    }
                }
                else
                {
                    int roll = RollD20();
                    if (roll != 1)
                    {
                        int total = roll + eAtkBonus;
                        bool crit = roll == 20;
                        bool hit = crit || total >= pAC;
                        if (hit)
                        {
                            int dmg = Math.Max(1, RollDamage(6) + Mod(eStats.Strength));
                            if (crit) dmg *= 2;
                            playerHp -= Math.Max(1, dmg - pDR);
                        }
                    }
                }
                playerTurn = !playerTurn;
            }

            bool playerWon = playerHp > 0 && enemyHp <= 0;
            var loot = (playerWon && enemy.LootTable is not null) ? enemy.LootTable : new List<string>();
            return new CombatResult(playerWon, Math.Max(playerHp, 0), Math.Max(enemyHp, 0), loot);
        }

        // -------------------- INTERACTIVE (new) --------------------
        public CombatResult StartCombatInteractive(GameState state, EnemyDef enemy, ICombatCallbacks cb)
        {
            if (cb is null) throw new ArgumentNullException(nameof(cb));

            var pStats = BuildPlayerEffectiveStats(state);
            var eStats = enemy.BaseStats ?? new Stats(8, 8, 8, 8, 8, 8, 8);

            int pMax = Math.Max(1, pStats.MaxHp > 0 ? pStats.MaxHp : state.CurrentHp);
            int playerHp = Math.Clamp(state.CurrentHp, 1, pMax);
            int enemyHp = Math.Max(1, enemy.Hp ?? (eStats.MaxHp / 2 + 4));

            var (pWType, pDie, pAttr) = GetPlayerWeapon(state);
            int baseAC = ComputePlayerAC(state, pStats);
            int pDR = ComputePlayerDR(state);
            int eAC = 10 + Mod(eStats.Dexterity) + enemy.Level;
            int eDR = Math.Max(0, eStats.Vitality / 5);

            var profBonus = IsProficient(state, pWType) ? 2 : 0;
            int pAtkBonus = profBonus + Mod(pAttr switch
            {
                "DEX" => pStats.Dexterity,
                "INT" => pStats.Intelligence,
                _ => pStats.Strength
            });
            int eAtkBonus = enemy.Level + Mod(eStats.Strength);

            // Safe Champion check (case-insensitive)
            bool isChampion = state.Flags.Any(f => string.Equals(f, "spec.champion", StringComparison.OrdinalIgnoreCase));
            int critMin = isChampion ? 19 : 20;
            double extraCritChance = Math.Clamp(((pStats.Luck - 10) * 0.01) + (isChampion ? 0.05 : 0.0), 0.0, 0.30);

            bool secondWindUsed = false;
            bool guardUp = false;
            int ConMod(int v) => (v - 10) / 2;

            bool playerTurn = RollD20() + Mod(pStats.Dexterity) >= RollD20() + Mod(eStats.Dexterity);
            cb.Show(playerTurn ? "You act first." : $"{enemy.Name} acts first.");

            int round = 1;
            while (playerHp > 0 && enemyHp > 0 && round <= 200)
            {
                cb.Show($"-- Round {round} --");
                if (playerTurn)
                {
                    int pAC = baseAC + (guardUp ? 2 : 0);
                    bool canSecondWind = !secondWindUsed && pMax > 1 && playerHp <= (pMax / 2);
                    bool hasItem = true; // UI callback validates real availability when chosen
                    var choice = cb.ChooseAction(round, playerHp, pMax, enemyHp, canSecondWind, hasItem);

                    switch (choice)
                    {
                        case CombatAction.Attack:
                            {
                                guardUp = false;
                                int roll = RollD20();
                                cb.Show($"You attack (d20={roll}{(roll >= critMin ? " crit-range" : "")})...");
                                if (roll == 1) { cb.Show("You miss badly!"); break; }

                                int total = roll + pAtkBonus;
                                bool crit = roll >= critMin;
                                bool hit = crit || total >= eAC;
                                if (!hit) { cb.Show($"Miss (total {total} vs AC {eAC})."); break; }

                                if (!crit && _rng.NextDouble() < extraCritChance) crit = true;

                                int mod = pAttr switch
                                {
                                    "DEX" => Mod(pStats.Dexterity),
                                    "INT" => Mod(pStats.Intelligence),
                                    _ => Mod(pStats.Strength)
                                };
                                int dmg = Math.Max(1, RollDamage(pDie) + mod);
                                if (crit) { dmg *= 2; cb.Show("Critical hit!"); }
                                dmg = Math.Max(1, dmg - eDR);
                                enemyHp -= dmg;
                                cb.Show($"You deal {dmg}. Foe HP: {Math.Max(0, enemyHp)}.");
                                break;
                            }
                        case CombatAction.Guard:
                            guardUp = true;
                            cb.Show("You raise your guard (+2 AC until your next turn).");
                            break;

                        case CombatAction.SecondWind:
                            if (!canSecondWind)
                            {
                                cb.Show("Second Wind not available.");
                            }
                            else
                            {
                                int healSw = Math.Max(1, RollDamage(8) + ConMod(pStats.Vitality));
                                int before = playerHp;
                                playerHp = Math.Min(playerHp + healSw, pMax);
                                secondWindUsed = true;
                                cb.Show($"Second Wind restores {playerHp - before} HP (now {playerHp}/{pMax}).");
                            }
                            break;

                        case CombatAction.UseItem:
                            if (cb.TryUseHealingItem(out int itemHeal, out string label))
                            {
                                int before = playerHp;
                                playerHp = Math.Min(playerHp + itemHeal, pMax);
                                cb.Show($"You use {label} and recover {playerHp - before} HP.");
                            }
                            else cb.Show("No usable healing items.");
                            break;

                        case CombatAction.Flee:
                            {
                                int fleeRoll = RollD20() + Mod(pStats.Dexterity);
                                int target = 10 + enemy.Level;
                                if (fleeRoll >= target)
                                {
                                    cb.Show("You successfully flee!");
                                    goto EndCombat;
                                }
                                cb.Show("You fail to escape!");
                                break;
                            }
                    }
                }
                else
                {
                    int pAC = baseAC + (guardUp ? 2 : 0);
                    int roll = RollD20();
                    if (roll == 1) cb.Show($"{enemy.Name} swings wide and misses.");
                    else
                    {
                        int total = roll + eAtkBonus;
                        bool crit = roll == 20;
                        bool hit = crit || total >= pAC;
                        if (!hit) cb.Show($"{enemy.Name} misses (total {total} vs AC {pAC}).");
                        else
                        {
                            int dmg = Math.Max(1, RollDamage(6) + Mod(eStats.Strength));
                            if (crit) { dmg *= 2; cb.Show($"{enemy.Name} crits!"); }
                            dmg = Math.Max(1, dmg - pDR);
                            playerHp -= dmg;
                            cb.Show($"{enemy.Name} hits for {dmg}. Your HP: {Math.Max(0, playerHp)}/{pMax}.");
                        }
                    }
                    guardUp = false; // guard ends after enemy finishes
                }

                playerTurn = !playerTurn;
                round++;
            }

        EndCombat:
            bool playerWon = playerHp > 0 && enemyHp <= 0;
            var loot = (playerWon && enemy.LootTable is not null) ? enemy.LootTable : new List<string>();
            return new CombatResult(playerWon, Math.Max(playerHp, 0), Math.Max(enemyHp, 0), loot);
        }

        // -------------------- helpers --------------------
        private static int Mod(int stat) => (stat - 10) / 2;
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
            var baseStats = new Stats(12, 10, 8, 12, 8, 10, 10); // Warrior-ish seed
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

        private static bool IsProficient(GameState s, WeaponType? wtype)
        {
            if (wtype is null) return true;
            var flag = s.Flags.FirstOrDefault(f => f.StartsWith("class.", StringComparison.OrdinalIgnoreCase));
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
