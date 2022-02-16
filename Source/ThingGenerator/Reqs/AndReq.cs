using System.Collections.Generic;

namespace AAM.Reqs
{
    public class AndReq : Req
    {
        public List<Req> subs = new List<Req>();

        public override bool Evaluate(ReqInput input)
        {
            return subs.TrueForAll(s => s.Evaluate(input));
        }
    }
}
