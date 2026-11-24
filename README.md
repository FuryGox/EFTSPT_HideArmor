# Hide Armor (SPT Client Mod)

A client-side BepInEx mod for **Escape from Tarkov (SPT)** that visually hides equipped body armor, tactical chest rigs, and helmets while **preserving 100% of armor protection, durability, and ballistics mechanics**.

Show off your tactical clothing, faction jackets, and shirts without bulky armor covering your character!

---

## Features & Mechanics

1. **Current Player**:
   - The current player (local human player in raid and in main menu / stash preview) **always uses the settings in Section 2: `Equipment Slots (Current Player)`**.
   - Supports: Body Armor (`ArmorVest`), Chest Rigs (`TacticalVest` - armored or unarmored), Helmets (`Headwear`), Backpacks (`Backpack`), Face Covers (`FaceCover`), Eyewear (`Eyewear`), Headsets (`Earpiece`), Armbands (`ArmBand`), and Holsters (`Holster`).
2. **Character Selection & Dedicated Character Config**:
   - **`Character List`**: Comma-separated list of names or IDs to configure (e.g. `Killa, Tagilla, Reshala, Glukhar, Sanitar, Knight, Birdeye, Big Pipe`).
   - **`Selected Character`**: A dropdown **option select** resolved directly from `Character List`.
   - **`4. Selected Character Config`**: A dedicated section displaying all slot options for the currently selected character.
   - When you switch the character in `Selected Character`, the mod automatically saves the previous character's profile and loads the new character's options into the UI in real time.
   - Per-character profiles are persisted to: `BepInEx\config\HideArmor_Characters.json`.
3. **Real-Time Live Updates**:
   - All changes in the in-game **F12 BepInEx Configuration Manager** take effect immediately without restarting the game or leaving raid.
4. **100% Armor Mechanics Intact**:
   - Ballistic plates, penetration calculation, durability degradation, and hit registration function identically to vanilla EFT.

---

## In-Game Configuration (F12)

Press **F12** in-game to open the BepInEx Configuration Manager:

### 1. General Settings
| Setting | Default | Description |
|---|---|---|
| **Enabled** | `true` | Master toggle to enable/disable the mod. |
| **Toggle Hotkey** | `None` | Optional hotkey to toggle armor hiding in-raid. |
| **Hide For Local Player** | `true` | Enable armor hiding on your character in-raid (uses Section 2). |
| **Hide In Menu / Inventory** | `true` | Enable armor hiding on your character in menu/stash (uses Section 2). |
| **Hide For Other Bots (Not In List)** | `false` | Hide armor on other AI bots not specified in the Character List. |
| **Log Character Info** | `true` | Print Nickname, Profile ID, and Account ID to BepInEx log when loaded. |

### 2. Equipment Slots (Current Player)
*(Applies exclusively to your own character)*
| Setting | Default | Description |
|---|---|---|
| **Hide Armor Vest** | `true` | Hide body armor vests (Slick, Korund, PACA, Trooper, etc.). |
| **Hide Armored Chest Rigs** | `true` | Hide tactical chest rigs **only if armored** (AVS, TV-110, CPC, Thor CR). |
| **Hide Non-Armored Chest Rigs** | `false` | Hide unarmored chest rigs (Bank Robber, Triton, Blackrock, etc.). |
| **Hide Armored Headwear (Helmets)** | `false` | Hide armored helmets (Altyn, Fast MT, Team Wendy, etc.). |
| **Hide Non-Armored Headwear** | `false` | Hide caps, beanies, berets, etc. |
| **Hide Backpacks** | `false` | Hide backpacks. |
| **Hide Face Covers** | `false` | Hide balaclavas, masks, and shemaghs. |
| **Hide Eyewear** | `false` | Hide glasses and goggles. |
| **Hide Earpieces (Headsets)** | `false` | Hide headsets and earpieces (ComTac 2, Sordin, Razor). |
| **Hide Armbands** | `false` | Hide armbands. |
| **Hide Holsters** | `false` | Hide sidearm holsters. |

### 3. Character Selection
- **`Character List`**: Comma-separated list of characters, bots, or bosses to configure (defaults to empty/null). Leave empty to disable per-character overrides.
- **`Selected Character`**: Dropdown select resolved from `Character List`. **Automatically hidden if `Character List` is null or empty.**

### 4. Selected Character Config
*(Dedicated options for the character selected in Section 3. **Automatically hidden if `Character List` is null or empty.**)*
- **`Custom Hiding Enabled`**: Toggle custom hiding for this character.
- **`Hide Armor Vest`**: Hide body armor vest.
- **`Hide Armored Chest Rigs`**: Hide armored chest rigs.
- **`Hide Non-Armored Chest Rigs`**: Hide unarmored chest rigs.
- **`Hide Armored Headwear (Helmets)`**: Hide helmets.
- **`Hide Non-Armored Headwear`**: Hide caps, beanies, etc.
- **`Hide Backpacks`**: Hide backpacks.
- **`Hide Face Covers`**: Hide masks/face covers.
- **`Hide Eyewear`**: Hide eyewear.
- **`Hide Earpieces (Headsets)`**: Hide headsets.
- **`Hide Armbands`**: Hide armbands.
- **`Hide Holsters`**: Hide holsters.

---

## Installation & Building

The compiled mod has been built and deployed to:
`J:\SteamLibrary\steamapps\common\Escape from Tarkov\BepInEx\plugins\HideArmor\HideArmor.dll`

To rebuild from source:
```powershell
cd "c:\Users\furyg\Desktop\Dev\Self\Hide_Armor"
dotnet build -c Release
```
