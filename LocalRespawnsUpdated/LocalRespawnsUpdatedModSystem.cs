using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using System.Linq.Expressions;


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
    
    public override void Start(ICoreAPI api)
    {

        spawnRadi = api.World.Config.GetAsInt("spawnRadius", 100);
        //worldHeight = api.World.Config.GetAsInt("MapSizeY", 256);
        Mod.Logger.Notification("Set Local Respawn Radius to world config respawn radius: " +spawnRadi);
    }
    
    public override void StartServerSide(ICoreServerAPI api)
    {
        _sapi = api;
        api.Event.OnEntityDeath += OnEntityDeath;
        api.Event.PlayerRespawn += OnPlayerRespawn;
        api.Event.ChunkColumnLoaded += OnChunkLoaded;

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
        List<int> to_delete = new List<int>();
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
                to_delete.Append(i);
                break;
             }
        }
        for (int i = to_delete.Count-1; i >= 0; i--)
        {
            lastSpawnLocations.RemoveAt(i);
        }
        


        
    }
    private Vec3i GenerateSpawnLocation(EntityPos pos)
    {
        Random rnd = new Random();
        int posNeg = rnd.NextDouble() < 0.5 ? -1 : 1;
        double randx = rnd.NextDouble() * spawnRadi * posNeg;
        posNeg = rnd.NextDouble() < 0.5 ? -1 : 1;
        double randz = rnd.NextDouble() * spawnRadi * posNeg;
        EntityPos NewPosition = pos.Copy();
        NewPosition.X += randx;
        NewPosition.Z += randz;
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
