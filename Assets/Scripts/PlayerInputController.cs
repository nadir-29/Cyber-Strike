using UnityEngine;
using UnityEngine.InputSystem;

// Reads keyboard/mouse input and forwards it to the Fighter and FighterCombat.
// Double-tapping A or D asks the Fighter to dash that way.
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
    InputAction dashLeftAction;
    InputAction dashRightAction;

    [Tooltip("Max real seconds between the two taps of a dash double-tap.")]
    [SerializeField] float doubleTapWindow = 0.25f;

    int lastTapDirection;
    float lastTapTime = -1f;

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
        dashLeftAction = new InputAction("DashLeft", InputActionType.Button, "<Keyboard>/a");
        dashRightAction = new InputAction("DashRight", InputActionType.Button, "<Keyboard>/d");
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
        dashLeftAction.Enable();
        dashRightAction.Enable();
        lastTapTime = -1f;
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
        dashLeftAction.Disable();
        dashRightAction.Disable();
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
        dashLeftAction.Dispose();
        dashRightAction.Dispose();
    }

    void Update()
    {
        fighter.SetMoveInput(moveAction.ReadValue<float>());
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

        if (dashLeftAction.WasPressedThisFrame()) OnDirectionTap(-1);
        if (dashRightAction.WasPressedThisFrame()) OnDirectionTap(1);
    }

    void OnDirectionTap(int direction)
    {
        bool doubleTap = direction == lastTapDirection && lastTapTime >= 0f
                         && Time.unscaledTime - lastTapTime <= doubleTapWindow;
        if (doubleTap)
        {
            fighter.Dash(direction);
            lastTapTime = -1f; // a third tap starts a new double-tap
            return;
        }
        lastTapDirection = direction;
        lastTapTime = Time.unscaledTime;
    }
}
