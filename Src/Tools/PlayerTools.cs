using System;
using Lib.GAB.Tools;

namespace ValBridgeServer.Tools
{
    /// <summary>
    /// Tools for the Player class (Player.m_localPlayer)
    /// </summary>
    public class PlayerTools
    {
        /// <summary>
        /// Get the player's current health statistics
        /// </summary>
        [Tool("player_get_health", Description = "Get player's current health statistics")]
        public object GetHealth()
        {
            try
            {
                var player = Player.m_localPlayer;
                if (player == null)
                {
                    return new
                    {
                        success = false,
                        error = "No local player found. Are you in-game?"
                    };
                }

                var health = player.GetHealth();
                var maxHealth = player.GetMaxHealth();
                var healthPercentage = player.GetHealthPercentage();

                return new
                {
                    success = true,
                    health = health,
                    maxHealth = maxHealth,
                    healthPercentage = healthPercentage * 100f
                };
            }
            catch (Exception ex)
            {
                ValBridgeServerPlugin.ModLogger.LogError($"Error getting player health: {ex.Message}");
                return new
                {
                    success = false,
                    error = ex.Message
                };
            }
        }

        /// <summary>
        /// Get the player's world position
        /// </summary>
        [Tool("player_get_position", Description = "Get player's world position coordinates")]
        public object GetPosition()
        {
            try
            {
                var player = Player.m_localPlayer;
                if (player == null)
                {
                    return new
                    {
                        success = false,
                        error = "No local player found. Are you in-game?"
                    };
                }

                var position = player.transform.position;

                return new
                {
                    success = true,
                    position = new
                    {
                        x = position.x,
                        y = position.y,
                        z = position.z
                    }
                };
            }
            catch (Exception ex)
            {
                ValBridgeServerPlugin.ModLogger.LogError($"Error getting player position: {ex.Message}");
                return new
                {
                    success = false,
                    error = ex.Message
                };
            }
        }
    }
}
