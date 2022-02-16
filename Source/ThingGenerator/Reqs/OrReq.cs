using System.Collections.Generic;
using Verse;

namespace AAM.Reqs
{
    public class OrReq : Req
    {
        public List<Req> subs = new List<Req>();

        public override bool Evaluate(ReqInput input)
        {
            return subs.Any(s => s.Evaluate(input));
        }
    }
}
