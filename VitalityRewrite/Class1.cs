using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using Pipakin.SkillInjectorMod;
using System;

namespace VitalityRewrite
{
    [BepInPlugin(PluginId, "Vitality Rewrite", "1.0.0")]
    [BepInDependency("com.pipakin.SkillInjectorMod", BepInDependency.DependencyFlags.HardDependency)]
    [BepInIncompatibility("RD_Valheim_Vitality_Mod")]

    //removed: self heal (the old way was stupid and the I don't want to make it the EpicLoot way to avoid a conflict. Also, faster reg is bascially the same.
    //removed: hardcore. No idea what was ever the purpose of this except for making the mod more complicated
    //removed: reduced stamina cost of working as that would overwrite whatever is set for the tool...
    public class VitalityRewrite : BaseUnityPlugin
    {
        public const string PluginId = "VitalityRewrite";
        private static VitalityRewrite _instance;
        private static ConfigEntry<bool> _loggingEnabled;
        private static readonly int skillId = 638;
        private static readonly Skills.SkillType skill = (Skills.SkillType)skillId;

        private static ConfigEntry<float> cfgMaxHealth;
        private static ConfigEntry<float> cfgHealthRegen;
        private static ConfigEntry<float> cfgMaxStamina;
        private static ConfigEntry<float> cfgMaxEitr;
        private static ConfigEntry<float> cfgStaminaRegen;
        private static ConfigEntry<float> cfgEitrRegen;
        private static ConfigEntry<float> cfgStaminaDelay;
        private static ConfigEntry<float> cfgStaminaJump;
        private static ConfigEntry<float> cfgStaminaSwim;
        private static ConfigEntry<float> cfgWalkSpeed;
        private static ConfigEntry<float> cfgRunSpeed;
        private static ConfigEntry<float> cfgSwimSpeed;
        private static ConfigEntry<float> cfgCarryWeight;
        private static ConfigEntry<float> cfgJumpHeight;
        private static ConfigEntry<float> cfgFallDamage;
        private static ConfigEntry<bool> cfgDoubleJumpFallDamageReset;
        private static ConfigEntry<float> cfgTreeLogging;
        private static ConfigEntry<float> cfgPickAxe;
        private static ConfigEntry<float> cfgSkillGainMultiplier;
        private static ConfigEntry<float> cfgSkillGainByWorkDamageMultiplier;

        private static float skillFactor;

        private Harmony _harmony;



        private void Awake()
        {
            _instance = this;
            _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginId);
            Init();
        }

        public void Init()
        {
            _loggingEnabled = Config.Bind("Logging", "Logging Enabled", false, "Enable logging.");
            cfgMaxHealth = Config.Bind("Health", "MaxIncrease", 125f, "Amount of additional max health at vitality skill 100. Additive to other modification.");
            cfgHealthRegen = Config.Bind("Health", "RegenerationIncrease", 100f, "Increase of base health regeneration in percent at vitality skill 100. Implemented by reducing the time between regenerations accordingly. Multiplicative to other modifications.");
            cfgMaxStamina = Config.Bind("Stamina", "MaxIncrease", 40f, "Amount of additional max stamina at vitality skill 100. Additive to other modification.");
            cfgMaxEitr = Config.Bind("Eitr", "MaxIncrease", 40f, "Amount of additional max Eitr at vitality skill 100. Only active if the food Eitr is above zero. Additive to other modification.");
            cfgStaminaRegen = Config.Bind("Stamina", "Regeneration increase", 72f, "Increase of base stamina regeneration in percent at vitality skill 100. Multiplicative to other modifications.");
            cfgEitrRegen = Config.Bind("Eitr", "Regeneration increase", 72f, "Increase of base eitr regeneration in percent at vitality skill 100. Multiplicative to other modifications.");
            cfgStaminaDelay = Config.Bind("Stamina", "RegenerationDelayReduction", 50f, "Decrease the delay for stamina regeneration to start after usage in percent at vitality skill 100. Be aware that at 100% or higher this means regeneration while using stamina (except swimming). Multiplicative to other modifications.");
            cfgStaminaJump = Config.Bind("Stamina", "JumpCostReduction", 25f, "Decrease of stamina cost per jump in percent at vitality skill 100. Values above 100% have no other effects than 100%. Multiplicative to other modifications.");
            cfgStaminaSwim = Config.Bind("Stamina", "SwimCostReduction", 33f, "Decrease of stamina cost while swimming in percent at vitality skill 100. Values above 100% means you regenerate stamina while swimming. Don't do that. Multiplicative to other modifications.");
            cfgWalkSpeed = Config.Bind("Speed", "WalkingBase", 12.5f, "Increase of base walking speed at vitality skill 100. Additive to other modification.");
            cfgRunSpeed = Config.Bind("Speed", "RunBase", 12.5f, "Increase of base running speed at vitality skill 100. Additive to other modification.");
            cfgSwimSpeed = Config.Bind("Speed", "SwimBase", 100f, "Increase of base swimming speed and swimming turning speed at vitality skill 100. Additive to other modification.");
            cfgCarryWeight = Config.Bind("Various", "Carryweight", 400f, "Amount of additional carry weight at vitality skill 100. Additive to other modification.");
            cfgJumpHeight = Config.Bind("Various", "JumpHeight", 10f, "Increase of base jump height in percent at vitality skill 100. Multiplicative to other modifications.");
            cfgFallDamage = Config.Bind("Various", "FallDamageReduction", 10f, "Reduces the fall height and thus fall damage in percent at vitality skill 100. Multiplicative to other modifications.");
            cfgDoubleJumpFallDamageReset = Config.Bind("Various", "DoubleJumpFallDamageReset", true, "Set the current fall height to zero whenever a successful jump is performed. No effect without a mod that allows jumping in the air, like EpicLoot Double-/Air-Jump.");
            cfgTreeLogging = Config.Bind("Various", "WoodcuttingIncrease", 25f, "Increase chop damage done to trees in percent at vitality skill 100. Multiplicative to other modifications.");
            cfgPickAxe = Config.Bind("Various", "MiningIncrease", 25f, "Increase pickaxe damage done to stones and ores in percent at vitality skill 100. Multiplicative to other modifications.");
            cfgSkillGainMultiplier = Config.Bind("SkillGain", "GeneralMultiplier", 1f, "Multiplier determining how fast skill is gained. Higher values increase skill gain.");
            cfgSkillGainByWorkDamageMultiplier = Config.Bind("SkillGain", "WorkDamageMultiplier", 1f, "Multiplier determining how fast skill is gained via damage of your tools. Higher values mean increased skill gain in late game. Be aware, that this is multiplicative with 'GeneralMultiplier' for tool damage.");
            SkillInjector.RegisterNewSkill(skillId, "Vitality", "Increase base stats", 1f, VitalityRewrite.LoadCustomTexture("heart.png"), Skills.SkillType.None);
        }

        private void OnDestroy()
        {
            _instance = null;
            _harmony?.UnpatchSelf();
        }

        [HarmonyPatch(typeof(Player), "UpdateFood")]
        public static class HealthRegeneration
        {
            private static void Prefix(Player __instance, ref float ___m_foodRegenTimer)
            {
                if (___m_foodRegenTimer == 0)
                {
                    float cfg = cfgHealthRegen.Value;
                    float regenMultiplier = skillFactor * cfg / 100 + 1; //from percentage increase to unitless multiplier
                    if (regenMultiplier > 0) //-100% regeneration or lower is ignored
                        ___m_foodRegenTimer = 10 - 10 / regenMultiplier; //converges to 10 for regenMultiplier -> infinity. Any positive finite value works.
                    //10 is a magic number if valheim source code so I can't replace it by the value from valheim unfortunately.

                    //Log("Health regenation value set to " + ___m_foodRegenTimer + " (out of 10)");
                }
            }
        }

        [HarmonyPatch(typeof(Player), "SetMaxHealth")]
        public static class MaxHealth
        {
            private static void Prefix(Player __instance, ref float health, bool flashBar)
            {
                float cfg = cfgMaxHealth.Value;
                health += skillFactor * cfg;
                //Log("Health increased by " + skillFactor * cfg);
            }
        }

        [HarmonyPatch(typeof(Player), "SetMaxStamina")]
        public static class MaxStamina
        {
            private static void Prefix(Player __instance, ref float stamina, bool flashBar)
            {
                float cfg = cfgMaxStamina.Value;
                stamina += skillFactor * cfg;
                //Log("Stamina increased by " + skillFactor * cfg);
            }
        }

        [HarmonyPatch(typeof(Player), "SetMaxEitr")]
        public static class MaxEitr
        {
            private static void Prefix(Player __instance, ref float eitr, bool flashBar)
            {
                if (eitr > 0)
                {
                    float cfg = cfgMaxEitr.Value;
                    eitr += skillFactor * cfg;
                    //Log("Eitr increased by " + skillFactor * cfg);
                }
            }
        }


        [HarmonyPatch(typeof(Player), "GetJogSpeedFactor")]
        public static class VitalityWalkSpeed
        {
            private static void Postfix(Player __instance, ref float __result)
            {
                float cfg = cfgWalkSpeed.Value;
                __result += skillFactor * cfg / 100;
                //Log("Base walk speed increased by " + skillFactor * cfg/100);
            }
        }

        [HarmonyPatch(typeof(Player), "GetRunSpeedFactor")]
        public static class VitalityRunSpeed
        {
            private static void Postfix(Player __instance, ref float __result)
            {
                float cfg = cfgRunSpeed.Value;
                __result += skillFactor * cfg / 100;
                //Log("Base run speed increased by " + skillFactor * cfg / 100);
            }
        }

        [HarmonyPatch(typeof(Character), "UpdateGroundContact")]
        public static class FallDamageReduction
        {
            private static void Prefix(Character __instance, float dt, ref float ___m_maxAirAltitude, bool ___m_groundContact)
            {
                if (!__instance.IsPlayer() || !___m_groundContact)
                {
                    return;
                }
                float fall = (___m_maxAirAltitude - __instance.transform.position.y);
                if (fall > 4f) //4 being the magic number in Valheim code from where damage starts
                {
                    if (_loggingEnabled.Value)
                        Log("Fall damage reduced from: " + Mathf.Clamp01((fall - 4f) / 16f) * 100f);
                    float cfg = cfgFallDamage.Value;
                    ___m_maxAirAltitude -= fall * skillFactor * cfg / 100;
                    if (_loggingEnabled.Value)
                    {
                        fall = (___m_maxAirAltitude - __instance.transform.position.y);
                        Log("to: " + Mathf.Clamp01((fall - 4f) / 16f) * 100f);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnJump))]
        public static class FallDamageReductionDoubleJump
        {
            public static void Postfix(Player __instance, ref float ___m_maxAirAltitude)
            {
                Log("___m_maxAirAltitude: " + ___m_maxAirAltitude);
                if (__instance.IsPlayer() && cfgDoubleJumpFallDamageReset.Value)
                {
                    ___m_maxAirAltitude = __instance.transform.position.y;
                    Log("___m_maxAirAltitude after: " + ___m_maxAirAltitude);
                }
            }
        }

        [HarmonyPatch(typeof(Player), "UpdateStats", new Type[] { typeof(float) })]
        public static class IncreaseSkill
        {
            private static void Prefix(Player __instance, float dt)
            {
                if (!__instance.IsFlying())
                {
                    if (__instance.IsRunning() && __instance.IsOnGround())
                    {
                        runSwimSkill += 0.1f * dt;
                        // Increase(__instance, 0.1f * dt);
                    }
                    if (__instance.InWater() && !__instance.IsOnGround())
                    {
                        if (stamina != __instance.GetStaminaPercentage()) //make sure player is actually swimming
                        {
                            runSwimSkill += 0.25f * dt;
                            // Increase(__instance, 0.25f * dt);
                        }
                        stamina = __instance.GetStaminaPercentage();
                    }
                    if (runSwimSkill >= 1.0f)
                    {
                        Increase(__instance, runSwimSkill);
                        runSwimSkill = 0.0f;
                    }
                }
            }

            private static float stamina;
            private static float runSwimSkill = 0.0f;
        }

        [HarmonyPatch(typeof(Player), "Load")]
        public static class AttributeOverWriteOnLoad
        {
            private static float m_jumpForce;
            private static float m_maxCarryWeight;
            private static float m_jumpStaminaUsage;
            private static float m_staminaRegenDelay;
            private static float m_staminaRegen;
            private static float m_eitrRegen;
            private static float m_swimStaminaDrainMinSkill;
            private static float m_swimStaminaDrainMaxSkill;
            private static float m_swimSpeed;
            private static float m_swimTurnSpeed;
            //private static void Prefix(Player __instance)
            //{
            //}
            private static void Postfix(Player __instance, ZPackage pkg)
            {
                AttributeOverWriteOnLoad.m_jumpForce = __instance.m_jumpForce;
                AttributeOverWriteOnLoad.m_staminaRegen = __instance.m_staminaRegen;
                AttributeOverWriteOnLoad.m_eitrRegen = __instance.m_eiterRegen;
                AttributeOverWriteOnLoad.m_swimStaminaDrainMinSkill = __instance.m_swimStaminaDrainMinSkill;
                AttributeOverWriteOnLoad.m_swimStaminaDrainMaxSkill = __instance.m_swimStaminaDrainMaxSkill;
                AttributeOverWriteOnLoad.m_maxCarryWeight = __instance.m_maxCarryWeight;
                AttributeOverWriteOnLoad.m_jumpStaminaUsage = __instance.m_jumpStaminaUsage;
                AttributeOverWriteOnLoad.m_staminaRegenDelay = __instance.m_staminaRegenDelay;
                AttributeOverWriteOnLoad.m_swimSpeed = __instance.m_swimSpeed;
                AttributeOverWriteOnLoad.m_swimTurnSpeed = __instance.m_swimTurnSpeed;
                applyChangedValues(__instance);
            }

            public static void applyChangedValues(Player __instance)
            {
                skillFactor = __instance.GetSkillFactor(VitalityRewrite.skill);
                Log("Player: " + __instance.GetPlayerName() + " has skillfactor of " + skillFactor + " applied. ");
                float cfg;

                cfg = cfgJumpHeight.Value;
                Log("Base jump force changed from: " + __instance.m_jumpForce);
                __instance.m_jumpForce = AttributeOverWriteOnLoad.m_jumpForce * (1 + skillFactor * cfg / 100);
                Log("to: " + __instance.m_jumpForce);

                cfg = cfgStaminaRegen.Value;
                Log("Stamina base regeneration changed from: " + __instance.m_staminaRegen);
                __instance.m_staminaRegen = AttributeOverWriteOnLoad.m_staminaRegen * (1 + skillFactor * cfg / 100);
                Log("to: " + __instance.m_staminaRegen);

                cfg = cfgEitrRegen.Value;
                Log("Eitr base regeneration changed from: " + __instance.m_eiterRegen);
                __instance.m_eiterRegen = AttributeOverWriteOnLoad.m_eitrRegen * (1 + skillFactor * cfg / 100);
                Log("to: " + __instance.m_eiterRegen);

                cfg = cfgStaminaSwim.Value;
                Log("Base swim stamina strain at zero skill changed from: " + __instance.m_swimStaminaDrainMinSkill);
                __instance.m_swimStaminaDrainMinSkill = AttributeOverWriteOnLoad.m_swimStaminaDrainMinSkill * (1 - skillFactor * cfg / 100);
                Log("to: " + __instance.m_swimStaminaDrainMinSkill);

                Log("Base swim stamina strain at max skill changed from: " + __instance.m_swimStaminaDrainMaxSkill);
                __instance.m_swimStaminaDrainMaxSkill = AttributeOverWriteOnLoad.m_swimStaminaDrainMaxSkill * (1 - skillFactor * cfg / 100);
                Log("to: " + __instance.m_swimStaminaDrainMaxSkill);

                cfg = cfgCarryWeight.Value;
                Log("Base carry weight changed from: " + __instance.m_maxCarryWeight);
                __instance.m_maxCarryWeight = AttributeOverWriteOnLoad.m_maxCarryWeight + skillFactor * cfg;
                Log("to: " + __instance.m_maxCarryWeight);

                cfg = cfgStaminaJump.Value;
                Log("Base jump stamina use changed from: " + __instance.m_jumpStaminaUsage);
                __instance.m_jumpStaminaUsage = AttributeOverWriteOnLoad.m_jumpStaminaUsage * (1 - skillFactor * cfg / 100);
                Log("to: " + __instance.m_jumpStaminaUsage);

                cfg = cfgStaminaDelay.Value;
                Log("Base stamina regeneration delay changed from: " + __instance.m_staminaRegenDelay);
                __instance.m_staminaRegenDelay = AttributeOverWriteOnLoad.m_staminaRegenDelay * (1 - skillFactor * cfg / 100);
                Log("to: " + __instance.m_staminaRegenDelay);

                cfg = cfgSwimSpeed.Value;
                Log("Base swim speed changed from: " + __instance.m_swimSpeed);
                __instance.m_swimSpeed = AttributeOverWriteOnLoad.m_swimSpeed * (1 + skillFactor * cfg / 100);
                Log("to: " + __instance.m_swimSpeed);

                Log("Base swim turning speed changed from: " + __instance.m_swimTurnSpeed);
                __instance.m_swimTurnSpeed = AttributeOverWriteOnLoad.m_swimTurnSpeed * (1 + skillFactor * cfg / 100);
                Log("to: " + __instance.m_swimTurnSpeed);

                if (_loggingEnabled.Value)
                {
                    //these need to be changed elsewhere (more regularly), yet we do the debug output here to prevent spam.
                    cfg = cfgHealthRegen.Value;
                    float regenMultiplier = skillFactor * cfg / 100 + 1;
                    Log("Health regenation value set to " + (10 - 10 / regenMultiplier) + " (out of 10) whenever it reaches zero");

                    cfg = cfgMaxHealth.Value;
                    Log("Health increased by " + skillFactor * cfg);

                    cfg = cfgMaxStamina.Value;
                    Log("Stamina increased by " + skillFactor * cfg);

                    cfg = cfgMaxEitr.Value;
                    Log("Eitr increased by " + skillFactor * cfg);

                    cfg = cfgWalkSpeed.Value;
                    Log("Base walk speed increased by " + skillFactor * cfg + "%");

                    cfg = cfgRunSpeed.Value;
                    Log("Base run speed increased by " + skillFactor * cfg + "%");
                }
            }
        }

        [HarmonyPatch(typeof(Player), "OnSkillLevelup")]
        public static class LevelUpSkillApplyValues
        {
            private static void Postfix(Player __instance, Skills.SkillType skill, float level)
            {
                if (skill == VitalityRewrite.skill)
                {
                    AttributeOverWriteOnLoad.applyChangedValues(__instance);
                }
            }
        }



        [HarmonyPatch(typeof(Player), nameof(Player.OnJump))]
        public static class VitalitySkillOnJump
        {
            private static void Prefix(Player __instance)
            {
                float num = __instance.m_jumpStaminaUsage - __instance.m_jumpStaminaUsage * __instance.GetEquipmentJumpStaminaModifier();
                bool b = __instance.HaveStamina(num * Game.m_moveStaminaRate );
                if (b)
                    Increase(__instance, 0.14f);
            }
        }

        [HarmonyPatch(typeof(MineRock5), "DamageArea")]
        public static class Mining_Big_Rocks
        {
            private static void Prefix(MineRock5 __instance, HitData hit, float __state)
            {
                Player player = Player.m_localPlayer;
                if (( player == null ) || !(player.GetZDOID() == hit.m_attacker) || hit.m_skill != Skills.SkillType.Pickaxes)
                {
                    return;
                }
                float cfg = cfgPickAxe.Value;
                hit.m_damage.m_pickaxe *= (1.0f + skillFactor * cfg / 100);
                float skillGain = 0.04f + hit.m_damage.m_pickaxe * 0.00075f * cfgSkillGainByWorkDamageMultiplier.Value; //this is lower than the others. mineRock5 usually allows you to hit multiple pieces at once and each will trigger this.
                Increase(player,skillGain);
                Log("(MineRock5) Player: " + player.GetPlayerName() + " hit on: " + __instance.name + " used Skill: " + hit.m_skill.ToString()+" damage done: "+ hit.m_damage.m_pickaxe+" skill gain: "+skillGain);
            }
        }

        [HarmonyPatch(typeof(Destructible), "Damage")]
        public static class Destroy_Little_Things
        {
            private static void Prefix(Destructible __instance, HitData hit)
            {
                Player player = Player.m_localPlayer;
                if (( player == null ) || !(player.GetZDOID() == hit.m_attacker))
                {
                    return;
                }
                if (__instance.name.ToLower().Contains("rock") && hit.m_skill == Skills.SkillType.Pickaxes)
                {
                    float cfg = cfgPickAxe.Value;
                    hit.m_damage.m_pickaxe *= (1.0f + skillFactor * cfg / 100);
                    float skillGain = 0.1f + hit.m_damage.m_pickaxe * 0.001f * cfgSkillGainByWorkDamageMultiplier.Value;
                    Increase(player, skillGain);
                    Log("(Destructible) Player: " + player.GetPlayerName() + " hit on: " + __instance.name + " used Skill: " + hit.m_skill.ToString() + " damage done: " + hit.m_damage.m_pickaxe + " skill gain: " + skillGain);
                }
                else if (hit.m_skill == Skills.SkillType.WoodCutting)
                {
                    float cfg = cfgTreeLogging.Value;
                    hit.m_damage.m_chop *= (1.0f + skillFactor * cfg / 100);
                    float skillGain = 0.1f + hit.m_damage.m_chop * 0.001f * cfgSkillGainByWorkDamageMultiplier.Value;
                    Increase(player, skillGain);
                    Log("(Destructible) Player: " + player.GetPlayerName() + " hit on: " + __instance.name + " used Skill: " + hit.m_skill.ToString() + " damage done: " + hit.m_damage.m_chop + " skill gain: " + skillGain);
                }
            }
        }

        [HarmonyPatch(typeof(TreeBase), "Damage")]
        public static class WoodCutting
        {
            // Token: 0x06000021 RID: 33 RVA: 0x000038A8 File Offset: 0x00001AA8
            private static void Prefix(TreeBase __instance, HitData hit)
            {
                Player player = Player.m_localPlayer;
                if (( player == null ) || !(player.GetZDOID() == hit.m_attacker))
                {
                    return;
                }
                if (hit.m_skill == Skills.SkillType.WoodCutting && hit.m_toolTier >= __instance.m_minToolTier)
                {
                    float cfg = cfgTreeLogging.Value;
                    hit.m_damage.m_chop *= (1.0f + skillFactor * cfg / 100);
                    float skillGain = 0.1f + hit.m_damage.m_chop * 0.001f * cfgSkillGainByWorkDamageMultiplier.Value;
                    Increase(player, skillGain);
                    Log("(TreeBase) Player: " + player.GetPlayerName() + " hit on: " + __instance.name + " used Skill: " + hit.m_skill.ToString() + " damage done: " + hit.m_damage.m_chop + " skill gain: " + skillGain);
                }
            }
        }

        [HarmonyPatch(typeof(TreeLog), "Damage")]
        public static class WoodCutting_II
        {
            // Token: 0x06000022 RID: 34 RVA: 0x00003AB0 File Offset: 0x00001CB0
            private static void Prefix(TreeLog __instance, HitData hit)
            {
                Player player = Player.m_localPlayer;
                if (( player == null ) || !(player.GetZDOID() == hit.m_attacker))
                {
                    return;
                }
                if (hit.m_skill == Skills.SkillType.WoodCutting && hit.m_toolTier >= __instance.m_minToolTier)
                {
                    float cfg = cfgTreeLogging.Value;
                    hit.m_damage.m_chop *= (1.0f + skillFactor * cfg / 100);
                    float skillGain = 0.1f + hit.m_damage.m_chop * 0.001f * cfgSkillGainByWorkDamageMultiplier.Value;
                    Increase(player, skillGain);
                    Log("(TreeLog) Player: " + player.GetPlayerName() + " hit on: " + __instance.name + " used Skill: " + hit.m_skill.ToString() + " damage done: " + hit.m_damage.m_chop + " skill gain: " + skillGain);
                }
            }
        }


        [HarmonyPatch(typeof(Terminal), "InputText")]
        private static class InputText_Patch
        {
            private static void Postfix(Terminal __instance)
            {
                string text = __instance.m_input.text;
                if (text.ToLower().Contains("raiseskill vitality") && ( Player.m_localPlayer != null ) )
                    AttributeOverWriteOnLoad.applyChangedValues(Player.m_localPlayer);
            }
            private static bool Prefix(Terminal __instance)
            {
                string text = __instance.m_input.text;
                if (text.ToLower().Equals("vitalityrewrite reload"))
                {
                    _instance.Config.Reload();
                    foreach (var player in Player.GetAllPlayers())
                        AttributeOverWriteOnLoad.applyChangedValues(player);
                    Traverse.Create(__instance).Method("AddString", new object[]
                    {
                            text
                    }).GetValue();
                    Traverse.Create(__instance).Method("AddString", new object[]
                    {
                            "Vitality rewrite config reloaded"
                    }).GetValue();
                    return false;
                }
                else if (text.ToLower().Equals("vitalityrewrite apply"))
                {
                    foreach (var player in Player.GetAllPlayers())
                        AttributeOverWriteOnLoad.applyChangedValues(player);
                    Traverse.Create(__instance).Method("AddString", new object[]
                    {
                            text
                    }).GetValue();
                    Traverse.Create(__instance).Method("AddString", new object[]
                    {
                            "Vitality rewrite config applied"
                    }).GetValue();
                    return false;
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(Terminal), "InitTerminal")]
        public static class TerminalInitConsole_Patch
        {
            private static void Postfix()
            {
                new Terminal.ConsoleCommand("vitalityrewrite", "with keyword 'reload': Reload config of VitalityRewrite. With keyword 'apply': Apply changes done in-game (Configuration Manager)", null);
            }
        }
        
        public static void Log(string message)
        {
            if (_loggingEnabled.Value)
                _instance.Logger.LogInfo(message);
        }

        public static void LogWarning(string message)
        {
            if (_loggingEnabled.Value)
                _instance.Logger.LogWarning(message);
        }

        public static void LogError(string message)
        {
            //if (_loggingEnabled.Value)
            _instance.Logger.LogError(message);
        }

        public static void Increase(Player player, float value)
        {
            player.RaiseSkill(skill, value*cfgSkillGainMultiplier.Value);
        }

        private static Sprite LoadCustomTexture(string filename)
        {
            Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("VitalityRewrite.Resources." + filename);
            byte[] array = new byte[manifestResourceStream.Length];
            manifestResourceStream.Read(array, 0, (int)manifestResourceStream.Length);
            Texture2D texture2D = new Texture2D(2, 2);
            ImageConversion.LoadImage(texture2D, array);
            texture2D.Apply();
            return Sprite.Create(texture2D, new Rect(0f, 0f, (float)texture2D.width, (float)texture2D.height), new Vector2(0f, 0f), 50f);
        }
    }
}
