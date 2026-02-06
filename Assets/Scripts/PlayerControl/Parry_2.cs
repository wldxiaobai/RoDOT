using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Parry_2 : AttackHitBoxControl
{
    private void Parry2On()
    {
        EnableAttackHitbox();
    }
    private void Parry2Off()
    {
        DisableAttackHitbox();
    }
}
