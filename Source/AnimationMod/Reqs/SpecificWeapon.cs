using System.Collections.Generic;

namespace AM.Reqs
{
    public class SpecificWeapon : Req
    {
        public string weapon;
        public List<string> weapons;

        public override bool Evaluate(ReqInput input)
        {
            if (input.WeaponDef == null)
                return false;

            return input.WeaponDef.defName == weapon || weapons != null && weapons.Contains(input.WeaponDef.defName);
        }
    }
}
