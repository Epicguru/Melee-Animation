using AAM.Tweaks;

namespace AAM.Reqs
{
    public class AnyType : Req
    {
        public MeleeWeaponType types;

        public override bool Evaluate(ReqInput input)
        {
            uint result = (uint)(input.TypeFlags & types);
            return result != 0;
        }
    }
}
