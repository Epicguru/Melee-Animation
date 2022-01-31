using AAM.Patches;
using Verse;

namespace AAM
{
    public class GameComp : GameComponent
    {
        public GameComp(Game _) { }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            Patch_Corpse_DrawAt.Tick();
            Patch_PawnRenderer_LayingFacing.Tick();
        }
    }
}
