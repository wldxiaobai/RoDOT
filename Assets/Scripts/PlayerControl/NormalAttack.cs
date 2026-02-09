using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NormalAttack : AttackHitBoxControl
{
    private void NormalOn()
    {
        EnableAttackHitbox();
    }
    private void NormalOff()
    {
        DisableAttackHitbox();
    }
}
