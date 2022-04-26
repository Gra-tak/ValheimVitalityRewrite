using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using Pipakin.SkillInjectorMod;

namespace VitalityRewrite
{
    [BepInPlugin(PluginId, "Vitality Rewrite", "1.0.0")]
    [BepInDependency("com.pipakin.SkillInjectorMod", BepInDependency.DependencyFlags.HardDependency)]

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
        private static ConfigEntry<float> cfgStaminaRegen;
        private static ConfigEntry<float> cfgStaminaDelay;
        private static ConfigEntry<float> cfgStaminaJump;
        private static ConfigEntry<float> cfgStaminaSwim;
        private static ConfigEntry<float> cfgWalkSpeed;
        private static ConfigEntry<float> cfgRunSpeed;
        private static ConfigEntry<float> cfgSwimSpeed;
        private static ConfigEntry<float> cfgCarryWeight;
        private static ConfigEntry<float> cfgJumpHeight;
        private static ConfigEntry<float> cfgTreeLogging;
        private static ConfigEntry<float> cfgPickAxe;

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
            _loggingEnabled = Config.Bind("Logging", "Logging Enabled", true, "Enable logging.");
            cfgMaxHealth = Config.Bind("Health", "MaxIncrease", 125f, "Amount of additional max health at vitality skill 100. Additive to other modification.");
            cfgHealthRegen = Config.Bind("Health", "RegenerationIncrease", 100f, "Increase of base health regeneration in percent at vitality skill 100. Implemented by reducing the time between regenerations accordingly. Multiplicative to other modifications.");
            cfgMaxStamina = Config.Bind("Stamina", "MaxIncrease", 40f, "Amount of additional max stamina at vitality skill 100. Additive to other modification.");
            cfgStaminaRegen = Config.Bind("Stamina", "Regeneration increase", 72f, "Increase of base stamina regeneration in percent at vitality skill 100. Multiplicative to other modifications.");
            cfgStaminaDelay = Config.Bind("Stamina", "RegenerationDelayReduction", 50f, "Decrease the delay for stamina regeneration to start after usage in percent at vitality skill 100. Multiplicative to other modifications.");
            cfgStaminaJump = Config.Bind("Stamina", "JumpCostReduction", 25f, "Decrease of stamina cost per jump in percent at vitality skill 100. Multiplicative to other modifications.");
            cfgStaminaSwim = Config.Bind("Stamina", "SwimCostReduction", 33f, "Decrease of stamina cost while swimming in percent at vitality skill 100. Multiplicative to other modifications.");
            cfgWalkSpeed = Config.Bind("Speed", "WalkingBase", 12.5f, "Increase of base walking speed at vitality skill 100. Additive to other modification.");
            cfgRunSpeed = Config.Bind("Speed", "RunBase", 12.5f, "Increase of base running speed at vitality skill 100. Additive to other modification.");
            cfgSwimSpeed = Config.Bind("Speed", "SwimBase", 100f, "Increase of base swimming speed and swimming turning speed at vitality skill 100. Additive to other modification.");
            cfgCarryWeight = Config.Bind("Various", "Carryweight", 400f, "Amount of additional carry weight at vitality skill 100. Additive to other modification.");
            cfgJumpHeight = Config.Bind("Various", "JumpHeight", 10f, "Increase of base jump height in percent at vitality skill 100. Multiplicative to other modifications.");
            cfgTreeLogging = Config.Bind("Various", "WoodcuttingIncrease", 25f, "Increase chop damage done to trees in percent at vitality skill 100. Multiplicative to other modifications.");
            cfgPickAxe = Config.Bind("Various", "MiningIncrease", 25f, "Increase pickaxe damage done to stones and ores in percent at vitality skill 100. Multiplicative to other modifications.");

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
            private static void Prefix(Player __instance, ref float health)
            {
                float cfg = cfgMaxHealth.Value;
                health += skillFactor * cfg;
                //Log("Health increased by " + skillFactor * cfg);
            }
        }

        [HarmonyPatch(typeof(Player), "SetMaxStamina")]
        public static class MaxStamina
        {
            private static void Prefix(Player __instance, ref float stamina)
            {
                float cfg = cfgMaxStamina.Value;
                stamina += skillFactor * cfg;
                //Log("Stamina increased by " + skillFactor * cfg);
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

        [HarmonyPatch(typeof(Player), "UpdateStats")]
        public static class IncreaseSkill
        {
            // Token: 0x06000014 RID: 20 RVA: 0x00002CFC File Offset: 0x00000EFC
            private static void Prefix(Player __instance, float dt)
            {
                if (!__instance.IsFlying())
                {
                    if (__instance.IsRunning() && __instance.IsOnGround())
                    {
                        Increase(__instance, 0.1f * dt);
                    }
                    if (__instance.InWater())
                    {
                        if (stamina != __instance.GetStaminaPercentage()) //make sure player is actually swimming
                        {
                            Increase(__instance, 0.25f * dt);
                        }
                        stamina = __instance.GetStaminaPercentage();
                    }
                }
            }

            // Token: 0x0400002D RID: 45
            private static float stamina;
        }

        [HarmonyPatch(typeof(Player), "Load")]
        public static class AttributeOverWriteOnLoad
        {
            private static float m_jumpForce;
            private static float m_maxCarryWeight;
            private static float m_jumpStaminaUsage;
            private static float m_staminaRegenDelay;
            private static float m_staminaRegen;
            private static float m_swimStaminaDrainMinSkill;
            private static float m_swimStaminaDrainMaxSkill;
            private static float m_swimSpeed;
            private static float m_swimTurnSpeed;
            //private static void Prefix(Player __instance)
            //{
            //}
            private static void Postfix(Player __instance)
            {
                AttributeOverWriteOnLoad.m_jumpForce = __instance.m_jumpForce;
                AttributeOverWriteOnLoad.m_staminaRegen = __instance.m_staminaRegen;
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
            private static void Postfix(Player __instance, Skills.SkillType skill)
            {
                Log("skill level up");
                if (skill == VitalityRewrite.skill)
                {
                    Log("skill level up vitality");
                    AttributeOverWriteOnLoad.applyChangedValues(__instance);
                }
            }
        }



        [HarmonyPatch(typeof(Character), "Jump")]
        public static class VitalitySkillOnJump
        {
            private static void Prefix(Character __instance)
            {
                if (!__instance.IsPlayer())
                {
                    return;
                }
                Increase((Player)__instance, 0.015f);
            }
        }

        [HarmonyPatch(typeof(MineRock5), "DamageArea")]
        public static class Mining_Big_Rocks
        {
            private static void Prefix(MineRock5 __instance, HitData hit, float __state)
            {
                if (!(Player.m_localPlayer.GetZDOID() == hit.m_attacker) || hit.m_skill != Skills.SkillType.Pickaxes)
                {
                    return;
                }
                Player player = Player.m_localPlayer;
                float cfg = cfgPickAxe.Value;
                hit.m_damage.m_pickaxe *= (1.0f + skillFactor * cfg / 100);
                float skillGain = 0.1f + hit.m_damage.m_pickaxe * 0.001f;
                Increase(player,skillGain);
                Log("(MineRock5) Player: " + player.GetPlayerName() + " hit on: " + __instance.name + " used Skill: " + hit.m_skill.ToString()+" damage done: "+ hit.m_damage.m_pickaxe+" skill gain: "+skillGain);
            }
        }

        [HarmonyPatch(typeof(Destructible), "Damage")]
        public static class Destroy_Little_Things
        {
            private static void Prefix(Destructible __instance, HitData hit)
            {
                if (!(Player.m_localPlayer.GetZDOID() == hit.m_attacker))
                {
                    return;
                }
                Player player = Player.m_localPlayer;
                if (__instance.name.ToLower().Contains("rock") && hit.m_skill == Skills.SkillType.Pickaxes)
                {
                    float cfg = cfgPickAxe.Value;
                    hit.m_damage.m_pickaxe *= (1.0f + skillFactor * cfg / 100);
                    float skillGain = 0.1f + hit.m_damage.m_pickaxe * 0.001f;
                    Increase(player, skillGain);
                    Log("(Destructible) Player: " + player.GetPlayerName() + " hit on: " + __instance.name + " used Skill: " + hit.m_skill.ToString() + " damage done: " + hit.m_damage.m_pickaxe + " skill gain: " + skillGain);
                }
                else if (hit.m_skill == Skills.SkillType.WoodCutting)
                {
                    float cfg = cfgTreeLogging.Value;
                    hit.m_damage.m_chop *= (1.0f + skillFactor * cfg / 100);
                    float skillGain = 0.1f + hit.m_damage.m_chop * 0.001f;
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
                if (!(Player.m_localPlayer.GetZDOID() == hit.m_attacker))
                {
                    return;
                }
                Player player = Player.m_localPlayer;
                if (hit.m_skill == Skills.SkillType.WoodCutting && hit.m_toolTier >= __instance.m_minToolTier)
                {
                    float cfg = cfgTreeLogging.Value;
                    hit.m_damage.m_chop *= (1.0f + skillFactor * cfg / 100);
                    float skillGain = 0.1f + hit.m_damage.m_chop * 0.001f;
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
                if (!(Player.m_localPlayer.GetZDOID() == hit.m_attacker))
                {
                    return;
                }
                Player player = Player.m_localPlayer;
                if (hit.m_skill == Skills.SkillType.WoodCutting && hit.m_toolTier >= __instance.m_minToolTier)
                {
                    float cfg = cfgTreeLogging.Value;
                    hit.m_damage.m_chop *= (1.0f + skillFactor * cfg / 100);
                    float skillGain = 0.1f + hit.m_damage.m_chop * 0.001f;
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
                if (text.ToLower().Contains("raiseskill vitality"))
                    AttributeOverWriteOnLoad.applyChangedValues(Player.m_localPlayer);
            }
            //    private static bool Prefix(Terminal __instance)
            //    {
            //        string text = __instance.m_input.text;
            //        if (text.ToLower().Equals("VitalityRewrite reload"))
            //        {
            //            _instance.Config.Reload();
            //            WeatherPatch.ReApply();
            //            Traverse.Create(__instance).Method("AddString", new object[]
            //            {
            //                text
            //            }).GetValue();
            //            Traverse.Create(__instance).Method("AddString", new object[]
            //            {
            //                "Weather Tweaks config reloaded"
            //            }).GetValue();
            //            return false;
            //        }
            //        else if (text.ToLower().Equals("VitalityRewrite apply"))
            //        {
            //            WeatherPatch.ReApply();
            //            _instance.Config.SaveShort();
            //            Traverse.Create(__instance).Method("AddString", new object[]
            //            {
            //                text
            //            }).GetValue();
            //            Traverse.Create(__instance).Method("AddString", new object[]
            //            {
            //                "Weather Tweaks config applied"
            //            }).GetValue();
            //            return false;
            //        }
            //        return true;
            //    }
            //}
            //[HarmonyPatch(typeof(Terminal), "InitTerminal")]
            //public static class TerminalInitConsole_Patch
            //{
            //    private static void Postfix()
            //    {
            //        new Terminal.ConsoleCommand("VitalityRewrite", "with keyword 'reload': Reload config of VitalityRewrite. With keyword 'apply': Apply changes done in-game (Configuration Manager)", null);
            //    }
            //}

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
            player.RaiseSkill(skill, value);
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
