using MMOServer.Server;
using MMOServer.Configuration;
using WebSocketSharp.Server;
using System.Diagnostics;

namespace MMOServer
{
    class Program
    {
        private static WebSocketServer? server;
        private static readonly object consoleLock = new object();
        
        static void Main(string[] args)
        {
            // Configura encoding para exibir caracteres especiais
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            
            PrintBanner();
            
            try
            {
                InitializeServer();
                StartCommandLoop();
            }
            catch (Exception ex)
            {
                LogError($"Fatal error: {ex.Message}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
            finally
            {
                Shutdown();
            }
        }

        private static void PrintBanner()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔════════════════════════════════════════╗");
            Console.WriteLine("║     MMO SERVER - Professional v3.0    ║");
            Console.WriteLine("║  Authoritative Server Architecture     ║");
            Console.WriteLine("╚════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static void InitializeServer()
        {
            var stopwatch = Stopwatch.StartNew();
            
            // [1/8] Configuration
            Log("[1/8] Loading application settings...");
            ConfigLoader.Instance.LoadConfiguration();
            
            // [2/8] JSON Configurations
            Log("[2/8] Loading JSON configurations...");
            ConfigManager.Instance.Initialize();
            
            // [3/8] Database
            Log("[3/8] Initializing database...");
            DatabaseHandler.Instance.Initialize();
            
            // [4/8] Terrain
            Log("[4/8] Loading terrain heightmap...");
            TerrainHeightmap.Instance.Initialize();
            
            // [5/8] Items
            Log("[5/8] Initializing item system...");
            ItemManager.Instance.Initialize();
            
            // [6/8] Skills
            Log("[6/8] Initializing skill system...");
            SkillManager.Instance.Initialize();
            
            // [7/8] World
            Log("[7/8] Initializing world managers...");
            WorldManager.Instance.Initialize();
            
            // [8/8] WebSocket Server
            Log("[8/8] Starting WebSocket server...");
            var settings = ConfigLoader.Instance.Settings.ServerSettings;
            string serverUrl = $"ws://{settings.Host}:{settings.Port}";
            
            server = new WebSocketServer(serverUrl);
            server.AddWebSocketService<GameServer>("/game");
            server.Start();
            
            stopwatch.Stop();
            
            Console.WriteLine();
            PrintSuccessMessage(serverUrl, stopwatch.ElapsedMilliseconds);
            PrintFeatures();
            PrintTerrainStatus();
            PrintConfigFiles();
            PrintCommands();
        }

        private static void PrintSuccessMessage(string url, long elapsedMs)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("╔════════════════════════════════════════╗");
            Console.WriteLine($"║  ✓ Server Online: {url,-18} ║");
            Console.WriteLine($"║  ✓ Startup Time: {elapsedMs}ms{new string(' ', 21 - elapsedMs.ToString().Length)}║");
            Console.WriteLine("╚════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static void PrintFeatures()
        {
            Console.WriteLine("Features:");
            var features = new[]
            {
                "JSON Configuration System",
                "3D Terrain Heightmap Support",
                "Authoritative Movement",
                "Combat System (Ragnarok-style)",
                "Monster AI with Terrain Awareness",
                "Experience & Leveling",
                "Death & Respawn",
                "Item & Inventory System",
                "Loot System with Drop Tables",
                "Area-Based Monster Spawning",
                "Skill System with Effects",
                "Anti-Cheat Movement Validation",
                "Performance Metrics & Monitoring"
            };
            
            foreach (var feature in features)
            {
                Console.WriteLine($"  • {feature}");
            }
            Console.WriteLine();
        }

        private static void PrintTerrainStatus()
        {
            if (TerrainHeightmap.Instance.IsLoaded)
            {
                Console.WriteLine("Terrain Status:");
                Console.WriteLine(TerrainHeightmap.Instance.GetTerrainInfo());
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Terrain Status: Using flat ground (Y=0)");
                Console.WriteLine("  Export heightmap: Unity > MMO > Export Terrain Heightmap");
                Console.ResetColor();
            }
            Console.WriteLine();
        }

        private static void PrintConfigFiles()
        {
            Console.WriteLine("Configuration Files:");
            var configs = new Dictionary<string, string>
            {
                { "appsettings.json", "Server & Database settings" },
                { "monsters.json", "Monster templates" },
                { "classes.json", "Class configurations" },
                { "terrain_heightmap.json", "Terrain data" },
                { "items.json", "Item definitions" },
                { "loot_tables.json", "Monster drop tables" },
                { "spawn_areas.json", "Spawn area definitions" },
                { "skills.json", "Skill definitions" }
            };
            
            foreach (var config in configs)
            {
                Console.WriteLine($"  • Config/{config.Key} - {config.Value}");
            }
            Console.WriteLine();
        }

        private static void PrintCommands()
        {
            Console.WriteLine("Commands:");
            var commands = new Dictionary<string, string>
            {
                { "status", "Show server status" },
                { "players", "List online players" },
                { "monsters", "List all monsters" },
                { "areas", "Show spawn area statistics" },
                { "items", "Show item statistics" },
                { "loot", "Test loot tables" },
                { "combat", "Show combat statistics" },
                { "balance", "Test combat balance" },
                { "respawn", "Force respawn all dead monsters" },
                { "reload", "Reload JSON configurations" },
                { "config", "Show current configuration" },
                { "terrain", "Show terrain info" },
                { "metrics", "Show performance metrics" },
                { "health", "Database health check" },
                { "clear", "Clear console" },
                { "help", "Show all commands" },
                { "exit", "Stop the server" }
            };
            
            foreach (var cmd in commands)
            {
                Console.WriteLine($"  • {cmd.Key,-12} - {cmd.Value}");
            }
            Console.WriteLine();
        }

        private static void StartCommandLoop()
        {
            bool running = true;
            
            while (running)
            {
                Console.Write("> ");
                string? input = Console.ReadLine();
                
                if (string.IsNullOrWhiteSpace(input))
                    continue;
                
                try
                {
                    running = ProcessCommand(input.Trim().ToLower());
                }
                catch (Exception ex)
                {
                    LogError($"Command error: {ex.Message}");
                }
            }
        }

        private static bool ProcessCommand(string command)
        {
            switch (command)
            {
                case "status":
                    CommandStatus();
                    break;
                
                case "players":
                    CommandPlayers();
                    break;
                
                case "monsters":
                    CommandMonsters();
                    break;
                
                case "areas":
                    CommandAreas();
                    break;
                
                case "items":
                    CommandItems();
                    break;
                
                case "loot":
                    CommandLoot();
                    break;
                
                case "combat":
                    CommandCombat();
                    break;
                
                case "balance":
                    CommandBalance();
                    break;
                
                case "respawn":
                    CommandRespawn();
                    break;
                
                case "reload":
                    CommandReload();
                    break;
                
                case "config":
                    CommandConfig();
                    break;
                
                case "terrain":
                    CommandTerrain();
                    break;
                
                case "metrics":
                    CommandMetrics();
                    break;
                
                case "health":
                    CommandHealth();
                    break;
                
                case "clear":
                    Console.Clear();
                    PrintBanner();
                    break;
                
                case "help":
                    PrintCommands();
                    break;
                
                case "exit":
                case "quit":
                case "stop":
                    return false;
                
                default:
                    LogWarning($"Unknown command: '{command}'. Type 'help' for available commands.");
                    break;
            }
            
            return true;
        }

        // ==================== COMMAND IMPLEMENTATIONS ====================

        private static void CommandStatus()
        {
            Console.WriteLine();
            Console.WriteLine("🖥️ Server Status:");
            Console.WriteLine($"  Uptime: {WorldManager.Instance.GetUptime():d'd 'h'h 'm'm'}");
            Console.WriteLine($"  Players: {PlayerManager.Instance.GetAllPlayers().Count}");
            Console.WriteLine($"  Monsters: {MonsterManager.Instance.GetAliveMonsters().Count}/{MonsterManager.Instance.GetAllMonsters().Count}");
            Console.WriteLine($"  Spawn Areas: {SpawnAreaManager.Instance.GetAllAreas().Count}");
            Console.WriteLine($"  Classes: {ConfigManager.Instance.ClassConfig.classes.Count}");
            Console.WriteLine($"  Terrain: {(TerrainHeightmap.Instance.IsLoaded ? "Loaded" : "Flat")}");
            Console.WriteLine();
        }

        private static void CommandPlayers()
        {
            Console.WriteLine();
            var players = PlayerManager.Instance.GetAllPlayers();
            
            if (players.Count == 0)
            {
                Console.WriteLine("👤 No players online");
            }
            else
            {
                Console.WriteLine($"👤 Players Online ({players.Count}):");
                foreach (var player in players)
                {
                    var c = player.character;
                    Console.WriteLine($"  [{player.sessionId[..8]}] {c.nome}");
                    Console.WriteLine($"    Lv.{c.level} {c.classe} | HP:{c.health}/{c.maxHealth} | Pos:({c.position.x:F1},{c.position.z:F1})");
                    Console.WriteLine($"    Combat: {(player.inCombat ? $"Yes (Target: {player.targetMonsterId})" : "No")}");
                }
            }
            Console.WriteLine();
        }

        private static void CommandMonsters()
        {
            Console.WriteLine();
            var monsters = MonsterManager.Instance.GetAllMonsters();
            var alive = monsters.Count(m => m.isAlive);
            
            Console.WriteLine($"👹 Monsters ({alive}/{monsters.Count} alive):");
            
            var grouped = monsters.GroupBy(m => m.template.name)
                .OrderBy(g => g.Key);
            
            foreach (var group in grouped)
            {
                var aliveCount = group.Count(m => m.isAlive);
                var total = group.Count();
                Console.WriteLine($"  {group.Key}: {aliveCount}/{total} alive");
            }
            Console.WriteLine();
        }

        private static void CommandAreas()
        {
            Console.WriteLine();
            Console.WriteLine("📍 Spawn Areas:");
            var areas = SpawnAreaManager.Instance.GetAllAreas();
            var stats = MonsterManager.Instance.GetSpawnAreaStats();
            
            foreach (var area in areas)
            {
                Console.WriteLine($"  [{area.id}] {area.name}");
                Console.WriteLine($"    Type: {area.shape} | Center: ({area.centerX:F1},{area.centerZ:F1})");
                
                if (stats.TryGetValue(area.id, out var stat))
                {
                    Console.WriteLine($"    Monsters: {stat.aliveMonsters}/{stat.totalMonsters} | Combat: {stat.inCombat}");
                }
            }
            Console.WriteLine();
        }

        private static void CommandItems()
        {
            Console.WriteLine();
            Console.WriteLine("📦 Item Statistics:");
            var players = PlayerManager.Instance.GetAllPlayers();
            
            if (players.Count == 0)
            {
                Console.WriteLine("  No players online");
            }
            else
            {
                foreach (var player in players)
                {
                    var inv = ItemManager.Instance.LoadInventory(player.character.id);
                    Console.WriteLine($"  {player.character.nome}:");
                    Console.WriteLine($"    Gold: {inv.gold} | Items: {inv.items.Count}/{inv.maxSlots}");
                }
            }
            Console.WriteLine();
        }

        private static void CommandLoot()
        {
            Console.WriteLine();
            Console.WriteLine("💰 Testing Loot Tables:");
            var monsters = MonsterManager.Instance.GetAllMonsters().Take(5);
            
            foreach (var m in monsters)
            {
                Console.WriteLine($"  {m.template.name}:");
                var loot = ItemManager.Instance.GenerateLoot(m.templateId);
                Console.WriteLine($"    Gold: {loot.gold}");
                Console.WriteLine($"    Items: {loot.items.Count}");
            }
            Console.WriteLine();
        }

        private static void CommandCombat()
        {
            Console.WriteLine();
            Console.WriteLine("⚔️ Combat Statistics:");
            var players = PlayerManager.Instance.GetAllPlayers();
            
            foreach (var player in players)
            {
                Console.WriteLine(CombatManager.Instance.GetCombatStats(player));
            }
            Console.WriteLine();
        }

        private static void CommandBalance()
        {
            Console.WriteLine();
            Console.WriteLine("⚖️ Combat Balance Test:");
            
            var player = PlayerManager.Instance.GetAllPlayers().FirstOrDefault();
            var monster = MonsterManager.Instance.GetAliveMonsters().FirstOrDefault();
            
            if (player == null || monster == null)
            {
                Console.WriteLine("  Need at least 1 player and 1 monster online");
                Console.WriteLine();
                return;
            }
            
            Console.WriteLine($"  Testing: {player.character.nome} vs {monster.template.name}");
            Console.WriteLine($"  Simulating 10 attacks each...");
            Console.WriteLine();
            
            // Test player attacks
            int playerDamage = 0;
            for (int i = 0; i < 10; i++)
            {
                var result = CombatManager.Instance.PlayerAttackMonster(player, monster);
                playerDamage += result.damage;
                monster.currentHealth = monster.template.maxHealth;
            }
            
            Console.WriteLine($"  Player → Monster: {playerDamage / 10} avg damage");
            
            // Test monster attacks
            int monsterDamage = 0;
            int originalHP = player.character.health;
            for (int i = 0; i < 10; i++)
            {
                var result = CombatManager.Instance.MonsterAttackPlayer(monster, player);
                monsterDamage += result.damage;
                player.character.health = originalHP;
            }
            
            Console.WriteLine($"  Monster → Player: {monsterDamage / 10} avg damage");
            Console.WriteLine();
        }

        private static void CommandRespawn()
        {
            Console.WriteLine();
            Console.WriteLine("✨ Force respawning dead monsters...");
            
            int count = 0;
            foreach (var m in MonsterManager.Instance.GetAllMonsters().Where(m => !m.isAlive))
            {
                m.Respawn();
                count++;
            }
            
            Console.WriteLine($"  Respawned {count} monsters");
            Console.WriteLine();
        }

        private static void CommandReload()
        {
            Console.WriteLine();
            Console.WriteLine("🔄 Reloading configurations...");
            
            ConfigManager.Instance.ReloadConfigs();
            ItemManager.Instance.ReloadConfigs();
            SkillManager.Instance.ReloadConfigs();
            MonsterManager.Instance.ReloadFromConfig();
            
            Console.WriteLine("✅ All configurations reloaded");
            Console.WriteLine();
        }

        private static void CommandConfig()
        {
            Console.WriteLine();
            Console.WriteLine("📋 Current Configuration:");
            
            var server = ConfigLoader.Instance.Settings.ServerSettings;
            Console.WriteLine($"  Server: {server.Host}:{server.Port}");
            Console.WriteLine($"  Max Connections: {server.MaxConnections}");
            Console.WriteLine($"  Update Rate: {server.UpdateRate}ms");
            
            var db = ConfigLoader.Instance.Settings.DatabaseSettings;
            Console.WriteLine($"  Database: {db.Server}:{db.Port}/{db.Database}");
            Console.WriteLine($"  User: {db.UserId}");
            Console.WriteLine();
        }

        private static void CommandTerrain()
        {
            Console.WriteLine();
            if (TerrainHeightmap.Instance.IsLoaded)
            {
                Console.WriteLine(TerrainHeightmap.Instance.GetTerrainInfo());
            }
            else
            {
                Console.WriteLine("Terrain: Not loaded (flat ground)");
            }
            Console.WriteLine();
        }

        private static void CommandMetrics()
        {
            Console.WriteLine();
            Console.WriteLine("📊 Performance Metrics:");
            Console.WriteLine(WorldManager.Instance.GetServerStats());
            Console.WriteLine();
        }

        private static void CommandHealth()
        {
            Console.WriteLine();
            Console.WriteLine("🏥 Database Health Check:");
            var (healthy, message) = DatabaseHandler.Instance.HealthCheck();
            
            if (healthy)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  ✓ {message}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ✗ {message}");
            }
            Console.ResetColor();
            Console.WriteLine();
        }

        // ==================== LOGGING ====================

        private static void Log(string message)
        {
            lock (consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(message);
                Console.ResetColor();
            }
        }

        private static void LogWarning(string message)
        {
            lock (consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"⚠️ {message}");
                Console.ResetColor();
            }
        }

        private static void LogError(string message)
        {
            lock (consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ {message}");
                Console.ResetColor();
            }
        }

        // ==================== SHUTDOWN ====================

        private static void Shutdown()
        {
            Console.WriteLine();
            Console.WriteLine("🛑 Shutting down server...");
            
            try
            {
                WorldManager.Instance.Shutdown();
                server?.Stop();
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✅ Server stopped successfully");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                LogError($"Shutdown error: {ex.Message}");
            }
        }
    }
}
