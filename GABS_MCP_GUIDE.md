# GABS MCP Server Guide

Quick reference for using GABS with AI assistants (Claude Code, Cursor, etc.)

## Setup

### Claude Code
```bash
claude mcp add --transport stdio --scope user gabs -- /path/to/gabs server
```

### Cursor
Add to `~/.cursor/mcp.json`:
```json
{
  "mcpServers": {
    "gabs": {
      "command": "/path/to/gabs",
      "args": ["server"]
    }
  }
}
```

## Core Tools

| Tool | Description | Example |
|------|-------------|---------|
| `games.list` | List all configured games | - |
| `games.start` | Launch a game | `{"gameId": "valheim"}` |
| `games.stop` | Stop gracefully | `{"gameId": "valheim"}` |
| `games.kill` | Force stop | `{"gameId": "valheim"}` |
| `games.status` | Check if running | `{"gameId": "valheim"}` |
| `games.tools` | List game-specific tools | `{"gameId": "valheim"}` |
| `games.connect` | Manually connect to GABP | `{"gameId": "valheim"}` |

## Workflow

1. **Start the game**: `games.start valheim`
2. **Wait for mod to load**: Game must be fully loaded with GABP mod active
3. **Connect**: `games.connect valheim` (or wait for auto-connect)
4. **Discover tools**: `games.tools valheim`
5. **Use game tools**: e.g., `valheim.get_player_health`

## Dynamic Tool Discovery

When a GABP-enabled game connects:
- GABS discovers tools exposed by the game mod
- Tools are prefixed with game ID (e.g., `valheim.get_player_health`)
- Claude Code receives `ToolListChangedNotification` and auto-refreshes
- Cursor requires restart to see new tools (no dynamic refresh)

## Game-Specific Tools

After connecting, game mods expose their own tools:

```
valheim.get_player_health    - Get player health stats
valheim.get_player_position  - Get world coordinates
```

Use `games.tools {gameId}` to see available tools for each game.

## Troubleshooting

### "GABP connection failed/timeout"
- Game is starting but mod hasn't initialized yet
- Wait for game to fully load, then run `games.connect`

### "0 tools discovered"
- Mod is running but tools aren't registered
- Check mod logs for GABP server startup messages

### Tools not appearing in Cursor
- Cursor caches tools at startup
- Restart Cursor after `games.connect` succeeds

### Tools not appearing in Claude Code
- Claude Code supports dynamic updates
- Run `/mcp` to check server status
- Verify `ToolListChangedNotification` was received

## Architecture

```
AI Agent ←→ MCP ←→ GABS ←→ GABP ←→ Game Mod ←→ Game
           (stdio)      (TCP)
```

- **GABS** = MCP server + GABP client
- **Game Mod** = GABP server (your mod listens on a port)
- **GABP** = Protocol for game-AI communication

## Environment Variables (for Mods)

GABS passes these to launched games:
- `GABP_SERVER_PORT` - Port the mod should listen on
- `GABP_TOKEN` - Auth token for GABS connection
- `GABS_GAME_ID` - Game identifier

## References

- [GABS Documentation](https://github.com/pardeike/gabs)
- [GABP Protocol](https://github.com/pardeike/GABP)
- [Mod Development Guide](https://github.com/pardeike/gabs/blob/main/docs/MOD_DEVELOPMENT.md)
