using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;
using Terraria.Graphics.Effects;

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
            // "JadeFables.Tiles.SomeTileWithTileEntity",
            "JadeFables.Tiles.JadePylon.JadePylonTile",
            "JadeFables.Tiles.WarriorStatue.WarriorStatue"
        };

        private static readonly List<IDisposable> _hooks = new();

        public override void Load()
        {
            if (!ModLoader.TryGetMod("JadeFables", out Mod jadeFables))
            return;

            Assembly asm = jadeFables.GetType().Assembly;
            int patched = 0;

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

                _hooks.Add(new Hook(method,
                (Action<ModTile, int, int, int, int> orig,
                ModTile self, int i, int j, int frameX, int frameY) => { }));

                patched++;
            }
        }

        public override void Unload()
        {
            foreach (IDisposable hook in _hooks)
                hook.Dispose();

            _hooks.Clear();
        }
    }

    //Jade Ore no drop fix
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

    //Jade Hook "Here" message fix
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

    /*
    public class JadeLakeWaterFixSystem : ModSystem
    {
        private static On_Main.hook_CheckMonoliths? _hook;

        public override void Load()
        {
            if (!ModLoader.HasMod("JadeFables"))
                return;

            _hook = orig =>
            {
                orig();
                ResetOffset();
            };

            On_Main.CheckMonoliths += _hook;
        }

        private static void ResetOffset()
        {
            if (Main.gameMenu)
                return;

            var effect = Filters.Scene["JadeLakeWater"]?.GetShader()?.Shader;
            effect?.Parameters["offset"]?.SetValue(Vector2.Zero);
        }

        public override void Unload()
        {
            if (_hook is not null)
            {
                On_Main.CheckMonoliths -= _hook;
                _hook = null;
            }
        }
    }*/
}
