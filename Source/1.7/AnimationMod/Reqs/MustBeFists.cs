namespace AM.Reqs;

public sealed class MustBeFists : Req
{
    public override bool Evaluate(ReqInput input) => input.IsFists;
}