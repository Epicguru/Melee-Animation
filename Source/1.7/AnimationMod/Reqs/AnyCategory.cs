using AM.Idle;

namespace AM.Reqs;

public class AnyCategory : Req
{
    public WeaponCat category;

    public override bool Evaluate(ReqInput input)
    {
        return (input.CategoryFlags & category) != 0;
    }
}
