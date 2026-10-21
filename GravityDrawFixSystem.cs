using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;

public class GravityDrawFixSystem : ModSystem
{
    private delegate void BeginOrig(SpriteBatch self, SpriteSortMode m, BlendState b, SamplerState s, DepthStencilState d, RasterizerState r, Effect e, Matrix t);

    private static readonly List<IDisposable> _hooks = new();
    private static int _depth;
    private static RasterizerState _noCull, _noCullScissor;

    // Gravitation Potion fix
    public override void Load()
    {
        if (Main.dedServ || !ModLoader.TryGetMod("JadeFables", out Mod jf))
            return;

        Assembly asm = jf.GetType().Assembly;
        const BindingFlags priv = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        // Replacing the rasterizer within the marked methods
        MethodInfo begin = typeof(SpriteBatch).GetMethod("Begin", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(SpriteSortMode), typeof(BlendState), typeof(SamplerState), typeof(DepthStencilState), typeof(RasterizerState), typeof(Effect), typeof(Matrix) }, null);
        MethodInfo detour = typeof(GravityDrawFixSystem).GetMethod(nameof(BeginDetour), BindingFlags.NonPublic | BindingFlags.Static);
        if (begin != null)
            _hooks.Add(new Hook(begin, detour));

        // Lanterns
        MethodInfo lantern = asm.GetType("JadeFables.Tiles.JadeLantern.JadeLanternTileEntity")?.GetMethod("Main_DrawProjectiles", priv);
        if (lantern != null)
            _hooks.Add(new Hook(lantern, (Action<ModTileEntity, On_Main.orig_DrawProjectiles, Main> orig,
                ModTileEntity self, On_Main.orig_DrawProjectiles o, Main m) =>
            { _depth++; try { orig(self, o, m); } finally { _depth--; } }));

        // Waterfalls
        MethodInfo waterfall = asm.GetType("JadeFables.Tiles.JadeWaterfall.JadeWaterfallProj")?.GetMethod("Main_DrawPlayers_AfterProjectiles", priv);
        if (waterfall != null)
            _hooks.Add(new Hook(waterfall, (Action<ModProjectile, On_Main.orig_DrawPlayers_AfterProjectiles, Main> orig,
                ModProjectile self, On_Main.orig_DrawPlayers_AfterProjectiles o, Main m) =>
            { _depth++; try { orig(self, o, m); } finally { _depth--; } }));

        // Fishing Spot (n lilypad fix)
        MethodInfo spot = asm.GetType("JadeFables.NPCs.FishingSpot")?.GetMethod("PreDraw",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        if (spot != null)
            _hooks.Add(new Hook(spot, (Func<ModNPC, SpriteBatch, Vector2, Color, bool> orig,
                ModNPC self, SpriteBatch sb, Vector2 pos, Color c) =>
            { _depth++; try { return orig(self, sb, pos, c); } finally { _depth--; } }));
    }

    private static void BeginDetour(BeginOrig orig, SpriteBatch self, SpriteSortMode m, BlendState b,
        SamplerState s, DepthStencilState d, RasterizerState r, Effect e, Matrix t)
    {
        if (_depth > 0 && (Main.GameViewMatrix.Effects & SpriteEffects.FlipVertically) != 0)
        {
            bool scissor = r?.ScissorTestEnable ?? false;
            r = scissor
                ? (_noCullScissor ??= new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true })
                : (_noCull ??= new RasterizerState { CullMode = CullMode.None });
        }
        orig(self, m, b, s, d, r, e, t);
    }

    public override void Unload()
    {
        foreach (IDisposable h in _hooks)
            h.Dispose();
        _hooks.Clear();
        _noCull = _noCullScissor = null;
    }
}