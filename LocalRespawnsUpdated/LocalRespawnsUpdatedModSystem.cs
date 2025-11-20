using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using System.Linq.Expressions;
using System.Diagnostics.Contracts;
using Vintagestory.API.Client;


namespace LocalRespawns;

public class LocalRespawnsModSystem : ModSystem
{

    private ICoreServerAPI _sapi = null!;

    private int spawnRadi = 10;

    private int worldHeight = 3000;

    private struct lastSpawnLocation
    {
        public Vec3i position;
        public IServerPlayer player;
        public int uses;
    }
    
    private List<lastSpawnLocation> lastSpawnLocations = new List<lastSpawnLocation>();
    
    public class RespawnConfigData
    {
        // Minimum radius away from death point players will respawn. 
        // Set to greater/equal to the MaxDistance to always spawn at the edge.
        public int MinDistance = 0;
        // Maximum radius away from death point that players will respawn.
        public int MaxDistance = 0;
        // The players that will/not be affected by this mod.
        // Each entry should be the player's username.
        public List<String> PlayerList = new List<String>();
        // Whether the Player list is a blacklist or whitelist.
        // True: Only players on the PlayerList are affected by the mod.
        // False: Only players not on the PlayerList are affected by the mod.
        public bool IsWhitelist = false;
    }

    public static RespawnConfigData config;

    /*public override void Start(ICoreAPI api)
    {
        TryToLoadConfig(api);
        spawnRadi = api.World.Config.GetAsInt("spawnRadius", 100);
        //worldHeight = api.World.Config.GetAsInt("MapSizeY", 256);
        Mod.Logger.Notification("Set Local Respawn Radius to world config respawn radius: " +spawnRadi);
    }*/
    
    public override void StartServerSide(ICoreServerAPI api)
    {
        TryToLoadConfig(api);
        _sapi = api;
        api.Event.OnEntityDeath += OnEntityDeath;
        api.Event.PlayerRespawn += OnPlayerRespawn;
        api.Event.ChunkColumnLoaded += OnChunkLoaded;

    }

    private void TryToLoadConfig(ICoreAPI api)
    {
        try
        {
            config = api.LoadModConfig<RespawnConfigData>("RespawnConfig.json");
            if (config == null) // File not found
            {
                config = new RespawnConfigData();
            }
            
            api.StoreModConfig<RespawnConfigData>(config, "RespawnConfig.json");
            if (config.MinDistance < 0)
            {
                config.MinDistance = 0;
            }
            if (config.MaxDistance <= 0)
            {
                config.MaxDistance = 1;
            }
            if (config.MinDistance > config.MaxDistance)
            {
                config.MinDistance = config.MaxDistance;
            }
        }
        catch (Exception e) //Any possible error
        {
            Mod.Logger.Error("Failed to load config! Loading defaults instead.");
            Mod.Logger.Error(e);
            config = new RespawnConfigData();
        }
    }

    private void OnEntityDeath(Entity entity, DamageSource damageSource)
    {
        if (entity is EntityPlayer entityPlayer)
        {
            OnPlayerDeath((IServerPlayer)entityPlayer.Player, damageSource);
        }
    }

    private void OnPlayerDeath(IServerPlayer byPlayer, DamageSource damageSource)
    {
        if (config.IsWhitelist && !config.PlayerList.Contains(byPlayer.PlayerName))
        {
            return;
        } else if (config.PlayerList.Contains(byPlayer.PlayerName))
        {
            return;
        }

        lastSpawnLocation playerData = new lastSpawnLocation();
        playerData.player = byPlayer;
        playerData.position = byPlayer.GetSpawnPosition(true).XYZInt;

        lastSpawnLocations.Add(playerData);
        //Mod.Logger.Notification("Uh oh! Player died! Trying to find floor!");
        var loc = byPlayer.Entity.ServerPos;
        Vec3i newLocation = GenerateSpawnLocation(loc);
        byPlayer.ClearSpawnPosition();
        byPlayer.SetSpawnPosition(new PlayerSpawnPos(newLocation.X, newLocation.Y, newLocation.Z));
    }

    private void OnPlayerRespawn(IServerPlayer byPlayer)
    {
        /*for (int i = 0; i < lastSpawnLocations.Count; i++)
        {
            if (byPlayer == lastSpawnLocations[i].player) {
                BlockPos surface_block = findFloor(byPlayer.Entity.ServerPos.AsBlockPos);
                if (surface_block == new BlockPos(0)) {
                    break;
                }
                Vec3d surf_block_double = surface_block.ToVec3d();
                byPlayer.Entity.TeleportToDouble(surf_block_double.X, surf_block_double.Y, surf_block_double.Z);
                PlayerSpawnPos loc = new PlayerSpawnPos(lastSpawnLocations[i].position.X, lastSpawnLocations[i].position.Y, lastSpawnLocations[i].position.Z);
                lastSpawnLocations[i].player.SetSpawnPosition(loc);
                lastSpawnLocations.RemoveAt(i);
                break;
            }
        }*/
    }

    private void OnChunkLoaded(Vec2i chunkCoord, IWorldChunk[] chunks)
    {
        List<lastSpawnLocation> to_delete = new List<lastSpawnLocation>();
        for (int i = 0; i < lastSpawnLocations.Count; i++)
        {
            if (lastSpawnLocations[i].player.Entity.ServerPos.SquareDistanceTo(lastSpawnLocations[i].player.GetSpawnPosition(false)) < 0.25f) 
            {
                //Mod.Logger.Notification("Tried to teleport the player!");
                // Teleport the player to the ground, to counteract spawning in the ground.
                BlockPos surface_block = findFloor(lastSpawnLocations[i].player.Entity.ServerPos.AsBlockPos);
                if (surface_block == new BlockPos(0)){
                    //Mod.Logger.Notification("Failed to find floor. Try again later.");
                    continue;
                }
                Vec3d surf_block_double = surface_block.ToVec3d();
                lastSpawnLocations[i].player.Entity.TeleportToDouble(surf_block_double.X, surf_block_double.Y, surf_block_double.Z);
                PlayerSpawnPos loc = new PlayerSpawnPos(lastSpawnLocations[i].position.X, lastSpawnLocations[i].position.Y, lastSpawnLocations[i].position.Z);
                lastSpawnLocations[i].player.SetSpawnPosition(loc);
                to_delete.Append(lastSpawnLocations[i]);
                break;
             }
        }
        for (int i = 0; i < to_delete.Count; i++)
        {
            lastSpawnLocations.Remove(to_delete[i]);
        }
        


        
    }
    private Vec3i GenerateSpawnLocation(EntityPos pos)
    {
        Random rnd = new Random();
        int lower_bound = config.MinDistance;
        int upper_bound = config.MaxDistance;
        Mod.Logger.Notification("Bounds: " + lower_bound.ToString() + ", " + upper_bound.ToString());

        // Generates evenly distributed points within the upper/lower bounds of two circles.
        double radius = Math.Sqrt(rnd.NextDouble()*(Math.Pow(upper_bound,2) - Math.Pow(lower_bound,2)) + Math.Pow(lower_bound,2));
        double theta = rnd.NextDouble() * 2 * Math.PI;
        // Convert to Cartesian Coordinates
        double x = radius * Math.Cos(theta);
        double y = radius * Math.Sin(theta);
        EntityPos NewPosition = pos.Copy();
        NewPosition.X += x;
        NewPosition.Z += y;
        Mod.Logger.Warning("Generating spawn location! Printing Debug Information:");
        Mod.Logger.Notification("Death Position: " + pos.ToString());
        double length = Math.Sqrt(Math.Pow(x,2) + Math.Pow(y,2));
        Mod.Logger.Notification("New Position: (" + NewPosition.X.ToString() + ", " + NewPosition.Z.ToString() + "); (Length: " + length.ToString() + ", Radius: " + radius.ToString() + ")");

        BlockPos floorPos = findFloor(NewPosition.AsBlockPos);
        if (floorPos == new BlockPos(0)) {
            floorPos.X = NewPosition.AsBlockPos.X;
            floorPos.Z = NewPosition.AsBlockPos.Z;
            floorPos.Y = 100;
        }


        Vec3i newPos = floorPos.ToVec3i();
        
        return newPos;
    }

    private BlockPos findFloor(BlockPos origin)
    {
        var floorPos = origin.Copy();

        for (int i = worldHeight - 1; i > 0; i--)
        {

            floorPos.Set(origin.X, i, origin.Z);
            var block = _sapi.World.BlockAccessor.GetBlock(floorPos);
            //Mod.Logger.Notification($"Checking Block; Location: {floorPos}");
            if (block.BlockId != 0 && block.CollisionBoxes != null)
            {
                //Mod.Logger.Notification("Successfully found a floor!");
                floorPos.Set(origin.X, i + 1, origin.Z);
                return floorPos;
            }
        }
        // Should only call when there is all air.
        //Mod.Logger.Notification("Uh oh! Returning origin!");
        return new BlockPos(0);
    }
}
