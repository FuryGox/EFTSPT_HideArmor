using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace HideArmor
{
    [BepInPlugin("com.furyg.hidearmor", "Hide Armor", "1.0.0")]
    public class HideArmorPlugin : BaseUnityPlugin
    {
        public static HideArmorPlugin Instance { get; private set; }
        public static ManualLogSource Log => Instance?.Logger;

        // Section 1 - General Settings
        public static ConfigEntry<bool> ModEnabled { get; private set; }
        public static ConfigEntry<KeyboardShortcut> ToggleHotkey { get; private set; }
        public static ConfigEntry<bool> HideForPlayer { get; private set; }
        public static ConfigEntry<bool> HideInMenu { get; private set; }
        public static ConfigEntry<bool> HideForBots { get; private set; }

        // Section 2 - Equipment Slots
        public static ConfigEntry<bool> HideArmorVest { get; private set; }
        public static ConfigEntry<bool> HideArmoredChestRigs { get; private set; }
        public static ConfigEntry<bool> HideNonArmoredChestRigs { get; private set; }
        public static ConfigEntry<bool> HideArmoredHeadwear { get; private set; }
        public static ConfigEntry<bool> HideNonArmoredHeadwear { get; private set; }
        public static ConfigEntry<bool> HideBackpacks { get; private set; }
        public static ConfigEntry<bool> HideFaceCovers { get; private set; }
        public static ConfigEntry<bool> HideEyewear { get; private set; }
        public static ConfigEntry<bool> HideArmBands { get; private set; }
        public static ConfigEntry<bool> HideHolsters { get; private set; }

        private static readonly FieldInfo IsYourPlayerField = typeof(PlayerBody).GetField("_isYourPlayer", BindingFlags.Instance | BindingFlags.NonPublic);

        private void Awake()
        {
            Instance = this;

            InitConfig();

            try
            {
                var harmony = new Harmony("com.furyg.hidearmor");
                harmony.PatchAll(typeof(SlotViewPatches));
                harmony.PatchAll(typeof(PlayerBodyPatches));
                Logger.LogInfo("Hide Armor mod loaded successfully!");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to patch EFT methods: {ex}");
            }
        }

        private void InitConfig()
        {
            const string secGeneral = "1. General Settings";
            ModEnabled = Config.Bind(secGeneral, "Enabled", true, "Master toggle to enable or disable the Hide Armor mod.");
            ToggleHotkey = Config.Bind(secGeneral, "Toggle Hotkey", new KeyboardShortcut(KeyCode.None), "Optional keybind to quickly toggle armor visibility in-raid.");
            HideForPlayer = Config.Bind(secGeneral, "Hide For Local Player", true, "Hide armor on your character in-raid (both first person and third person).");
            HideInMenu = Config.Bind(secGeneral, "Hide In Menu / Inventory", true, "Hide armor on your character in the main menu and inventory screen.");
            HideForBots = Config.Bind(secGeneral, "Hide For Bots", false, "Hide armor on AI scavs, PMCs, and bosses (default: false).");

            const string secSlots = "2. Equipment Slots";
            HideArmorVest = Config.Bind(secSlots, "Hide Armor Vest", true, "Hide body armor in the ArmorVest slot (e.g. Slick, Korund, PACA, Trooper).");
            HideArmoredChestRigs = Config.Bind(secSlots, "Hide Armored Chest Rigs", true, "Hide chest rigs in the TacticalVest slot ONLY if they provide armor (e.g. AVS, TV-110, CPC, Thor CR).");
            HideNonArmoredChestRigs = Config.Bind(secSlots, "Hide Non-Armored Chest Rigs", false, "Hide chest rigs in the TacticalVest slot even if they are unarmored (e.g. Bank Robber, Triton, Blackrock).");
            HideArmoredHeadwear = Config.Bind(secSlots, "Hide Armored Headwear (Helmets)", false, "Hide helmets and armored headgear in the Headwear slot (e.g. Altyn, Fast MT, Team Wendy).");
            HideNonArmoredHeadwear = Config.Bind(secSlots, "Hide Non-Armored Headwear (Hats/Caps)", false, "Hide non-armored headwear in the Headwear slot (e.g. baseball caps, beanies, berets).");
            HideBackpacks = Config.Bind(secSlots, "Hide Backpacks", false, "Hide backpacks in the Backpack slot.");
            HideFaceCovers = Config.Bind(secSlots, "Hide Face Covers", false, "Hide balaclavas, masks, and shemaghs in the FaceCover slot.");
            HideEyewear = Config.Bind(secSlots, "Hide Eyewear", false, "Hide glasses and goggles in the Eyewear slot.");
            HideArmBands = Config.Bind(secSlots, "Hide Armbands", false, "Hide armbands in the ArmBand slot.");
            HideHolsters = Config.Bind(secSlots, "Hide Holsters", false, "Hide sidearm holsters in the Holster slot.");

            // Register setting change listeners to apply changes immediately
            ModEnabled.SettingChanged += OnConfigChanged;
            HideForPlayer.SettingChanged += OnConfigChanged;
            HideInMenu.SettingChanged += OnConfigChanged;
            HideForBots.SettingChanged += OnConfigChanged;
            HideArmorVest.SettingChanged += OnConfigChanged;
            HideArmoredChestRigs.SettingChanged += OnConfigChanged;
            HideNonArmoredChestRigs.SettingChanged += OnConfigChanged;
            HideArmoredHeadwear.SettingChanged += OnConfigChanged;
            HideNonArmoredHeadwear.SettingChanged += OnConfigChanged;
            HideBackpacks.SettingChanged += OnConfigChanged;
            HideFaceCovers.SettingChanged += OnConfigChanged;
            HideEyewear.SettingChanged += OnConfigChanged;
            HideArmBands.SettingChanged += OnConfigChanged;
            HideHolsters.SettingChanged += OnConfigChanged;
        }

        private void OnConfigChanged(object sender, EventArgs e)
        {
            ApplyToAllActiveBodies();
        }

        private void Update()
        {
            if (ToggleHotkey != null && ToggleHotkey.Value.IsDown())
            {
                ModEnabled.Value = !ModEnabled.Value;
                Log?.LogInfo($"Hide Armor toggled: {(ModEnabled.Value ? "ENABLED" : "DISABLED")}");
                ApplyToAllActiveBodies();
            }
        }

        public static bool CheckIsYourPlayer(PlayerBody playerBody)
        {
            if (playerBody == null) return false;

            var player = playerBody.GetComponentInParent<Player>();
            if (player != null) return player.IsYourPlayer;

            if (playerBody.GetComponentInParent<MenuPlayerPoser>() != null) return true;

            if (IsYourPlayerField != null)
            {
                try
                {
                    return (bool)IsYourPlayerField.GetValue(playerBody);
                }
                catch { }
            }

            return false;
        }

        public static bool CheckIsMenuPlayer(PlayerBody playerBody)
        {
            if (playerBody == null) return false;
            return playerBody.GetComponentInParent<MenuPlayerPoser>() != null || playerBody.GetComponentInParent<Player>() == null;
        }

        public static bool IsArmoredItem(Item item)
        {
            if (item == null) return false;

            if (item.Template is ArmoredEquipmentTemplate armoredTemplate)
            {
                if (armoredTemplate.armorClass > 0) return true;
                if (armoredTemplate.armorColliders != null && armoredTemplate.armorColliders.Length > 0) return true;
                if (armoredTemplate.armorPlateColliders != null && armoredTemplate.armorPlateColliders.Length > 0) return true;
            }

            if (item.Template is VestTemplate vestTemplate)
            {
                if (vestTemplate.armorClass > 0) return true;
                if (vestTemplate.armorPlateColliders != null && vestTemplate.armorPlateColliders.Length > 0) return true;
                if (vestTemplate.armorZone != null && vestTemplate.armorZone.Length > 0) return true;
            }

            if (item.GetItemComponent<ArmorComponent>() != null) return true;
            if (item.GetItemComponent<ArmorHolderComponent>() != null) return true;

            return false;
        }

        public static bool ShouldHideSlot(EquipmentSlot slot, Item item, PlayerBody playerBody)
        {
            if (!ModEnabled.Value) return false;
            if (playerBody == null) return false;

            bool isMenu = CheckIsMenuPlayer(playerBody);
            bool isYourPlayer = CheckIsYourPlayer(playerBody);

            if (isMenu)
            {
                if (!HideInMenu.Value) return false;
            }
            else
            {
                if (isYourPlayer)
                {
                    if (!HideForPlayer.Value) return false;
                }
                else
                {
                    if (!HideForBots.Value) return false;
                }
            }

            switch (slot)
            {
                case EquipmentSlot.ArmorVest:
                    return HideArmorVest.Value;

                case EquipmentSlot.TacticalVest:
                    bool isArmoredRig = IsArmoredItem(item);
                    return isArmoredRig ? HideArmoredChestRigs.Value : HideNonArmoredChestRigs.Value;

                case EquipmentSlot.Headwear:
                    bool isArmoredHeadwear = IsArmoredItem(item);
                    return isArmoredHeadwear ? HideArmoredHeadwear.Value : HideNonArmoredHeadwear.Value;

                case EquipmentSlot.Backpack:
                    return HideBackpacks.Value;

                case EquipmentSlot.FaceCover:
                    return HideFaceCovers.Value;

                case EquipmentSlot.Eyewear:
                    return HideEyewear.Value;

                case EquipmentSlot.ArmBand:
                    return HideArmBands.Value;

                case EquipmentSlot.Holster:
                    return HideHolsters.Value;

                default:
                    return false;
            }
        }

        public static void SetSlotRenderersEnabled(PlayerBody.SlotView slotView, bool enabled)
        {
            if (slotView == null) return;

            // 1. Direct slot renderers
            var renderers = slotView.Renderers;
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r != null && r.enabled != enabled)
                    {
                        r.enabled = enabled;
                    }
                }
            }

            // 2. Dresses (e.g. skinned armor/clothing)
            if (slotView.Dresses != null)
            {
                for (int i = 0; i < slotView.Dresses.Length; i++)
                {
                    var dress = slotView.Dresses[i];
                    if (dress == null) continue;

                    if (dress.Renderers != null)
                    {
                        for (int j = 0; j < dress.Renderers.Length; j++)
                        {
                            var r = dress.Renderers[j];
                            if (r != null && r.enabled != enabled)
                            {
                                r.enabled = enabled;
                            }
                        }
                    }

                    var enumRenderers = dress.GetRenderers();
                    if (enumRenderers != null)
                    {
                        foreach (var r in enumRenderers)
                        {
                            if (r != null && r.enabled != enabled)
                            {
                                r.enabled = enabled;
                            }
                        }
                    }
                }
            }

            // 3. Any additional child renderers in Model hierarchy (plates, attachments, pouches)
            var model = slotView.Model;
            if (model != null)
            {
                var allRenderers = model.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < allRenderers.Length; i++)
                {
                    var r = allRenderers[i];
                    if (r != null && r.enabled != enabled)
                    {
                        r.enabled = enabled;
                    }
                }
            }
        }

        public static void UpdateSlotVisibility(PlayerBody.SlotView slotView, PlayerBody playerBody)
        {
            if (slotView == null || playerBody == null) return;

            bool shouldHide = ShouldHideSlot(slotView.EquipmentSlot, slotView._item, playerBody);
            SetSlotRenderersEnabled(slotView, !shouldHide);
        }

        public static void ApplyToPlayerBody(PlayerBody body)
        {
            if (body == null || body.SlotViews == null) return;

            int count = body.SlotViews.Count;
            for (int i = 0; i < count; i++)
            {
                var slotView = body.SlotViews.GetByIndex(i);
                if (slotView != null)
                {
                    UpdateSlotVisibility(slotView, body);
                }
            }
        }

        public static void ApplyToAllActiveBodies()
        {
            try
            {
                var bodies = FindObjectsOfType<PlayerBody>();
                if (bodies == null) return;

                for (int i = 0; i < bodies.Length; i++)
                {
                    ApplyToPlayerBody(bodies[i]);
                }
            }
            catch (Exception ex)
            {
                Log?.LogError($"Error in ApplyToAllActiveBodies: {ex}");
            }
        }
    }

    public static class SlotViewPatches
    {
        [HarmonyPatch(typeof(PlayerBody.SlotView), nameof(PlayerBody.SlotView.CreateAndParent))]
        [HarmonyPostfix]
        public static void CreateAndParentPostfix(PlayerBody.SlotView __instance, PlayerBody playerBody)
        {
            try
            {
                HideArmorPlugin.UpdateSlotVisibility(__instance, playerBody);
            }
            catch (Exception ex)
            {
                HideArmorPlugin.Log?.LogError($"Error in CreateAndParentPostfix: {ex}");
            }
        }
    }

    public static class PlayerBodyPatches
    {
        [HarmonyPatch(typeof(PlayerBody), nameof(PlayerBody.UpdatePlayerRenders))]
        [HarmonyPostfix]
        public static void UpdatePlayerRendersPostfix(PlayerBody __instance)
        {
            try
            {
                HideArmorPlugin.ApplyToPlayerBody(__instance);
            }
            catch (Exception ex)
            {
                HideArmorPlugin.Log?.LogError($"Error in UpdatePlayerRendersPostfix: {ex}");
            }
        }
    }
}
