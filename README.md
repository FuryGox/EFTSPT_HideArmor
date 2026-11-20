# Hide Armor (SPT Client Mod)

A client-side BepInEx mod for **Escape from Tarkov (SPT)** that visually hides equipped body armor, tactical chest rigs, and helmets while **preserving 100% of armor protection, durability, and ballistics mechanics**.

Show off your tactical clothing, faction jackets, and shirts without bulky armor covering your character!

---

## Features

- **Purely Visual**: Disables mesh renderers on equipped armor. Armor stats, ballistics, plate inserts, durability, and hit registration remain completely untouched and fully functional.
- **Granular Slot Configuration**:
  - **Body Armor**: Hide body armor vests (Slick, Korund, PACA, Trooper, etc.).
  - **Tactical Chest Rigs**: Hide chest rigs *only* if they provide armor (AVS, TV-110, CPC, Thor CR, etc.), or optionally hide all chest rigs.
  - **Headwear / Helmets**: Optionally hide armored helmets (Altyn, Fast MT, Team Wendy, etc.) or non-armored hats/caps.
  - **Backpacks**: Optionally hide backpacks.
  - **Face Covers, Eyewear, Armbands, Holsters**: Fully configurable toggles for every slot.
- **Target Selection**:
  - **Local Player**: Hides armor on your character in-raid (both 1st-person and 3rd-person).
  - **Menu / Inventory**: Hides armor in the character inspect screen and main menu.
  - **AI Bots**: Option to hide armor on AI Scavs, PMCs, and Bosses (disabled by default so enemies still look armored).
- **Real-Time Dynamic Updates**: Change any setting in the **F12 BepInEx Configuration Manager** menu in-game, and changes take effect immediately without restarting or leaving raid.
- **Hotkey Toggle**: Optional configurable hotkey to toggle armor hiding on the fly.

---

## Installation

The compiled mod has already been built and installed into your SPT game directory:
`J:\SteamLibrary\steamapps\common\Escape from Tarkov\BepInEx\plugins\HideArmor\HideArmor.dll`

If you ever need to manually install or share it:
1. Copy the `HideArmor` folder (containing `HideArmor.dll`) into `<SPT_Root>\BepInEx\plugins\`.
2. Launch SPT as usual.

---

## Configuration

Press **F12** in-game to open the BepInEx Configuration Manager, or edit `BepInEx\config\com.furyg.hidearmor.cfg`:

### 1. General Settings
| Setting | Default | Description |
|---|---|---|
| **Enabled** | `true` | Master toggle to enable/disable the mod. |
| **Toggle Hotkey** | `None` | Keybind to toggle armor visibility on the fly in-raid. |
| **Hide For Local Player** | `true` | Hide armor on your character in-raid. |
| **Hide In Menu / Inventory** | `true` | Hide armor in the main menu and inventory inspect screen. |
| **Hide For Bots** | `false` | Hide armor on AI bots (Scavs, PMCs, Bosses). |

### 2. Equipment Slots
| Setting | Default | Description |
|---|---|---|
| **Hide Armor Vest** | `true` | Hide body armor vests in the `ArmorVest` slot. |
| **Hide Armored Chest Rigs** | `true` | Hide tactical chest rigs in the `TacticalVest` slot ONLY if armored. |
| **Hide Non-Armored Chest Rigs** | `false` | Hide unarmored chest rigs in the `TacticalVest` slot. |
| **Hide Armored Headwear (Helmets)** | `false` | Hide armored helmets in the `Headwear` slot. |
| **Hide Non-Armored Headwear** | `false` | Hide baseball caps, beanies, berets, etc. |
| **Hide Backpacks** | `false` | Hide backpacks in the `Backpack` slot. |
| **Hide Face Covers** | `false` | Hide balaclavas, masks, and shemaghs. |
| **Hide Eyewear** | `false` | Hide glasses and goggles. |
| **Hide Armbands** | `false` | Hide armbands. |
| **Hide Holsters** | `false` | Hide sidearm holsters. |

---

## How It Works

Escape from Tarkov structures equipment models as child GameObjects under character bones with a dedicated `SlotView` for each inventory slot. 

1. **Rendering**: The mod patches `PlayerBody.SlotView.CreateAndParent` and `PlayerBody.UpdatePlayerRenders`. When an armor piece or rig is equipped, it disables the Unity `Renderer.enabled` property for that slot's mesh renderers, skinned dress renderers, and child plates/attachments.
2. **Underlying Clothing**: Unlike some games that cull the player model under armor, EFT keeps the torso clothing (`BodySkins`) active underneath body armor and rigs. With renderers disabled on the armor, the player's PMC clothing is seamlessly visible.
3. **Ballistics & Protection**: EFT calculates ballistic collision, hit registration, plate degradation, and damage reduction using server/client item data, colliders, and `ArmorComponent` logic — completely independent of Unity mesh renderers. You retain 100% protection.

---

## Building from Source

To rebuild the mod:
```powershell
cd "c:\Users\furyg\Desktop\Dev\Self\Hide_Armor"
dotnet build -c Release
```
The post-build step will automatically copy the updated `HideArmor.dll` to your SPT BepInEx plugins folder.
