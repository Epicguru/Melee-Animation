namespace AM.Reqs
{
    public sealed class True : Req
    {
        public static readonly True Instance = new();

        public override bool Evaluate(ReqInput input) => true;
    }
}
