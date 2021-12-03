using System.Collections.Generic;
using Verse;

namespace AAM
{
    public class AnimCellData
    {
        public enum Type
        {
            PawnStart,
            PawnEnd,
            MustBeClear
        }

        public Type type;
        public IntVec2? position;
        public CellRect? bounds;
        public int? pawnIndex;

        public virtual IEnumerable<string> ConfigErrors()
        {
            if (position == null && bounds == null)
                yield return "This cell data has neither position nor bounds! Must have one or the other!";

            if ((type == Type.PawnStart || type == Type.PawnEnd) && (pawnIndex == null || pawnIndex.Value < 0))
                yield return $"This cell data is for '{type}' but the pawnIndex is invalid: {pawnIndex?.ToString() ?? "null"}. Please assign a pawnIndex.";
        }

        public IEnumerable<IntVec2> GetCells()
        {
            if (bounds != null)
                foreach (var cell in bounds)
                    yield return cell.ToIntVec2;
            else if (position != null)
                yield return position.Value;
        }

        public IntVec2 GetCell()
        {
            if (position != null)
                return position.Value;
            else if (bounds != null)
                return bounds.Value.CenterCell.ToIntVec2;
            else
                return IntVec2.Zero;
        }
    }
}
