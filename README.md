# OpenWorld3DGame

**OpenWorld3DGame** is a Unity 3D open-world gameplay prototype focused on building interconnected systems commonly used in action, RPG and sandbox games.
The project includes player movement, FPS/TPP weapon handling, inventory management, NPC AI, missions, dialogue, racing, vehicles, minimap/GPS navigation, banking, loans, UI systems and several gameplay prototypes.

The main goal of this project is to demonstrate practical Unity programming skills, system architecture, debugging, UI workflow, gameplay feature implementation and the ability to connect multiple mechanics into one playable open-world environment.

---

## Project Goals

This project is designed as a portfolio-level Unity prototype showing:

* modular gameplay systems,
* object-oriented C# architecture,
* gameplay UI implementation,
* NPC behavior and mission logic,
* inventory and item handling,
* vehicles and racing modes,
* minimap, world map and GPS routing,
* bank, ATM and loan systems,
* FPS/TPP weapon handling,
* dialogue-driven mission flow,
* real gameplay edge case handling.

The project is not focused on final art quality. Most assets are placeholder or work-in-progress. The focus is on gameplay programming, systems integration and architecture.

---

## Core Features

### Player Controller

The player system supports exploration, interaction and combat-oriented gameplay.

Implemented features include:

* first-person and third-person camera support,
* movement locking during UI/dialogue states,
* stamina and health handling,
* armor and damage system,
* underwater oxygen and drowning mechanics,
* death handling with camera fall effect,
* interaction input through a centralized player input handler,
* UI blocking during inventory, dialogue, race and bank interfaces.

The player can interact with NPCs, vehicles, world objects, inventory items, weapons, banks, race triggers and mission targets.

---

## FPS / TPP Weapon System

The project includes a modular weapon system with FPS and TPP support.

Main features:

* weapon inventory slots,
* melee, pistol, rifle and grenade categories,
* FPS weapon view models,
* TPP weapon models,
* weapon switching,
* weapon dropping,
* weapon pickups,
* ammo handling,
* reload logic,
* ADS logic,
* sniper/scope support,
* recoil and fire handling,
* weapon HUD updates,
* grenade stack synchronization,
* holster objects,
* NPC weapon usage.

The weapon system was refactored into smaller controllers, including input handling, switching, ADS, FOV control, HUD notification and holster logic.

The goal was to avoid one oversized `WeaponManager` class and move toward a more maintainable architecture.

---

## Inventory System

The inventory system is one of the larger gameplay systems in the project.

Features include:

* grid-based inventory,
* variable item sizes,
* item rotation,
* stackable items,
* stack splitting,
* quick split,
* drag and drop,
* item placement preview,
* occupied slot highlighting,
* locked and unlocked slots,
* item tooltips,
* weapon item integration,
* grenade stack integration,
* ammo and weapon compatibility,
* money dropping,
* bank card items,
* key items,
* quest/package items,
* inventory-to-box transfer,
* box-to-inventory transfer.

The inventory supports both normal player items and gameplay-specific items such as delivery packages, cards, weapons and ammo.

Example use cases:

* moving weapons from a box into player inventory,
* splitting grenade or medkit stacks,
* dragging ammo onto a compatible weapon,
* receiving a quest package from an NPC,
* removing a delivered package after successful mission completion.

---

## Box Inventory System

The box inventory is a secondary storage system that works together with the player inventory.

Features include:

* world box interaction,
* opening box UI next to player inventory,
* drag and drop between inventory and box,
* transfer all buttons,
* stack splitting between inventories,
* cash transfer,
* item placement preview,
* weapon transfer validation,
* auto-sorting only when explicitly requested.

This system was designed to behave differently from automatic inventory sorting. Manual drag placement is preserved, while sorting happens only through transfer actions.

---

## NPC System

The NPC system contains multiple NPC archetypes and behavior types.

Implemented NPC types:

* coward civilians,
* aggressive NPCs,
* fighter NPCs,
* melee NPCs,
* story-critical NPCs,
* mission NPCs,
* bank employees.

NPCs can react to:

* being hit,
* nearby gunshots,
* player aiming at them,
* seeing the player,
* witnessing another NPC die,
* coward panic propagation,
* mission/dialogue interaction.

NPC features include:

* health through `NPCCore`,
* invulnerability and prevent-death rules,
* weapon assignment,
* weapon drops,
* ragdoll death behavior,
* hit feedback,
* blood FX,
* alert/scared icons,
* NavMesh movement,
* fleeing,
* investigation,
* combat behavior,
* dialogue interaction blocking during aggression.

Important NPCs can be configured as mission-related or story-critical, allowing special handling such as invulnerability or death prevention.

---

## NPC LOD / World Coordinator

The project includes an NPC world coordination concept for better performance with larger NPC counts.

The coordinator handles:

* NPC registration,
* scene scanning,
* active/simple/sleeping LOD states,
* ambient NPC despawn,
* global NPC limits,
* combat and ambient NPC counting,
* reducing logic for distant NPCs,
* protecting important NPCs from being fully disabled.

The goal is to avoid every NPC running full AI logic every frame.

Planned direction:

* full AI for nearby NPCs,
* simplified logic for mid-range NPCs,
* sleeping state for distant ambient NPCs,
* no sleeping for story-critical mission NPCs,
* batched AI checks over several frames.

---

## Dialogue System

The dialogue system is based on dialogue graph assets.

Features include:

* NPC dialogue graph selection,
* player response options,
* dialogue event keys,
* mission event triggering,
* dialogue UI locking player movement,
* typing/history style UI,
* shared dialogue UI for story NPCs and bank employees,
* dialogue graph routing based on mission state.

The dialogue system is used for:

* story NPCs,
* mission acceptance,
* mission progress dialogue,
* mission reward claiming,
* delivery confirmation,
* bank employee interactions.

Dialogue options can trigger mission events through a centralized event router.

---

## Mission and Objective System

The mission system was refactored around a coordinator/facade architecture.

Main components:

* `MissionDefinition`,
* `IMissionRuntime`,
* `MissionRuntimeState`,
* `MissionCoordinator`,
* `ObjectivesUI`,
* `MissionTrackerEntryUI`,
* NPC mission links with giver/receiver roles.

The goal of the refactor was to support many mission types without hardcoding UI to one specific mission.

Current mission types:

### Kill Mission

Example: eliminate armed NPCs.

Features:

* score-based progress,
* armed NPC kills increase progress,
* innocent NPC kills can penalize progress,
* return-to-NPC reward flow,
* objective UI integration,
* HUD tracker integration,
* dialogue graph state switching.

### Delivery Mission

Example: Fredo gives a package to deliver to Ralph.

Features:

* NPC giver role,
* NPC receiver role,
* package item added to inventory,
* item must physically exist in `InventoryUI`,
* delivery fails if player no longer has the package,
* item is removed after successful delivery,
* reward can be given by receiver or by returning to giver,
* abandon removes the package from inventory,
* GPS route leads to delivery target,
* GPS route can then return to mission giver,
* simple text HUD tracker support.

Example delivery flow:

1. Player talks to Fredo.
2. Player accepts the delivery mission.
3. Fredo gives the player a package item.
4. GPS points to Ralph.
5. Player talks to Ralph.
6. Ralph confirms delivery only if the player has the package.
7. Package is removed from inventory.
8. GPS points back to Fredo if reward is configured at giver.
9. Player returns to Fredo and claims reward.
10. Mission is completed and removed from active objectives.

---

## Objectives UI

The project includes an objectives window and HUD tracker.

Features:

* open/close objectives UI,
* mission list entries,
* expanded/collapsed entries,
* objective details window,
* abandon confirmation,
* screen tracker toggle,
* objective status colors,
* support for progress-based objectives,
* support for simple text objectives.

The objectives UI reads mission data from the mission coordinator instead of referencing a specific mission directly.

---

## Vehicles

The project includes an arcade-style vehicle driving system.

Features include:

* car entering/exiting,
* arcade acceleration,
* steering,
* braking,
* nitro,
* headlights,
* speedometer,
* tachometer,
* vehicle-specific tuning,
* racing integration,
* player/camera switching during vehicle use.

The vehicle handling goal is closer to arcade racers than simulation.

---

## Racing System

The racing system supports multiple event types and race flows.

Implemented or prototyped race modes:

* circuit races,
* sprint races,
* time challenge,
* speed trap,
* elimination concept.

Race features:

* race trigger interaction,
* race event panel,
* loading screen,
* teleport to start point,
* countdown,
* lap logic,
* split gates,
* checkpoints,
* race time tracking,
* lap time UI,
* best time tracking,
* reward handling,
* race pause menu,
* restart/continue flow,
* finish panel,
* optional cinematic finish camera,
* race route minimap overlay.

Race flow:

1. Player enters race trigger.
2. Race panel opens.
3. Player accepts race.
4. Loading screen appears.
5. Player is teleported to race start.
6. Countdown starts.
7. Race begins.
8. Race mode-specific rules run.
9. Finish panel displays result.

---

## Minimap, World Map and GPS

The project contains a reusable minimap/world map/GPS system.

Features include:

* minimap camera,
* minimap road rendering,
* minimap icons,
* race route overlay,
* GPS route rendering,
* dashed GPS routes,
* destination marker,
* HUD arrow,
* world map road rendering,
* world map event icons,
* hover info panel,
* GPS follow/unfollow logic,
* GPS route recalculation,
* delivery mission GPS routing.

The GPS system is reused by:

* race events,
* delivery missions,
* destination tracking,
* minimap rendering,
* world map rendering,
* HUD navigation arrow.

Recent improvements include disabling fog/post-processing on the minimap camera and adjusting minimap colors for better GPS readability.

---

## Bank, ATM and Loan System

The bank system is one of the most complex and debug-heavy systems in the project.

Implemented systems include:

* ATM UI,
* bank employee dialogue,
* account operations,
* deposit,
* withdraw,
* transfer,
* transaction history,
* credit cards,
* card blocking states,
* PIN failed handling,
* owner blocked handling,
* card variant changing,
* cooldowns,
* loan application,
* loan approval logic,
* loan terms,
* active loans,
* deferred payments,
* restructuring,
* tax/interest configuration,
* player cash synchronization.

The loan system includes configurable approval logic and terms depending on player state and history.

This part of the project was especially useful for practicing UI validation, data flow, state management and handling many edge cases.

---

## Platform Minigames

The project also includes a set of platform-style minigames/prototypes.

Current or planned minigame types include:

* bridge,
* maze,
* memory,
* arkanoid-style mode.

The minigame system includes:

* stages,
* lives,
* timers,
* stage reset logic,
* total time tracking,
* current stage time,
* countdowns,
* different difficulty concepts,
* stage-specific rules.

The difficulty plan:

* Easy: current behavior,
* Medium: time limits with lives,
* Hard: stricter rules and additional obstacles.

---

## UI Systems

The project contains many UI modules connected to gameplay systems.

Examples:

* inventory UI,
* box inventory UI,
* weapon HUD,
* gun UI,
* objectives UI,
* mission tracker,
* dialogue UI,
* race UI,
* race final panel,
* race pause panel,
* ATM UI,
* bank dialogue UI,
* card unblock UI,
* loan UI,
* transaction UI,
* minimap UI,
* world map UI,
* communicate/notification UI.

Most gameplay systems interact with UI through separate controllers instead of embedding all UI logic directly into gameplay classes.

---

## Architecture

The project uses several architectural patterns and refactors.

Examples:

### Coordinator / Facade

Used in systems where many smaller components need to be accessed through one clear entry point.

Examples:

* `MissionCoordinator`,
* weapon manager refactor,
* NPC world coordinator concept.

### ScriptableObjects

Used for data-driven configuration.

Examples:

* `MissionDefinition`,
* `DeliveryMissionDefinition`,
* `InventoryItemData`,
* `NPCProfile`,
* weapon/item data.

### Runtime Interfaces

Used to support multiple mission types through a shared contract.

Example:

* `IMissionRuntime`.

### Event Routing

Used for dialogue-to-mission communication.

Example:

* `DialogueMissionEventRouter`.

### UI Separation

Many UI systems are separated into display/controller scripts rather than being placed directly inside gameplay logic.

---

## Example Mission Architecture

A mission consists of:

```text
MissionDefinition
    Static mission data:
    - missionId
    - title
    - description
    - objective text
    - reward text
    - dialogue graphs

MissionRuntime
    Runtime state:
    - not started
    - active
    - ready to claim
    - reward claimed

MissionCoordinator
    Central facade:
    - returns mission state
    - builds active objectives
    - resolves dialogue graphs

NPCMissionGiver
    NPC-side mission list:
    - mission definition
    - NPC role

ObjectivesUI
    Displays active objectives

DialogueGraph
    Handles player/NPC conversation and triggers event keys
```

This structure allows adding new mission types without rewriting the objectives UI or NPC mission list UI.

---

## Example Delivery Mission Flow

```text
Fredo NPC
    Role: Giver
    Mission: Delivery for Fredo

Ralph NPC
    Role: Receiver
    Mission: Delivery for Fredo

DeliveryMissionRuntime
    State:
    NotStarted -> CarryingPackage -> ReadyToClaimAtGiver -> RewardClaimed

InventoryUI
    Receives package item
    Checks if package exists
    Removes package after delivery

GPS
    Accept mission -> route to Ralph
    Delivered package -> route back to Fredo
    Completed mission -> clear route
```

---

## Controls

The project uses different controls depending on game state.

Common controls:

| Action             | Input            |
| ------------------ | ---------------- |
| Move               | WASD             |
| Look               | Mouse            |
| Interact           | E                |
| Inventory          | I                |
| Objectives         | O                |
| Escape / close UI  | ESC              |
| Weapon slots       | 1-4              |
| Drop weapon        | configured input |
| ADS                | right mouse      |
| Fire               | left mouse       |
| Vehicle enter/exit | E                |
| Race restart       | F1               |
| Race continue      | Enter            |

Some controls are handled through `PlayerInputHandler`.

---

## Technical Stack

* Unity 3D
* C#
* Unity NavMesh
* Unity UI / TextMeshPro
* ScriptableObjects
* URP
* RenderTextures for minimap
* Custom gameplay systems
* Custom dialogue graph system
* Custom inventory grid system
* Custom race and GPS systems

---

## Folder Structure

Example high-level project structure:

```text
Assets/
    Scripts/
        Player/
        WeaponsSystem/
        Inventory/
        BankSystem/
        Enemies,Npc/
        MissionsSystem/
        Minimap/
        WorldMap/
        RacingSystem/
        Vehicles/
        Minigames/
        UI/
```

The project has grown into multiple independent systems, each focused on a specific gameplay domain.

---

## Screenshots / GIFs

Recommended screenshots for the repository:

```text
README_Images/
    01_open_world.png
    02_inventory_grid.png
    03_box_inventory.png
    04_weapon_system.png
    05_npc_combat.png
    06_dialogue_system.png
    07_objectives_ui.png
    08_delivery_mission.png
    09_race_event.png
    10_minimap_gps.png
    11_bank_atm.png
```

Suggested sections:

### Inventory and Item Placement

Show the inventory grid with rotated items, stacks and item tooltips.

### Dialogue and Missions

Show NPC dialogue with mission options.

### Delivery Mission

Show package objective and GPS route.

### Racing

Show race countdown, minimap route and finish panel.

### Bank System

Show ATM/loan/card UI.

---

## Current Status

The project is a work-in-progress gameplay prototype.

Implemented and working systems include:

* player movement,
* FPS/TPP weapon handling,
* inventory and box inventory,
* NPC combat and reactions,
* mission/objective system,
* kill mission,
* delivery mission,
* dialogue graph flow,
* minimap/world map/GPS,
* racing modes,
* vehicle driving,
* bank/ATM/loan features,
* UI systems.

The project is actively being expanded with new mission types and gameplay systems.

---

## Planned Features

Possible next additions:

* retrieve item mission,
* escort NPC mission,
* bank transfer mission objective,
* car delivery mission,
* taxi-style mission,
* police chase system,
* save/load for mission states,
* NPC schedule system,
* improved AI LOD,
* object pooling for NPCs and FX,
* better world map icons,
* improved FPS hands animations,
* improved vehicle handling per car type,
* more race types,
* more complete quest chain system.

---

## What This Project Demonstrates

This project demonstrates experience with:

* C# gameplay programming,
* Unity component architecture,
* UI programming,
* gameplay state machines,
* event-driven systems,
* data-driven ScriptableObject workflows,
* inventory systems,
* mission systems,
* NPC AI,
* NavMesh usage,
* minimap/GPS implementation,
* vehicle/racing logic,
* debugging complex interactions,
* refactoring large systems into smaller modules.

The most important part of the project is not a single mechanic, but how many systems interact with each other:

* dialogue triggers missions,
* missions update objectives,
* objectives update HUD,
* delivery missions use inventory,
* missions control GPS routing,
* NPCs provide mission roles,
* bank systems modify player money,
* weapons affect NPC behavior,
* vehicles interact with racing and GPS.

---

## Notes

This is a personal learning and portfolio project.
Some visuals, models and UI elements are placeholders.
The main focus is programming, gameplay systems and architecture.

---

## Author

Created by **Lukasz Jowik** as a Unity gameplay programming portfolio project.
