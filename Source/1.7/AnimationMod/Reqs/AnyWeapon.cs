namespace AM.Reqs;

public sealed class AnyWeapon : Req
{
    public override bool Evaluate(ReqInput input) => input.WeaponDef != null;
}