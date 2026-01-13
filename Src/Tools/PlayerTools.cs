using Lib.GAB.Tools;

namespace ValBridgeServer.Tools
{
    public class PlayerTools
    {
        [Tool("player_get_health", Description = "Get player's current health statistics")]
        public object GetHealth()
        {
            var player = Player.m_localPlayer;
            if (player == null)
                return new { success = false, error = "No local player found" };

            return new
            {
                success = true,
                health = player.GetHealth(),
                maxHealth = player.GetMaxHealth(),
                healthPercentage = player.GetHealthPercentage() * 100f
            };
        }

        [Tool("player_get_position", Description = "Get player's world position coordinates")]
        public object GetPosition()
        {
            var player = Player.m_localPlayer;
            if (player == null)
                return new { success = false, error = "No local player found" };

            var pos = player.transform.position;
            return new
            {
                success = true,
                position = new { x = pos.x, y = pos.y, z = pos.z }
            };
        }
    }
}
