using AAM.Idle;

namespace AAM.Reqs;

public class AnyCategory : Req
{
    public WeaponCat category;

    public override bool Evaluate(ReqInput input)
    {
        return (input.CategoryFlags & category) != 0;
    }
}
