using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace AM
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
                    var pos = new IntVec3(x, 0, z);
                    bool mustBeClear = false;
                    foreach (var cell in def.GetMustBeClearCells(flipX, false, IntVec3.Zero))
                    {
                        if (cell == pos with { y = cell.y })
                        {
                            mustBeClear = true;
                            break;
                        }
                    }

                    if (!mustBeClear)
                        continue;

                    int index = (x + 3) + (z + 3) * 7;
                    mask |= (ulong)1 << index;
                }
            }

            return mask;
        }

        /// <summary>
        /// Makes a 7x7 cell mask around the <paramref name="center"/> position where each bit represents the
        /// occupied state of that cell. A bit value of 1 means occupied (cannot stand there), 0 means free (can stand there).
        /// The index of a bit is: <c>(cx + cz * w)</c> where <c>cx, cz</c> are the offset of the cell from the bottom-left of the mask, and <c>w</c> is the width of the mask.
        /// The remaining 15 bits are unused.
        /// </summary>
        /// <param name="map">The map to check. Must not be null.</param>
        /// <param name="center">The center cell coordinates.</param>
        /// <param name="smallMask">A smaller version of the mask, 3x3 cells rather than the 7x7 that is the return value.</param>
        /// <returns>A 7x7 cell bitmask.</returns>
        public static ulong MakeOccupiedMask(Map map, IntVec3 center, out uint smallMask)
        {
            // 1 means cell is occupied (cannot stand there).
            ulong mask = 0;
            smallMask = 0;
            int w = map.Size.x;
            int h = map.Size.z;

            for (int x = -3; x <= 3; x++)
            {
                for (int z = -3; z <= 3; z++)
                {
                    if (IsValidPawnPosFast(map, w, h, new IntVec3(x, 0, z) + center))
                        continue;

                    int index = (x + 3) + (z + 3) * 7;
                    mask |= (ulong)1 << index;

                    if (x is > -2 and < 2 && z is > -2 and < 2)
                    {
                        int index2 = (x + 1) + (z + 1) * 3;
                        smallMask |= (uint)1 << index2;
                    }
                }
            }

            return mask;
        }

        public static bool GetBit(this ulong occupiedMask, int xOffset, int zOffset) => (occupiedMask & ((ulong)1 << ((xOffset + 3) + (zOffset + 3) * 7))) != 0;
    }
}
