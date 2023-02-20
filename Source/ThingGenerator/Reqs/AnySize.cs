using AAM.Idle;
using System;

namespace AAM.Reqs
{
    public class AnySize : Req
    {
        public WeaponSize size;

        public override bool Evaluate(ReqInput input)
        {
            return (input.SizeFlags & size) != 0;
        }
    }
}
