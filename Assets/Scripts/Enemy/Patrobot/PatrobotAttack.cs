using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PatrobotAttack : AttackHitBoxControl
{
    private void PAOn()
    {
        EnableAttackHitbox();
    }

    private void PAOff()
    {
        DisableAttackHitbox();
    }
}
