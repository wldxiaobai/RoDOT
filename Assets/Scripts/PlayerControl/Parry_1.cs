using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Parry_1 : AttackHitBoxControl
{
    private void ParryOn()
    {
        EnableAttackHitbox();
    }
    private void ParryOff()
    {
        DisableAttackHitbox();
    }
}
