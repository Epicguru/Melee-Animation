namespace AAM.Reqs
{
    public sealed class True : Req
    {
        public static readonly True Instance = new True();

        public override bool Evaluate(ReqInput input) => true;
    }
}
