using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace EverjadeFixPatch
{
	public class EverjadeFixPatch : Mod
	{

    }

    //Duplication fix
    public class FurnitureDropFixSystem : ModSystem
    {
        private static readonly HashSet<string> _excludedTypes = new()
        {
            "JadeFables.Tiles.JadePylon.JadePylonTile",
            "JadeFables.Tiles.WarriorStatue.WarriorStatue",
            "JadeFables.Tiles.SpringChest.SpringChest"
        };

        private static readonly List<IDisposable> _hooks = new();

        public override void Load()
        {
            if (!ModLoader.TryGetMod("JadeFables", out Mod jadeFables))
                return;

            Assembly asm = jadeFables.GetType().Assembly;

            foreach (Type type in asm.GetTypes())
            {
                if (!typeof(ModTile).IsAssignableFrom(type) || type.IsAbstract)
                    continue;

                if (_excludedTypes.Contains(type.FullName ?? string.Empty))
                    continue;

                var method = type.GetMethod(
                    nameof(ModTile.KillMultiTile),
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                    null,
                    new[] { typeof(int), typeof(int), typeof(int), typeof(int) },
                    null);

                if (method is null)
                    continue;

                _hooks.Add(new Hook(method, (Action<ModTile, int, int, int, int> orig, ModTile self, int i, int j, int frameX, int frameY) => { }));
            }

            // Directly Spring Chest fix
            var chestType = asm.GetType("JadeFables.Tiles.SpringChest.SpringChest");
            var chestMethod = chestType?.GetMethod(nameof(ModTile.KillMultiTile), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            if (chestMethod != null)
            {
                _hooks.Add(new Hook(chestMethod,
                    (Action<ModTile, int, int, int, int> orig,
                     ModTile self, int i, int j, int frameX, int frameY) =>
                    {
                        Chest.DestroyChest(i, j);
                    }));
            }
        }

        public override void Unload()
        {
            foreach (IDisposable hook in _hooks)
                hook.Dispose();

            _hooks.Clear();
        }
    }

    // Jade Ore no drop fix
    public class JadeOreDropFix : GlobalTile
    {
        public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            var tile = TileLoader.GetTile(type);

            if (tile?.Mod?.Name == "JadeFables" && tile.Name == "JadeOre")
            {
                noItem = false;
            }
        }

        public override void Drop(int i, int j, int type)
        {
            var tile = TileLoader.GetTile(type);

            if (tile?.Mod?.Name == "JadeFables" && tile.Name == "JadeOre")
            {
                Mod jadeMod = ModLoader.GetMod("JadeFables");
                if (jadeMod == null)
                    return;

                int itemType = jadeMod.Find<ModItem>("JadeChunk").Type;

                Item.NewItem(WorldGen.GetItemSource_FromTileBreak(i, j), new Microsoft.Xna.Framework.Vector2(i * 16, j * 16), itemType, 1);
            }
        }
    }

    // Jade Hook "Here" message fix
    public class JadeHookFixSystem : ModSystem
    {
        private static IDisposable? _hook;

        public override void Load()
        {
            if (!ModLoader.TryGetMod("JadeFables", out Mod jadeFables))
                return;

            Assembly asm = jadeFables.GetType().Assembly;
            var projType = asm.GetType("JadeFables.Items.Jade.JadeHook.JadeHookProjectile");

            var aiMethod = projType.GetMethod("AI", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            _hook = new Hook(aiMethod, (Action<ModProjectile> orig, ModProjectile self) => { });
        }

        public override void Unload()
        {
            _hook?.Dispose();
            _hook = null;
        }
    }

    // Fishing Spot buffs fix
    public class FishingSpotImmunity : GlobalNPC
    {
        private static int _fishingSpotType = -1;

        public override void SetStaticDefaults()
        {
            if (ModLoader.TryGetMod("JadeFables", out Mod jf) &&
                jf.TryFind<ModNPC>("FishingSpot", out ModNPC spot))
            {
                _fishingSpotType = spot.Type;
            }
        }

        public override void SetDefaults(NPC npc)
        {
            if (npc.type != _fishingSpotType)
                return;

            for (int i = 0; i < npc.buffImmune.Length; i++)
                npc.buffImmune[i] = true;
        }

        public override void PostAI(NPC npc)
        {
            if (npc.type != _fishingSpotType)
                return;

            for (int i = 0; i < NPC.maxBuffs; i++)
            {
                npc.buffType[i] = 0;
                npc.buffTime[i] = 0;
            }
        }
    }

    // Lantern fix
    public class JadeLanternFix : GlobalTile
    {
        private static int _lanternType = -1;

        public override void SetStaticDefaults()
        {
            if (ModLoader.TryGetMod("JadeFables", out Mod jf) &&
                jf.TryFind<ModTile>("JadeLantern", out ModTile lantern))
            {
                _lanternType = lantern.Type;
            }
        }

        private static bool HoldsLantern(int i, int j)
        {
            if (_lanternType < 0)
                return false;

            Tile below = Framing.GetTileSafely(i, j + 1);
            return below.HasTile && below.TileType == _lanternType;
        }

        private static bool HasSupport(int i, int j)
        {
            Tile above = Framing.GetTileSafely(i, j - 1);
            return above.HasTile && !above.IsActuated && Main.tileSolid[above.TileType];
        }

        public override bool CanKillTile(int i, int j, int type, ref bool blockDamaging)
        {
            if (HoldsLantern(i, j))
                return false;

            if (type == _lanternType && HasSupport(i, j))
                return false;

            return true;
        }

        public override bool CanExplode(int i, int j, int type)
        {
            if (HoldsLantern(i, j))
                return false;

            if (type == _lanternType && HasSupport(i, j))
                return false;

            return true;
        }

        // If tiles destroyed by any factors
        public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            if (type == _lanternType && !fail)
                noItem = true;
        }
    }

    // Bull Frog direction to player fix
    public class BullfrogFix : GlobalNPC
    {
        private static int _bullfrogType = -1;
        private static float _oldVelX;

        public override void SetStaticDefaults()
        {
            if (ModLoader.TryGetMod("JadeFables", out Mod jf) &&
                jf.TryFind<ModNPC>("Bullfrog", out ModNPC frog))
            {
                _bullfrogType = frog.Type;
            }
        }

        public override bool PreAI(NPC npc)
        {
            if (npc.type != _bullfrogType)
                return true;

            _oldVelX = npc.velocity.X;

            if (npc.HasValidTarget)
            {
                Player p = Main.player[npc.target];
                npc.spriteDirection = p.Center.X > npc.Center.X ? 1 : -1;
            }

            return true;
        }

        public override void PostAI(NPC npc)
        {
            if (npc.type != _bullfrogType)
                return;

            bool airborne = !npc.collideY && npc.velocity.Y != 0f;
            if (airborne && _oldVelX != 0f &&
                System.Math.Abs(npc.velocity.X - _oldVelX * 1.03f) < 0.0001f)
            {
                npc.velocity.X = _oldVelX;
            }
        }
    }

    // Artisan Mantis direction to player fix
    public class MantisFix : GlobalNPC
    {
        private static int _mantisType = -1;

        public override void SetStaticDefaults()
        {
            if (ModLoader.TryGetMod("JadeFables", out Mod jf) &&
                jf.TryFind<ModNPC>("JadeMantis", out ModNPC mantis))
            {
                _mantisType = mantis.Type;
            }
        }

        public override bool PreAI(NPC npc)
        {
            if (npc.type != _mantisType)
                return true;

            Player p = Main.player[npc.target];
            if (p.active && !p.dead)
                npc.spriteDirection = p.Center.X > npc.Center.X ? 1 : -1;

            return true;
        }
    }

    // Waterfall spawn changes
    public class JadeWaterfallSpawnerSystem : ModSystem
    {
        private const int RadiusTiles = 200;
        private const int ScanInterval = 30;

        private static int _tileType = -1;
        private static int _projType = -1;
        private int _timer;

        public override void PostSetupContent()
        {
            if (ModContent.TryFind("JadeFables/JadeWaterfallTile", out ModTile tile) &&
                ModContent.TryFind("JadeFables/JadeWaterfallProj", out ModProjectile proj))
            {
                _tileType = tile.Type;
                _projType = proj.Type;
            }
        }

        public override void PostUpdateEverything()
        {
            if (_tileType < 0 || _projType < 0 || Main.gameMenu || Main.netMode == NetmodeID.Server)
                return;

            if (++_timer % ScanInterval != 0)
                return;

            Player player = Main.LocalPlayer;
            if (!player.active || player.dead)
                return;

            var existing = new HashSet<Point>();
            foreach (Projectile p in Main.projectile)
            {
                if (p.active && p.type == _projType)
                    existing.Add(new Point((int)(p.Center.X / 16f), (int)(p.Center.Y / 16f)));
            }

            Point center = player.Center.ToTileCoordinates();
            int x0 = System.Math.Max(1, center.X - RadiusTiles);
            int x1 = System.Math.Min(Main.maxTilesX - 2, center.X + RadiusTiles);
            int y0 = System.Math.Max(1, center.Y - RadiusTiles);
            int y1 = System.Math.Min(Main.maxTilesY - 2, center.Y + RadiusTiles);

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    Tile tile = Main.tile[x, y];
                    if (!tile.HasTile || tile.TileType != _tileType)
                        continue;

                    if (existing.Contains(new Point(x, y)))
                        continue;

                    Projectile.NewProjectile(new EntitySource_Misc("Jade Waterfall"), new Vector2(x, y) * 16f, Vector2.Zero, _projType, 0, 0f, Main.myPlayer);
                }
            }
        }
    }

    // Water jitterig fix
    public class JadeLakeWaterJitterFix : ModSystem
    {
        private delegate void OrigHotspring(object self, On_Main.orig_CheckMonoliths origMain);
        private delegate void OrigVoid(object self);

        private static readonly List<IDisposable> _hooks = new();
        private static FieldInfo _oldScreenPos, _framesSinceDraw;
        private static Vector2 _mapOrigin;
        private static bool _hasOrigin;

        public override void Load()
        {
            if (Main.dedServ || !ModLoader.TryGetMod("JadeFables", out Mod jf))
                return;

            Assembly asm = jf.GetType().Assembly;
            Type mapType = asm.GetType("JadeFables.Biomes.JadeLake.JadeLakeMapTarget");
            Type addonType = asm.GetType("JadeFables.Biomes.JadeLake.JadeLakeAddon");
            if (mapType == null || addonType == null)
                return;

            const BindingFlags statics = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            const BindingFlags inst = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            const BindingFlags own = BindingFlags.NonPublic | BindingFlags.Static;

            _oldScreenPos = mapType.GetField("oldScreenPos", statics);
            _framesSinceDraw = mapType.GetField("framesSinceDraw", statics);
            MethodInfo hotspring = mapType.GetMethod("HotspringTarget", inst);
            MethodInfo change = addonType.GetMethod("SpritebatchChange", inst);
            MethodInfo changeBack = addonType.GetMethod("SpritebatchChangeBack", inst);

            if (_oldScreenPos == null || _framesSinceDraw == null ||
                hotspring == null || change == null || changeBack == null)
                return;

            Type me = typeof(JadeLakeWaterJitterFix);
            _hooks.Add(new Hook(hotspring, me.GetMethod(nameof(HotspringDetour), own)));
            _hooks.Add(new Hook(change, me.GetMethod(nameof(ChangeDetour), own)));
            _hooks.Add(new Hook(changeBack, me.GetMethod(nameof(ChangeBackDetour), own)));
        }

        private static void HotspringDetour(OrigHotspring orig, object self, On_Main.orig_CheckMonoliths origMain)
        {
            _oldScreenPos.SetValue(null, Main.screenPosition);

            Vector2 originBefore = Main.sceneWaterPos;
            orig(self, origMain);

            if (_framesSinceDraw.GetValue(null) is int frames && frames == 0)
            {
                _mapOrigin = originBefore;
                _hasOrigin = true;
            }
        }

        private static void ChangeDetour(OrigVoid orig, object self)
        {
            orig(self);
            ApplyOffset(Main.sceneWaterPos);
        }

        private static void ChangeBackDetour(OrigVoid orig, object self)
        {
            orig(self);
            ApplyOffset(Main.sceneBackgroundPos);
        }

        private static void ApplyOffset(Vector2 targetOrigin)
        {
            if (!_hasOrigin || Main.waterTarget == null)
                return;

            Effect effect = Filters.Scene["JadeLakeWater"]?.GetShader()?.Shader;
            effect?.Parameters["offset"]?.SetValue((targetOrigin - _mapOrigin) / Main.waterTarget.Size());
        }

        public override void Unload()
        {
            foreach (IDisposable h in _hooks)
                h.Dispose();
            _hooks.Clear();
            _hasOrigin = false;
        }
    }

    // idk if this is necessary
    // Seaweed move to behind blocks
    /*public class SeaweedDrawFix : GlobalProjectile
    {
        private static int _seaweedType = -1;

        public override void SetStaticDefaults()
        {
            if (ModLoader.TryGetMod("JadeFables", out Mod jf) &&
                jf.TryFind<ModProjectile>("JadeSeaweedProj", out ModProjectile seaweed))
            {
                _seaweedType = seaweed.Type;
            }
        }

        public override void SetDefaults(Projectile projectile)
        {
            if (projectile.type == _seaweedType)
                projectile.hide = true;
        }

        public override void DrawBehind(Projectile projectile, int index,
            List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles,
            List<int> overPlayers, List<int> overWiresUI)
        {
            if (projectile.type == _seaweedType)
                behindNPCsAndTiles.Add(index);
        }

        public override bool PreDraw(Projectile projectile, ref Color lightColor)
        {
            if (projectile.type != _seaweedType)
                return true;

            int x = (int)(projectile.Center.X / 16f);
            int y = (int)(projectile.Center.Y / 16f);

            Vector3 sum = Vector3.Zero;
            int n = 0;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -2; dy <= 0; dy++)
                {
                    sum += Lighting.GetColor(x + dx, y + dy).ToVector3();
                    n++;
                }
            }

            lightColor = new Color(sum / n);
            return true;
        }
    }*/
}
