using RimWorld;
using System.Runtime.CompilerServices;
using Verse;

namespace AAM
{
    public static class SpaceChecker
    {
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

        public static ulong MakeClearMask(AnimDef def, bool flipX)
        {
            // 1 means must be clear.
            ulong mask = 0;

            for (int x = -3; x <= 3; x++)
            {
                for (int z = -3; z <= 3; z++)
                {
                    int index = x + z * 7;

                    IntVec3 pos = new IntVec3(x, 0, z);
                    bool mustBeClear = false;
                    foreach (var cell in def.GetMustBeClearCells(flipX, false, IntVec3.Zero))
                    {
                        if (cell == pos)
                        {
                            mustBeClear = true;
                            break;
                        }
                    }

                    if(mustBeClear)
                        mask &= (ulong)1 << index;
                }
            }

            return mask;
        }

        public static ulong MakeOccupiedMask(Map map, IntVec3 center)
        {
            // 1 means cell is occupied (cannot stand there).
            ulong mask = 0;
            int w = map.Size.x;
            int h = map.Size.z;

            for (int x = -3; x <= 3; x++)
            {
                for (int z = -3; z <= 3; z++)
                {
                    if (!IsValidPawnPosFast(map, w, h, new IntVec3(x, 0, z) + center))
                        mask &= (ulong)1 << (x + z * 7);
                }
            }

            return mask;
        }
    }
}
