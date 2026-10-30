using UnityEngine;
using UnityEngine.InputSystem;

// Reads keyboard/mouse input and forwards it to the Fighter and FighterCombat.
[RequireComponent(typeof(Fighter), typeof(FighterCombat))]
public class PlayerInputController : MonoBehaviour
{
    Fighter fighter;
    FighterCombat combat;
    InputAction moveAction;
    InputAction jumpAction;
    InputAction punchAction;
    InputAction kickAction;

    void Awake()
    {
        fighter = GetComponent<Fighter>();
        combat = GetComponent<FighterCombat>();

        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("1DAxis")
            .With("Negative", "<Keyboard>/a")
            .With("Positive", "<Keyboard>/d");

        jumpAction = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
        punchAction = new InputAction("Punch", InputActionType.Button, "<Mouse>/leftButton");
        kickAction = new InputAction("Kick", InputActionType.Button, "<Mouse>/rightButton");
    }

    void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
        punchAction.Enable();
        kickAction.Enable();
    }

    void OnDisable()
    {
        moveAction.Disable();
        jumpAction.Disable();
        punchAction.Disable();
        kickAction.Disable();
        fighter.SetMoveInput(0f);
    }

    void OnDestroy()
    {
        moveAction.Dispose();
        jumpAction.Dispose();
        punchAction.Dispose();
        kickAction.Dispose();
    }

    void Update()
    {
        fighter.SetMoveInput(moveAction.ReadValue<float>());

        if (jumpAction.WasPressedThisFrame())
            fighter.Jump();
        if (punchAction.WasPressedThisFrame())
            combat.Punch();
        if (kickAction.WasPressedThisFrame())
            combat.Kick();
    }
}
