using RimWorld;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Verse;

namespace AAM
{
    public static class SpaceChecker
    {
        public static bool Check(Map map, AnimDef animDef, bool mirrorX, bool mirrorY, in IntVec3 root)
        {
            if (animDef == null)
                return false;

            return Check(map, animDef.GetMustBeClearCells(mirrorX, mirrorY, root));
        }

        public static bool Check(Map map, IEnumerable<IntVec3> cells)
        {
            if (map == null)
                return false;

            int mx = map.Size.x;
            int mz = map.Size.z;

            foreach (var cell in cells)
                if (!IsValidPawnPosFast(map, mx, mz, cell))
                    return false;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidPawnPosFast(Map map, int mapWidth, int mapHeight, in IntVec3 cell)
        {
            if (cell.x < 0 || cell.z < 0 || cell.x >= mapWidth || cell.z >= mapHeight)
            {
                return false;
            }

            if (cell.Impassable(map) || !cell.Walkable(map))
            {
                return false;
            }

            return cell.GetEdifice(map) is not Building_Door bd || bd.Open;
        }
    }
}
