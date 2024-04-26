using AM.Tweaks;

namespace AM.Reqs
{
    public class OnlyType : Req
    {
        public MeleeWeaponType types;

        public override bool Evaluate(ReqInput input)
        {
            return types == input.TypeFlags;
        }
    }
}
