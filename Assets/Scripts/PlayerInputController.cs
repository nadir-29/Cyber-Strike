using UnityEngine;
using UnityEngine.InputSystem;

// Reads keyboard/mouse input and forwards it to the Fighter and FighterCombat.
// Left Shift + A or D asks the Fighter to dash that way (either key first); against the facing it's a backdash.
// A / D never turn the fighter on the ground: Fighter only turns it mid-air.
[RequireComponent(typeof(Fighter), typeof(FighterCombat))]
public class PlayerInputController : MonoBehaviour
{
    Fighter fighter;
    FighterCombat combat;
    FighterAbilities abilities;
    InputAction moveAction;
    InputAction jumpAction;
    InputAction punchAction;
    InputAction kickAction;
    InputAction abilityAction;
    InputAction blockAction;
    InputAction specialAction;
    InputAction dashAction;
    InputAction leftAction;
    InputAction rightAction;

    void Awake()
    {
        fighter = GetComponent<Fighter>();
        combat = GetComponent<FighterCombat>();
        abilities = GetComponent<FighterAbilities>();

        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("1DAxis")
            .With("Negative", "<Keyboard>/a")
            .With("Positive", "<Keyboard>/d");

        jumpAction = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
        punchAction = new InputAction("Punch", InputActionType.Button, "<Mouse>/leftButton");
        kickAction = new InputAction("Kick", InputActionType.Button, "<Keyboard>/f");
        abilityAction = new InputAction("Ability", InputActionType.Button, "<Keyboard>/e");
        blockAction = new InputAction("Block", InputActionType.Button, "<Mouse>/rightButton");
        specialAction = new InputAction("Special", InputActionType.Button, "<Keyboard>/q");
        dashAction = new InputAction("Dash", InputActionType.Button, "<Keyboard>/leftShift");
        leftAction = new InputAction("Left", InputActionType.Button, "<Keyboard>/a");
        rightAction = new InputAction("Right", InputActionType.Button, "<Keyboard>/d");
    }

    void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
        punchAction.Enable();
        kickAction.Enable();
        abilityAction.Enable();
        blockAction.Enable();
        specialAction.Enable();
        dashAction.Enable();
        leftAction.Enable();
        rightAction.Enable();
    }

    void OnDisable()
    {
        moveAction.Disable();
        jumpAction.Disable();
        punchAction.Disable();
        kickAction.Disable();
        abilityAction.Disable();
        blockAction.Disable();
        specialAction.Disable();
        dashAction.Disable();
        leftAction.Disable();
        rightAction.Disable();
        fighter.SetMoveInput(0f);
        combat.SetBlock(false);
    }

    void OnDestroy()
    {
        moveAction.Dispose();
        jumpAction.Dispose();
        punchAction.Dispose();
        kickAction.Dispose();
        abilityAction.Dispose();
        blockAction.Dispose();
        specialAction.Dispose();
        dashAction.Dispose();
        leftAction.Dispose();
        rightAction.Dispose();
    }

    void Update()
    {
        float move = moveAction.ReadValue<float>();
        fighter.SetMoveInput(move);
        combat.SetBlock(blockAction.IsPressed());

        if (jumpAction.WasPressedThisFrame())
            fighter.Jump();
        if (punchAction.WasPressedThisFrame())
            combat.Punch();
        if (kickAction.WasPressedThisFrame())
            combat.Kick();
        if (abilityAction.WasPressedThisFrame() && abilities != null)
            abilities.UseAbility();
        if (specialAction.WasPressedThisFrame())
            combat.Special();

        // Shift pressed while holding a direction, or a direction pressed while holding Shift.
        if (dashAction.WasPressedThisFrame() && Mathf.Abs(move) > 0.5f)
            fighter.Dash(move > 0f ? 1 : -1);
        else if (dashAction.IsPressed() && leftAction.WasPressedThisFrame())
            fighter.Dash(-1);
        else if (dashAction.IsPressed() && rightAction.WasPressedThisFrame())
            fighter.Dash(1);
    }
}
