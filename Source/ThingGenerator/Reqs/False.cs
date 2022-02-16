namespace AAM.Reqs
{
    public sealed class False : Req
    {
        public static readonly False Instance = new False();

        public override bool Evaluate(ReqInput input) => false;
    }
}
