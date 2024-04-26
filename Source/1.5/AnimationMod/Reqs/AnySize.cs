using AM.Idle;

namespace AM.Reqs
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
