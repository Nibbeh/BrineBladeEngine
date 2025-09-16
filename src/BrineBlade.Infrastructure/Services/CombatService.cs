
// src/BrineBlade.Infrastructure/Services/CombatService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using BrineBlade.Domain.Entities;
using BrineBlade.Domain.Game;
using BrineBlade.Infrastructure.Content;
using BrineBlade.Services.Abstractions;

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

        // ---------------------------------------------------------------------
        // Auto-Resolve
        // ---------------------------------------------------------------------
        public CombatResult StartCombat(GameState state, EnemyDef enemy)
        {
            var pStats = BuildPlayerEffectiveStats(state);
            var eStats = enemy.BaseStats ?? new Stats(8, 8, 8, 8, 8, 8, 8);

            int playerHp = Math.Max(1, state.CurrentHp);
            int enemyHp = Math.Max(1, enemy.Hp ?? (eStats.MaxHp / 2 + 4));

            var (pWType, pDie, pAttr) = GetPlayerWeapon(state);
            int pAC = ComputePlayerAC(state, pStats);
            int pDR = ComputePlayerDR(state);
            int eAC = 10 + Mod(eStats.Dexterity) + enemy.Level;
            int eDR = Math.Max(0, eStats.Vitality / 5);

            int profBonus = IsProficient(state, pWType) ? 2 : 0;
            int pAtkBonus = profBonus + Mod(pAttr switch
            {
                "DEX" => pStats.Dexterity,
                "INT" => pStats.Intelligence,
                _ => pStats.Strength
            });
            int eAtkBonus = enemy.Level + Mod(eStats.Strength);

            bool isChampion = state.Flags.Any(f => string.Equals(f, "spec.champion", StringComparison.OrdinalIgnoreCase));
            int critMin = isChampion ? 19 : 20;
            double extraCritChance = Math.Clamp(((pStats.Luck - 10) * 0.01) + (isChampion ? 0.05 : 0.0), 0.0, 0.30);

            bool playerTurn = RollD20() + Mod(pStats.Dexterity) >= RollD20() + Mod(eStats.Dexterity);
            bool secondWindUsed = false;
            int rounds = 0;

            while (playerHp > 0 && enemyHp > 0 && rounds++ < 100)
            {
                if (playerTurn)
                {
                    if (!secondWindUsed && pStats.MaxHp > 0 && playerHp <= (pStats.MaxHp / 2))
                    {
                        int healSw = Math.Max(1, RollDamage(8) + Mod(pStats.Vitality));
                        playerHp = Math.Min(playerHp + healSw, Math.Max(pStats.MaxHp, state.CurrentHp));
                        secondWindUsed = true;
                    }

                    int roll = RollD20();
                    if (roll != 1)
                    {
                        int total = roll + pAtkBonus;
                        bool crit = roll >= critMin || (!critMin.Equals(20) && _rng.NextDouble() < extraCritChance);

                        if (crit || total >= eAC)
                        {
                            int statMod = pAttr switch
                            {
                                "DEX" => Mod(pStats.Dexterity),
                                "INT" => Mod(pStats.Intelligence),
                                _ => Mod(pStats.Strength)
                            };

                            int dmg = Math.Max(1, RollDamage(pDie) + statMod);
                            if (crit) dmg *= 2;
                            dmg = Math.Max(1, dmg - eDR);
                            enemyHp -= dmg;
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

                        if (crit || total >= pAC)
                        {
                            int dmg = Math.Max(1, RollDamage(6) + Mod(eStats.Strength));
                            if (crit) dmg *= 2;
                            dmg = Math.Max(1, dmg - pDR);
                            playerHp -= dmg;
                        }
                    }
                }

                playerTurn = !playerTurn;
            }

            bool playerWon = playerHp > 0 && enemyHp <= 0;
            var loot = (playerWon && enemy.LootTable is not null) ? enemy.LootTable : new List<string>();

            return new CombatResult(playerWon, Math.Max(playerHp, 0), Math.Max(enemyHp, 0), loot);
        }

        // ---------------------------------------------------------------------
        // Interactive (traits / shield / graze)
        // ---------------------------------------------------------------------
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

            int profBonus = IsProficient(state, pWType) ? 2 : 0;
            int pAtkBonus = profBonus + Mod(pAttr switch
            {
                "DEX" => pStats.Dexterity,
                "INT" => pStats.Intelligence,
                _ => pStats.Strength
            });
            int eAtkBonus = enemy.Level + Mod(eStats.Strength);

            bool isChampion = state.Flags.Any(f => string.Equals(f, "spec.champion", StringComparison.OrdinalIgnoreCase));
            int critMinBase = isChampion ? 19 : 20;
            double extraCritChance = Math.Clamp(((pStats.Luck - 10) * 0.01) + (isChampion ? 0.05 : 0.0), 0.0, 0.30);

            int pCritMin = Math.Max(18, critMinBase - (pWType == WeaponType.Dagger ? 1 : 0));
            int penetration = pWType switch { WeaponType.TwoHanded => 2, WeaponType.OneHanded => 1, _ => 0 };
            bool hasShield = state.Equipment.TryGetValue(EquipmentSlot.Offhand, out var offId)
                              && !string.IsNullOrWhiteSpace(offId)
                              && _items.TryGet(offId, out var off)
                              && off.Type == ItemType.Shield;
            double blockChance = hasShield ? 0.20 : 0.0;

            bool guardUp = false;
            bool secondWindUsed = false;

            cb.Show($"You: AC {baseAC}, DR {pDR}; Weapon: {(pWType?.ToString() ?? "Unarmed")} d{pDie}, crit {pCritMin}+, pen {penetration}{(hasShield ? ", block 20%" : "")}");
            cb.Show($"{enemy.Name}: AC {eAC}, DR {eDR}.");

            bool playerTurn = RollD20() + Mod(pStats.Dexterity) >= RollD20() + Mod(eStats.Dexterity);
            int round = 1;

            while (playerHp > 0 && enemyHp > 0 && round <= 100)
            {
                int pAC = baseAC + (guardUp ? 2 : 0);
                bool canSecondWind = !secondWindUsed && pStats.MaxHp > 0 && playerHp <= (pMax / 2);

                var action = cb.ChooseAction(round, playerHp, pMax, enemyHp, canSecondWind, true);

                if (action == CombatAction.Flee)
                {
                    int fleeRoll = RollD20() + Mod(pStats.Dexterity);
                    int target = 10 + enemy.Level;

                    if (fleeRoll >= target)
                    {
                        cb.Show("You successfully flee!");
                        break;
                    }

                    cb.Show("You fail to escape!");
                    playerTurn = false;
                }
                else if (action == CombatAction.SecondWind)
                {
                    if (!canSecondWind)
                    {
                        cb.Show("Second Wind not available.");
                    }
                    else
                    {
                        int healSw = Math.Max(1, RollDamage(8) + Mod(pStats.Vitality));
                        int before = playerHp;

                        playerHp = Math.Min(playerHp + healSw, pMax);
                        secondWindUsed = true;

                        cb.Show($"Second Wind restores {playerHp - before} HP (now {playerHp}/{pMax}).");
                    }
                }
                else if (action == CombatAction.UseItem)
                {
                    if (cb.TryUseHealingItem(out int itemHeal, out string label))
                    {
                        int before = playerHp;
                        playerHp = Math.Min(playerHp + itemHeal, pMax);

                        cb.Show($"You use {label} and recover {playerHp - before} HP.");
                    }
                    else
                    {
                        cb.Show("No usable healing items.");
                    }
                }
                else if (action == CombatAction.Guard)
                {
                    guardUp = true;
                    cb.Show("You raise your guard (+2 AC, -2 dmg until your next turn).");
                }
                else if (action == CombatAction.Attack)
                {
                    int roll = RollD20();
                    cb.Show($"You attack (d20={roll}{(roll >= pCritMin ? " crit-range" : "")})...");

                    if (roll == 1)
                    {
                        cb.Show("You miss badly!");
                    }
                    else
                    {
                        int total = roll + pAtkBonus;
                        bool crit = roll >= pCritMin;
                        bool hit = crit || total >= eAC;

                        if (!hit && pWType == WeaponType.Ranged && (eAC - total) <= 2)
                        {
                            int mod = pAttr switch { "DEX" => Mod(pStats.Dexterity), "INT" => Mod(pStats.Intelligence), _ => Mod(pStats.Strength) };
                            int graze = Math.Max(1, (RollDamage(pDie) + mod) / 2);

                            int effDR = Math.Max(0, eDR - penetration);
                            graze = Math.Max(1, graze - effDR);
                            enemyHp -= graze;

                            cb.Show($"Graze! You deal {graze}. Foe HP: {Math.Max(0, enemyHp)}.");
                        }
                        else if (!hit)
                        {
                            cb.Show($"Miss (total {total} vs AC {eAC}).");
                        }
                        else
                        {
                            if (!crit && _rng.NextDouble() < extraCritChance) crit = true;

                            int mod = pAttr switch { "DEX" => Mod(pStats.Dexterity), "INT" => Mod(pStats.Intelligence), _ => Mod(pStats.Strength) };
                            int dmg = Math.Max(1, RollDamage(pDie) + mod);

                            if (crit)
                            {
                                dmg *= 2;
                                cb.Show("Critical hit!");
                            }

                            int effDR = Math.Max(0, eDR - penetration);
                            dmg = Math.Max(1, dmg - effDR);
                            enemyHp -= dmg;

                            cb.Show($"You deal {dmg}. Foe HP: {Math.Max(0, enemyHp)}.");
                        }
                    }
                }

                if (enemyHp > 0)
                {
                    int roll = RollD20();
                    if (roll == 1)
                    {
                        cb.Show($"{enemy.Name} swings wide and misses.");
                    }
                    else
                    {
                        int total = roll + eAtkBonus;
                        bool crit = roll == 20;
                        bool hit = crit || total >= pAC;

                        if (!hit)
                        {
                            cb.Show($"{enemy.Name} misses (total {total} vs AC {pAC}).");
                        }
                        else
                        {
                            int dmg = Math.Max(1, RollDamage(6) + Mod(eStats.Strength));

                            if (crit)
                            {
                                dmg *= 2;
                                cb.Show($"{enemy.Name} crits!");
                            }

                            if (guardUp) dmg = Math.Max(1, dmg - 2);

                            if (hasShield && _rng.NextDouble() < blockChance)
                            {
                                int before = dmg;
                                dmg = Math.Max(1, (dmg + 1) / 2);
                                cb.Show($"You block with your shield! {before} → {dmg}.");
                            }

                            dmg = Math.Max(1, dmg - pDR);
                            playerHp -= dmg;

                            cb.Show($"{enemy.Name} hits for {dmg}. Your HP: {Math.Max(0, playerHp)}/{pMax}.");
                        }
                    }

                    guardUp = false;
                }

                if (playerHp <= 0 || enemyHp <= 0) break;

                playerTurn = !playerTurn;
                round++;
            }

            bool playerWon = playerHp > 0 && enemyHp <= 0;
            var loot = (playerWon && enemy.LootTable is not null) ? enemy.LootTable : new List<string>();

            return new CombatResult(playerWon, Math.Max(playerHp, 0), Math.Max(enemyHp, 0), loot);
        }

        // ---------------------------------------------------------------------
        // Player Snapshot
        // ---------------------------------------------------------------------
        public PlayerSnapshot GetPlayerSnapshot(GameState state)
        {
            var stats = BuildPlayerEffectiveStats(state);
            var (wt, die, _) = GetPlayerWeapon(state);

            int ac = ComputePlayerAC(state, stats);
            int dr = ComputePlayerDR(state);

            bool isChampion = state.Flags.Any(f => string.Equals(f, "spec.champion", StringComparison.OrdinalIgnoreCase));
            int critMinBase = isChampion ? 19 : 20;
            int critMin = Math.Max(18, critMinBase - (wt == WeaponType.Dagger ? 1 : 0));
            int pen = wt switch { WeaponType.TwoHanded => 2, WeaponType.OneHanded => 1, _ => 0 };

            bool hasShield = state.Equipment.TryGetValue(EquipmentSlot.Offhand, out var offId)
                             && !string.IsNullOrWhiteSpace(offId)
                             && _items.TryGet(offId, out var off)
                             && off.Type == ItemType.Shield;

            return new PlayerSnapshot(
                MaxHp: Math.Max(1, stats.MaxHp > 0 ? stats.MaxHp : state.CurrentHp),
                CurrentHp: Math.Max(1, state.CurrentHp),
                Strength: stats.Strength,
                Dexterity: stats.Dexterity,
                Intelligence: stats.Intelligence,
                Vitality: stats.Vitality,
                Charisma: stats.Charisma,
                Perception: stats.Perception,
                Luck: stats.Luck,
                ArmorClass: ac,
                DamageReduction: dr,
                WeaponLabel: wt?.ToString() ?? "Unarmed",
                WeaponDie: die,
                CritMin: critMin,
                Penetration: pen,
                HasShield: hasShield,
                ShieldBlockChance: hasShield ? 0.20 : 0.0
            );
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------
        private int RollD20() => _rng.Next(1, 21);
        private int RollDamage(int die) => _rng.Next(1, Math.Max(die, 2) + 1);
        private static int Mod(int stat) => (stat - 10) / 2;

        private static readonly (int d, string attr) DEFAULT_WEAPON = (6, "STR");

        private (WeaponType? Type, int Die, string Attr) GetPlayerWeapon(GameState s)
        {
            if (s.Equipment.TryGetValue(EquipmentSlot.Weapon, out var wid)
                && !string.IsNullOrWhiteSpace(wid)
                && _items.All.TryGetValue(wid, out var w)
                && w.Type == ItemType.Weapon)
            {
                var wt = w.WeaponType ?? WeaponType.OneHanded;
                var die = wt switch
                {
                    WeaponType.Dagger => 4,
                    WeaponType.OneHanded => 6,
                    WeaponType.Staff => 6,
                    WeaponType.Ranged => 6,
                    WeaponType.TwoHanded => 10,
                    _ => DEFAULT_WEAPON.d
                };
                var attr = wt == WeaponType.Ranged ? "DEX" : "STR";
                return (wt, die, attr);
            }

            return (WeaponType.OneHanded, DEFAULT_WEAPON.d, DEFAULT_WEAPON.attr);
        }

        private int ComputePlayerAC(GameState s, Stats p)
        {
            int ac = 10 + Mod(p.Dexterity);

            if (s.Equipment.TryGetValue(EquipmentSlot.Head, out var headId)
                && !string.IsNullOrWhiteSpace(headId)
                && _items.All.TryGetValue(headId, out var head)
                && head.Type == ItemType.Armor)
            {
                ac += head.ArmorType switch
                {
                    ArmorType.Cloth => 0,
                    ArmorType.Leather => 0,
                    ArmorType.Plate => 1,
                    _ => 0
                };
            }

            if (s.Equipment.TryGetValue(EquipmentSlot.Chest, out var chestId)
                && !string.IsNullOrWhiteSpace(chestId)
                && _items.All.TryGetValue(chestId, out var chest)
                && chest.Type == ItemType.Armor)
            {
                ac += chest.ArmorType switch
                {
                    ArmorType.Cloth => 0,
                    ArmorType.Leather => 1,
                    ArmorType.Plate => 2,
                    _ => 0
                };
            }

            if (s.Equipment.TryGetValue(EquipmentSlot.Offhand, out var offId)
                && !string.IsNullOrWhiteSpace(offId)
                && _items.All.TryGetValue(offId, out var off)
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
                && _items.All.TryGetValue(chestId, out var chest)
                && chest.Type == ItemType.Armor)
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
                && _items.All.TryGetValue(offId, out var off)
                && off.Type == ItemType.Shield)
            {
                dr += 1;
            }

            return dr;
        }

        private Stats BuildPlayerEffectiveStats(GameState s)
        {
            // Keep tutorial default perfectly stable (Human Warrior Champion)
            bool isDefault = s.Flags.Contains("race.human", StringComparer.OrdinalIgnoreCase)
                          && s.Flags.Contains("class.warrior", StringComparer.OrdinalIgnoreCase)
                          && s.Flags.Contains("spec.champion", StringComparer.OrdinalIgnoreCase);

            var baseStats = isDefault
                ? new Stats(12, 10, 8, 12, 8, 10, 10)   // legacy tutorial baseline
                : new Stats(10, 10, 10, 10, 10, 10, 10);

            // Lightweight race/class/spec seasoning (non-breaking)
            bool Has(string t) => s.Flags.Contains(t, StringComparer.OrdinalIgnoreCase);
            if (!isDefault)
            {
                if (Has("race.elf")) { baseStats = baseStats with { Dexterity = baseStats.Dexterity + 2, Intelligence = baseStats.Intelligence + 2, Perception = baseStats.Perception + 1, Strength = baseStats.Strength - 1, Vitality = baseStats.Vitality - 1 }; }
                if (Has("race.human")) { baseStats = baseStats with { Strength = baseStats.Strength + 1, Vitality = baseStats.Vitality + 1, Luck = baseStats.Luck + 1 }; }

                if (Has("class.mage"))   baseStats = baseStats with { Intelligence = baseStats.Intelligence + 2 };
                if (Has("class.rogue"))  baseStats = baseStats with { Dexterity    = baseStats.Dexterity    + 2 };
                if (Has("class.warrior"))baseStats = baseStats with { Strength     = baseStats.Strength     + 2 };

                if (Has("spec.champion"))  baseStats = baseStats with { Vitality = baseStats.Vitality + 1 };
                if (Has("spec.berserker")) baseStats = baseStats with { Strength = baseStats.Strength + 1 };
                if (Has("spec.templar"))   baseStats = baseStats with { Perception = baseStats.Perception + 1 };
                if (Has("spec.elemental")) baseStats = baseStats with { Intelligence = baseStats.Intelligence + 1 };
                if (Has("spec.druid"))     baseStats = baseStats with { Perception = baseStats.Perception + 1 };
                if (Has("spec.warlock"))   baseStats = baseStats with { Luck = baseStats.Luck + 1 };
                if (Has("spec.ranger"))    baseStats = baseStats with { Dexterity = baseStats.Dexterity + 1 };
                if (Has("spec.thief"))     baseStats = baseStats with { Dexterity = baseStats.Dexterity + 1 };
                if (Has("spec.trickster")) baseStats = baseStats with { Charisma = baseStats.Charisma + 1 };
            }

            var bonus = new StatDelta();

            foreach (var kv in s.Equipment)
            {
                if (!string.IsNullOrWhiteSpace(kv.Value)
                    && _items.All.TryGetValue(kv.Value, out var def)
                    && def.Bonuses is not null)
                {
                    bonus = bonus + def.Bonuses;
                }
            }

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

            string? classId = flag is null
                ? s.Player.Archetype?.ToUpperInvariant() switch
                {
                    "WARRIOR" => "CLASS_WARRIOR",
                    "MAGE" => "CLASS_MAGE",
                    "ROGUE" => "CLASS_ROGUE",
                    _ => null
                }
                : "CLASS_" + flag["class.".Length..].ToUpperInvariant();

            if (classId == "CLASS_WARRIOR")
            {
                return wtype is WeaponType.OneHanded
                           or WeaponType.TwoHanded
                           or WeaponType.Dagger
                           or WeaponType.Staff
                           or WeaponType.Ranged;
            }

            return wtype is WeaponType.OneHanded or WeaponType.Dagger or WeaponType.Ranged;
        }
    }
}
