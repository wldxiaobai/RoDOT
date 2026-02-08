# Copilot Instructions

## General Guidelines
- First general instruction
- Second general instruction

## Logging Behavior
- Ensure that the `CurrentStateName` logging updates reflect simple logging behavior as expected.

## Enemy AI Enhancements
- Extend the enemy AI with a 'hurt' state.
- Adjust the `OnHitByPlayerAttack` logic in `BaseEnemy` to handle the new behavior properly.
- Focus on precise, state-driven enemy behavior updates, utilizing state machines and explicit state transitions for future enhancements.
- Modify Patrobot's Attack/Hurt/Idle ActSeq to utilize only ActionNode for construction.