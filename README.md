# ValBridgeServer

A Valheim BepInEx mod that enables AI agents to control and inspect Valheim through the [GABP (Game Agent Bridge Protocol)](https://github.com/jneb802/GABS/blob/main/docs/GABP.md). Built using forked verions of [@pardeike](https://github.com/pardeike)'s [GABS](https://github.com/pardeike/GABS) and [Lib.GAB](https://github.com/pardeike/Lib.GAB) frameworks that are adapted to work with Valheim.

**Note**: Tested with Claude Code. Not compatible with Cursor as it doesn't dynamically reload MCP tools.

## 🏗️ Architecture

```
AI Agent (Claude Code)
  ↕ MCP Protocol
GABS (orchestration server)
  ↕ GABP Protocol
ValBridgeServer (this mod - GABP server)
  ↓ Uses
Valheim Game APIs
```

**Important**: ValBridgeServer acts as a GABP **server** (listens for connections), while GABS acts as a GABP **client** (connects to your mod).

## 🔗 Required Components

This mod requires two other components to function:

1. **[Lib.GAB](https://github.com/jneb802/Lib.GAB)** - .NET library implementing the GABP protocol
2. **[GABS](https://github.com/jneb802/GABS)** - Game Agent Bridge Server that connects AI agents to games

## ✨ Available Tools

### Player Tools
- **`player_get_health`** - Get player's current health statistics (health, max health, percentage)
- **`player_get_position`** - Get player's world position coordinates (x, y, z)

### Terminal Tools
- **`run_command`** - Execute Valheim console commands (e.g., `spawn Boar 1 1`, `god`, `heal`, `tod 0`)

### ZNetScene Tools (Prefab Registry)
- **`znetscene_search_prefabs`** - Search for prefabs by name (partial match, case-insensitive)
- **`znetscene_get_prefab`** - Get detailed prefab information by exact name
- **`znetscene_list_prefab_names`** - List all available prefab names with optional prefix filter

### Unity Explorer Tools (Scene Inspection)
- **`unity_search_objects`** - Search for Unity objects (GameObjects, Components) by name/type with filters
- **`unity_search_singletons`** - Find singleton instances by type name
- **`unity_get_loaded_scenes`** - Get list of all currently loaded scenes (including DontDestroyOnLoad)
- **`unity_get_scene_roots`** - Get root GameObjects in a scene by handle
- **`unity_inspect_object`** - Get detailed information about any Unity object by instance ID
- **`unity_read_component`** - Read component data from a GameObject by type name
- **`unity_get_children`** - Get child GameObjects of a parent by instance ID

### Event Channels
- `player/death` - Player death notifications
- `player/health_changed` - Health change events

## 📦 Installation

### Prerequisites
- **Valheim** via Steam
- **BepInEx 5.4.2200+** - For macOS, follow [this guide](https://www.reddit.com/r/valheim/comments/1dcko3i/guide_running_mods_on_macos/) to set up modded Valheim with `run_bepinex.sh`
- **.NET SDK** for building
- **[GABS](https://github.com/jneb802/GABS)** installed
- **[Lib.GAB](https://github.com/jneb802/Lib.GAB)** - Clone and build, reference the DLL

### Steps

1. **Build and Install Dependencies**
   ```bash
   # Clone and build Lib.GAB
   git clone https://github.com/jneb802/Lib.GAB.git
   cd Lib.GAB/Lib.GAB
   dotnet build
   cd ../..
   
   # Clone and build ValBridgeServer
   git clone https://github.com/jneb802/ValBridgeServer.git
   cd ValBridgeServer
   dotnet build
   
   # Copy all required DLLs to BepInEx plugins
   cp bin/Debug/ValBridgeServer.dll "<Valheim>/BepInEx/plugins/"
   cp ../Lib.GAB/Lib.GAB/bin/Debug/netstandard2.0/Lib.GAB.dll "<Valheim>/BepInEx/plugins/"
   cp bin/Debug/Newtonsoft.Json.dll "<Valheim>/BepInEx/plugins/"
   ```

2. **Configure GABS for Valheim** ([Configuration Guide](https://github.com/jneb802/GABS/blob/main/docs/CONFIGURATION.md))
   
   For vanilla Valheim:
   ```bash
   gabs games add valheim --steam-app-id 892970
   ```
   
   For modded Valheim (macOS with BepInEx):
   ```bash
   gabs games add valheim
   # When prompted, select "DirectPath" mode
   # Target: /path/to/Valheim/run_bepinex.sh
   # Working Directory: /path/to/Valheim
   # Stop Process Name: Valheim
   ```
   
   Example config:
   ```
   Launch Mode: DirectPath
   Target: ~/Library/Application Support/Steam/steamapps/common/Valheim/run_bepinex.sh
   Stop Process Name: Valheim
   ```

3. **Add GABS MCP Server**
   
   Add to your MCP settings (e.g., Claude Desktop `config.json`):
   ```json
   {
     "mcpServers": {
       "gabs": {
         "command": "gabs",
         "args": ["server"]
       }
     }
   }
   ```

4. **Launch and Connect**

   In Claude Code:
   ```
   Tell Claude to launch Valheim using GABS
   Wait for Valheim to start, launch into world. 
   Tell Claude to use game connect tool.
   Tools should sync. 
   Now you can access any of the available tools.
   ```

## 💡 Example Usage

```
AI: "What's the player's current health?"
> Uses player_get_health
> Returns: { "success": true, "health": 25.0, "maxHealth": 25.0, "healthPercentage": 100.0 }

AI: "Where is the player?"
> Uses player_get_position  
> Returns: { "success": true, "position": { "x": 123.45, "y": 50.0, "z": -67.89 } }

AI: "Spawn 5 boars near me"
> Uses run_command with "spawn Boar 5"
> Returns: { "success": true, "command": "spawn Boar 5", "queued": true }

AI: "Search for all wolf prefabs"
> Uses znetscene_search_prefabs with nameFilter: "wolf"
> Returns: List of matching prefabs (Wolf, Fenring, etc.)

AI: "Find the Player singleton"
> Uses unity_search_singletons with typeFilter: "Player"
> Returns: Player singleton instance with ID for further inspection
```

## 📄 License

MIT License - see LICENSE file for details.

## 🔗 Related Projects

- **[GABS](https://github.com/jneb802/GABS)** - Game Agent Bridge Server (orchestrator)
- **[Lib.GAB](https://github.com/jneb802/Lib.GAB)** - GABP protocol library for .NET
- **[GABP Specification](https://github.com/jneb802/GABS/blob/main/docs/GABP.md)** - Protocol documentation