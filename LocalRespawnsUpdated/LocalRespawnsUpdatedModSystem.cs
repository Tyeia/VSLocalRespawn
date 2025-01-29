using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;


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
        
        var loc = byPlayer.Entity.ServerPos;
        Vec3i newLocation = GenerateSpawnLocation(loc);
        byPlayer.ClearSpawnPosition();
        byPlayer.SetSpawnPosition(new PlayerSpawnPos(newLocation.X, newLocation.Y, newLocation.Z));
    }

    private void OnPlayerRespawn(IServerPlayer byPlayer)
    {
        for (int i = 0; i < lastSpawnLocations.Count; i++)
        {
            if (lastSpawnLocations[i].player == byPlayer)
            {
                
                PlayerSpawnPos loc = new PlayerSpawnPos(lastSpawnLocations[i].position.X, lastSpawnLocations[i].position.Y, lastSpawnLocations[i].position.Z);
                byPlayer.SetSpawnPosition(loc);
                lastSpawnLocations.RemoveAt(i);
                return;
            }
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
            if (block.BlockId != 0 && block.CollisionBoxes != null)
            {
                floorPos.Set(origin.X, i + 1, origin.Z);
                return floorPos;
            }
        }
        // Should only call when there is all air.
        return origin.Copy();
    }
}