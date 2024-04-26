using System.Collections.Generic;
using Verse;

namespace AM.Reqs
{
    public class Or : Req
    {
        public List<Req> subs = new();

        public override bool Evaluate(ReqInput input)
        {
            return subs.Any(s => s.Evaluate(input));
        }
    }
}
