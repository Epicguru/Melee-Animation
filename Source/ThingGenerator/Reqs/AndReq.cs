using System.Collections.Generic;

namespace AAM.Reqs
{
    public class AndReq : Req
    {
        public List<Req> subs = new();

        public override bool Evaluate(ReqInput input)
        {
            return subs.TrueForAll(s => s.Evaluate(input));
        }
    }
}
