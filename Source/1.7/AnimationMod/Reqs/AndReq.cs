using System.Collections.Generic;

namespace AM.Reqs
{
    public class And : Req
    {
        public List<Req> subs = new List<Req>();

        public override bool Evaluate(ReqInput input)
        {
            return subs.TrueForAll(s => s.Evaluate(input));
        }
    }
}
